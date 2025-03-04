using System.Runtime.Intrinsics;

namespace SharpBlaze;

using static Utils;
using static F24Dot8;

public static unsafe partial class SIMD
{
    public static void FloatPointsToF24Dot8Points(
        in Matrix matrix,
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        MatrixComplexity complexity = matrix.DetermineComplexity();

        switch (complexity)
        {
            case MatrixComplexity.Identity:
            {
                ConvertIdentity(dst, src, count, origin, size);
                break;
            }

            case MatrixComplexity.TranslationOnly:
            {
                ConvertTranslationOnly(matrix, dst, src, count, origin, size);
                break;
            }

            case MatrixComplexity.ScaleOnly:
            {
                ConvertScaleOnly(matrix, dst, src, count, origin, size);
                break;
            }

            case MatrixComplexity.TranslationScale:
            {
                ConvertTranslationScale(matrix, dst, src, count, origin, size);
                break;
            }

            case MatrixComplexity.Complex:
            {
                ConvertComplex(matrix, dst, src, count, origin, size);
                break;
            }
        }
    }

    private static void ConvertIdentity(
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        // Identity matrix, convert only.
        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            for (; i + 1 < count; i += 2)
            {
                Vector128<double> src0 = src[i + 0].AsVector128();
                Vector128<double> src1 = src[i + 1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.FromFloatToV128(src0, src1);
                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.Store((int*) (dst + i));
            }
        }

        for (; i < count; i++)
        {
            dst[i].X = Clamp(DoubleToF24Dot8(
                src[i].X) - origin.X, 0, size.X);

            dst[i].Y = Clamp(DoubleToF24Dot8(
                src[i].Y) - origin.Y, 0, size.Y);
        }
    }

    private static void ConvertTranslationOnly(
        in Matrix matrix,
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        // Translation only matrix.
        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<double> t = matrix.M3();
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            for (; i + 1 < count; i += 2)
            {
                Vector128<double> src0 = src[i + 0].AsVector128();
                Vector128<double> src1 = src[i + 1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.FromFloatToV128(src0 + t, src1 + t);
                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.Store((int*) (dst + i));
            }
        }

        double tx = matrix.M31();
        double ty = matrix.M32();

        for (; i < count; i++)
        {
            dst[i].X = Clamp(DoubleToF24Dot8(
                src[i].X + tx) - origin.X, 0, size.X);

            dst[i].Y = Clamp(DoubleToF24Dot8(
                src[i].Y + ty) - origin.Y, 0, size.Y);
        }
    }

    private static void ConvertScaleOnly(
        in Matrix matrix,
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        // Scale only matrix.
        double sx = matrix.M11() * 256.0;
        double sy = matrix.M22() * 256.0;

        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();
            Vector128<double> s = Vector128.Create(sx, sy);

            for (; i + 1 < count; i += 2)
            {
                Vector128<double> src0 = src[i + 0].AsVector128();
                Vector128<double> src1 = src[i + 1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.ConvertToInt32(src0 * s, src1 * s);
                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.Store((int*) (dst + i));
            }
        }

        for (; i < count; i++)
        {
            dst[i].X = Clamp(ConvertToInt32(
                src[i].X * sx) - origin.X, 0, size.X);

            dst[i].Y = Clamp(ConvertToInt32(
                src[i].Y * sy) - origin.Y, 0, size.Y);
        }
    }

    private static void ConvertTranslationScale(
        in Matrix matrix,
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        // Scale and translation matrix.
        Matrix m = matrix;
        m *= Matrix.CreateScale(256.0);

        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<double> s = Vector128.Create(m.M11(), m.M22());
            Vector128<double> t = m.M3();
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            for (; i + 1 < count; i += 2)
            {
                Vector128<double> src0 = src[i + 0].AsVector128();
                Vector128<double> src1 = src[i + 1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.ConvertToInt32((src0 * s) + t, (src1 * s) + t);
                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.Store((int*) (dst + i));
            }
        }

        double tx = m.M31();
        double ty = m.M32();
        double sx = m.M11();
        double sy = m.M22();

        for (; i < count; i++)
        {
            dst[i].X = Clamp(ConvertToInt32(
                (src[i].X * sx) + tx) - origin.X, 0, size.X);

            dst[i].Y = Clamp(ConvertToInt32(
                (src[i].Y * sy) + ty) - origin.Y, 0, size.Y);
        }
    }

    private static void ConvertComplex(
        in Matrix matrix,
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        Matrix m = matrix;
        m *= Matrix.CreateScale(256.0);

        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<double> f0 = Vector128.Create(m.M11(), m.M21());
            Vector128<double> f1 = Vector128.Create(m.M12(), m.M22());
            Vector128<double> t = m.M3();
            Vector128<int> vOrigin = origin.ToVector128();
            Vector128<int> vSize = size.ToVector128();

            for (; i + 1 < count; i += 2)
            {
                Vector128<double> src0 = src[i + 0].AsVector128();
                Vector128<double> src1 = src[i + 1].AsVector128();

                Vector128<int> val = F24Dot8PointX2.ConvertToInt32(
                    (src0 * f0) + t,
                    (src1 * f1) + t);

                Vector128<int> clamped = Clamp(val - vOrigin, Vector128<int>.Zero, vSize);
                clamped.Store((int*) (dst + i));
            }
        }

        double m00 = m.M11();
        double m01 = m.M12();
        double m10 = m.M21();
        double m11 = m.M22();
        double m20 = m.M31();
        double m21 = m.M32();
        
        for (; i < count; i++)
        {
            double x = src[i].X;
            double y = src[i].Y;

            dst[i].X = Clamp(ConvertToInt32(m00 * x + m10 * y + m20) - origin.X, 0, size.X);
            dst[i].Y = Clamp(ConvertToInt32(m01 * x + m11 * y + m21) - origin.Y, 0, size.Y);
        }
    }
}