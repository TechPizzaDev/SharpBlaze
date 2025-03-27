using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe partial struct LineArrayX32Y16
{

    public static void Construct(Span<LineArrayX32Y16> placement,
        TileIndex columnCount,
        ThreadMemory memory)
    {
        int rowCount = placement.Length;

        //Debug.Assert(placement != null);
        Debug.Assert(rowCount > 0);
        Debug.Assert(columnCount > 0);

        for (int i = 0; i < rowCount; i++)
        {
            placement[i] = new LineArrayX32Y16();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineArrayX32Y16Block* GetFrontBlock()
    {
        return mCurrent;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFrontBlockLineCount()
    {
        return mCount;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1)
    {
        AppendLine(memory, x, y0, x, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineDownR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineUpR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineDownL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineUpL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        AppendLine(memory, x0, y0, x1, y1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendLine(ThreadMemory memory, F8Dot8x2 y0y1, F24Dot8 x0, F24Dot8 x1)
    {
        LineArrayX32Y16Block* current = mCurrent;
        int count = mCount;

        if (count < LineArrayX32Y16Block.LinesPerBlock)
        {
            // Most common.
            current->P0P1[count] = new LineArrayX32Y16Block.Line(y0y1, x0, x1);

            mCount = count + 1;
        }
        else
        {
            LineArrayX32Y16Block* b = memory.FrameNewX32Y16Block(current);

            b->P0P1[0] = new LineArrayX32Y16Block.Line(y0y1, x0, x1);

            // Set count to 1 for segment being added.
            mCount = 1;

            mCurrent = b;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendLine(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        if (y0 != y1)
        {
            AppendLine(memory, F8Dot8.Pack(y0, y1), x0, x1);
        }
    }

}