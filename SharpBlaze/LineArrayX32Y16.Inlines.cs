using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe partial struct LineArrayX32Y16
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial void Construct(ref LineArrayX32Y16 placement,
        TileIndex rowCount, TileIndex columnCount,
        ThreadMemory memory)
    {
        //Debug.Assert(placement != null);
        Debug.Assert(rowCount > 0);
        Debug.Assert(columnCount > 0);

        for (TileIndex i = 0; i < rowCount; i++)
        {
            Unsafe.Add(ref placement, i) = new LineArrayX32Y16();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial LineArrayX32Y16Block* GetFrontBlock()
    {
        return mCurrent;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial int GetFrontBlockLineCount()
    {
        return mCount;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1)
    {
        AppendLine(memory, x, y0, x, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendLineDownR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendLineUpR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendLineDownL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendLineUpL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial void AppendLine(ThreadMemory memory, F8Dot8x2 y0y1, F24Dot8 x0, F24Dot8 x1)
    {
        LineArrayX32Y16Block* current = mCurrent;
        int count = mCount;

        if (count < LineArrayX32Y16Block.LinesPerBlock)
        {
            // Most common.
            current->Y0Y1[count] = y0y1;
            current->X0[count] = x0;
            current->X1[count] = x1;

            mCount = count + 1;
        }
        else
        {
            LineArrayX32Y16Block* b = memory.FrameNewX32Y16Block(current);

            b->Y0Y1[0] = y0y1;
            b->X0[0] = x0;
            b->X1[0] = x1;

            // Set count to 1 for segment being added.
            mCount = 1;

            mCurrent = b;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial void AppendLine(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        if (y0 != y1)
        {
            AppendLine(memory, F8Dot8.PackF24Dot8ToF8Dot8x2(y0, y1), x0, x1);
        }
    }

}