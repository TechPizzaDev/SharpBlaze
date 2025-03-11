using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

using static Utils;
using static F24Dot8;

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

        switch (complexity)
        {
            case MatrixComplexity.Identity:
            {
                ConvertIdentity(castDst, src, origin, size);
                break;
            }

            case MatrixComplexity.TranslationOnly:
            {
                ConvertTranslationOnly(matrix, castDst, src, origin, size);
                break;
            }

            case MatrixComplexity.ScaleOnly:
            {
                ConvertScaleOnly(matrix, castDst, src, origin, size);
                break;
            }

            case MatrixComplexity.TranslationScale:
            {
                ConvertTranslationScale(matrix, castDst, src, origin, size);
                break;
            }

            case MatrixComplexity.Complex:
            {
                ConvertComplex(matrix, castDst, src, origin, size);
                break;
            }
        }
    }

    private static void ConvertIdentity(
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        // Identity matrix, convert only.
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            while (src.Length >= 2 && dst.Length >= 4)
            {
                Vector128<double> src0 = src[0].AsVector128();
                Vector128<double> src1 = src[1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.FromFloatToV128(src0, src1);
                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.CopyTo(dst);

                src = src[2..];
                dst = dst[4..];
            }
        }
        
        while (src.Length >= 1 && dst.Length >= 2)
        {
            dst[0] = Clamp(DoubleToF24Dot8(src[0].X) - origin.X, 0, size.X);
            dst[1] = Clamp(DoubleToF24Dot8(src[0].Y) - origin.Y, 0, size.Y);

            src = src[1..];
            dst = dst[2..];
        }
    }

    private static void ConvertTranslationOnly(
        in Matrix matrix,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        // Translation only matrix.
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<double> t = matrix.M3();
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            while (src.Length >= 2 && dst.Length >= 4)
            {
                Vector128<double> src0 = src[0].AsVector128();
                Vector128<double> src1 = src[1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.FromFloatToV128(src0 + t, src1 + t);

                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.CopyTo(dst);

                src = src[2..];
                dst = dst[4..];
            }
        }

        double tx = matrix.M31();
        double ty = matrix.M32();
        
        while (src.Length >= 1 && dst.Length >= 2)
        {
            dst[0] = Clamp(DoubleToF24Dot8(src[0].X + tx) - origin.X, 0, size.X);
            dst[1] = Clamp(DoubleToF24Dot8(src[0].Y + ty) - origin.Y, 0, size.Y);

            src = src[1..];
            dst = dst[2..];
        }
    }

    private static void ConvertScaleOnly(
        in Matrix matrix,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        // Scale only matrix.
        double sx = matrix.M11() * 256.0;
        double sy = matrix.M22() * 256.0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();
            Vector128<double> s = Vector128.Create(sx, sy);

            while (src.Length >= 2 && dst.Length >= 4)
            {
                Vector128<double> src0 = src[0].AsVector128();
                Vector128<double> src1 = src[1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.ConvertToInt32(src0 * s, src1 * s);

                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.CopyTo(dst);

                src = src[2..];
                dst = dst[4..];
            }
        }
        
        while (src.Length >= 1 && dst.Length >= 2)
        {
            dst[0] = Clamp(ConvertToInt32(src[0].X * sx) - origin.X, 0, size.X);
            dst[1] = Clamp(ConvertToInt32(src[0].Y * sy) - origin.Y, 0, size.Y);
            
            src = src[1..];
            dst = dst[2..];
        }
    }

    private static void ConvertTranslationScale(
        in Matrix matrix,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        // Scale and translation matrix.
        Matrix m = matrix * Matrix.CreateScale(256.0);

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<double> s = Vector128.Create(m.M11(), m.M22());
            Vector128<double> t = m.M3();
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            while (src.Length >= 2 && dst.Length >= 4)
            {
                Vector128<double> src0 = src[0].AsVector128();
                Vector128<double> src1 = src[1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.ConvertToInt32(
                    (src0 * s) + t,
                    (src1 * s) + t);

                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.CopyTo(dst);

                src = src[2..];
                dst = dst[4..];
            }
        }

        double tx = m.M31();
        double ty = m.M32();
        double sx = m.M11();
        double sy = m.M22();
        
        while (src.Length >= 1 && dst.Length >= 2)
        {
            dst[0] = Clamp(ConvertToInt32((src[0].X * sx) + tx) - origin.X, 0, size.X);
            dst[1] = Clamp(ConvertToInt32((src[0].Y * sy) + ty) - origin.Y, 0, size.Y);

            src = src[1..];
            dst = dst[2..];
        }
    }

    private static void ConvertComplex(
        in Matrix matrix,
        Span<int> dst,
        ReadOnlySpan<FloatPoint> src,
        F24Dot8Point origin,
        F24Dot8Point size)
    {
        Matrix m = matrix * Matrix.CreateScale(256.0);

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<double> f0 = Vector128.Create(m.M11(), m.M21());
            Vector128<double> f1 = Vector128.Create(m.M12(), m.M22());
            Vector128<double> t = m.M3();
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            while (src.Length >= 2 && dst.Length >= 4)
            {
                Vector128<double> src0 = src[0].AsVector128();
                Vector128<double> src1 = src[1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.ConvertToInt32(
                    (src0 * f0) + t,
                    (src1 * f1) + t);

                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.CopyTo(dst);

                src = src[2..];
                dst = dst[4..];
            }
        }

        double m00 = m.M11();
        double m01 = m.M12();
        double m10 = m.M21();
        double m11 = m.M22();
        double m20 = m.M31();
        double m21 = m.M32();

        while (src.Length >= 1 && dst.Length >= 2)
        {
            double x = src[0].X;
            double y = src[0].Y;

            dst[0] = Clamp(ConvertToInt32(m00 * x + m10 * y + m20) - origin.X, 0, size.X);
            dst[1] = Clamp(ConvertToInt32(m01 * x + m11 * y + m21) - origin.Y, 0, size.Y);
            
            src = src[1..];
            dst = dst[2..];
        }
    }
}