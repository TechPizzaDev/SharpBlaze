using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

public static partial class SIMD
{
    interface ISimdOp<O, I>
    {
        Vector128<O> Invoke(Vector128<I> a);

        Vector256<O> Invoke(Vector256<I> a);

        Vector512<O> Invoke(Vector512<I> a);
    }

    interface ISimdReduce<O, I>
    {
        Vector64<O> Invoke(Vector128<I> a);

        Vector128<O> Invoke(Vector128<I> a, Vector128<I> b);

        Vector128<O> Invoke(Vector256<I> a);

        Vector256<O> Invoke(Vector512<I> a);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Invoke<T1, T2, R1>(ReadOnlySpan<double> src, Span<int> dst, T1 t1, T2 t2)
        where T1 : ISimdOp<R1, double>
        where T2 : ISimdReduce<int, R1>
    {
        if (Vector512.IsHardwareAccelerated)
        {
            while (src.Length >= 8 && dst.Length >= 8)
            {
                Vector512<R1> sum0 = t1.Invoke(Vector512.Create(src));
                t2.Invoke(sum0).CopyTo(dst);
                src = src.Slice(8);
                dst = dst.Slice(8);
            }
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            while (src.Length >= 4 && dst.Length >= 4)
            {
                Vector256<R1> sum0 = t1.Invoke(Vector256.Create(src));
                t2.Invoke(sum0).CopyTo(dst);
                src = src.Slice(4);
                dst = dst.Slice(4);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            while (src.Length >= 4 && dst.Length >= 4)
            {
                Vector128<R1> sum0 = t1.Invoke(Vector128.Create(src));
                Vector128<R1> sum1 = t1.Invoke(Vector128.Create(src.Slice(2, 2)));
                t2.Invoke(sum0, sum1).CopyTo(dst);
                src = src.Slice(4);
                dst = dst.Slice(4);
            }
        }

        while (src.Length >= 2 && dst.Length >= 2)
        {
            Vector128<R1> sum = t1.Invoke(Vector128.Create(src));
            t2.Invoke(sum).CopyTo(dst);
            src = src.Slice(2);
            dst = dst.Slice(2);
        }
    }
}
