using System.Runtime.CompilerServices;

namespace SharpBlaze;

using static Utils;
using static F24Dot8;

public static partial class SIMD
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 RoundTo24Dot8(double v)
    {
        return (F24Dot8) (int) (Round(v));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void FloatPointsToF24Dot8Points(in Matrix matrix,
        F24Dot8Point* dst, FloatPoint* src, int count,
        F24Dot8Point origin, F24Dot8Point size)
    {
        MatrixComplexity complexity = matrix.DetermineComplexity();

        switch (complexity)
        {
            case MatrixComplexity.Identity:
            {
                // Identity matrix, convert only.
                for (int i = 0; i < count; i++)
                {
                    dst[i].X = Clamp(DoubleToF24Dot8(
                        src[i].X) - origin.X, 0, size.X);

                    dst[i].Y = Clamp(DoubleToF24Dot8(
                        src[i].Y) - origin.Y, 0, size.Y);
                }

                break;
            }

            case MatrixComplexity.TranslationOnly:
            {
                // Translation only matrix.
                double tx = matrix.M31();
                double ty = matrix.M32();

                for (int i = 0; i < count; i++)
                {
                    dst[i].X = Clamp(DoubleToF24Dot8(
                        src[i].X + tx) - origin.X, 0, size.X);

                    dst[i].Y = Clamp(DoubleToF24Dot8(
                        src[i].Y + ty) - origin.Y, 0, size.Y);
                }

                break;
            }

            case MatrixComplexity.ScaleOnly:
            {
                // Scale only matrix.
                double sx = matrix.M11() * 256.0;
                double sy = matrix.M22() * 256.0;

                for (int i = 0; i < count; i++)
                {
                    dst[i].X = Clamp(RoundTo24Dot8(
                        src[i].X * sx) - origin.X, 0, size.X);

                    dst[i].Y = Clamp(RoundTo24Dot8(
                        src[i].Y * sy) - origin.Y, 0, size.Y);
                }

                break;
            }

            case MatrixComplexity.TranslationScale:
            {
                // Scale and translation matrix.
                Matrix m = new(matrix);

                m.PreScale(256.0, 256.0);

                double tx = m.M31();
                double ty = m.M32();
                double sx = m.M11();
                double sy = m.M22();

                for (int i = 0; i < count; i++)
                {
                    dst[i].X = Clamp(RoundTo24Dot8(
                        (src[i].X * sx) + tx) - origin.X, 0, size.X);

                    dst[i].Y = Clamp(RoundTo24Dot8(
                        (src[i].Y * sy) + ty) - origin.Y, 0, size.Y);
                }

                break;
            }

            case MatrixComplexity.Complex:
            {
                Matrix m = new(matrix);

                m.PreScale(256.0, 256.0);

                double m00 = m.M11();
                double m01 = m.M12();
                double m10 = m.M21();
                double m11 = m.M22();
                double m20 = m.M31();
                double m21 = m.M32();

                for (int i = 0; i < count; i++)
                {
                    double x = src[i].X;
                    double y = src[i].Y;

                    dst[i].X = Clamp(RoundTo24Dot8(
                        m00 * x + m10 * y + m20) - origin.X, 0, size.X);

                    dst[i].Y = Clamp(RoundTo24Dot8(
                        m01 * x + m11 * y + m21) - origin.Y, 0, size.Y);
                }

                break;
            }
        }
    }

}