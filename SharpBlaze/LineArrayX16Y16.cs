using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;


public unsafe struct LineArrayX16Y16Block
{
    [InlineArray(LinesPerBlock)]
    public struct Array
    {
        private F8Dot8x4 _e0;
    }

    public LineArrayX16Y16Block(LineArrayX16Y16Block* next)
    {
        Next = next;
    }


    public const int LinesPerBlock = 16;


    // P0 and P1 encoded as two by two 8.8 fixed point numbers packed into one 64-bit int.
    public Array P0P1;

    // Pointer to the next block of lines in the same row.
    public LineArrayX16Y16Block* Next;

    [Obsolete]
    public LineArrayX16Y16Block() { }
};


public unsafe partial struct LineArrayX16Y16 : ILineArrayBlock<LineArrayX16Y16>
{
    public LineArrayX16Y16()
    {
    }

    void* ILineArrayBlock<LineArrayX16Y16>.GetFrontBlock()
    {
        return GetFrontBlock();
    }

    private LineArrayX16Y16Block* mCurrent = null;
    private int mCount = LineArrayX16Y16Block.LinesPerBlock;
};
