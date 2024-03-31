using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe struct LineArrayX32Y16Block
{
    [InlineArray(LinesPerBlock)]
    public struct ArrayF8Dot8x2
    {
        private F8Dot8x2 _e0;
    }

    [InlineArray(LinesPerBlock)]
    public struct ArrayF24Dot8
    {
        private F24Dot8 _e0;
    }

    public LineArrayX32Y16Block(LineArrayX32Y16Block* next)
    {
        Next = next;
    }


    public const int LinesPerBlock = 32;


    // Y0 and Y1 encoded as two 8.8 fixed point numbers packed into one 32 bit
    // integer.
    public ArrayF8Dot8x2 Y0Y1;
    public ArrayF24Dot8 X0;
    public ArrayF24Dot8 X1;

    // Pointer to the next block of lines in the same row.
    public LineArrayX32Y16Block* Next;

    [Obsolete]
    public LineArrayX32Y16Block() { }
}


public unsafe partial struct LineArrayX32Y16 : ILineArrayBlock<LineArrayX32Y16>
{
    public LineArrayX32Y16()
    {
    }

    void* ILineArrayBlock<LineArrayX32Y16>.GetFrontBlock()
    {
        return GetFrontBlock();
    }

    private LineArrayX32Y16Block* mCurrent = null;
    private int mCount = LineArrayX32Y16Block.LinesPerBlock;
}
