using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe struct LineArrayTiledBlock : IConstructible<LineArrayTiledBlock, Pointer<LineArrayTiledBlock>>
{
    public static void Construct(ref LineArrayTiledBlock instance, in Pointer<LineArrayTiledBlock> args)
    {
        instance = new LineArrayTiledBlock(args.Value);
    }

    [InlineArray(LinesPerBlock)]
    public struct Array
    {
        private F8Dot8x4 _e0;
    }

    public LineArrayTiledBlock(LineArrayTiledBlock* next)
    {
        Next = next;
    }


    public const int LinesPerBlock = 8;


    public Array P0P1;

    public LineArrayTiledBlock* Next;

    [Obsolete]
    public LineArrayTiledBlock() { }
}


public unsafe partial struct LineArrayTiled<T>
    where T : ITileDescriptor
{

    public static partial void Construct(LineArrayTiled<T>* placement,
        TileIndex rowCount, TileIndex columnCount,
        ThreadMemory memory);

    public partial BitVector* GetTileAllocationBitVectors();
    public partial LineArrayTiledBlock* GetFrontBlockForColumn(TileIndex columnIndex);
    public partial int* GetCoversForColumn(TileIndex columnIndex);
    public partial int GetTotalLineCountForColumn(TileIndex columnIndex);

    public partial void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1);
    public partial void AppendLineDownR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineDownL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private partial void AppendLineDownR(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    private partial void AppendLineUpR(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    private partial void AppendLineDownL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    private partial void AppendLineUpL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private partial void Append(ThreadMemory memory, TileIndex columnIndex,
        F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    private partial void Push(ThreadMemory memory, TileIndex columnIndex,
        F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private static int AdjustmentMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (F24Dot8.F24Dot8_1 * T.TileW) - 1;
    }

    private static int FindTileColumnAdjustment(F24Dot8 value)
    {
        // Will be set to 0 is value is zero or less. Otherwise it will be 1.
        int lte0 = ~((value - 1) >> 31) & 1;

        // Will be set to 1 if value is divisible by tile width (in 24.8
        // format) without a reminder. Otherwise it will be 0.
        int db = (((value & AdjustmentMask) - 1) >> 31) & 1;

        // Return 1 if both bits (more than zero and disisible by 256) are set.
        return lte0 & db;
    }

    private LineArrayTiled(BitVector* bitVectors,
        LineArrayTiledBlock** blocks, int** covers, int* counts)
    {
        mBitVectors = bitVectors;
        mBlocks = blocks;
        mCovers = covers;
        mCounts = counts;
    }

    // One bit for each tile column.
    private BitVector* mBitVectors = null;

    // One block pointer for each tile column. Not zero-filled at the
    // beginning, individual pointers initialized to newly allocated blocks
    // once the first line is inserted into particular column.
    private LineArrayTiledBlock** mBlocks = null;

    // One cover array for each tile column. Not zero-filled at the beginning,
    // individual cover arrays allocated and zero-filled once the first line
    // is inserted into particular column.
    private int** mCovers = null;

    // One count for each tile column. Not zero-filled at the beginning,
    // individual counts initialized to one once the first line is inserted
    // into particular column.
    private int* mCounts = null;
}