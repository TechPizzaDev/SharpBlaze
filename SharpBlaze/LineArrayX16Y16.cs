using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;


public unsafe partial struct LineArrayX16Y16Block : IConstructible<LineArrayX16Y16Block, Pointer<LineArrayX16Y16Block>>
{
    [InlineArray(LinesPerBlock)]
    public struct Array
    {
        private F8Dot8x2 _e0;
    }
    
    public static void Construct(ref LineArrayX16Y16Block instance, in Pointer<LineArrayX16Y16Block> args)
    {
        instance = new LineArrayX16Y16Block(args.Value);
    }

    public LineArrayX16Y16Block(LineArrayX16Y16Block* next)
    {
        Next = next;
    }


    public const int LinesPerBlock = 32;


    // Y0 and Y1 encoded as two 8.8 fixed point numbers packed into one 32 bit
    // integer.
    public Array Y0Y1;
    public Array X0X1;

    // Pointer to the next block of lines in the same row.
    public LineArrayX16Y16Block* Next;

    [Obsolete]
    public LineArrayX16Y16Block() { }
};


public unsafe partial struct LineArrayX16Y16 : ILineArray<LineArrayX16Y16>
{
    public LineArrayX16Y16()
    {
    }

    public static partial void Construct(ref LineArrayX16Y16 placement,
        TileIndex rowCount, TileIndex columnCount,
        ThreadMemory memory);

    public partial LineArrayX16Y16Block* GetFrontBlock();
    public partial int GetFrontBlockLineCount();

    public partial void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1);
    public partial void AppendLineDownR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineDownL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    public partial void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private partial void AppendLine(ThreadMemory memory, F8Dot8x2 y0y1, F8Dot8x2 x0x1);
    private partial void AppendLine(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);

    private LineArrayX16Y16Block* mCurrent = null;
    private int mCount = LineArrayX16Y16Block.LinesPerBlock;
};
