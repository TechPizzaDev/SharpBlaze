using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static V128Helper;

public static class SIMD
{
    public static void FloatPointsToF24Dot8Points(
        in Matrix matrix,
        Span<F24Dot8Point> dst,
        ReadOnlySpan<FloatPoint> src,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        MatrixComplexity complexity = matrix.DetermineComplexity();
        Span<int> castDst = MemoryMarshal.Cast<F24Dot8Point, int>(dst);

        Vector128<int> vOrigin = origin.ToVector128();
        Vector128<int> vSize = size.ToVector128();
        
        switch (complexity)
        {
            case MatrixComplexity.Identity:
            {
                Vector128<double> s = Vector128.Create(256.0);
                TransformScaleOnly(s, castDst, src, vOrigin, vSize);
                break;
            }

            case MatrixComplexity.TranslationOnly:
            {
                Vector128<double> s = Vector128.Create(256.0);
                Vector128<double> t = matrix.M3() * s;
                TransformTranslationScale(s, t, castDst, src, vOrigin, vSize);
                break;
            }

            case MatrixComplexity.ScaleOnly:
            {
                Vector128<double> s = Vector128.Create(matrix.M11(), matrix.M22()) * 256.0;
                TransformScaleOnly(s, castDst, src, vOrigin, vSize);
                break;
            }

            case MatrixComplexity.TranslationScale:
                ConvertTranslationScale(matrix, castDst, src, vOrigin, vSize);
                break;

            case MatrixComplexity.Complex:
            default:
                ConvertComplex(matrix, castDst, src, vOrigin, vSize);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void TransformScaleOnly(
        Vector128<double> s,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        Vector128<int> origin,
        Vector128<int> size)
    {
        while (src.Length >= 2 && dst.Length >= 4)
        {
            Vector128<double> sum0 = src[0].AsVector128() * s;
            Vector128<double> sum1 = src[1].AsVector128() * s;
            ClampToInt32(sum0, sum1, origin, size).CopyTo(dst);
            src = src[2..];
            dst = dst[4..];
        }

        while (src.Length >= 1 && dst.Length >= 2)
        {
            Vector128<double> sum = src[0].AsVector128() * s;
            ClampToInt32(sum, origin, size).CopyTo(dst);
            src = src[1..];
            dst = dst[2..];
        }
    }

    private static void ConvertTranslationScale(
        in Matrix matrix,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        Vector128<int> origin,
        Vector128<int> size)
    {
        Matrix m = matrix * Matrix.CreateScale(256.0);
        Vector128<double> s = Vector128.Create(m.M11(), m.M22());
        Vector128<double> t = m.M3();
        
        TransformTranslationScale(s, t, dst, src, origin, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void TransformTranslationScale(
        Vector128<double> s,
        Vector128<double> t,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        Vector128<int> origin,
        Vector128<int> size)
    {
        while (src.Length >= 2 && dst.Length >= 4)
        {
            Vector128<double> sum0 = MulAdd(src[0].AsVector128(), s, t);
            Vector128<double> sum1 = MulAdd(src[1].AsVector128(), s, t);
            ClampToInt32(sum0, sum1, origin, size).CopyTo(dst);
            src = src[2..];
            dst = dst[4..];
        }

        while (src.Length >= 1 && dst.Length >= 2)
        {
            Vector128<double> sum = MulAdd(src[0].AsVector128(), s, t);
            ClampToInt32(sum, origin, size).CopyTo(dst);
            src = src[1..];
            dst = dst[2..];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ConvertComplex(
        in Matrix matrix,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        Vector128<int> origin,
        Vector128<int> size)
    {
        Matrix m = matrix * Matrix.CreateScale(256.0);
        Vector128<double> m0 = m.M1();
        Vector128<double> m1 = m.M2();
        Vector128<double> m2 = m.M3();

        while (src.Length >= 2 && dst.Length >= 4)
        {
            Vector128<double> sum0 = Transform(src[0].AsVector128(), m0, m1, m2);
            Vector128<double> sum1 = Transform(src[1].AsVector128(), m0, m1, m2);
            ClampToInt32(sum0, sum1, origin, size).CopyTo(dst);
            src = src[2..];
            dst = dst[4..];
        }

        while (src.Length >= 1 && dst.Length >= 2)
        {
            Vector128<double> sum = Transform(src[0].AsVector128(), m0, m1, m2);
            ClampToInt32(sum, origin, size).CopyTo(dst);
            src = src[1..];
            dst = dst[2..];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> Transform(
        Vector128<double> p, Vector128<double> m0, Vector128<double> m1, Vector128<double> m2)
    {
        Vector128<double> pX = Vector128.Create(p.GetElement(0));
        Vector128<double> pY = Vector128.Create(p.GetElement(1));
        return MulAdd(m0, pX, MulAdd(m1, pY, m2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> ClampToInt32(
        Vector128<double> sum0, Vector128<double> sum1, Vector128<int> origin, Vector128<int> size)
    {
        return Clamp(RoundToInt32(sum0, sum1) - origin, Vector128<int>.Zero, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector64<int> ClampToInt32(
        Vector128<double> sum, Vector128<int> origin, Vector128<int> size)
    {
        return Clamp(RoundToInt32(sum) - origin, Vector128<int>.Zero, size).GetLower();
    }
}
