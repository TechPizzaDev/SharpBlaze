using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe partial struct LineArrayTiled<T>
    where T : ITileDescriptor
{
    public static void Construct(Span<LineArrayTiled<T>> placement,
        TileIndex columnCount,
        ThreadMemory memory)
    {
        int rowCount = placement.Length;

        //Debug.Assert(placement != null);
        Debug.Assert(rowCount > 0);
        Debug.Assert(columnCount > 0);

        int bitVectorsPerRow = BitOps.BitVectorsForMaxBitCount((int) columnCount);
        int bitVectorCount = bitVectorsPerRow * rowCount;

        BitVector* bitVectors = memory.FrameMallocArrayZeroFill<BitVector>(bitVectorCount);

        LineArrayTiledBlock** blocks =
            memory.TaskMallocPointers<LineArrayTiledBlock>((int) columnCount * rowCount);

        int** covers =
            memory.TaskMallocPointers<int>((int) columnCount * rowCount);

        int* counts =
            memory.TaskMallocArray<int>((int) columnCount * rowCount);

        for (int i = 0; i < rowCount; i++)
        {
            placement[i] = new LineArrayTiled<T>(bitVectors, blocks, covers, counts);

            bitVectors += bitVectorsPerRow;
            blocks += columnCount;
            covers += columnCount;
            counts += columnCount;
        }
    }


    public BitVector* GetTileAllocationBitVectors()
    {
        return mBitVectors;
    }


    public LineArrayTiledBlock* GetFrontBlockForColumn(TileIndex columnIndex)
    {
        return mBlocks[columnIndex];
    }


    public int* GetCoversForColumn(TileIndex columnIndex)
    {
        return mCovers[columnIndex];
    }


    public int GetTotalLineCountForColumn(TileIndex columnIndex)
    {
        return mCounts[columnIndex];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1)
    {
        TileIndex columnIndex = T.F24Dot8ToTileColumnIndex(x - FindTileColumnAdjustment(x));
        F24Dot8 ex = x - T.TileColumnIndexToF24Dot8(columnIndex);

        Push(memory, columnIndex, ex, y0, ex, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineDownR_V(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x <= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        if (p0x < p1x)
        {
            AppendLineDownR(memory, p0x, p0y, p1x, p1y);
        }
        else
        {
            AppendVerticalLine(memory, p0x, p0y, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineUpR_V(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x <= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        if (p0x < p1x)
        {
            AppendLineUpR(memory, p0x, p0y, p1x, p1y);
        }
        else
        {
            AppendVerticalLine(memory, p0x, p0y, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineDownL_V(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        if (p0x > p1x)
        {
            AppendLineDownL(memory, p0x, p0y, p1x, p1y);
        }
        else
        {
            AppendVerticalLine(memory, p0x, p0y, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineUpL_V(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        if (p0x > p1x)
        {
            AppendLineUpL(memory, p0x, p0y, p1x, p1y);
        }
        else
        {
            AppendVerticalLine(memory, p0x, p0y, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(x0 != x1);

        if (x0 < x1)
        {
            AppendLineDownR(memory, x0, y0, x1, y1);
        }
        else
        {
            AppendLineDownL(memory, x0, y0, x1, y1);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        Debug.Assert(x0 != x1);

        if (x0 < x1)
        {
            AppendLineUpR(memory, x0, y0, x1, y1);
        }
        else
        {
            AppendLineUpL(memory, x0, y0, x1, y1);
        }
    }


    private void AppendLineDownR(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x <= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        TileIndex columnIndex0 = T.F24Dot8ToTileColumnIndex(p0x);
        TileIndex columnIndex1 = T.F24Dot8ToTileColumnIndex(p1x - 1);

        Debug.Assert(columnIndex0 <= columnIndex1);

        if (columnIndex0 == columnIndex1)
        {
            Append(memory, columnIndex0, p0x, p0y, p1x, p1y);
        }
        else
        {
            // Number of pixels + 24.8 fraction for start and end points in coordinate
            // system of tiles these points belong to.
            F24Dot8 fx = p0x - T.TileColumnIndexToF24Dot8(columnIndex0);

            Debug.Assert(fx >= 0);
            Debug.Assert(fx <= T.TileWF24Dot8);

            // Horizontal and vertical deltas.
            F24Dot8 dx = p1x - p0x;
            F24Dot8 dy = p1y - p0y;

            F24Dot8 pp = (T.TileWF24Dot8 - fx) * dy;

            F24Dot8 cy = p0y + (pp / dx);

            TileIndex idx = columnIndex0 + 1;

            F24Dot8 cursor = T.TileColumnIndexToF24Dot8(idx);

            Append(memory, columnIndex0, p0x, p0y, cursor, cy);

            if (idx != columnIndex1)
            {
                F24Dot8 mod = (pp % dx) - dx;

                F24Dot8 p = T.TileWF24Dot8 * dy;
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
                    F24Dot8 nx = cursor + T.TileWF24Dot8;

                    Append(memory, idx, cursor, cy, nx, ny);

                    cy = ny;
                    cursor = nx;
                }
            }

            Append(memory, columnIndex1, cursor, cy, p1x, p1y);
        }
    }


    private void AppendLineUpR(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x <= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        TileIndex columnIndex0 = T.F24Dot8ToTileColumnIndex(p0x);
        TileIndex columnIndex1 = T.F24Dot8ToTileColumnIndex(p1x - 1);

        Debug.Assert(columnIndex0 <= columnIndex1);

        if (columnIndex0 == columnIndex1)
        {
            Append(memory, columnIndex0, p0x, p0y, p1x, p1y);
        }
        else
        {
            // Number of pixels + 24.8 fraction for start and end points in coordinate
            // system of tiles these points belong to.
            F24Dot8 fx = p0x - T.TileColumnIndexToF24Dot8(columnIndex0);

            Debug.Assert(fx >= 0);
            Debug.Assert(fx <= T.TileWF24Dot8);

            // Horizontal and vertical deltas.
            F24Dot8 dx = p1x - p0x;
            F24Dot8 dy = p0y - p1y;

            F24Dot8 pp = (T.TileWF24Dot8 - fx) * dy;

            F24Dot8 cy = p0y - (pp / dx);

            TileIndex idx = columnIndex0 + 1;

            F24Dot8 cursor = T.TileColumnIndexToF24Dot8(idx);

            Append(memory, columnIndex0, p0x, p0y, cursor, cy);

            if (idx != columnIndex1)
            {
                F24Dot8 mod = (pp % dx) - dx;

                F24Dot8 p = T.TileWF24Dot8 * dy;
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
                    F24Dot8 nx = cursor + T.TileWF24Dot8;

                    Append(memory, idx, cursor, cy, nx, ny);

                    cy = ny;
                    cursor = nx;
                }
            }

            Append(memory, columnIndex1, cursor, cy, p1x, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendLineDownL(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y <= p1y);

        TileIndex columnIndex0 = T.F24Dot8ToTileColumnIndex(p0x - 1);
        TileIndex columnIndex1 = T.F24Dot8ToTileColumnIndex(p1x);

        Debug.Assert(columnIndex1 <= columnIndex0);

        if (columnIndex0 == columnIndex1)
        {
            Append(memory, columnIndex0, p0x, p0y, p1x, p1y);
        }
        else
        {
            // Number of pixels + 24.8 fraction for start and end points in coordinate
            // system of tiles these points belong to.
            F24Dot8 fx = p0x - T.TileColumnIndexToF24Dot8(columnIndex0);

            Debug.Assert(fx >= 0);
            Debug.Assert(fx <= T.TileWF24Dot8);

            // Horizontal and vertical deltas.
            F24Dot8 dx = p0x - p1x;
            F24Dot8 dy = p1y - p0y;

            F24Dot8 pp = fx * dy;

            F24Dot8 cy = p0y + (pp / dx);

            TileIndex idx = columnIndex0 - 1;

            F24Dot8 cursor = T.TileColumnIndexToF24Dot8(columnIndex0);

            Append(memory, columnIndex0, p0x, p0y, cursor, cy);

            if (idx != columnIndex1)
            {
                F24Dot8 mod = (pp % dx) - dx;

                F24Dot8 p = T.TileWF24Dot8 * dy;
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
                    F24Dot8 nx = cursor - T.TileWF24Dot8;

                    Append(memory, idx, cursor, cy, nx, ny);

                    cy = ny;
                    cursor = nx;
                }
            }

            Append(memory, columnIndex1, cursor, cy, p1x, p1y);
        }
    }


    private void AppendLineUpL(ThreadMemory memory,
        F24Dot8 p0x, F24Dot8 p0y, F24Dot8 p1x,
        F24Dot8 p1y)
    {
        Debug.Assert(p0x >= p1x);
        Debug.Assert(p0y >= 0);
        Debug.Assert(p0y <= T.TileHF24Dot8);
        Debug.Assert(p1y >= 0);
        Debug.Assert(p1y <= T.TileHF24Dot8);
        Debug.Assert(p0y >= p1y);

        TileIndex columnIndex0 = T.F24Dot8ToTileColumnIndex(p0x - 1);
        TileIndex columnIndex1 = T.F24Dot8ToTileColumnIndex(p1x);

        Debug.Assert(columnIndex1 <= columnIndex0);

        if (columnIndex0 == columnIndex1)
        {
            Append(memory, columnIndex0, p0x, p0y, p1x, p1y);
        }
        else
        {
            // Number of pixels + 24.8 fraction for start and end points in coordinate
            // system of tiles these points belong to.
            F24Dot8 fx = p0x - T.TileColumnIndexToF24Dot8(columnIndex0);

            Debug.Assert(fx >= 0);
            Debug.Assert(fx <= T.TileWF24Dot8);

            // Horizontal and vertical deltas.
            F24Dot8 dx = p0x - p1x;
            F24Dot8 dy = p0y - p1y;

            F24Dot8 pp = fx * dy;

            F24Dot8 cy = p0y - (pp / dx);

            TileIndex idx = columnIndex0 - 1;

            F24Dot8 cursor = T.TileColumnIndexToF24Dot8(columnIndex0);

            Append(memory, columnIndex0, p0x, p0y, cursor, cy);

            if (idx != columnIndex1)
            {
                F24Dot8 mod = (pp % dx) - dx;

                F24Dot8 p = T.TileWF24Dot8 * dy;
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
                    F24Dot8 nx = cursor - T.TileWF24Dot8;

                    Append(memory, idx, cursor, cy, nx, ny);

                    cy = ny;
                    cursor = nx;
                }
            }

            Append(memory, columnIndex1, cursor, cy, p1x, p1y);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Append(ThreadMemory memory,
        TileIndex columnIndex, F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        if (y0 != y1)
        {
            F24Dot8 cx = T.TileColumnIndexToF24Dot8(columnIndex);
            F24Dot8 ex0 = x0 - cx;
            F24Dot8 ex1 = x1 - cx;

            Push(memory, columnIndex, ex0, y0, ex1, y1);
        }
    }


    private void Push(ThreadMemory memory,
        TileIndex columnIndex, F24Dot8 x0, F24Dot8 y0,
        F24Dot8 x1, F24Dot8 y1)
    {
        if (BitOps.ConditionalSetBit(mBitVectors, columnIndex))
        {
            // First time line is inserted into this column.
            LineArrayTiledBlock* b = memory.FrameNewTiledBlock(null);
            int* covers = memory.FrameMallocArrayZeroFill<int>(T.TileH);

            LinearizerUtils.UpdateCoverTable(covers, y0, y1);

            b->P0P1[0] = F8Dot8.PackF24Dot8ToF8Dot8x4(x0, y0, x1, y1);

            // First line sets count to 1 for line being inserted right now.
            mBlocks[columnIndex] = b;
            mCovers[columnIndex] = covers;
            mCounts[columnIndex] = 1;
        }
        else
        {
            LineArrayTiledBlock* current = mBlocks[columnIndex];

            // Count is total number of lines in all blocks. This makes things
            // easier later, at GPU data preparation stage.
            int count = mCounts[columnIndex];

            Debug.Assert(count > 0);

            const int Mask = LineArrayTiledBlock.LinesPerBlock - 1;

            // Find out line count in current block. Assuming count will always be
            // at least 1 (first block allocation is handled as a special case and
            // sets count to 1). This value will be from 1 to maximum line count
            // for block.
            int countInCurrentBlock = ((count - 1) & Mask) + 1;

            if (countInCurrentBlock < LineArrayTiledBlock.LinesPerBlock)
            {
                current->P0P1[countInCurrentBlock] = F8Dot8.PackF24Dot8ToF8Dot8x4(x0, y0, x1, y1);

                LinearizerUtils.UpdateCoverTable(mCovers[columnIndex], y0, y1);
            }
            else
            {
                LineArrayTiledBlock* b = memory.FrameNewTiledBlock(current);

                b->P0P1[0] = F8Dot8.PackF24Dot8ToF8Dot8x4(x0, y0, x1, y1);

                LinearizerUtils.UpdateCoverTable(mCovers[columnIndex], y0, y1);

                mBlocks[columnIndex] = b;
            }

            mCounts[columnIndex] = count + 1;
        }
    }
}
