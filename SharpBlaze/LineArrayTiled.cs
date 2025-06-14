using System;
using System.Runtime.CompilerServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public unsafe struct LineArrayTiledBlock
{
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


public unsafe partial struct LineArrayTiled<T> : ILineArray<LineArrayTiled<T>>
    where T : ITileDescriptor<T>
{
    private static F24Dot8 AdjustmentMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => T.TileW.ToF24D8() - F24Dot8.Epsilon;
    }

    private static F24Dot8 FindTileColumnAdjustment(F24Dot8 value)
    {
        F24Dot8 e = F24Dot8.Epsilon;
        
        // Will be set to 0 is value is zero or less. Otherwise it will be 1.
        F24Dot8 lte0 = ~((value - e) >> 31) & e;

        // Will be set to 1 if value is divisible by tile width (in 24.8
        // format) without a reminder. Otherwise it will be 0.
        F24Dot8 db = (((value & AdjustmentMask) - e) >> 31) & e;

        // Return 1 if both bits (more than zero and disisible by 256) are set.
        return lte0 & db;
    }

    private LineArrayTiled(
        BitVector* bitVectors,
        int bitVectorCount,
        LineArrayTiledBlock** blocks,
        F24Dot8** covers,
        int* counts)
    {
        mBitVectors = bitVectors;
        mBitVectorCount = bitVectorCount;
        mBlocks = blocks;
        mCovers = covers;
        mCounts = counts;
    }

    // One bit for each tile column.
    private BitVector* mBitVectors = null;

    private int mBitVectorCount; 

    // One block pointer for each tile column. Not zero-filled at the
    // beginning, individual pointers initialized to newly allocated blocks
    // once the first line is inserted into particular column.
    private LineArrayTiledBlock** mBlocks = null;

    // One cover array for each tile column. Not zero-filled at the beginning,
    // individual cover arrays allocated and zero-filled once the first line
    // is inserted into particular column.
    private F24Dot8** mCovers = null;

    // One count for each tile column. Not zero-filled at the beginning,
    // individual counts initialized to one once the first line is inserted
    // into particular column.
    private int* mCounts = null;
}