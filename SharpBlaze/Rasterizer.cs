using System;
using System.Diagnostics;
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

    public static partial void Rasterize(Geometry* inputGeometries,
        int inputGeometryCount, in Matrix matrix, Threads threads,
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
        public delegate* managed<RasterizableItem*, BitVector**, int**, void> value;

        public LineIterationFunction(delegate*<Rasterizer<T>.RasterizableItem*, BitVector**, int**, void> value)
        {
            this.value = value;
        }
    }

    unsafe partial struct RasterizableGeometry
    {
        public RasterizableGeometry(Geometry* geometry,
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

        public readonly Geometry* Geometry = null;
        public readonly LineIterationFunction IterationFunction = default;
        public readonly TileBounds Bounds;
        public void** Lines = null;
        public int* FirstBlockLineCounts = null;
        public int** StartCoverTable = null;
    }


    unsafe partial struct RasterizableItem : IConstructible<RasterizableItem, nint>
    {
        public static void Construct(ref RasterizableItem instance, in nint args)
        {
            instance = new RasterizableItem();
        }

        public RasterizableItem(RasterizableGeometry* rasterizable,
            int localRowIndex)
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


    private static partial void IterateLinesX32Y16(RasterizableItem* item,
        BitVector** bitVectorTable, int** coverAreaTable);


    private static partial void IterateLinesX16Y16(RasterizableItem* item,
        BitVector** bitVectorTable, int** coverAreaTable);


    private static partial RasterizableGeometry* CreateRasterizable(void* placement,
        Geometry* geometry, IntSize imageSize, ThreadMemory memory);


    private static partial RasterizableGeometry* Linearize<L>(void* placement, Geometry* geometry,
        TileBounds bounds, IntSize imageSize,
        LineIterationFunction iterationFunction, ThreadMemory memory)
        where L : unmanaged, ILineArrayBlock<L>;


    private static partial void Vertical_Down(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex columnIndex, F24Dot8 y0, F24Dot8 y1, F24Dot8 x);


    private static partial void Vertical_Up(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex columnIndex, F24Dot8 y0, F24Dot8 y1, F24Dot8 x);


    private static partial void CellVertical(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex px, PixelIndex py,
        F24Dot8 x, F24Dot8 y0, F24Dot8 y1);


    private static partial void Cell(BitVector* bitVector, int* coverArea,
        PixelIndex px, F24Dot8 x0,
        F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);


    /**
     * ⬊
     *
     * Rasterize line within single pixel row. Line must go from left to
     * right.
     */
    private static partial void RowDownR(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬊
     *
     * Rasterize line within single pixel row. Line must go from left to
     * right or be vertical.
     */
    private static partial void RowDownR_V(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬈
     *
     * Rasterize line within single pixel row. Line must go from left to
     * right.
     */
    private static partial void RowUpR(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬈
     *
     * Rasterize line within single pixel row. Line must go from left to
     * right or be vertical.
     */
    private static partial void RowUpR_V(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬋
     *
     * Rasterize line within single pixel row. Line must go from right to
     * left.
     */
    private static partial void RowDownL(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬋
     *
     * Rasterize line within single pixel row. Line must go from right to
     * left or be vertical.
     */
    private static partial void RowDownL_V(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬉
     *
     * Rasterize line within single pixel row. Line must go from right to
     * left.
     */
    private static partial void RowUpL(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬉
     *
     * Rasterize line within single pixel row. Line must go from right to
     * left or be vertical.
     */
    private static partial void RowUpL_V(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex, F24Dot8 p0x, F24Dot8 p0y,
        F24Dot8 p1x, F24Dot8 p1y);


    /**
     * ⬊
     */
    private static partial void LineDownR(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex0, PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0, F24Dot8 x1,
        F24Dot8 y1);


    /**
     * ⬈
     */
    private static partial void LineUpR(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex0, PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0, F24Dot8 x1,
        F24Dot8 y1);


    /**
     * ⬋
     */
    private static partial void LineDownL(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex0, PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0, F24Dot8 x1,
        F24Dot8 y1);


    /**
     * ⬉
     */
    private static partial void LineUpL(BitVector** bitVectorTable, int** coverAreaTable,
        PixelIndex rowIndex0, PixelIndex rowIndex1,
        F24Dot8 x0, F24Dot8 y0, F24Dot8 x1,
        F24Dot8 y1);


    private static partial void RasterizeLine(F24Dot8 X0, F24Dot8 Y0,
        F24Dot8 X1, F24Dot8 Y1, BitVector** bitVectorTable,
        int** coverAreaTable);


    private static partial void RenderOneLine<B, F>(byte* image, BitVector* bitVectorTable,
        int bitVectorCount, int* coverAreaTable, int x,
        int rowLength, int startCover, B blender)
        where B : ISpanBlender
        where F : IFillRuleFn;


    /**
     * Rasterize one item within a single row.
     */
    private static partial void RasterizeOneItem(RasterizableItem* item,
        BitVector** bitVectorTable, int** coverAreaTable,
        int columnCount, ImageData image);


    /**
     * Rasterize all items in one row.
     */
    private static partial void RasterizeRow(RowItemList<RasterizableItem>* rowList,
        ThreadMemory memory, ImageData image);

    [Obsolete]
    public Rasterizer() { }


    public static partial void Rasterize(Geometry* inputGeometries,
        int inputGeometryCount, in Matrix matrix, Threads threads,
        ImageData image)
    {
        Debug.Assert(inputGeometries != null);
        Debug.Assert(inputGeometryCount > 0);
        Debug.Assert(image.Data != null);
        Debug.Assert(image.Width > 0);
        Debug.Assert(image.Height > 0);
        Debug.Assert(image.BytesPerRow >= (image.Width * 4));

        // TODO
        // Skip transform if matrix is identity.
        Geometry* geometries = (Geometry*) (
            threads.MallocMain(sizeof(Geometry) * inputGeometryCount));

        for (int i = 0; i < inputGeometryCount; i++)
        {
            Geometry* s = inputGeometries + i;

            Matrix tm = new(s->TM);

            tm *= matrix;

            *(geometries + i) = new Geometry(
                tm.MapBoundingRect(s->PathBounds),
                s->Tags,
                s->Points,
                tm,
                s->TagCount,
                s->PointCount,
                s->Color,
                s->Rule);
        }

        // Step 1.
        //
        // Create and array of RasterizableGeometry instances. Instances are
        // created and prepared for further processing in parallel.

        // Allocate memory for RasterizableGeometry instance pointers.
        RasterizableGeometry** rasterizables = (RasterizableGeometry**) (
            threads.MallocMain(sizeof(RasterizableGeometry*) * inputGeometryCount));

        // Allocate memory for RasterizableGeometry instances.
        RasterizableGeometry* rasterizableGeometryMemory = (RasterizableGeometry*) (
            threads.MallocMain(sizeof(RasterizableGeometry) * inputGeometryCount));

        IntSize imageSize = new(
            image.Width,
            image.Height
        );

        threads.ParallelFor(inputGeometryCount, (int index, ThreadMemory memory) =>
        {
            rasterizables[index] = CreateRasterizable(
                rasterizableGeometryMemory + index, geometries + index, imageSize,
                memory);
        });

        // Linearizer may decide that some paths do not contribute to the final
        // image. In these situations CreateRasterizable will return null. In
        // the following step, a new array is created and only non-null items
        // are copied to it.

        RasterizableGeometry** visibleRasterizables = (RasterizableGeometry**) (
            threads.MallocMain(sizeof(RasterizableGeometry*) * inputGeometryCount));

        int visibleRasterizableCount = 0;

        for (int i = 0; i < inputGeometryCount; i++)
        {
            RasterizableGeometry* rasterizable = rasterizables[i];

            if (rasterizable != null)
            {
                visibleRasterizables[visibleRasterizableCount++] = rasterizable;
            }
        }


        // Step 2.
        //
        // Create lists of rasterizable items for each interval.

        TileIndex rowCount = CalculateRowCount<T>(imageSize.Height);

        RowItemList<RasterizableItem>* rowLists =
            (RowItemList<RasterizableItem>*) (threads.MallocMain(sizeof(RowItemList<RasterizableItem>) * (int) rowCount));

        int threadCount = Threads.GetHardwareThreadCount();

        Debug.Assert(threadCount > 0);

        int iterationHeight = (int) Math.Max((uint) ((int) rowCount / threadCount), 1);
        int iterationCount = ((int) rowCount / iterationHeight) +
            (int) Math.Min((uint) ((int) rowCount % iterationHeight), 1);

        threads.ParallelFor(iterationCount, (int index, ThreadMemory memory) =>
        {
            TileIndex threadY = (TileIndex) (index * iterationHeight);
            TileIndex threadHeight = (TileIndex) Math.Min(iterationHeight, (int) (rowCount - threadY));
            TileIndex threadMaxY = threadY + threadHeight;

            for (TileIndex i = threadY; i < threadMaxY; i++)
            {
                *(rowLists + i) = new RowItemList<RasterizableItem>();
            }

            for (int i = 0; i < visibleRasterizableCount; i++)
            {
                RasterizableGeometry* rasterizable = visibleRasterizables[i];
                TileBounds b = rasterizable->Bounds;

                TileIndex min = Clamp(b.Y, threadY, threadMaxY);
                TileIndex max = Clamp(b.Y + b.RowCount, threadY,
                    threadMaxY);

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

                    RowItemList<RasterizableItem>* list = rowLists + y;

                    list->Append(memory, new RasterizableItem(rasterizable, (int) localIndex));
                }
            }
        });

        // Step 3.
        //
        // Rasterize all intervals.

        threads.ParallelFor((int) rowCount, (int rowIndex, ThreadMemory memory) =>
        {
            RowItemList<RasterizableItem>* item = rowLists + rowIndex;

            RasterizeRow(item, memory, image);
        });
    }

    unsafe partial struct RasterizableGeometry
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

    private static partial void IterateLinesX32Y16(RasterizableItem* item, BitVector** bitVectorTable, int** coverAreaTable)
    {
        int count = item->GetFirstBlockLineCount();

        LineArrayX32Y16Block* v =
           (LineArrayX32Y16Block*) (item->GetLineArray());

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

                RasterizeLine(x0, UnpackLoFromF8Dot8x2(y0y1), x1,
                    UnpackHiFromF8Dot8x2(y0y1), bitVectorTable,
                    coverAreaTable);
            }

            v = v->Next;
            count = LineArrayX32Y16Block.LinesPerBlock;
        }
    }


    private static partial void IterateLinesX16Y16(RasterizableItem* item, BitVector** bitVectorTable, int** coverAreaTable)
    {
        int count = item->GetFirstBlockLineCount();

        LineArrayX16Y16Block* v =
           (LineArrayX16Y16Block*) (item->GetLineArray());

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
                    UnpackHiFromF8Dot8x2(y0y1), bitVectorTable,
                    coverAreaTable);
            }

            v = v->Next;
            count = LineArrayX32Y16Block.LinesPerBlock;
        }
    }

    private static partial RasterizableGeometry*
        CreateRasterizable(void* placement, Geometry* geometry, IntSize imageSize, ThreadMemory memory)
    {
        Debug.Assert(placement != null);
        Debug.Assert(geometry != null);
        Debug.Assert(imageSize.Width > 0);
        Debug.Assert(imageSize.Height > 0);

        if (geometry->TagCount < 1)
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

        IntRect geometryBounds = geometry->PathBounds;

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

        bool narrow =
            128 > (bounds.ColumnCount * T.TileW);

        if (narrow)
        {
            return Linearize<LineArrayX16Y16>(placement, geometry, bounds,
                imageSize, new(&IterateLinesX16Y16), memory);
        }
        else
        {
            return Linearize<LineArrayX32Y16>(placement, geometry, bounds,
                imageSize, new(&IterateLinesX32Y16), memory);
        }
    }


    private static partial RasterizableGeometry*
        Linearize<L>(void* placement, Geometry* geometry, TileBounds bounds, IntSize imageSize, LineIterationFunction iterationFunction, ThreadMemory memory)
        where L : unmanaged, ILineArrayBlock<L>
    {
        RasterizableGeometry* linearized = (RasterizableGeometry*) placement;
        *linearized = new RasterizableGeometry(
            geometry, iterationFunction, bounds);

        // Determine if path is completely within destination image bounds. If
        // geometry bounds fit within destination image, a shortcut can be made
        // when generating lines.
        bool contains =
            geometry->PathBounds.MinX >= 0 &&
                geometry->PathBounds.MinY >= 0 &&
                geometry->PathBounds.MaxX <= imageSize.Width &&
                geometry->PathBounds.MaxY <= imageSize.Height;

        Linearizer<T, L> linearizer =
            Linearizer<T, L>.Create(memory, bounds, contains, geometry);

        //Debug.Assert(linearizer != null);

        // Finalize.
        void** lineBlocks = (void**) memory.FrameMallocArray<nint>((int) bounds.RowCount);

        int* firstLineBlockCounts = memory.FrameMallocArray<int>(
            (int) bounds.RowCount);

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
                    // Don't need cover array after all, all segments cancelled
                    // each other.
                    startCoverTable[i] = null;
                }
            }

            linearized->StartCoverTable = startCoverTable;
        }

        return linearized;
    }


    private static partial void Vertical_Down(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex columnIndex, F24Dot8 y0,
        F24Dot8 y1, F24Dot8 x)
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
        else
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, F24Dot8_1);

            for (PixelIndex i = rowIndex0 + 1; i < rowIndex1; i++)
            {
                CellVertical(bitVectorTable, coverAreaTable, columnIndex, i, fx, 0, F24Dot8_1);
            }

            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex1, fx, 0, fy1);
        }
    }


    private static partial void Vertical_Up(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex columnIndex, F24Dot8 y0,
        F24Dot8 y1, F24Dot8 x)
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
        }
        else
        {
            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex0, fx, fy0, 0);

            for (PixelIndex i = rowIndex0 - 1; i > rowIndex1; i--)
            {
                CellVertical(bitVectorTable, coverAreaTable, columnIndex, i, fx, F24Dot8_1, 0);
            }

            CellVertical(bitVectorTable, coverAreaTable, columnIndex, rowIndex1, fx, F24Dot8_1, fy1);
        }
    }


    private static partial void CellVertical(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex px, PixelIndex py,
        F24Dot8 x, F24Dot8 y0, F24Dot8 y1)
    {
        Debug.Assert(px >= 0);
        Debug.Assert(py >= 0);
        Debug.Assert(py < T.TileH);

        F24Dot8 delta = y0 - y1;
        F24Dot8 a = delta * (F24Dot8_2 - x - x);
        int index = (int) px << 1;
        int* ca = coverAreaTable[py];

        if (ConditionalSetBit(bitVectorTable[py], px))
        {
            // New.
            ca[index] = delta;
            ca[index + 1] = a;
        }
        else
        {
            // Update old.
            int cover = ca[index];
            int area = ca[index + 1];

            ca[index] = cover + delta;
            ca[index + 1] = area + a;
        }
    }


    private static partial void Cell(BitVector* bitVector, int* coverArea,
        PixelIndex px, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(px >= 0);

        F24Dot8 delta = y0 - y1;
        F24Dot8 a = delta * (F24Dot8_2 - x0 - x1);
        int index = (int) px << 1;
        int* ca = coverArea;

        if (ConditionalSetBit(bitVector, px))
        {
            // New.
            ca[index] = delta;
            ca[index + 1] = a;
        }
        else
        {
            // Update old.
            int cover = ca[index];
            int area = ca[index + 1];

            ca[index] = cover + delta;
            ca[index + 1] = area + a;
        }
    }


    private static partial void RowDownR(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x < p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        BitVector* bitVector = bitVectorTable[rowIndex];
        int* coverArea = coverAreaTable[rowIndex];

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
        }
        else
        {
            // Horizontal and vertical deltas.
            F24Dot8 dx = p1x - p0x;
            F24Dot8 dy = p1y - p0y;

            F24Dot8 pp = (F24Dot8_1 - fx0) * dy;

            F24Dot8 cy = p0y + (pp / dx);

            Cell(bitVector, coverArea, columnIndex0, fx0, p0y,
                F24Dot8_1, cy);

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
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial void RowDownR_V(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
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


    private static partial void RowUpR(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x < p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        BitVector* bitVector = bitVectorTable[rowIndex];
        int* coverArea = coverAreaTable[rowIndex];

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
        }
        else
        {
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

                for (; idx != columnIndex1; idx++)
                {
                    F24Dot8 delta = lift;

                    mod += rem;

                    if (mod >= 0)
                    {
                        mod -= dx;
                        delta++;
                    }

                    F24Dot8 ny = cy - delta;

                    Cell(bitVector, coverArea, idx, 0, cy, F24Dot8_1, ny);

                    cy = ny;
                }
            }

            Cell(bitVector, coverArea, columnIndex1, 0, cy, fx1, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial void RowUpR_V(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
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


    private static partial void RowDownL(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x > p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);
        
        BitVector* bitVector = bitVectorTable[rowIndex];
        int* coverArea = coverAreaTable[rowIndex];

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
        }
        else
        {
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
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial void RowDownL_V(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
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


    private static partial void RowUpL(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
    {
        Debug.Assert(rowIndex >= 0);
        Debug.Assert(rowIndex < T.TileH);
        Debug.Assert(p0x > p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);
        
        BitVector* bitVector = bitVectorTable[rowIndex];
        int* coverArea = coverAreaTable[rowIndex];

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
        }
        else
        {
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
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial void RowUpL_V(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex, F24Dot8 p0x,
        F24Dot8 p0y, F24Dot8 p1x, F24Dot8 p1y)
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


    private static partial void LineDownR(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex0,
        PixelIndex rowIndex1, F24Dot8 x0, F24Dot8 y0,
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

        RowDownR_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx,
            F24Dot8_1);

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

                RowDownR_V(bitVectorTable, coverAreaTable, idy, cx, 0, nx,
                    F24Dot8_1);

                cx = nx;
            }
        }

        RowDownR_V(bitVectorTable, coverAreaTable, rowIndex1, cx, 0, x1, fy1);
    }


    /**
     * ⬈
     */
    private static partial void LineUpR(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex0,
        PixelIndex rowIndex1, F24Dot8 x0, F24Dot8 y0,
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
    private static partial void LineDownL(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex0,
        PixelIndex rowIndex1, F24Dot8 x0, F24Dot8 y0,
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

        RowDownL_V(bitVectorTable, coverAreaTable, rowIndex0, x0, fy0, cx,
            F24Dot8_1);

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

                RowDownL_V(bitVectorTable, coverAreaTable, idy, cx, 0, nx,
                    F24Dot8_1);

                cx = nx;
            }
        }

        RowDownL_V(bitVectorTable, coverAreaTable, rowIndex1, cx, 0, x1, fy1);
    }


    /**
     * ⬉
     */
    private static partial void LineUpL(BitVector** bitVectorTable,
        int** coverAreaTable, PixelIndex rowIndex0,
        PixelIndex rowIndex1, F24Dot8 x0, F24Dot8 y0,
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


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial void RasterizeLine(F24Dot8 X0,
        F24Dot8 Y0, F24Dot8 X1, F24Dot8 Y1,
        BitVector** bitVectorTable, int** coverAreaTable)
    {
        Debug.Assert(Y0 != Y1);
        Debug.Assert(bitVectorTable != null);
        Debug.Assert(coverAreaTable != null);

        if (X0 == X1)
        {
            PixelIndex columnIndex = F24Dot8ToPixelIndex(X0 - FindAdjustment(X0));

            // Special case, vertical line, simplifies this thing a lot.
            if (Y0 < Y1)
            {
                // Line is going down ↓
                Vertical_Down(bitVectorTable, coverAreaTable, columnIndex, Y0, Y1, X0);
                return;
            }
            else
            {
                // Line is going up ↑
                Vertical_Up(bitVectorTable, coverAreaTable, columnIndex, Y0, Y1, X0);
                return;
            }
        }

        if (Y0 < Y1)
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
                    RowDownR(bitVectorTable, coverAreaTable, rowIndex0,
                        X0, y0, X1, y1);
                    return;
                }
                else
                {
                    RowDownL(bitVectorTable, coverAreaTable, rowIndex0,
                        X0, y0, X1, y1);
                    return;
                }
            }
            else if (X0 < X1)
            {
                // Line is going from left to right →
                LineDownR(bitVectorTable, coverAreaTable, rowIndex0,
                    rowIndex1, X0, Y0, X1, Y1);
                return;
            }
            else
            {
                // Line is going right to left ←
                LineDownL(bitVectorTable, coverAreaTable, rowIndex0,
                    rowIndex1, X0, Y0, X1, Y1);
                return;
            }
        }
        else
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
                    RowUpR(bitVectorTable, coverAreaTable, rowIndex0,
                        X0, y0, X1, y1);
                    return;
                }
                else
                {
                    RowUpL(bitVectorTable, coverAreaTable, rowIndex0,
                        X0, y0, X1, y1);
                    return;
                }
            }
            else if (X0 < X1)
            {
                // Line is going from left to right →
                LineUpR(bitVectorTable, coverAreaTable, rowIndex0,
                    rowIndex1, X0, Y0, X1, Y1);
                return;
            }
            else
            {
                // Line is going right to left ←
                LineUpL(bitVectorTable, coverAreaTable, rowIndex0,
                    rowIndex1, X0, Y0, X1, Y1);
                return;
            }
        }
    }


    private static partial void RenderOneLine<B, F>(byte* image,
        BitVector* bitVectorTable, int bitVectorCount,
        int* coverAreaTable, int x, int rowLength,
        int startCover, B blender)
        where B : ISpanBlender
        where F : IFillRuleFn
    {
        Debug.Assert(image != null);
        Debug.Assert(bitVectorTable != null);
        Debug.Assert(bitVectorCount > 0);
        Debug.Assert(coverAreaTable != null);
        Debug.Assert(rowLength > 0);

        // X must be aligned on tile boundary.
        Debug.Assert((x & (T.TileW - 1)) == 0);

        uint* d = (uint*) (image);

        // Cover accumulation.
        int cover = startCover;

        // Span state.
        uint spanX = (uint) x;
        uint spanEnd = (uint) x;
        uint spanAlpha = 0;

        for (uint i = 0; i < bitVectorCount; i++)
        {
            BitVector bitset = bitVectorTable[i];

            while (bitset != 0)
            {
                BitVector t = bitset & (nuint) (-(nint) (nuint) bitset);
                uint r = (uint) CountTrailingZeroes((nuint) bitset);
                uint index = (uint) ((i * BIT_SIZE_OF<BitVector>()) + r);

                bitset ^= t;

                // Note that index is in local geometry coordinates.
                uint tableIndex = index << 1;
                uint edgeX = (uint) (index + x);
                uint nextEdgeX = edgeX + 1;

                // Signed area for pixel at bit index.
                int area = coverAreaTable[tableIndex + 1] + (cover << 9);

                // Area converted to alpha according to fill rule.
                uint alpha = (uint) F.ApplyFillRule(area);

                if (spanEnd == edgeX)
                {
                    // No gap between previous span and current pixel.
                    if (alpha == 0)
                    {
                        if (spanAlpha != 0)
                        {
                            blender.CompositeSpan((int) spanX, (int) spanEnd, d, (int) spanAlpha);
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
                        // Alpha is not zero, but not equal to previous span
                        // alpha.
                        if (spanAlpha != 0)
                        {
                            blender.CompositeSpan((int) spanX, (int) spanEnd, d, (int) spanAlpha);
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
                            blender.CompositeSpan((int) spanX, (int) spanEnd, d, (int) spanAlpha);
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
                                blender.CompositeSpan((int) spanX, (int) edgeX, d, (int) spanAlpha);

                                spanX = edgeX;
                                spanEnd = nextEdgeX;
                                spanAlpha = alpha;
                            }
                        }
                        else
                        {
                            if (spanAlpha != 0)
                            {
                                blender.CompositeSpan((int) spanX, (int) spanEnd, d, (int) spanAlpha);
                            }

                            // Compose gap.
                            blender.CompositeSpan((int) spanEnd, (int) edgeX, d, (int) gapAlpha);

                            spanX = edgeX;
                            spanEnd = nextEdgeX;
                            spanAlpha = alpha;
                        }
                    }
                }

                cover += coverAreaTable[tableIndex];
            }
        }

        if (spanAlpha != 0)
        {
            // Composite current span.
            blender.CompositeSpan((int) spanX, (int) spanEnd, d, (int) spanAlpha);
        }

        if (cover != 0 && spanEnd < rowLength)
        {
            // Composite anything that goes to the edge of destination image.
            int alpha = F.ApplyFillRule(cover << 9);

            blender.CompositeSpan((int) spanEnd, rowLength, d, alpha);
        }
    }


    private static partial void RasterizeOneItem(RasterizableItem* item,
        BitVector** bitVectorTable, int** coverAreaTable, int columnCount,
        ImageData image)
    {
        // A maximum number of horizontal tiles.
        int horizontalCount = (int) item->Rasterizable->Bounds.ColumnCount;

        Debug.Assert(horizontalCount <= columnCount);

        int bitVectorsPerRow = BitVectorsForMaxBitCount(
            horizontalCount * T.TileW);

        // Erase bit vector table.
        for (int i = 0; i < T.TileH; i++)
        {
            NativeMemory.Clear(bitVectorTable[i], (nuint) (sizeof(BitVector) * bitVectorsPerRow));
        }

        item->Rasterizable->IterationFunction.value(item, bitVectorTable, coverAreaTable);

        // Pointer to backdrop.
        int* coversStart = item->GetActualCovers();

        int x = (int) item->Rasterizable->Bounds.X * T.TileW;

        // Y position, measured in tiles.
        int miny = (int) (item->Rasterizable->Bounds.Y + item->LocalRowIndex);

        // Y position, measure in pixels.
        int py = miny * T.TileH;

        // Maximum y position, measured in pixels.
        int maxpy = py + T.TileH;

        // Start row.
        byte* ptr = image.Data + (py * image.BytesPerRow);

        // Calculate maximum height. This can only get less than 8 when rendering
        // the last row of the image and image height is not multiple of row
        // height.
        int hh = Math.Min(maxpy, image.Height) - py;

        // Fill color.
        uint color = item->Rasterizable->Geometry->Color;
        FillRule rule = item->Rasterizable->Geometry->Rule;

        if (color >= 0xff000000)
        {
            if (rule == FillRule.NonZero)
            {
                for (int i = 0; i < hh; i++)
                {
                    RenderOneLine<SpanBlenderOpaque, AreaToAlphaNonZeroFn>(ptr,
                        bitVectorTable[i], bitVectorsPerRow, coverAreaTable[i], x,
                        image.Width, coversStart[i], new(color));

                    ptr += image.BytesPerRow;
                }
            }
            else
            {
                for (int i = 0; i < hh; i++)
                {
                    RenderOneLine<SpanBlenderOpaque, AreaToAlphaEvenOddFn>(ptr,
                        bitVectorTable[i], bitVectorsPerRow, coverAreaTable[i], x,
                        image.Width, coversStart[i], new(color));

                    ptr += image.BytesPerRow;
                }
            }
        }
        else
        {
            if (rule == FillRule.NonZero)
            {
                for (int i = 0; i < hh; i++)
                {
                    RenderOneLine<SpanBlender, AreaToAlphaNonZeroFn>(ptr,
                        bitVectorTable[i], bitVectorsPerRow, coverAreaTable[i], x,
                        image.Width, coversStart[i], new(color));

                    ptr += image.BytesPerRow;
                }
            }
            else
            {
                for (int i = 0; i < hh; i++)
                {
                    RenderOneLine<SpanBlender, AreaToAlphaEvenOddFn>(ptr,
                        bitVectorTable[i], bitVectorsPerRow, coverAreaTable[i], x,
                        image.Width, coversStart[i], new(color));

                    ptr += image.BytesPerRow;
                }
            }
        }
    }


    /**
     * Rasterize all items in one row.
     */
    private static partial void RasterizeRow(
        RowItemList<RasterizableItem>* rowList, ThreadMemory memory,
        ImageData image)
    {
        // How many columns can fit into image.
        TileIndex columnCount = CalculateColumnCount<T>(image.Width);

        // Create bit vector arrays.
        int bitVectorsPerRow = BitVectorsForMaxBitCount(
            (int) columnCount * T.TileW);
        int bitVectorCount = bitVectorsPerRow * T.TileH;

        BitVector* bitVectors = (BitVector*) (
            memory.TaskMalloc(sizeof(BitVector) * bitVectorCount));

        // Create cover/area table.
        int coverAreaIntsPerRow = (int) columnCount * T.TileW * 2;
        int coverAreaIntCount = coverAreaIntsPerRow * T.TileH;

        int* coverArea = (int*) (
            memory.TaskMalloc(sizeof(int) * coverAreaIntCount));

        // Setup row pointers for bit vectors and cover/area table.
        BitVector** bitVectorTable = stackalloc BitVector*[T.TileH];
        int** coverAreaTable = stackalloc int*[T.TileH];

        for (int i = 0; i < T.TileH; i++)
        {
            bitVectorTable[i] = bitVectors;
            coverAreaTable[i] = coverArea;

            bitVectors += bitVectorsPerRow;
            coverArea += coverAreaIntsPerRow;
        }

        // Rasterize all items, from bottom to top that were added to this row.
        RowItemList<RasterizableItem>.Block* b = rowList->First;

        while (b != null)
        {
            int count = b->Count;
            RasterizableItem* itm = &b->Items[0];
            RasterizableItem* e = itm + count;

            while (itm < e)
            {
                RasterizeOneItem(itm++, bitVectorTable, coverAreaTable,
                    (int) columnCount, image);
            }

            b = b->Next;
        }
    }

}