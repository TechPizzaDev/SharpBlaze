using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe struct LineArrayX32Y16Block
{
    [InlineArray(LinesPerBlock)]
    public struct Array
    {
        private Line _e0;
    }

    public LineArrayX32Y16Block(LineArrayX32Y16Block* next)
    {
        Next = next;
    }


    public const int LinesPerBlock = 32;


    public Array P0P1;

    // Pointer to the next block of lines in the same row.
    public LineArrayX32Y16Block* Next;

    [Obsolete]
    public LineArrayX32Y16Block() { }

    public struct Line
    {
        // Y0 and Y1 encoded as two 8.8 fixed point numbers packed into one 32-bit int.
        public F8Dot8x2 Y0Y1;
        
        public F24Dot8 X0;
        public F24Dot8 X1;

        public Line(F8Dot8x2 y0Y1, F24Dot8 x0, F24Dot8 x1)
        {
            Y0Y1 = y0Y1;
            X0 = x0;
            X1 = x1;
        }
    }
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
