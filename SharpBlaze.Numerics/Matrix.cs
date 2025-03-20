using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static Utils;
using static V128Helper;

public readonly partial struct Matrix
{
    /**
     * Constructs translation matrix with given position.
     */
    public static explicit operator Matrix(FloatPoint translation)
    {
        Vector128<double> r0 = Vector128.Create(1.0, 0);
        Vector128<double> r1 = Vector128.Create(0.0, 1);
        Vector128<double> r2 = Vector128.Create(translation.X, translation.Y);
        return new Matrix(r0, r1, r2);
    }


    public static partial Matrix CreateRotation(double degrees)
    {
        if (FuzzyIsZero(degrees))
        {
            return Identity;
        }

        double c = 0;
        double s = 0;

        if (degrees == 90.0 || degrees == -270.0)
        {
            s = 1;
        }
        else if (degrees == 180.0 || degrees == -180.0)
        {
            c = -1;
        }
        else if (degrees == -90.0 || degrees == 270.0)
        {
            s = -1;
        }
        else
        {
            // Arbitrary rotation.
            double radians = Deg2Rad(degrees);

            (s, c) = Math.SinCos(radians);
        }

        return new Matrix(c, s, -s, c, 0, 0);
    }


    public static partial Matrix Lerp(in Matrix matrix1, in Matrix matrix2,
        double t)
    {
        return new Matrix(
            matrix1.m[0][0] + (matrix2.m[0][0] - matrix1.m[0][0]) * t,
            matrix1.m[0][1] + (matrix2.m[0][1] - matrix1.m[0][1]) * t,
            matrix1.m[1][0] + (matrix2.m[1][0] - matrix1.m[1][0]) * t,
            matrix1.m[1][1] + (matrix2.m[1][1] - matrix1.m[1][1]) * t,
            matrix1.m[2][0] + (matrix2.m[2][0] - matrix1.m[2][0]) * t,
            matrix1.m[2][1] + (matrix2.m[2][1] - matrix1.m[2][1]) * t);
    }


    public readonly partial bool Invert(out Matrix result)
    {
        double det = GetDeterminant();

        if (FuzzyIsZero(det))
        {
            result = Identity;
            return false;
        }

        result = new Matrix(
             m[1][1] / det,
            -m[0][1] / det,
            -m[1][0] / det,
             m[0][0] / det,
            (m[1][0] * m[2][1] - m[1][1] * m[2][0]) / det,
            (m[0][1] * m[2][0] - m[0][0] * m[2][1]) / det);

        return true;
    }


    public readonly partial Matrix Inverse()
    {
        Invert(out Matrix result);
        return result;
    }


    public readonly partial FloatRect Map(in FloatRect rect)
    {
        Vector128<double> topLeft = Map(rect.Min).AsVector128();
        Vector128<double> topRight = Map(rect.Max.X, rect.Min.Y).AsVector128();
        Vector128<double> botLeft = Map(rect.Min.X, rect.Max.Y).AsVector128();
        Vector128<double> botRight = Map(rect.Max).AsVector128();

        Vector128<double> min = MinNative(topLeft, MinNative(topRight, MinNative(botLeft, botRight)));
        Vector128<double> max = MaxNative(topLeft, MaxNative(topRight, MaxNative(botLeft, botRight)));

        return new FloatRect(new FloatPoint(min), new FloatPoint(max));
    }


    public readonly partial IntRect MapBoundingRect(in IntRect rect)
    {
        FloatRect r = Map(new FloatRect(rect));

        return r.ToExpandedIntRect();
    }


    public readonly partial bool IsIdentity()
    {
        // Look at diagonal elements first to return if scale is not 1.
        return
            Vector128.EqualsAll(m[0], Vector128.Create(1.0, 0)) &&
            Vector128.EqualsAll(m[1], Vector128.Create(0, 1.0)) &&
            Vector128.EqualsAll(m[2], Vector128<double>.Zero);
    }


    public readonly partial bool IsEqual(in Matrix matrix)
    {
        Vector128<double> eq0 = FuzzyIsEqual(m[0], matrix.m[0]);
        Vector128<double> eq1 = FuzzyIsEqual(m[1], matrix.m[1]);
        Vector128<double> eq2 = FuzzyIsEqual(m[2], matrix.m[2]);
        return Vector128.EqualsAll((eq0 & eq1 & eq2).AsInt64(), Vector128<long>.AllBitsSet);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix operator *(in Matrix left, in Matrix right)
    {
        Vector128<double> o0 = right.m[0];
        Vector128<double> o1 = right.m[1];
        Vector128<double> o2 = right.m[2];

        Vector128<double> m00 = Vector128.Shuffle(left.m[0], Vector128.Create(0L));
        Vector128<double> m01 = Vector128.Shuffle(left.m[0], Vector128.Create(1L));
        Vector128<double> r0 = m00 * o0 + m01 * o1;

        Vector128<double> m10 = Vector128.Shuffle(left.m[1], Vector128.Create(0L));
        Vector128<double> m11 = Vector128.Shuffle(left.m[1], Vector128.Create(1L));
        Vector128<double> r1 = m10 * o0 + m11 * o1;

        Vector128<double> m20 = Vector128.Shuffle(left.m[2], Vector128.Create(0L));
        Vector128<double> m21 = Vector128.Shuffle(left.m[2], Vector128.Create(1L));
        Vector128<double> r2 = m20 * o0 + m21 * o1 + o2;

        return new Matrix(r0, r1, r2);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial MatrixComplexity DetermineComplexity()
    {
        Vector128<double> m0 = FuzzyNotEqual(m[0], Vector128.Create(1.0, 0));
        Vector128<double> m1 = FuzzyNotEqual(m[1], Vector128.Create(0, 1.0));
        uint m01 = (m0 | Vector128.Shuffle(m1, Vector128.Create(1, 0))).ExtractMostSignificantBits();
        bool m2 = FuzzyNotZero(m[2]);

        uint translation = m2 ? 0b001 : 0u;
        uint scale = (m01 & 0b001) << 1;
        uint complex = (m01 & 0b010) << 1;

        // Identity = 0b000,
        // Translation = 0b001,
        // Scale = 0b010,
        // TranslationScale = 0b011,
        // Complex = 0b1XX,
        uint mask = translation | scale | complex;
        return (MatrixComplexity) Math.Min(mask, (uint) MatrixComplexity.Complex);
    }
}
