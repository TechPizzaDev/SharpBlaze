using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe partial struct LineArrayX16Y16
{

    public static void Construct(Span<LineArrayX16Y16> placement,
        TileIndex columnCount,
        ThreadMemory memory)
    {
        int rowCount = placement.Length;

        //Debug.Assert(placement != null);
        Debug.Assert(rowCount > 0);
        Debug.Assert(columnCount > 0);

        for (int i = 0; i < rowCount; i++)
        {
            placement[i] = new LineArrayX16Y16();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineArrayX16Y16Block* GetFrontBlock()
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
    private void AppendLine(ThreadMemory memory, F8Dot8x4 p0p1)
    {
        int count = mCount;
        if ((uint) count < LineArrayX16Y16Block.LinesPerBlock)
        {
            // Most common.
            mCurrent->P0P1[count] = p0p1;

            mCount = count + 1;
            return;
        }
        GrowAndAppendLine(memory, p0p1);
    }
    
    private void GrowAndAppendLine(ThreadMemory memory, F8Dot8x4 p0p1)
    {
        LineArrayX16Y16Block* b = memory.FrameNewX16Y16Block(mCurrent);

        b->P0P1[0] = p0p1;

        // Set count to 1 for segment being added.
        mCount = 1;

        mCurrent = b;
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendLine(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1)
    {
        if (y0 != y1)
        {
            AppendLine(memory, F8Dot8.Pack(x0, y0, x1, y1));
        }
    }

}