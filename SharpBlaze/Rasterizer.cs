using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static ScalarHelper;
using static RasterizerUtils;
using static BitOps;
using static Linearizer;

using static F24Dot8;

using PixelIndex = uint;

public unsafe partial struct Rasterizer<T>
    where T : unmanaged, ITileDescriptor<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PixelIndex F24Dot8ToPixelIndex(F24Dot8 x)
    {
        return (PixelIndex) x.ToI32();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static F24Dot8 PixelIndexToF24Dot8(PixelIndex x)
    {
        return ((int) x).ToF24D8();
    }

    [Obsolete]
    public Rasterizer() { }

    public static partial void Rasterize(
        ReadOnlySpan<Geometry> geometries,
        in Matrix matrix,
        Executor threads,
        ImageData image,
        LineRasterizer lineRasterizer)
    {
        Debug.Assert(geometries.Length > 0);
        Debug.Assert(image.Data != null);
        Debug.Assert(image.Width > 0);
        Debug.Assert(image.Height > 0);
        Debug.Assert(image.Stride >= image.Width * image.BytesPerPixel);

        StepState1 state1 = Step1(threads, image, geometries, matrix);
        StepState2 state2 = Step2(threads, state1);
        Step3(threads, image, geometries, lineRasterizer, state2);
    }

    private ref struct StepState1
    {
        public required Span<bool> validRasters;
        public required Span<RasterizableGeometry> rasters;
        public required ReadOnlySpan<Geometry> geometries;
        public required Matrix transform;
        public required IntRect imageBounds;
    }

    /// <summary>
    /// Create and array of RasterizableGeometry instances. Instances are
    /// created and prepared for further processing in parallel.
    /// </summary>
    private static StepState1 Step1(
        Executor threads,
        in ImageData image,
        ReadOnlySpan<Geometry> geometries,
        in Matrix transform)
    {
        Span<bool> validRasters = 
            threads.MainMemory.Frame.Alloc<bool>(geometries.Length).AsSpan();
        
        // Allocate memory for RasterizableGeometry instances.
        Span<RasterizableGeometry> rasters = 
            threads.MainMemory.Frame.Alloc<RasterizableGeometry>(geometries.Length).AsSpan();

        IntRect imageBounds = new(0, 0, image.Width, image.Height);

        StepState1 state1 = new()
        {
            validRasters = validRasters,
            rasters = rasters,
            geometries = geometries,
            transform = transform,
            imageBounds = imageBounds,
        };
        threads.For(0, geometries.Length, &state1, static (index, state, memory) =>
        {
            StepState1* s = (StepState1*) state;

            ref readonly Geometry geometry = ref s->geometries[index];
            
            bool hasRaster = CreateRasterizable(
                out s->rasters[index],
                in geometry,
                index,
                s->transform,
                s->imageBounds,
                memory);

            s->validRasters[index] = hasRaster;
        });

        // Linearizer may decide that some paths do not contribute to the final
        // image. In these situations CreateRasterizable will return null.
        
        return state1;
    }

    private ref struct StepState2
    {
        public required ReadOnlySpan<bool> validRasters;
        public required ReadOnlySpan<RasterizableGeometry> rasters;

        public required TileIndex rowCount;
        public required Span<RowItemList<RasterizableItem>> rowLists;
        public required int iterationHeight;
    }

    /// <summary>
    /// Create lists of rasterizable items for each interval.
    /// </summary>
    private static StepState2 Step2(Executor threads, in StepState1 state1)
    {
        TileIndex rowCount = CalculateRowCount<T>(state1.imageBounds.Height);

        Span<RowItemList<RasterizableItem>> rowLists =
            threads.MainMemory.Frame.Alloc<RowItemList<RasterizableItem>>((int) rowCount).AsSpan();

        int threadCount = Math.Max(1, threads.WorkerCount);

        int iterationHeight = (int) Math.Max((uint) ((int) rowCount / threadCount), 1);

        int iterationCount = ((int) rowCount / iterationHeight) +
            (int) Math.Min((uint) ((int) rowCount % iterationHeight), 1);

        StepState2 state2 = new()
        {
            validRasters = state1.validRasters,
            rasters = state1.rasters,
            rowCount = rowCount,
            rowLists = rowLists,
            iterationHeight = iterationHeight,
        };
        threads.For(0, iterationCount, &state2, static (index, state, memory) =>
        {
            StepState2* s = (StepState2*) state;

            TileIndex threadY = (TileIndex) (index * s->iterationHeight);
            TileIndex threadHeight = (TileIndex) Math.Min(s->iterationHeight, (int) (s->rowCount - threadY));
            TileIndex threadMaxY = threadY + threadHeight;

            for (TileIndex i = threadY; i < threadMaxY; i++)
            {
                s->rowLists[(int) i] = new RowItemList<RasterizableItem>();
            }

            ReadOnlySpan<bool> validRasters = s->validRasters; 
            for (int i = 0; i < validRasters.Length; i++)
            {
                if (!validRasters[i])
                {
                    continue;
                }
                
                ref readonly RasterizableGeometry rasterizable = ref s->rasters[i];
                TileBounds b = rasterizable.Bounds;

                TileIndex min = Clamp(b.Y, threadY, threadMaxY);
                TileIndex max = Clamp(b.Y + b.RowCount, threadY, threadMaxY);

                if (min == max)
                {
                    continue;
                }

                // Populate all lists which intersect with this geometry.
                for (TileIndex y = min; y < max; y++)
                {
                    // Local row index within row array of geometry.
                    TileIndex localIndex = y - b.Y;

                    // There are two situations when this row needs to be
                    // inserted. Either it has segments or it has non-zero cover
                    // array.
                    bool emptyRow =
                        !rasterizable.HasLinesForRow((int) localIndex) &&
                        rasterizable.GetCoversForRow((int) localIndex).IsEmpty;

                    if (emptyRow)
                    {
                        // Both conditions failed, this geometry for this row will
                        // not produce any visible pixels.
                        continue;
                    }

                    ref RowItemList<RasterizableItem> list = ref s->rowLists[(int) y];

                    list.Append(memory, new RasterizableItem(i, (int) localIndex));
                }
            }
        });

        return state2;
    }

    private ref struct StepState3
    {
        public required ReadOnlySpan<RowItemList<RasterizableItem>> rowLists;
        public required ImageData image;
        public required ReadOnlySpan<RasterizableGeometry> rasters;
        public required ReadOnlySpan<Geometry> geometries;
        public required LineRasterizer rasterizer;
    }

    /// <summary>
    /// Rasterize all intervals.
    /// </summary>
    private static void Step3(
        Executor threads, 
        in ImageData image,
        ReadOnlySpan<Geometry> geometries, 
        LineRasterizer lineRasterizer,
        in StepState2 state2)
    {
        StepState3 state3 = new()
        {
            rowLists = state2.rowLists,
            image = image,
            rasters = state2.rasters,
            geometries = geometries,
            rasterizer = lineRasterizer,
        };
        threads.For(0, (int) state2.rowCount, &state3, static (rowIndex, state, memory) =>
        {
            StepState3* s = (StepState3*) state;

            ref readonly RowItemList<RasterizableItem> item = ref s->rowLists[rowIndex];

            RasterizeRow(item, s->rasters, s->geometries, memory, s->image, s->rasterizer);
        });
    }

    private static void IterateLinesX32Y16(
        int localRowIndex, 
        in RasterizableGeometry raster,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        int count = raster.GetFirstBlockLineCountForRow(localRowIndex);

        LineArrayX32Y16Block* v = raster.GetLinesForRow<LineArrayX32Y16Block>(localRowIndex);

        while (v != null)
        {
            foreach (LineArrayX32Y16Block.Line line in v->P0P1[..count])
            {
                F8Dot8x2 y0y1 = line.Y0Y1;
                F24Dot8 y0 = y0y1.X;
                F24Dot8 y1 = y0y1.Y;

                RasterizeLine(line.X0, y0, line.X1, y1, bitVectorTable, coverAreaTable);
            }

            v = v->Next;
            count = LineArrayX32Y16Block.LinesPerBlock;
        }
    }


    private static void IterateLinesX16Y16(
        int localRowIndex, 
        in RasterizableGeometry raster,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        int count = raster.GetFirstBlockLineCountForRow(localRowIndex);

        LineArrayX16Y16Block* v = raster.GetLinesForRow<LineArrayX16Y16Block>(localRowIndex);

        while (v != null)
        {
            foreach (F8Dot8x4 p0p1 in v->P0P1[..count])
            {
                RasterizeLine(
                    p0p1.X,
                    p0p1.Y,
                    p0p1.Z,
                    p0p1.W,
                    bitVectorTable,
                    coverAreaTable);
            }

            v = v->Next;
            count = LineArrayX16Y16Block.LinesPerBlock;
        }
    }

    private static bool CreateRasterizable(
        out RasterizableGeometry placement,
        ref readonly Geometry geometry,
        int geometryIndex,
        in Matrix transform,
        IntRect imageBounds,
        ThreadMemory memory)
    {
        Debug.Assert(imageBounds.HasArea());

        Unsafe.SkipInit(out placement);
        if (geometry.Tags.IsEmpty)
        {
            return false;
        }

        // Path bounds in geometry are transformed by transformation matrix, but
        // not intersected with destination image bounds (path bounds can be
        // bigger than destination image bounds).
        //
        // Next step is to intersect transformed path bounds with destination
        // image bounds and see if there is something left.
        //
        // Note that there is a special consideration regarding maximum X path
        // bounding box edge. Consider path representing a rectangle. Vertical
        // line going from top to bottom at the right edge of path bounding box.
        // This line should close rectangle. But line clipper simply ignores it
        // because it ignores all lines that have X coordinates equal to or to the
        // right of path bounding box. As a result, this path is then drawn to the
        // edge of destination image instead of terminating at the right rectangle edge.
        //
        // To solve this problem 1 is added to the maximum X coordinate of path
        // bounding box to allow inserting vertical lines at the right edge of
        // path bounding box so shapes get a chance to terminate fill. Perhaps
        // there are better ways to solve this (maybe clipper should not ignore
        // lines at the maximum X edge of path bounds?), but for now I'm keeping
        // this fix.

        Matrix geometryTransform = geometry.Transform * transform;
        FloatRect geometryBoundsFrac = geometryTransform.Map(new FloatRect(geometry.PathBounds));

        if (!geometryBoundsFrac.HasArea())
        {
            return false;
        }

        IntRect geometryBounds = geometryBoundsFrac.ToExpandedIntRect() + new IntRect(0, 0, 1, 0);
        IntRect intersection = IntRect.Intersect(imageBounds, geometryBounds);

        if (!intersection.HasArea())
        {
            // Geometry bounds do not intersect with destination image.
            return false;
        }
        
        // Determine if path is completely within destination image bounds. If
        // geometry bounds fit within destination image, a shortcut can be made
        // when generating lines.
        bool contains = imageBounds.Contains(geometryBounds);

        TileBounds bounds = CalculateTileBounds<T>(intersection);

        LinearGeometry linearGeo = new(
            geometryTransform,
            geometry.Tags.Span,
            geometry.Points.Span
        );

        if (IsNarrow(bounds))
        {
            Linearize<LineArrayX16Y16>(
                out placement, linearGeo, geometryIndex, bounds, contains, memory);
        }
        else
        {
            Linearize<LineArrayX32Y16>(
                out placement, linearGeo, geometryIndex, bounds, contains, memory);
        }
        return true;
    }


    private static bool IsNarrow(TileBounds bounds)
    {
        return 128 > (bounds.ColumnCount * T.TileW);
    }


    private static void Linearize<L>(
        out RasterizableGeometry placement,
        in LinearGeometry geometry,
        int geometryIndex,
        TileBounds bounds,
        bool contains,
        ThreadMemory memory)
        where L : unmanaged, ILineArrayBlock<L>
    {
        Linearizer<T, L> linearizer =
            Linearizer<T, L>.Create(memory, bounds, contains, geometry);

        // Finalize.
        BumpToken2D<byte> lineBlocks = memory.Frame.Alloc2D<byte>(L.BlockSize, (int) bounds.RowCount);

        BumpToken<int> firstLineBlockCounts = memory.Frame.Alloc<int>((int) bounds.RowCount);
        Span<int> countSpan = firstLineBlockCounts.AsSpan();

        for (TileIndex i = 0; i < bounds.RowCount; i++)
        {
            ref L la = ref linearizer.GetLineArrayAtIndex(i);

            BumpToken<byte> block = la.GetFrontBlock();
            if (!block.HasValue)
            {
                continue;
            }

            lineBlocks[(int) i] = la.GetFrontBlock();
            countSpan[(int) i] = la.GetFrontBlockLineCount();
        }

        BumpToken2D<F24Dot8> startCoverTable = linearizer.GetStartCoverTable();
        if (startCoverTable.HasValue)
        {
            for (int i = 0; i < bounds.RowCount; i++)
            {
                BumpToken<F24Dot8> t = startCoverTable[i];

                if (t.HasValue && T.CoverArrayContainsOnlyZeroes(t))
                {
                    // Don't need cover array after all,
                    // all segments cancelled each other.
                    startCoverTable[i] = default;
                }
            }
        }
        
        placement = new RasterizableGeometry(
            geometryIndex, 
            bounds,
            lineBlocks,
            firstLineBlockCounts,
            startCoverTable);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Vertical_Down(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex columnIndex,
        F24Dot8 y0, F24Dot8 y1,
        F24Dot8 x)
    {
        Debug.Assert(y0 < y1);

        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(y0);
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(y1 - Epsilon);
        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);
        F24Dot8 fx = x - PixelIndexToF24Dot8(columnIndex);

        if (rowIndex0 == rowIndex1)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, fy1);
            return;
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, One);

        for (PixelIndex i = rowIndex0 + 1; i < rowIndex1; i++)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, i, fx, Zero, One);
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex1, fx, Zero, fy1);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Vertical_Up(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex columnIndex,
        F24Dot8 y0, F24Dot8 y1,
        F24Dot8 x)
    {
        Debug.Assert(y0 > y1);

        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(y0 - Epsilon);
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(y1);
        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);
        F24Dot8 fx = x - PixelIndexToF24Dot8(columnIndex);

        if (rowIndex0 == rowIndex1)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, fy1);
            return;
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, Zero);

        for (PixelIndex i = rowIndex0 - 1; i > rowIndex1; i--)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, i, fx, One, Zero);
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex1, fx, One, fy1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CellVertical(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex px,
        PixelIndex py,
        F24Dot8 x,
        F24Dot8 y0, F24Dot8 y1)
    {
        ref CoverArea ca = ref coverAreaTable[(int) py][(int) px];

        Cell(bitVectorTable[(int) py], ref ca, px, x, y0, x, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Cell(
        Span<BitVector> bitVector,
        Span<CoverArea> coverArea,
        PixelIndex px,
        F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        ref CoverArea ca = ref coverArea[(int) px];

        Cell(bitVector, ref ca, px, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Cell(
        Span<BitVector> bitVector,
        ref CoverArea ca,
        PixelIndex px,
        F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        F24Dot8 delta = y0 - y1;
        F24Dot8 a = delta * (2.ToF24D8() - x0 - x1);

        if (ConditionalSetBit(bitVector, px))
        {
            // New.
            ca.Delta = delta;
            ca.Area = a;
        }
        else
        {
            // Update old.
            ca.Delta += delta;
            ca.Area += a;
        }
    }

    /// <summary>
    /// ⬊
    /// 
    /// Rasterize line within single pixel row. Line must go from left to right.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RowDownR(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(rowIndex < (uint) T.TileH);
        Debug.Assert(p0x < p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y <= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x - Epsilon);

        Debug.Assert(columnIndex0 <= columnIndex1);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        AssertInOneX(fx0, fx1);

        if (columnIndex0 == columnIndex1)
        {
            Cell(bitVector, coverArea, columnIndex0, fx0, p0y, fx1, p1y);
            return;
        }

        // Horizontal and vertical deltas.
        F24Dot8 dx = p1x - p0x;
        F24Dot8 dy = p1y - p0y;

        F24Dot8 pp = (One - fx0) * dy;

        F24Dot8 cy = p0y + (pp / dx);

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, One, cy);

        PixelIndex idx = columnIndex0 + 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = One * dy;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dx);
            
            Span<CoverArea> coverSpan = coverArea[..(int) columnIndex1];
            for (PixelIndex i = idx; i < (uint) coverSpan.Length; i++)
            {
                F24Dot8 delta = lift;

                mod += rem;

                if (mod >= 0)
                {
                    mod -= dx;
                    delta++;
                }

                F24Dot8 ny = cy + delta;

                Cell(bitVector, coverSpan, i, Zero, cy, One, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, Zero, cy, fx1, p1y);
    }


    /// <summary>
    /// ⬊
    /// 
    /// Rasterize line within single pixel row. Line must go from left to
    /// right or be vertical.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RowDownR_V(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x <= p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y <= p1y);

        if (p0x < p1x)
        {
            RowDownR(bitVectorTable, coverAreaTable, rowIndex, p0x, p0y, p1x, p1y);
        }
        else
        {
            PixelIndex columnIndex = F24Dot8ToPixelIndex(p0x - FindAdjustment(p0x));
            F24Dot8 x = p0x - PixelIndexToF24Dot8(columnIndex);

            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex, x, p0y, p1y);
        }
    }


    /// <summary>
    /// ⬈
    ///
    /// Rasterize line within single pixel row. Line must go from left to right.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RowUpR(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x < p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y >= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x - Epsilon);

        Debug.Assert(columnIndex0 <= columnIndex1);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        AssertInOneX(fx0, fx1);

        if (columnIndex0 == columnIndex1)
        {
            Cell(bitVector, coverArea, columnIndex0, fx0, p0y, fx1, p1y);
            return;
        }

        // Horizontal and vertical deltas.
        F24Dot8 dx = p1x - p0x;
        F24Dot8 dy = p0y - p1y;

        F24Dot8 pp = (One - fx0) * dy;

        F24Dot8 cy = p0y - (pp / dx);

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, One, cy);

        PixelIndex idx = columnIndex0 + 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = One * dy;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dx);

            Span<CoverArea> coverSpan = coverArea[..(int) columnIndex1];
            for (PixelIndex i = idx; i < (uint) coverSpan.Length; i++)
            {
                F24Dot8 delta = lift;

                mod += rem;

                if (mod >= 0)
                {
                    mod -= dx;
                    delta++;
                }

                F24Dot8 ny = cy - delta;

                Cell(bitVector, coverSpan, i, Zero, cy, One, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, Zero, cy, fx1, p1y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RowUpR_V(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x <= p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y >= p1y);

        if (p0x < p1x)
        {
            RowUpR(bitVectorTable, coverAreaTable, rowIndex, p0x, p0y, p1x, p1y);
        }
        else
        {
            PixelIndex columnIndex = F24Dot8ToPixelIndex(p0x - FindAdjustment(p0x));
            F24Dot8 x = p0x - PixelIndexToF24Dot8(columnIndex);

            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex, x, p0y, p1y);
        }
    }


    /// <summary>
    /// ⬋
    /// 
    /// Rasterize line within single pixel row. Line must go from right to left.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RowDownL(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x > p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y <= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x - Epsilon);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x);

        Debug.Assert(columnIndex1 <= columnIndex0);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        AssertInOneX(fx0, fx1);

        if (columnIndex0 == columnIndex1)
        {
            Cell(bitVector, coverArea, columnIndex0, fx0, p0y, fx1, p1y);
            return;
        }

        // Horizontal and vertical deltas.
        F24Dot8 dx = p0x - p1x;
        F24Dot8 dy = p1y - p0y;

        F24Dot8 pp = fx0 * dy;

        F24Dot8 cy = p0y + (pp / dx);

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, Zero, cy);

        PixelIndex idx = columnIndex0 - 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = One * dy;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dx);

            for (; idx != columnIndex1; idx--)
            {
                F24Dot8 delta = lift;

                mod += rem;

                if (mod >= 0)
                {
                    mod -= dx;
                    delta++;
                }

                F24Dot8 ny = cy + delta;

                Cell(bitVector, coverArea, idx, One, cy, Zero, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, One, cy, fx1, p1y);
    }


    /// <summary>
    /// ⬋
    /// 
    /// Rasterize line within single pixel row. Line must go from right to
    /// left or be vertical.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RowDownL_V(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y <= p1y);

        if (p0x > p1x)
        {
            RowDownL(bitVectorTable, coverAreaTable, rowIndex, p0x, p0y, p1x, p1y);
        }
        else
        {
            PixelIndex columnIndex = F24Dot8ToPixelIndex(p0x - FindAdjustment(p0x));
            F24Dot8 x = p0x - PixelIndexToF24Dot8(columnIndex);

            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex, x, p0y, p1y);
        }
    }


    /// <summary>
    /// ⬉
    ///
    /// Rasterize line within single pixel row. Line must go from right to left.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RowUpL(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x > p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y >= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x - Epsilon);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x);

        Debug.Assert(columnIndex1 <= columnIndex0);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        AssertInOneX(fx0, fx1);

        if (columnIndex0 == columnIndex1)
        {
            Cell(bitVector, coverArea, columnIndex0, fx0, p0y, fx1, p1y);
            return;
        }

        // Horizontal and vertical deltas.
        F24Dot8 dx = p0x - p1x;
        F24Dot8 dy = p0y - p1y;

        F24Dot8 pp = fx0 * dy;

        F24Dot8 cy = p0y - (pp / dx);

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, Zero, cy);

        PixelIndex idx = columnIndex0 - 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = One * dy;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dx);

            for (; idx != columnIndex1; idx--)
            {
                F24Dot8 delta = lift;

                mod += rem;

                if (mod >= 0)
                {
                    mod -= dx;
                    delta++;
                }

                F24Dot8 ny = cy - delta;

                Cell(bitVector, coverArea, idx, One, cy, Zero, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, One, cy, fx1, p1y);
    }


    /// <summary>
    /// ⬉
    /// 
    /// Rasterize line within single pixel row. Line must go from right to
    /// left or be vertical.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RowUpL_V(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        AssertInTileY(p0y, p1y);
        Debug.Assert(p0y >= p1y);

        if (p0x > p1x)
        {
            RowUpL(bitVectorTable, coverAreaTable, rowIndex, p0x, p0y, p1x, p1y);
        }
        else
        {
            PixelIndex columnIndex = F24Dot8ToPixelIndex(p0x - FindAdjustment(p0x));
            F24Dot8 x = p0x - PixelIndexToF24Dot8(columnIndex);

            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex, x, p0y, p1y);
        }
    }


    /// <summary>
    /// ⬊
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LineDownR(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex0,
        PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(y0 < y1);
        Debug.Assert(x0 < x1);
        Debug.Assert(rowIndex0 < rowIndex1);

        F24Dot8 dx = x1 - x0;
        F24Dot8 dy = y1 - y0;

        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);

        F24Dot8 p = (One - fy0) * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = x0 + delta;

        RowDownR_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, One);

        PixelIndex idy = rowIndex0 + 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = One * dx;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dy);

            for (; idy != rowIndex1; idy++)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx + delta;

                RowDownR_V(bitVectorTable, coverAreaTable, idy, cx, Zero, nx, One);

                cx = nx;
            }
        }

        RowDownR_V(bitVectorTable, coverAreaTable, rowIndex1, cx, Zero, x1, fy1);
    }


    /**
     * ⬈
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LineUpR(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex0,
        PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(y0 > y1);
        Debug.Assert(x0 < x1);
        Debug.Assert(rowIndex0 > rowIndex1);

        F24Dot8 dx = x1 - x0;
        F24Dot8 dy = y0 - y1;

        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);

        F24Dot8 p = fy0 * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = x0 + delta;

        RowUpR_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, Zero);

        PixelIndex idy = rowIndex0 - 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = One * dx;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dy);

            for (; idy != rowIndex1; idy--)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx + delta;

                RowUpR_V(bitVectorTable, coverAreaTable, idy, cx, One, nx, Zero);

                cx = nx;
            }
        }

        RowUpR_V(bitVectorTable, coverAreaTable, rowIndex1, cx, One, x1, fy1);
    }


    /**
     * ⬋
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LineDownL(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex0,
        PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(y0 < y1);
        Debug.Assert(x0 > x1);
        Debug.Assert(rowIndex0 < rowIndex1);

        F24Dot8 dx = x0 - x1;
        F24Dot8 dy = y1 - y0;

        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);

        F24Dot8 p = (One - fy0) * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = x0 - delta;

        RowDownL_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, One);

        PixelIndex idy = rowIndex0 + 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = One * dx;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dy);

            for (; idy != rowIndex1; idy++)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx - delta;

                RowDownL_V(bitVectorTable, coverAreaTable, idy, cx, Zero, nx, One);

                cx = nx;
            }
        }

        RowDownL_V(bitVectorTable, coverAreaTable, rowIndex1, cx, Zero, x1, fy1);
    }


    /**
     * ⬉
     */
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LineUpL(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex0,
        PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(y0 > y1);
        Debug.Assert(x0 > x1);
        Debug.Assert(rowIndex0 > rowIndex1);

        F24Dot8 dx = x0 - x1;
        F24Dot8 dy = y0 - y1;

        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);

        F24Dot8 p = fy0 * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = x0 - delta;

        RowUpL_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, Zero);

        PixelIndex idy = rowIndex0 - 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = One * dx;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dy);

            for (; idy != rowIndex1; idy--)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx - delta;

                RowUpL_V(bitVectorTable, coverAreaTable, idy, cx, One, nx, Zero);

                cx = nx;
            }
        }

        RowUpL_V(bitVectorTable, coverAreaTable, rowIndex1, cx, One, x1, fy1);
    }


    private static void RasterizeLine(
        F24Dot8 X0, F24Dot8 Y0,
        F24Dot8 X1, F24Dot8 Y1,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        Debug.Assert(Y0 != Y1);

        if (X0 == X1)
        {
            PixelIndex columnIndex = F24Dot8ToPixelIndex(X0 - FindAdjustment(X0));

            // Special case, vertical line, simplifies this thing a lot.
            if (Y0 < Y1)
            {
                // Line is going down ↓
                Vertical_Down(bitVectorTable, coverAreaTable, columnIndex, Y0, Y1, X0);
            }
            else
            {
                // Line is going up ↑
                Vertical_Up(bitVectorTable, coverAreaTable, columnIndex, Y0, Y1, X0);
            }
            return;
        }

        if (Y0 < Y1)
        {
            RasterizeLineDown(X0, Y0, X1, Y1, bitVectorTable, coverAreaTable);
        }
        else
        {
            RasterizeLineUp(X0, Y0, X1, Y1, bitVectorTable, coverAreaTable);
        }
    }

    private static void RasterizeLineDown(
        F24Dot8 X0, F24Dot8 Y0, F24Dot8 X1, F24Dot8 Y1,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        // Line is going down ↓
        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(Y0);
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(Y1 - Epsilon);

        Debug.Assert(rowIndex0 <= rowIndex1);

        if (rowIndex0 == rowIndex1)
        {
            // Entire line is completely within horizontal band. For curves
            // this is common case.
            F24Dot8 ty = PixelIndexToF24Dot8(rowIndex0);
            F24Dot8 y0 = Y0 - ty;
            F24Dot8 y1 = Y1 - ty;

            if (X0 < X1)
            {
                RowDownR(bitVectorTable, coverAreaTable, rowIndex0, X0, y0, X1, y1);
            }
            else
            {
                RowDownL(bitVectorTable, coverAreaTable, rowIndex0, X0, y0, X1, y1);
            }
        }
        else if (X0 < X1)
        {
            // Line is going from left to right →
            LineDownR(bitVectorTable, coverAreaTable, rowIndex0, rowIndex1, X0, Y0, X1, Y1);
        }
        else
        {
            // Line is going right to left ←
            LineDownL(bitVectorTable, coverAreaTable, rowIndex0, rowIndex1, X0, Y0, X1, Y1);
        }
    }

    private static void RasterizeLineUp(
        F24Dot8 X0, F24Dot8 Y0,
        F24Dot8 X1, F24Dot8 Y1,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        // Line is going up ↑
        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(Y0 - Epsilon);
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(Y1);

        Debug.Assert(rowIndex1 <= rowIndex0);

        if (rowIndex0 == rowIndex1)
        {
            // Entire line is completely within horizontal band. For curves
            // this is common case.
            F24Dot8 ty = PixelIndexToF24Dot8(rowIndex0);
            F24Dot8 y0 = Y0 - ty;
            F24Dot8 y1 = Y1 - ty;

            if (X0 < X1)
            {
                RowUpR(bitVectorTable, coverAreaTable, rowIndex0, X0, y0, X1, y1);
            }
            else
            {
                RowUpL(bitVectorTable, coverAreaTable, rowIndex0, X0, y0, X1, y1);
            }
        }
        else if (X0 < X1)
        {
            // Line is going from left to right →
            LineUpR(bitVectorTable, coverAreaTable, rowIndex0, rowIndex1, X0, Y0, X1, Y1);
        }
        else
        {
            // Line is going right to left ←
            LineUpL(bitVectorTable, coverAreaTable, rowIndex0, rowIndex1, X0, Y0, X1, Y1);
        }
    }


    /// <summary>
    /// Rasterize one item within a single row.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RasterizeOneItem(
        int localRowIndex,
        in RasterizableGeometry raster,
        in Geometry geometry,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        in ImageData image,
        LineRasterizer lineRasterizer)
    {
        TileBounds bounds = raster.Bounds;

        uint rowWidth = bounds.ColumnCount * (uint) T.TileW;
        uint bitVectorsPerRow = BitVectorsForMaxBitCount(rowWidth);

        // Limit views to proper dimensions.
        Span2D<BitVector> bitVectorIter = bitVectorTable.Cut((int) bitVectorsPerRow);
        Span2D<CoverArea> coverAreaIter = coverAreaTable.Cut((int) rowWidth);

        // Erase bit vector table.
        bitVectorIter.Clear();

        if (IsNarrow(bounds))
        {
            IterateLinesX16Y16(localRowIndex, in raster, bitVectorIter, coverAreaIter);
        }
        else
        {
            IterateLinesX32Y16(localRowIndex, in raster, bitVectorIter, coverAreaIter);
        }

        // X position, measured in pixels.
        int pX0 = (int) bounds.X * T.TileW;

        Debug.Assert((pX0 & (T.TileW - 1)) == 0, "X must be aligned on tile boundary.");

        Debug.Assert(rowWidth <= image.Width - pX0, "Row overruns image width.");

        // Y position, measured in tiles.
        int y = (int) (bounds.Y + localRowIndex);

        // Y position, measured in pixels.
        int pY0 = y * T.TileH;

        // Maximum y position, measured in pixels.
        int pY1 = pY0 + T.TileH;

        // Calculate maximum height. This can only get less than TileH when rendering
        // the last row of the image and image height is not multiple of row height.
        int viewHeight = Math.Min(pY1, image.Height) - pY0;

        int viewByteWidth = Math.Min((int) rowWidth, image.Width - pX0) * image.BytesPerPixel;

        Span2D<byte> rowView = image
            .GetSpan2D<byte>()
            .Slice(pX0 * image.BytesPerPixel, pY0, viewByteWidth, viewHeight);
            
        lineRasterizer.Rasterize(localRowIndex, raster, geometry, rowView, bitVectorIter, coverAreaIter);
    }


    /// <summary>
    /// Rasterize all items in one row.
    /// </summary>
    private static void RasterizeRow(
        in RowItemList<RasterizableItem> rowList,
        ReadOnlySpan<RasterizableGeometry> rasterizables,
        ReadOnlySpan<Geometry> geometries,
        ThreadMemory memory,
        in ImageData image,
        LineRasterizer lineRasterizer)
    {
        // How many columns can fit into image.
        uint columnCount = CalculateColumnCount<T>(image.Width);

        // Create bit vector arrays.
        int bitVectorsPerRow = (int) BitVectorsForMaxBitCount(columnCount * (uint) T.TileW);
        int bitVectorCount = bitVectorsPerRow * T.TileH;

        Span2D<BitVector> bitVectors = new(
            memory.Task.Alloc<BitVector>(bitVectorCount).AsSpan(),
            bitVectorsPerRow,
            T.TileH);

        // Create cover/area table.
        int coverAreaPerRow = (int) columnCount * T.TileW;
        int coverAreaCount = coverAreaPerRow * T.TileH;

        Span2D<CoverArea> coverArea = new(
            memory.Task.Alloc<CoverArea>(coverAreaCount).AsSpan(),
            coverAreaPerRow,
            T.TileH);

        // Rasterize all items, from bottom to top that were added to this row.
        RowItemList<RasterizableItem>.Block* b = rowList.First;

        while (b != null)
        {
            foreach (RasterizableItem item in b->AsSpan())
            {
                ref readonly RasterizableGeometry raster = ref rasterizables[item.Rasterizable];
                ref readonly Geometry geometry = ref geometries[raster.Geometry];
                
                Debug.Assert(raster.Bounds.ColumnCount <= columnCount);
                
                RasterizeOneItem(
                    item.LocalRowIndex,
                    in raster,
                    in geometry,
                    bitVectors,
                    coverArea,
                    in image,
                    lineRasterizer);
            }

            b = b->Next;
        }
    }

    [Conditional("DEBUG")]
    private static void AssertInOneX(F24Dot8 x0, F24Dot8 x1)
    {
        Debug.Assert(x0 >= 0);
        Debug.Assert(x0 <= One);
        
        Debug.Assert(x1 >= 0);
        Debug.Assert(x1 <= One);
    }

    [Conditional("DEBUG")]
    private static void AssertInTileY(F24Dot8 y0, F24Dot8 y1)
    {
        Debug.Assert(y0 >= 0);
        Debug.Assert(y0 <= T.TileHF24Dot8);
        
        Debug.Assert(y1 >= 0);
        Debug.Assert(y1 <= T.TileHF24Dot8);
    }
}