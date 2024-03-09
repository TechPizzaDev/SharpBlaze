using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe struct LineArrayX32Y16Block : IConstructible<LineArrayX32Y16Block, Pointer<LineArrayX32Y16Block>>
{
    [InlineArray(LinesPerBlock)]
    public struct ArrayF8Dot8x2
    {
        private F8Dot8x2 _e0;
    }

    [InlineArray(LinesPerBlock)]
    public struct ArrayF24Dot8
    {
        private F8Dot8x2 _e0;
    }

    public static void Construct(ref LineArrayX32Y16Block instance, in Pointer<LineArrayX32Y16Block> args)
    {
        instance = new LineArrayX32Y16Block(args.Value);
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


public unsafe partial struct LineArrayX32Y16
{
    public LineArrayX32Y16()
    {
    }

    public static partial void Construct(LineArrayX32Y16* placement,
        TileIndex rowCount, TileIndex columnCount,
        ThreadMemory memory);

    public partial LineArrayX32Y16Block* GetFrontBlock();
    public partial int GetFrontBlockLineCount();

    public partial void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1);
    public partial void AppendLineDownR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineDownL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private partial void AppendLine(ThreadMemory memory, F8Dot8x2 y0y1, F24Dot8 x0, F24Dot8 x1);
    private partial void AppendLine(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private LineArrayX32Y16Block* mCurrent = null;
    private int mCount = LineArrayX32Y16Block.LinesPerBlock;
}
