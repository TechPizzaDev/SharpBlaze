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
        ReadOnlyMemory<Geometry> inputGeometries,
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


    readonly struct LineIterationFunction
    {
        public readonly delegate* managed<in RasterizableItem, Span2D<BitVector>, Span2D<CoverArea>, void> value;

        public LineIterationFunction(
            delegate* managed<in RasterizableItem, Span2D<BitVector>, Span2D<CoverArea>, void> value)
        {
            this.value = value;
        }
    }

    unsafe partial struct RasterizableGeometry
    {
        public RasterizableGeometry(
            int geometry,
            LineIterationFunction iterationFunction,
            TileBounds bounds)
        {
            Geometry = geometry;
            IterationFunction = iterationFunction;
            Bounds = bounds;
        }

        public readonly partial void* GetLinesForRow(int rowIndex);
        public readonly partial int GetFirstBlockLineCountForRow(int rowIndex);
        public readonly partial int* GetCoversForRow(int rowIndex);
        public readonly partial int* GetActualCoversForRow(int rowIndex);

        public readonly int Geometry = -1;
        public readonly LineIterationFunction IterationFunction = default;
        public readonly TileBounds Bounds;
        public void** Lines = null;
        public int* FirstBlockLineCounts = null;
        public int** StartCoverTable = null;
    }


    unsafe partial struct RasterizableItem
    {
        public RasterizableItem(RasterizableGeometry* rasterizable, int localRowIndex)
        {
            Rasterizable = rasterizable;
            LocalRowIndex = localRowIndex;
        }

        public readonly partial int GetFirstBlockLineCount();
        public readonly partial void* GetLineArray();
        public readonly partial int* GetActualCovers();

        // Do not initialize these since they are allocated in bunches.
        public readonly RasterizableGeometry* Rasterizable;
        public int LocalRowIndex;
    };


    /**
     * Rasterize all items in one row.
     */
    private static partial void RasterizeRow(
        RowItemList<RasterizableItem>* rowList,
        ReadOnlySpan<Geometry> geometries,
        ThreadMemory memory,
        ImageData image);

    [Obsolete]
    public Rasterizer() { }

    public static partial void Rasterize(
        ReadOnlyMemory<Geometry> inputGeometries,
        in Matrix matrix,
        Executor threads,
        ImageData image)
    {
        int inputGeometryCount = inputGeometries.Length;

        Debug.Assert(inputGeometryCount > 0);
        Debug.Assert(image.Data != null);
        Debug.Assert(image.Width > 0);
        Debug.Assert(image.Height > 0);
        Debug.Assert(image.BytesPerRow >= (image.Width * 4));

        // TODO
        // Skip transform if matrix is identity.

        Memory<Geometry> geometries =
            // TODO: threads.MainMemory.FrameMallocArray<Geometry>(inputGeometryCount);
            new Geometry[inputGeometryCount];

        ReadOnlySpan<Geometry> srcSpan = inputGeometries.Span;
        Span<Geometry> dstSpan = geometries.Span;

        for (int i = 0; i < inputGeometryCount; i++)
        {
            ref readonly Geometry s = ref srcSpan[i];

            Matrix tm = s.TM;

            tm *= matrix;

            dstSpan[i] = new Geometry(
                tm.MapBoundingRect(s.PathBounds),
                s.Tags,
                s.Points,
                tm,
                s.Color,
                s.Rule);
        }

        StepState1 state1 = Step1(threads, image, geometries, inputGeometryCount);
        StepState2 state2 = Step2(threads, state1);
        Step3(threads, image, geometries, state2);
    }

    private struct StepState1
    {
        public required RasterizableGeometry** rasterizables;
        public required RasterizableGeometry* rasterizableGeometryMemory;
        public required ReadOnlyMemory<Geometry> geometries;
        public required IntSize imageSize;
        public int visibleRasterizableCount;
    }

    /// <summary>
    /// Create and array of RasterizableGeometry instances. Instances are
    /// created and prepared for further processing in parallel.
    /// </summary>
    private static StepState1 Step1(
        Executor threads,
        in ImageData image,
        ReadOnlyMemory<Geometry> geometries,
        int inputGeometryCount)
    {
        // Allocate memory for RasterizableGeometry instance pointers.
        RasterizableGeometry** rasterizables =
            threads.MainMemory.FrameMallocPointers<RasterizableGeometry>(inputGeometryCount);

        // Allocate memory for RasterizableGeometry instances.
        RasterizableGeometry* rasterizableGeometryMemory =
            threads.MainMemory.FrameMallocArray<RasterizableGeometry>(inputGeometryCount);

        IntSize imageSize = new(
            image.Width,
            image.Height
        );

        StepState1 state1 = new()
        {
            rasterizables = rasterizables,
            rasterizableGeometryMemory = rasterizableGeometryMemory,
            geometries = geometries,
            imageSize = imageSize,
        };
        threads.For(0, inputGeometryCount, &state1, static (index, state, memory) =>
        {
            StepState1* s = (StepState1*) state;

            s->rasterizables[index] = CreateRasterizable(
                s->rasterizableGeometryMemory + index,
                s->geometries.Span[index],
                index,
                s->imageSize,
                memory);
        });

        // Linearizer may decide that some paths do not contribute to the final
        // image. In these situations CreateRasterizable will return null.
        // In the following step, non-null pointers are packed into front of array.

        int visibleRasterizableCount = 0;

        for (int i = 0; i < inputGeometryCount; i++)
        {
            RasterizableGeometry* rasterizable = rasterizables[i];

            if (rasterizable != null)
            {
                rasterizables[visibleRasterizableCount++] = rasterizable;
            }
        }

        state1.visibleRasterizableCount = visibleRasterizableCount;

        return state1;
    }

    private struct StepState2
    {
        public required RasterizableGeometry** visibleRasterizables;
        public required int visibleRasterizableCount;

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
            visibleRasterizables = state1.rasterizables,
            visibleRasterizableCount = state1.visibleRasterizableCount,

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

            for (int i = 0; i < s->visibleRasterizableCount; i++)
            {
                RasterizableGeometry* rasterizable = s->visibleRasterizables[i];
                TileBounds b = rasterizable->Bounds;

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
                        rasterizable->GetLinesForRow((int) localIndex) == null &&
                        rasterizable->GetCoversForRow((int) localIndex) == null;

                    if (emptyRow)
                    {
                        // Both conditions failed, this geometry for this row will
                        // not produce any visible pixels.
                        continue;
                    }

                    RowItemList<RasterizableItem>* list = s->rowLists + y;

                    list->Append(memory, new RasterizableItem(rasterizable, (int) localIndex));
                }
            }
        });

        return state2;
    }

    private struct StepState3
    {
        public required RowItemList<RasterizableItem>* rowLists;
        public required ImageData image;
        public required Memory<Geometry> geometries;
    }

    /// <summary>
    /// Rasterize all intervals.
    /// </summary>
    private static void Step3(Executor threads, in ImageData image, Memory<Geometry> geometries, in StepState2 state2)
    {
        StepState3 state3 = new()
        {
            rowLists = state2.rowLists,
            image = image,
            geometries = geometries,
        };
        threads.For(0, (int) state2.rowCount, &state3, static (rowIndex, state, memory) =>
        {
            StepState3* s = (StepState3*) state;

            RowItemList<RasterizableItem>* item = s->rowLists + rowIndex;

            RasterizeRow(item, s->geometries.Span, memory, s->image);
        });
    }

    partial struct RasterizableGeometry
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial void* GetLinesForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            return Lines[rowIndex];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial int GetFirstBlockLineCountForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            return FirstBlockLineCounts[rowIndex];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial int* GetCoversForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            if (StartCoverTable == null)
            {
                // No table at all.
                return null;
            }

            return StartCoverTable[rowIndex];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial int* GetActualCoversForRow(int rowIndex)
        {
            Debug.Assert(rowIndex >= 0);
            Debug.Assert(rowIndex < Bounds.RowCount);

            if (StartCoverTable == null)
            {
                // No table at all.
                return T.ZeroCovers;
            }

            int* covers = StartCoverTable[rowIndex];

            if (covers == null)
            {
                return T.ZeroCovers;
            }

            return covers;
        }
    }


    unsafe partial struct RasterizableItem
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial int GetFirstBlockLineCount()
        {
            return Rasterizable->GetFirstBlockLineCountForRow(LocalRowIndex);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial void* GetLineArray()
        {
            return Rasterizable->GetLinesForRow(LocalRowIndex);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly partial int* GetActualCovers()
        {
            return Rasterizable->GetActualCoversForRow(LocalRowIndex);
        }
    }

    private static void IterateLinesX32Y16(
        in RasterizableItem item, Span2D<BitVector> bitVectorTable, Span2D<CoverArea> coverAreaTable)
    {
        int count = item.GetFirstBlockLineCount();

        LineArrayX32Y16Block* v = (LineArrayX32Y16Block*) item.GetLineArray();

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
        in RasterizableItem item, Span2D<BitVector> bitVectorTable, Span2D<CoverArea> coverAreaTable)
    {
        int count = item.GetFirstBlockLineCount();

        LineArrayX16Y16Block* v = (LineArrayX16Y16Block*) item.GetLineArray();

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

    private static RasterizableGeometry* CreateRasterizable(
        RasterizableGeometry* placement,
        in Geometry geometry,
        int geometryIndex,
        IntSize imageSize,
        ThreadMemory memory)
    {
        Debug.Assert(placement != null);
        Debug.Assert(imageSize.Width > 0);
        Debug.Assert(imageSize.Height > 0);

        if (geometry.Tags.IsEmpty)
        {
            return null;
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
            return null;
        }

        int minx = Math.Max(0, geometryBounds.MinX);
        int miny = Math.Max(0, geometryBounds.MinY);
        int maxx = Math.Min(imageSize.Width, geometryBounds.MaxX + 1);
        int maxy = Math.Min(imageSize.Height, geometryBounds.MaxY);

        if (minx >= maxx || miny >= maxy)
        {
            // Geometry bounds do not intersect with destination image.
            return null;
        }

        TileBounds bounds = CalculateTileBounds<T>(minx, miny, maxx, maxy);

        bool narrow = 128 > (bounds.ColumnCount * T.TileW);

        if (narrow)
        {
            return Linearize<LineArrayX16Y16>(
                placement, geometry, geometryIndex, bounds,
                imageSize, new(&IterateLinesX16Y16), memory);
        }

        return Linearize<LineArrayX32Y16>(
            placement, geometry, geometryIndex, bounds,
            imageSize, new(&IterateLinesX32Y16), memory);
    }


    private static RasterizableGeometry* Linearize<L>(
        RasterizableGeometry* placement,
        in Geometry geometry,
        int geometryIndex,
        TileBounds bounds,
        IntSize imageSize,
        LineIterationFunction iterationFunction,
        ThreadMemory memory)
        where L : unmanaged, ILineArrayBlock<L>
    {
        RasterizableGeometry* linearized = placement;
        *linearized = new RasterizableGeometry(geometryIndex, iterationFunction, bounds);

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

        //Debug.Assert(linearizer != null);

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

        linearized->Lines = lineBlocks;
        linearized->FirstBlockLineCounts = firstLineBlockCounts;

        int** startCoverTable = linearizer.GetStartCoverTable();

        if (startCoverTable != null)
        {
            for (int i = 0; i < bounds.RowCount; i++)
            {
                int* t = startCoverTable[i];

                if (t != null && T.CoverArrayContainsOnlyZeroes(t))
                {
                    // Don't need cover array after all,
                    // all segments cancelled each other.
                    startCoverTable[i] = null;
                }
            }

            linearized->StartCoverTable = startCoverTable;
        }

        return linearized;
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
            F24Dot8 lift = p / dx;
            F24Dot8 rem = p % dx;

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
            F24Dot8 lift = p / dx;
            F24Dot8 rem = p % dx;

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
            F24Dot8 lift = p / dx;
            F24Dot8 rem = p % dx;

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
            F24Dot8 lift = p / dx;
            F24Dot8 rem = p % dx;

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

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

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

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

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

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

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

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

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
        Span<uint> image,
        ReadOnlySpan<BitVector> bitVectorTable,
        ReadOnlySpan<CoverArea> coverAreaTable,
        int startCover,
        B blender)
        where B : ISpanBlender
        where F : IFillRuleFn
    {
        Span<uint> d = image;

        // Cover accumulation.
        int cover = startCover;

        // Span state.
        int spanX = 0;
        int spanEnd = 0;
        uint spanAlpha = 0;

        for (int i = 0; i < bitVectorTable.Length; i++)
        {
            nuint bitset = bitVectorTable[i]._value;

            while (bitset != 0)
            {
                nuint t = bitset & (nuint) (-(nint) bitset);
                int r = BitOperations.TrailingZeroCount(bitset);
                int index = (i * sizeof(BitVector) * 8) + r;

                bitset ^= t;

                // Note that index is in local geometry coordinates.
                int edgeX = index;
                int nextEdgeX = edgeX + 1;

                // Signed area for pixel at bit index.
                int area = coverAreaTable[index].Area + (cover << 9);

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

                cover += coverAreaTable[index].Delta;
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
        in RasterizableItem item,
        ReadOnlySpan<Geometry> geometries,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable,
        int columnCount,
        ImageData image)
    {
        // A maximum number of horizontal tiles.
        int horizontalCount = (int) item.Rasterizable->Bounds.ColumnCount;

        Debug.Assert(horizontalCount <= columnCount);

        int bitVectorsPerRow = BitVectorsForMaxBitCount(horizontalCount * T.TileW);

        // Erase bit vector table.
        for (int i = 0; i < T.TileH; i++)
        {
            bitVectorTable[i].Slice(0, bitVectorsPerRow).Clear();
        }

        item.Rasterizable->IterationFunction.value(item, bitVectorTable, coverAreaTable);

        // Pointer to backdrop.
        ReadOnlySpan<int> coversStart = new(item.GetActualCovers(), 32);

        int x = (int) item.Rasterizable->Bounds.X * T.TileW;

        // X must be aligned on tile boundary.
        Debug.Assert((x & (T.TileW - 1)) == 0);

        // Y position, measured in tiles.
        int miny = (int) (item.Rasterizable->Bounds.Y + item.LocalRowIndex);

        // Y position, measure in pixels.
        int py = miny * T.TileH;

        // Maximum y position, measured in pixels.
        int maxpy = py + T.TileH;

        // Start row.
        int bytesPerRow = image.BytesPerRow;
        int bytesPerSpan = (image.Width - x) * sizeof(uint);

        Span<byte> imageData = new(image.Data, image.Height * bytesPerRow);
        Span<byte> rowData = imageData.Slice(py * bytesPerRow + x * sizeof(uint));

        // Calculate maximum height. This can only get less than 8 when rendering
        // the last row of the image and image height is not multiple of row height.
        int height = Math.Min(maxpy, image.Height) - py;

        // Fill color.
        ref readonly Geometry geometry = ref geometries[item.Rasterizable->Geometry];
        uint color = geometry.Color;
        FillRule rule = geometry.Rule;

        if (color >= 0xff000000)
        {
            if (rule == FillRule.NonZero)
            {
                RenderLines<SpanBlenderOpaque, AreaToAlphaNonZeroFn>(
                    rowData, height, bytesPerRow, bytesPerSpan, bitVectorTable, bitVectorsPerRow, coverAreaTable,
                    coversStart, new(color));
            }
            else
            {
                RenderLines<SpanBlenderOpaque, AreaToAlphaEvenOddFn>(
                    rowData, height, bytesPerRow, bytesPerSpan, bitVectorTable, bitVectorsPerRow, coverAreaTable,
                    coversStart, new(color));
            }
        }
        else
        {
            if (rule == FillRule.NonZero)
            {
                RenderLines<SpanBlender, AreaToAlphaNonZeroFn>(
                    rowData, height, bytesPerRow, bytesPerSpan, bitVectorTable, bitVectorsPerRow, coverAreaTable,
                    coversStart, new(color));
            }
            else
            {
                RenderLines<SpanBlender, AreaToAlphaEvenOddFn>(
                    rowData, height, bytesPerRow, bytesPerSpan, bitVectorTable, bitVectorsPerRow, coverAreaTable,
                    coversStart, new(color));
            }
        }
    }

    private static void RenderLines<B, F>(
        Span<byte> rowData,
        int height,
        int bytesPerRow,
        int bytesPerSpan,
        ReadOnlySpan2D<BitVector> bitVectorTable,
        int bitVectorsPerRow,
        ReadOnlySpan2D<CoverArea> coverAreaTable,
        ReadOnlySpan<int> coversStart,
        B blender)
        where B : ISpanBlender
        where F : IFillRuleFn
    {
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * bytesPerRow;
            Span<byte> row = rowData.Slice(rowStart, bytesPerSpan);

            RenderOneLine<B, F>(
                MemoryMarshal.Cast<byte, uint>(row),
                bitVectorTable[y].Slice(0, bitVectorsPerRow),
                coverAreaTable[y],
                coversStart[y],
                blender);
        }
    }


    /**
     * Rasterize all items in one row.
     */
    private static partial void RasterizeRow(
        RowItemList<RasterizableItem>* rowList,
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
                RasterizeOneItem(
                    in *itm++,
                    geometries,
                    bitVectors,
                    coverArea,
                    (int) columnCount,
                    image);
            }

            b = b->Next;
        }
    }

}