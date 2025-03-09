using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpBlaze;

using static Utils;
using static RasterizerUtils;
using static BitOps;
using static Linearizer;

using static F24Dot8;
using static F8Dot8;

using PixelIndex = uint;

internal interface IFillRuleFn
{
    static abstract int ApplyFillRule(int value);
}

file struct AreaToAlphaNonZeroFn : IFillRuleFn
{
    public static int ApplyFillRule(int value) => AreaToAlphaNonZero(value);
}

file struct AreaToAlphaEvenOddFn : IFillRuleFn
{
    public static int ApplyFillRule(int value) => AreaToAlphaEvenOdd(value);
}

public unsafe partial struct Rasterizer<T>
    where T : unmanaged, ITileDescriptor
{

    public static partial void Rasterize(
        ReadOnlySpan<Geometry> geometries,
        in Matrix matrix,
        Executor threads,
        ImageData image);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PixelIndex F24Dot8ToPixelIndex(F24Dot8 x)
    {
        return (PixelIndex) (x >> 8);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static F24Dot8 PixelIndexToF24Dot8(PixelIndex x)
    {
        return (F24Dot8) (x) << 8;
    }

    struct LineIterationFunction
    {
        public required delegate* managed<
            int, 
            in RasterizableGeometry,
            Span2D<BitVector>,
            Span2D<CoverArea>,
            void> value;
    }

    readonly partial struct RasterizableGeometry
    {
        public RasterizableGeometry(
            int geometry,
            LineIterationFunction iterationFunction,
            TileBounds bounds,
            void** lines,
            int* firstBlockLineCounts,
            int** startCoverTable)
        {
            Geometry = geometry;
            IterationFunction = iterationFunction;
            Bounds = bounds;
            Lines = lines;
            FirstBlockLineCounts = firstBlockLineCounts;
            StartCoverTable = startCoverTable;
        }

        public readonly int Geometry;
        public readonly LineIterationFunction IterationFunction;
        public readonly TileBounds Bounds;
        public readonly void** Lines;
        public readonly int* FirstBlockLineCounts;
        public readonly int** StartCoverTable;
    }


    readonly record struct RasterizableItem(int Rasterizable, int LocalRowIndex);

    [Obsolete]
    public Rasterizer() { }

    public static partial void Rasterize(
        ReadOnlySpan<Geometry> geometries,
        in Matrix matrix,
        Executor threads,
        ImageData image)
    {
        Debug.Assert(geometries.Length > 0);
        Debug.Assert(image.Data != null);
        Debug.Assert(image.Width > 0);
        Debug.Assert(image.Height > 0);
        Debug.Assert(image.BytesPerRow >= (image.Width * 4));

        StepState1 state1 = Step1(threads, image, geometries, matrix);
        StepState2 state2 = Step2(threads, state1);
        Step3(threads, image, geometries, state2);
    }

    private ref struct StepState1
    {
        public required Span<int> rasterIndices;
        public required Span<RasterizableGeometry> rasters;
        public required ReadOnlySpan<Geometry> geometries;
        public required Matrix transform;
        public required IntSize imageSize;
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
        Span<int> rasterIndices = new(
            threads.MainMemory.FrameMallocArray<int>(geometries.Length),
            geometries.Length);
        
        // Allocate memory for RasterizableGeometry instances.
        Span<RasterizableGeometry> rasters = new(
            threads.MainMemory.FrameMallocArray<RasterizableGeometry>(geometries.Length),
            geometries.Length);

        IntSize imageSize = new(
            image.Width,
            image.Height
        );

        StepState1 state1 = new()
        {
            rasterIndices = rasterIndices,
            rasters = rasters,
            geometries = geometries,
            transform = transform,
            imageSize = imageSize,
        };
        threads.For(0, geometries.Length, &state1, static (index, state, memory) =>
        {
            StepState1* s = (StepState1*) state;

            ref readonly Geometry srcGeo = ref s->geometries[index];
            
            Matrix tm = srcGeo.TM * s->transform;
            IntRect bounds = tm.MapBoundingRect(srcGeo.PathBounds);

            Geometry rasterGeo = new(
                bounds,
                srcGeo.Tags,
                srcGeo.Points, 
                tm, 
                srcGeo.Color, 
                srcGeo.Rule
            );

            bool hasRaster = CreateRasterizable(
                out s->rasters[index],
                rasterGeo,
                index,
                s->imageSize,
                memory);

            s->rasterIndices[index] = hasRaster ? index : -1;
        });

        // Linearizer may decide that some paths do not contribute to the final
        // image. In these situations CreateRasterizable will return null.
        // In the following step, non-null pointers are packed into front of array.

        int rasterCount = 0;

        for (int i = 0; i < rasterIndices.Length; i++)
        {
            int rasterIndex = rasterIndices[i];
            if (rasterIndex != -1)
            {
                rasterIndices[rasterCount++] = rasterIndex;
            }
        }

        state1.rasterIndices = rasterIndices.Slice(0, rasterCount);

        return state1;
    }

    private ref struct StepState2
    {
        public required ReadOnlySpan<int> rasterIndices;
        public required ReadOnlySpan<RasterizableGeometry> rasters;

        public required TileIndex rowCount;
        public required RowItemList<RasterizableItem>* rowLists;
        public required int iterationHeight;
    }

    /// <summary>
    /// Create lists of rasterizable items for each interval.
    /// </summary>
    private static StepState2 Step2(Executor threads, in StepState1 state1)
    {
        TileIndex rowCount = CalculateRowCount<T>(state1.imageSize.Height);

        RowItemList<RasterizableItem>* rowLists =
            threads.MainMemory.FrameMallocArray<RowItemList<RasterizableItem>>((int) rowCount);

        int threadCount = Math.Max(1, threads.WorkerCount);

        int iterationHeight = (int) Math.Max((uint) ((int) rowCount / threadCount), 1);

        int iterationCount = ((int) rowCount / iterationHeight) +
            (int) Math.Min((uint) ((int) rowCount % iterationHeight), 1);

        StepState2 state2 = new()
        {
            rasterIndices = state1.rasterIndices,
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
                *(s->rowLists + i) = new RowItemList<RasterizableItem>();
            }

            foreach (int rasterIndex in s->rasterIndices)
            {
                ref readonly RasterizableGeometry rasterizable = ref s->rasters[rasterIndex];
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
                        rasterizable.GetLinesForRow((int) localIndex) == null &&
                        rasterizable.GetCoversForRow((int) localIndex) == null;

                    if (emptyRow)
                    {
                        // Both conditions failed, this geometry for this row will
                        // not produce any visible pixels.
                        continue;
                    }

                    RowItemList<RasterizableItem>* list = s->rowLists + y;

                    list->Append(memory, new RasterizableItem(rasterIndex, (int) localIndex));
                }
            }
        });

        return state2;
    }

    private ref struct StepState3
    {
        public required RowItemList<RasterizableItem>* rowLists;
        public required ImageData image;
        public required ReadOnlySpan<RasterizableGeometry> rasters;
        public required ReadOnlySpan<Geometry> geometries;
    }

    /// <summary>
    /// Rasterize all intervals.
    /// </summary>
    private static void Step3(
        Executor threads, 
        in ImageData image,
        ReadOnlySpan<Geometry> geometries, 
        in StepState2 state2)
    {
        StepState3 state3 = new()
        {
            rowLists = state2.rowLists,
            image = image,
            rasters = state2.rasters,
            geometries = geometries,
        };
        threads.For(0, (int) state2.rowCount, &state3, static (rowIndex, state, memory) =>
        {
            StepState3* s = (StepState3*) state;

            RowItemList<RasterizableItem>* item = s->rowLists + rowIndex;

            RasterizeRow(item, s->rasters, s->geometries, memory, s->image);
        });
    }

    partial struct RasterizableGeometry
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void* GetLinesForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            return Lines[rowIndex];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetFirstBlockLineCountForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            return FirstBlockLineCounts[rowIndex];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetCoversForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            if (StartCoverTable == null)
            {
                // No table at all.
                return default;
            }

            return new Span<int>(StartCoverTable[rowIndex], T.TileH);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<int> GetActualCoversForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            if (StartCoverTable == null)
            {
                // No table at all.
                return default;
            }

            int* covers = StartCoverTable[rowIndex];
            if (covers == null)
            {
                return default;
            }

            return new(covers, T.TileH);
        }
    }

    private static void IterateLinesX32Y16(
        int localRowIndex, 
        in RasterizableGeometry raster,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        int count = raster.GetFirstBlockLineCountForRow(localRowIndex);

        LineArrayX32Y16Block* v = (LineArrayX32Y16Block*) raster.GetLinesForRow(localRowIndex);

        while (v != null)
        {
            F8Dot8x2* yy = &v->Y0Y1[0];
            F24Dot8* xx0 = &v->X0[0];
            F24Dot8* xx1 = &v->X1[0];

            for (int i = 0; i < count; i++)
            {
                F8Dot8x2 y0y1 = yy[i];
                F24Dot8 x0 = xx0[i];
                F24Dot8 x1 = xx1[i];

                F24Dot8 y0 = UnpackLoFromF8Dot8x2(y0y1);
                F24Dot8 y1 = UnpackHiFromF8Dot8x2(y0y1);

                RasterizeLine(x0, y0, x1, y1, bitVectorTable, coverAreaTable);
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

        LineArrayX16Y16Block* v = (LineArrayX16Y16Block*) raster.GetLinesForRow(localRowIndex);

        while (v != null)
        {
            F8Dot8x2* yy = &v->Y0Y1[0];
            F8Dot8x2* xx = &v->X0X1[0];

            for (int i = 0; i < count; i++)
            {
                F8Dot8x2 y0y1 = yy[i];
                F8Dot8x2 x0x1 = xx[i];

                RasterizeLine(
                    UnpackLoFromF8Dot8x2(x0x1),
                    UnpackLoFromF8Dot8x2(y0y1),
                    UnpackHiFromF8Dot8x2(x0x1),
                    UnpackHiFromF8Dot8x2(y0y1),
                    bitVectorTable,
                    coverAreaTable);
            }

            v = v->Next;
            count = LineArrayX32Y16Block.LinesPerBlock;
        }
    }

    private static bool CreateRasterizable(
        out RasterizableGeometry placement,
        in Geometry geometry,
        int geometryIndex,
        IntSize imageSize,
        ThreadMemory memory)
    {
        Debug.Assert(imageSize.Width > 0);
        Debug.Assert(imageSize.Height > 0);

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
        // edge of destination image instead of terminating at the right rectangle
        // edge.
        //
        // To solve this problem 1 is added to the maximum X coordinate of path
        // bounding box to allow inserting vertical lines at the right edge of
        // path bounding box so shapes get a chance to terminate fill. Perhaps
        // there are better ways to solve this (maybe clipper should not ignore
        // lines at the maximum X edge of path bounds?), but for now I'm keeping
        // this fix.

        IntRect geometryBounds = geometry.PathBounds;

        if (geometryBounds.MinX == geometryBounds.MaxX)
        {
            return false;
        }

        int minx = Math.Max(0, geometryBounds.MinX);
        int miny = Math.Max(0, geometryBounds.MinY);
        int maxx = Math.Min(imageSize.Width, geometryBounds.MaxX + 1);
        int maxy = Math.Min(imageSize.Height, geometryBounds.MaxY);

        if (minx >= maxx || miny >= maxy)
        {
            // Geometry bounds do not intersect with destination image.
            return false;
        }

        TileBounds bounds = CalculateTileBounds<T>(minx, miny, maxx, maxy);

        bool narrow = 128 > (bounds.ColumnCount * T.TileW);

        if (narrow)
        {
            LineIterationFunction fn = new() { value = &IterateLinesX16Y16 };
            Linearize<LineArrayX16Y16>(
                out placement, geometry, geometryIndex, bounds, imageSize, fn, memory);
        }
        else
        {
            LineIterationFunction fn = new() { value = &IterateLinesX32Y16 };
            Linearize<LineArrayX32Y16>(
                out placement, geometry, geometryIndex, bounds, imageSize, fn, memory);
        }
        return true;
    }


    private static void Linearize<L>(
        out RasterizableGeometry placement,
        in Geometry geometry,
        int geometryIndex,
        TileBounds bounds,
        IntSize imageSize,
        LineIterationFunction iterationFunction,
        ThreadMemory memory)
        where L : unmanaged, ILineArrayBlock<L>
    {
        // Determine if path is completely within destination image bounds. If
        // geometry bounds fit within destination image, a shortcut can be made
        // when generating lines.
        bool contains =
            geometry.PathBounds.MinX >= 0 &&
            geometry.PathBounds.MinY >= 0 &&
            geometry.PathBounds.MaxX <= imageSize.Width &&
            geometry.PathBounds.MaxY <= imageSize.Height;

        Linearizer<T, L> linearizer =
            Linearizer<T, L>.Create(memory, bounds, contains, geometry);

        // Finalize.
        void** lineBlocks = (void**) memory.FrameMallocArray<nint>((int) bounds.RowCount);

        int* firstLineBlockCounts = memory.FrameMallocArray<int>((int) bounds.RowCount);

        for (TileIndex i = 0; i < bounds.RowCount; i++)
        {
            ref L la = ref linearizer.GetLineArrayAtIndex(i);

            //Debug.Assert(la != null);

            if (la.GetFrontBlock() == null)
            {
                lineBlocks[i] = null;
                firstLineBlockCounts[i] = 0;
                continue;
            }

            lineBlocks[i] = la.GetFrontBlock();
            firstLineBlockCounts[i] = la.GetFrontBlockLineCount();
        }

        int** startCoverTable = linearizer.GetStartCoverTable();
        if (startCoverTable != null)
        {
            for (int i = 0; i < bounds.RowCount; i++)
            {
                int* t = startCoverTable[i];

                if (t != null && T.CoverArrayContainsOnlyZeroes(new (t, T.TileH)))
                {
                    // Don't need cover array after all,
                    // all segments cancelled each other.
                    startCoverTable[i] = null;
                }
            }
        }
        
        placement = new RasterizableGeometry(
            geometryIndex, 
            iterationFunction,
            bounds,
            lineBlocks,
            firstLineBlockCounts,
            startCoverTable);
    }


    private static void Vertical_Down(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex columnIndex,
        F24Dot8 y0, F24Dot8 y1,
        F24Dot8 x)
    {
        Debug.Assert(y0 < y1);

        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(y0);
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(y1 - 1);
        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);
        F24Dot8 fx = x - PixelIndexToF24Dot8(columnIndex);

        if (rowIndex0 == rowIndex1)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, fy1);
            return;
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, F24Dot8_1);

        for (PixelIndex i = rowIndex0 + 1; i < rowIndex1; i++)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, i, fx, 0, F24Dot8_1);
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex1, fx, 0, fy1);
    }


    private static void Vertical_Up(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex columnIndex,
        F24Dot8 y0, F24Dot8 y1,
        F24Dot8 x)
    {
        Debug.Assert(y0 > y1);

        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(y0 - 1);
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(y1);
        F24Dot8 fy0 = y0 - PixelIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - PixelIndexToF24Dot8(rowIndex1);
        F24Dot8 fx = x - PixelIndexToF24Dot8(columnIndex);

        if (rowIndex0 == rowIndex1)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, fy1);
            return;
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, 0);

        for (PixelIndex i = rowIndex0 - 1; i > rowIndex1; i--)
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, i, fx, F24Dot8_1, 0);
        }

        CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex1, fx, F24Dot8_1, fy1);
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
        int delta = y0 - y1;
        int a = delta * (F24Dot8_2 - x0 - x1);

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
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x < p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x - 1);

        Debug.Assert(columnIndex0 <= columnIndex1);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        Debug.Assert(fx0 >= 0);
        Debug.Assert(fx0 <= F24Dot8_1);
        Debug.Assert(fx1 >= 0);
        Debug.Assert(fx1 <= F24Dot8_1);

        if (columnIndex0 == columnIndex1)
        {
            Cell(bitVector, coverArea, columnIndex0, fx0, p0y, fx1, p1y);
            return;
        }

        // Horizontal and vertical deltas.
        F24Dot8 dx = p1x - p0x;
        F24Dot8 dy = p1y - p0y;

        F24Dot8 pp = (F24Dot8_1 - fx0) * dy;

        F24Dot8 cy = p0y + (pp / dx);

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, F24Dot8_1, cy);

        PixelIndex idx = columnIndex0 + 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = F24Dot8_1 * dy;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dx);

            for (; idx != columnIndex1; idx++)
            {
                F24Dot8 delta = lift;

                mod += rem;

                if (mod >= 0)
                {
                    mod -= dx;
                    delta++;
                }

                F24Dot8 ny = cy + delta;

                Cell(bitVector, coverArea, idx, 0, cy, F24Dot8_1, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, 0, cy, fx1, p1y);
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
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
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
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x < p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x - 1);

        Debug.Assert(columnIndex0 <= columnIndex1);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        Debug.Assert(fx0 >= 0);
        Debug.Assert(fx0 <= F24Dot8_1);
        Debug.Assert(fx1 >= 0);
        Debug.Assert(fx1 <= F24Dot8_1);

        if (columnIndex0 == columnIndex1)
        {
            Cell(bitVector, coverArea, columnIndex0, fx0, p0y, fx1, p1y);
            return;
        }

        // Horizontal and vertical deltas.
        F24Dot8 dx = p1x - p0x;
        F24Dot8 dy = p0y - p1y;

        F24Dot8 pp = (F24Dot8_1 - fx0) * dy;

        F24Dot8 cy = p0y - (pp / dx);

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, F24Dot8_1, cy);

        PixelIndex idx = columnIndex0 + 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = F24Dot8_1 * dy;
            (F24Dot8 lift, F24Dot8 rem) = DivRem(p, dx);

            Span<CoverArea> coverSpan = coverArea.Slice(0, (int) columnIndex1);
            for (PixelIndex i = idx; i < coverSpan.Length; i++)
            {
                F24Dot8 delta = lift;

                mod += rem;

                if (mod >= 0)
                {
                    mod -= dx;
                    delta++;
                }

                F24Dot8 ny = cy - delta;

                Cell(bitVector, coverSpan, i, 0, cy, F24Dot8_1, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, 0, cy, fx1, p1y);
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
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
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
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x > p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x - 1);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x);

        Debug.Assert(columnIndex1 <= columnIndex0);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        Debug.Assert(fx0 >= 0);
        Debug.Assert(fx0 <= F24Dot8_1);
        Debug.Assert(fx1 >= 0);
        Debug.Assert(fx1 <= F24Dot8_1);

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

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, 0, cy);

        PixelIndex idx = columnIndex0 - 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = F24Dot8_1 * dy;
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

                Cell(bitVector, coverArea, idx, F24Dot8_1, cy, 0, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, F24Dot8_1, cy, fx1, p1y);
    }


    /// <summary>
    /// ⬋
    /// 
    /// Rasterize line within single pixel row. Line must go from right to
    /// left or be vertical.
    /// </summary>
    private static void RowDownL_V(
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        PixelIndex rowIndex,
        F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
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
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x > p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        Span<BitVector> bitVector = bitVectorTable[(int) rowIndex];
        Span<CoverArea> coverArea = coverAreaTable[(int) rowIndex];

        PixelIndex columnIndex0 = F24Dot8ToPixelIndex(p0x - 1);
        PixelIndex columnIndex1 = F24Dot8ToPixelIndex(p1x);

        Debug.Assert(columnIndex1 <= columnIndex0);

        // Extract remainders.
        F24Dot8 fx0 = p0x - PixelIndexToF24Dot8(columnIndex0);
        F24Dot8 fx1 = p1x - PixelIndexToF24Dot8(columnIndex1);

        Debug.Assert(fx0 >= 0);
        Debug.Assert(fx0 <= F24Dot8_1);
        Debug.Assert(fx1 >= 0);
        Debug.Assert(fx1 <= F24Dot8_1);

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

        Cell(bitVector, coverArea, columnIndex0, fx0, p0y, 0, cy);

        PixelIndex idx = columnIndex0 - 1;

        if (idx != columnIndex1)
        {
            F24Dot8 mod = (pp % dx) - dx;

            F24Dot8 p = F24Dot8_1 * dy;
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

                Cell(bitVector, coverArea, idx, F24Dot8_1, cy, 0, ny);

                cy = ny;
            }
        }

        Cell(bitVector, coverArea, columnIndex1, F24Dot8_1, cy, fx1, p1y);
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
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
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

        F24Dot8 p = (F24Dot8_1 - fy0) * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = x0 + delta;

        RowDownR_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, F24Dot8_1);

        PixelIndex idy = rowIndex0 + 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = F24Dot8_1 * dx;
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

                RowDownR_V(bitVectorTable, coverAreaTable, idy, cx, 0, nx, F24Dot8_1);

                cx = nx;
            }
        }

        RowDownR_V(bitVectorTable, coverAreaTable, rowIndex1, cx, 0, x1, fy1);
    }


    /**
     * ⬈
     */
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

        RowUpR_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, 0);

        PixelIndex idy = rowIndex0 - 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = F24Dot8_1 * dx;
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

                RowUpR_V(bitVectorTable, coverAreaTable, idy, cx, F24Dot8_1, nx, 0);

                cx = nx;
            }
        }

        RowUpR_V(bitVectorTable, coverAreaTable, rowIndex1, cx, F24Dot8_1, x1, fy1);
    }


    /**
     * ⬋
     */
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

        F24Dot8 p = (F24Dot8_1 - fy0) * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = x0 - delta;

        RowDownL_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, F24Dot8_1);

        PixelIndex idy = rowIndex0 + 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = F24Dot8_1 * dx;
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

                RowDownL_V(bitVectorTable, coverAreaTable, idy, cx, 0, nx, F24Dot8_1);

                cx = nx;
            }
        }

        RowDownL_V(bitVectorTable, coverAreaTable, rowIndex1, cx, 0, x1, fy1);
    }


    /**
     * ⬉
     */
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

        RowUpL_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx, 0);

        PixelIndex idy = rowIndex0 - 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = F24Dot8_1 * dx;
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

                RowUpL_V(bitVectorTable, coverAreaTable, idy, cx, F24Dot8_1, nx, 0);

                cx = nx;
            }
        }

        RowUpL_V(bitVectorTable, coverAreaTable, rowIndex1, cx, F24Dot8_1, x1, fy1);
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
        PixelIndex rowIndex1 = F24Dot8ToPixelIndex(Y1 - 1);

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
        PixelIndex rowIndex0 = F24Dot8ToPixelIndex(Y0 - 1);
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


    private static void RenderOneLine<B, F>(
        Span<byte> row,
        ReadOnlySpan<BitVector> bitVectors,
        ReadOnlySpan<CoverArea> coverAreas,
        int startCover,
        B blender)
        where B : ISpanBlender
        where F : IFillRuleFn
    {
        Span<uint> d = MemoryMarshal.Cast<byte, uint>(row);

        // Cover accumulation.
        int cover = startCover;

        // Span state.
        int spanX = 0;
        int spanEnd = 0;
        uint spanAlpha = 0;

        for (int i = 0; i < bitVectors.Length; i++)
        {
            nuint bitset = bitVectors[i]._value;

            while (bitset != 0)
            {
                nuint t = bitset & (nuint) (-(nint) bitset);
                int r = BitOperations.TrailingZeroCount(bitset);
                int index = (i * Unsafe.SizeOf<BitVector>() * 8) + r;

                bitset ^= t;

                // Note that index is in local geometry coordinates.
                int edgeX = index;
                int nextEdgeX = edgeX + 1;

                // Signed area for pixel at bit index.
                int area = coverAreas[index].Area + (cover << 9);

                // Area converted to alpha according to fill rule.
                uint alpha = (uint) F.ApplyFillRule(area);

                if (spanEnd == edgeX)
                {
                    // No gap between previous span and current pixel.
                    if (alpha == 0)
                    {
                        if (spanAlpha != 0)
                        {
                            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                        }

                        spanX = nextEdgeX;
                        spanEnd = spanX;
                        spanAlpha = 0;
                    }
                    else if (spanAlpha == alpha)
                    {
                        spanEnd = nextEdgeX;
                    }
                    else
                    {
                        // Alpha is not zero, but not equal to previous span alpha.
                        if (spanAlpha != 0)
                        {
                            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                        }

                        spanX = edgeX;
                        spanEnd = nextEdgeX;
                        spanAlpha = alpha;
                    }
                }
                else
                {
                    Debug.Assert(spanEnd < edgeX);

                    // There is a gap between last filled pixel and the new one.
                    if (cover == 0)
                    {
                        // Empty gap.
                        // Fill span if there is one and reset current span.
                        if (spanAlpha != 0)
                        {
                            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                        }

                        spanX = edgeX;
                        spanEnd = nextEdgeX;
                        spanAlpha = alpha;
                    }
                    else
                    {
                        // Non empty gap.
                        // Attempt to merge gap with current span.
                        uint gapAlpha = (uint) F.ApplyFillRule(cover << 9);

                        // If alpha matches, extend current span.
                        if (spanAlpha == gapAlpha)
                        {
                            if (alpha == gapAlpha)
                            {
                                // Current pixel alpha matches as well.
                                spanEnd = nextEdgeX;
                            }
                            else
                            {
                                // Only gap alpha matches current span.
                                blender.CompositeSpan(d[spanX..edgeX], spanAlpha);

                                spanX = edgeX;
                                spanEnd = nextEdgeX;
                                spanAlpha = alpha;
                            }
                        }
                        else
                        {
                            if (spanAlpha != 0)
                            {
                                blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                            }

                            // Compose gap.
                            blender.CompositeSpan(d[spanEnd..edgeX], gapAlpha);

                            spanX = edgeX;
                            spanEnd = nextEdgeX;
                            spanAlpha = alpha;
                        }
                    }
                }

                cover += coverAreas[index].Delta;
            }
        }

        if (spanAlpha != 0)
        {
            // Composite current span.
            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
        }

        if (cover != 0 && spanEnd < d.Length)
        {
            // Composite anything that goes to the edge of destination image.
            int alpha = F.ApplyFillRule(cover << 9);

            blender.CompositeSpan(d[spanEnd..], (uint) alpha);
        }
    }


    /// <summary>
    /// Rasterize one item within a single row.
    /// </summary>
    private static void RasterizeOneItem(
        int localRowIndex,
        in RasterizableGeometry raster,
        in Geometry geometry,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        int columnCount,
        ImageData image)
    {
        // A maximum number of horizontal tiles.
        int horizontalCount = (int) raster.Bounds.ColumnCount;
        Debug.Assert(horizontalCount <= columnCount);

        int rowWidth = horizontalCount * T.TileW;
        int bitVectorsPerRow = BitVectorsForMaxBitCount(rowWidth);

        // Pointer to backdrop.
        ReadOnlySpan<int> coversStart = raster.GetActualCoversForRow(localRowIndex);

        int x = (int) raster.Bounds.X * T.TileW;

        Debug.Assert((x & (T.TileW - 1)) == 0, "X must be aligned on tile boundary.");

        Debug.Assert(rowWidth <= image.Width - x, "Row overruns image width.");

        // Y position, measured in tiles.
        int miny = (int) (raster.Bounds.Y + localRowIndex);

        // Y position, measure in pixels.
        int py = miny * T.TileH;

        // Maximum y position, measured in pixels.
        int maxpy = py + T.TileH;

        // Calculate maximum height. This can only get less than 8 when rendering
        // the last row of the image and image height is not multiple of row height.
        int height = Math.Min(maxpy, image.Height) - py;

        int rowStride = image.BytesPerRow;
        int rowByteWidth = Math.Min(rowWidth, image.Width - x) * sizeof(uint);
        
        Span<byte> imageData = new(image.Data, image.Height * rowStride);
        Span2D<byte> rowView = new(
            imageData.Slice(py * rowStride + x * sizeof(uint)),
            rowByteWidth,
            height,
            rowStride);
        
        // Limit views to proper dimensions.
        Span2D<BitVector> bitVectorView = bitVectorTable.Cut(bitVectorsPerRow, height);
        Span2D<CoverArea> coverAreaView = coverAreaTable.Cut(rowWidth, height);
        
        // Erase bit vector table.
        for (int i = 0; i < bitVectorView.Height; i++)
        {
            bitVectorView[i].Clear();
        }

        raster.IterationFunction.value(
            localRowIndex, in raster, bitVectorView, coverAreaView);

        // Fill color.
        uint color = geometry.Color;
        FillRule rule = geometry.Rule;

        if (color >= 0xff000000)
        {
            SpanBlenderOpaque blender = new(color);
                
            if (rule == FillRule.NonZero)
            {
                RenderLines<SpanBlenderOpaque, AreaToAlphaNonZeroFn>(
                    rowView, bitVectorView, coverAreaView, coversStart, blender);
            }
            else
            {
                RenderLines<SpanBlenderOpaque, AreaToAlphaEvenOddFn>(
                    rowView, bitVectorView, coverAreaView, coversStart, blender);
            }
        }
        else
        {
            SpanBlender blender = new(color);
            
            if (rule == FillRule.NonZero)
            {
                RenderLines<SpanBlender, AreaToAlphaNonZeroFn>(
                    rowView, bitVectorView, coverAreaView, coversStart, blender);
            }
            else
            {
                RenderLines<SpanBlender, AreaToAlphaEvenOddFn>(
                    rowView, bitVectorView, coverAreaView, coversStart, blender);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RenderLines<B, F>(
        Span2D<byte> rowData,
        ReadOnlySpan2D<BitVector> bitVectorTable,
        ReadOnlySpan2D<CoverArea> coverAreaTable,
        ReadOnlySpan<int> coversStart,
        B blender)
        where B : ISpanBlender
        where F : IFillRuleFn
    {
        for (int y = 0; y < rowData.Height; y++)
        {
            Span<byte> row = rowData[y];
            int startCover = y < coversStart.Length ? coversStart[y] : 0;
            
            RenderOneLine<B, F>(
                row,
                bitVectorTable[y],
                coverAreaTable[y],
                startCover,
                blender);
        }
    }


    /// <summary>
    /// Rasterize all items in one row.
    /// </summary>
    private static void RasterizeRow(
        RowItemList<RasterizableItem>* rowList,
        ReadOnlySpan<RasterizableGeometry> rasterizables,
        ReadOnlySpan<Geometry> geometries,
        ThreadMemory memory,
        ImageData image)
    {
        // How many columns can fit into image.
        TileIndex columnCount = CalculateColumnCount<T>(image.Width);

        // Create bit vector arrays.
        int bitVectorsPerRow = BitVectorsForMaxBitCount((int) columnCount * T.TileW);
        int bitVectorCount = bitVectorsPerRow * T.TileH;

        Span2D<BitVector> bitVectors = new(
            memory.TaskMallocArray<BitVector>(bitVectorCount),
            bitVectorsPerRow,
            T.TileH);

        // Create cover/area table.
        int coverAreaPerRow = (int) columnCount * T.TileW;
        int coverAreaCount = coverAreaPerRow * T.TileH;

        Span2D<CoverArea> coverArea = new(
            memory.TaskMallocArray<CoverArea>(coverAreaCount),
            coverAreaPerRow,
            T.TileH);

        // Rasterize all items, from bottom to top that were added to this row.
        RowItemList<RasterizableItem>.Block* b = rowList->First;

        while (b != null)
        {
            int count = b->Count;
            RasterizableItem* itm = &b->Items[0];
            RasterizableItem* e = itm + count;

            while (itm < e)
            {
                RasterizableItem item = *itm++;
                ref readonly RasterizableGeometry raster = ref rasterizables[item.Rasterizable];
                ref readonly Geometry geometry = ref geometries[raster.Geometry];
                
                RasterizeOneItem(
                    item.LocalRowIndex,
                    in raster,
                    in geometry,
                    bitVectors,
                    coverArea,
                    (int) columnCount,
                    image);
            }

            b = b->Next;
        }
    }

}