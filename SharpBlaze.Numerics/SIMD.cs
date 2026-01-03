using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static V128Helper;

public static partial class SIMD
{
    public static void FloatPointsToF24Dot8Points(
        in Matrix matrix,
        Span<F24Dot8Point> destination,
        ReadOnlySpan<FloatPoint> source,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        Span<int> dst = MemoryMarshal.Cast<F24Dot8Point, int>(destination);
        ReadOnlySpan<double> src = MemoryMarshal.Cast<FloatPoint, double>(source);

        ConvertClampReduction clamper = new(origin, size);
        MatrixComplexity complexity = matrix.DetermineComplexity();
        switch (complexity)
        {
            case MatrixComplexity.Identity:
            {
                Vector128<double> s = Vector128.Create(256.0);
                Invoke<ScaleTransform, ConvertClampReduction, double>(src, dst, new(s), clamper);
                break;
            }

            case MatrixComplexity.TranslationOnly:
            {
                Vector128<double> s = Vector128.Create(256.0);
                Vector128<double> t = matrix.M3() * s;
                Invoke<TranslateScaleTransform, ConvertClampReduction, double>(src, dst, new(s, t), clamper);
                break;
            }


            case MatrixComplexity.ScaleOnly:
            {
                Vector128<double> s = Vector128.Create(matrix.M11(), matrix.M22()) * 256.0;
                Invoke<ScaleTransform, ConvertClampReduction, double>(src, dst, new(s), clamper);
                break;
            }

            case MatrixComplexity.TranslationScale:
            {
                Matrix m = matrix * Matrix.CreateScale(256.0);
                Vector128<double> s = Vector128.Create(m.M11(), m.M22());
                Vector128<double> t = m.M3();
                Invoke<TranslateScaleTransform, ConvertClampReduction, double>(src, dst, new(s, t), clamper);
                break;
            }

            case MatrixComplexity.Complex:
            default:
                Invoke<ComplexTransform, ConvertClampReduction, double>(src, dst, new(matrix), clamper);
                break;
        }
    }

    readonly struct ScaleTransform(Vector128<double> s) : ISimdOp<double, double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<double> Invoke(Vector128<double> a) => a * s;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<double> Invoke(Vector256<double> a) => a * V256Helper.Create(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<double> Invoke(Vector512<double> a) => a * V512Helper.Create(s);
    }

    readonly struct TranslateScaleTransform(Vector128<double> s, Vector128<double> t) : ISimdOp<double, double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<double> Invoke(Vector128<double> a) => MulAdd(a, s, t);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<double> Invoke(Vector256<double> a) => V256Helper.MulAdd(a, V256Helper.Create(s), V256Helper.Create(t));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<double> Invoke(Vector512<double> a) => V512Helper.MulAdd(a, V512Helper.Create(s), V512Helper.Create(t));
    }

    readonly struct ComplexTransform(Matrix mat) : ISimdOp<double, double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<double> Invoke(Vector128<double> a)
        {
            Vector128<double> pX = Vector128.Create(a.GetElement(0));
            Vector128<double> pY = Vector128.Create(a.GetElement(1));
            Vector128<double> m1 = mat.M1();
            Vector128<double> m2 = mat.M2();
            Vector128<double> m3 = mat.M3();
            return MulAdd(m1, pX, MulAdd(m2, pY, m3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<double> Invoke(Vector256<double> a)
        {
            Vector256<double> pX = Vector256.Create(a.GetElement(0));
            Vector256<double> pY = Vector256.Create(a.GetElement(1));
            Vector256<double> m1 = V256Helper.Create(mat.M1());
            Vector256<double> m2 = V256Helper.Create(mat.M2());
            Vector256<double> m3 = V256Helper.Create(mat.M3());
            return V256Helper.MulAdd(m1, pX, V256Helper.MulAdd(m2, pY, m3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<double> Invoke(Vector512<double> a)
        {
            Vector512<double> pX = Vector512.Create(a.GetElement(0));
            Vector512<double> pY = Vector512.Create(a.GetElement(1));
            Vector512<double> m1 = V512Helper.Create(mat.M1());
            Vector512<double> m2 = V512Helper.Create(mat.M2());
            Vector512<double> m3 = V512Helper.Create(mat.M3());
            return V512Helper.MulAdd(m1, pX, V512Helper.MulAdd(m2, pY, m3));
        }
    }

    readonly struct ConvertClampReduction(F24Dot8Point origin, F24Dot8Point size) : ISimdReduce<int, double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector64<int> Invoke(Vector128<double> a)
        {
            Vector128<int> i = RoundToInt32(a) - origin.ToVector128();
            return Clamp(i, Vector128<int>.Zero, size.ToVector128()).GetLower();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector128<double> a, Vector128<double> b)
        {
            Vector128<int> i = RoundToInt32(a, b) - origin.ToVector128();
            return Clamp(i, Vector128<int>.Zero, size.ToVector128());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<int> Invoke(Vector256<double> a)
        {
            Vector128<int> i = V256Helper.RoundToInt32(a) - origin.ToVector128();
            return Clamp(i, Vector128<int>.Zero, size.ToVector128());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<int> Invoke(Vector512<double> a)
        {
            Vector256<int> i = V512Helper.RoundToInt32(a) - origin.ToVector256();
            return V256Helper.Clamp(i, Vector256<int>.Zero, size.ToVector256());
        }
    }
}
