using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpBlaze.Numerics;

internal static class Marshal2D
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Invoke<T, O>(in Span2D<T> source, in Span2D<T> destination, O op)
        where O : ISpanOp<T>
    {
        if (source.IsContiguous && destination.IsContiguous)
        {
            InvokeContiguous(source, destination, op);
        }
        else
        {
            InvokeByLine(source, destination, op);
        }
    }

    private static void InvokeByLine<T, O>(in Span2D<T> source, in Span2D<T> destination, O op)
        where O : ISpanOp<T>
    {
        int height = source.Height;
        for (int y = 0; y < height; y++)
        {
            op.Invoke(source[y], destination[y]);
        }
    }

    private static void InvokeContiguous<T, O>(in Span2D<T> source, in Span2D<T> destination, O op)
        where O : ISpanOp<T>
    {
        ulong area = source.Area;
        if (area > destination.Area)
            ThrowHelper.ThrowDestinationTooShort();

        ref T src = ref source._data;
        ref T dst = ref destination._data;
        do
        {
            int length = int.CreateSaturating(area);
            Span<T> srcSpan = MemoryMarshal.CreateSpan(ref src, length);
            Span<T> dstSpan = MemoryMarshal.CreateSpan(ref dst, length);
            op.Invoke(srcSpan, dstSpan);

            src = ref Unsafe.Add(ref src, (uint) length);
            dst = ref Unsafe.Add(ref dst, (uint) length);
            area -= (uint) length;
        }
        while (area > 0);
    }
}
