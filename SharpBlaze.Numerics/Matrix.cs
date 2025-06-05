using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static Utils;
using static V128Helper;

public readonly partial struct Matrix
{
    public static partial Matrix CreateRotation(double degrees)
    {
        if (FuzzyIsZero(degrees))
        {
            return Identity;
        }

        double c = 0;
        double s = 0;

        Vector128<double> dg = Vector128.Create(degrees);
        if (Vector128.EqualsAny(dg, Vector128.Create(90.0, -270.0)))
        {
            s = 1.0;
        }
        else if (Vector128.EqualsAny(dg, Vector128.Create(180.0, -180.0)))
        {
            c = -1.0;
        }
        else if (Vector128.EqualsAny(dg, Vector128.Create(-90.0, 270.0)))
        {
            s = -1.0;
        }
        else
        {
            // Arbitrary rotation.
            double radians = Deg2Rad(degrees);

            (s, c) = Math.SinCos(radians);
        }

        return new Matrix(c, s, -s, c, 0, 0);
    }


    public static partial Matrix Lerp(in Matrix a, in Matrix b, double t)
    {
        Vector128<double> vt = Vector128.Create(t);
        return new Matrix(
            InterpolateLinear(a.m0, b.m0, vt),
            InterpolateLinear(a.m1, b.m1, vt),
            InterpolateLinear(a.m2, b.m2, vt));
    }


    public readonly partial bool Invert(out Matrix result)
    {
        double det = GetDeterminant();

        if (FuzzyIsZero(det))
        {
            result = Identity;
            return false;
        }

        Vector128<double> t1 = Shuffle(m1, m0, 0b10);
        Vector128<double> t2 = Shuffle(m2, m2, 0b01);
        Vector128<double> t3 = Shuffle(m1, m0, 0b01);
        Vector128<double> r2 = t1 * t2 - t3 * m2;

        Vector128<double> r0 = Shuffle(m1, m0, 0b11) ^ Vector128.Create(0, -0.0);
        Vector128<double> r1 = Shuffle(m1, m0, 0b00) ^ Vector128.Create(-0.0, 0);

        result = new Matrix(r0 / det, r1 / det, r2 / det);
        return true;
    }

    public readonly partial Matrix Inverse()
    {
        Invert(out Matrix result);
        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial FloatRect Map(FloatRect rect)
    {
        Vector128<double> min = rect.Min;
        Vector128<double> max = rect.Max;

        Vector128<double> topLeft = Map(min);
        Vector128<double> topRight = Map(Shuffle(max, min, 0b10));
        Vector128<double> botLeft = Map(Shuffle(min, max, 0b10));
        Vector128<double> botRight = Map(max);

        Vector128<double> rMin = MinNative(topLeft, MinNative(topRight, MinNative(botLeft, botRight)));
        Vector128<double> rMax = MaxNative(topLeft, MaxNative(topRight, MaxNative(botLeft, botRight)));

        return new FloatRect(rMin, rMax);
    }


    public readonly partial bool IsEqual(in Matrix matrix)
    {
        Vector128<double> eq0 = FuzzyIsEqual(m0, matrix.m0);
        Vector128<double> eq1 = FuzzyIsEqual(m1, matrix.m1);
        Vector128<double> eq2 = FuzzyIsEqual(m2, matrix.m2);
        return Vector128.EqualsAll((eq0 & eq1 & eq2).AsInt64(), Vector128<long>.AllBitsSet);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix operator *(in Matrix left, in Matrix right)
    {
        Vector128<double> o0 = right.m0;
        Vector128<double> o1 = right.m1;
        Vector128<double> o2 = right.m2;

        Vector128<double> m00 = Vector128.Create(left.m0.GetElement(0));
        Vector128<double> m01 = Vector128.Create(left.m0.GetElement(1));
        Vector128<double> r0 = MulAdd(m00, o0, m01 * o1);

        Vector128<double> m10 = Vector128.Create(left.m1.GetElement(0));
        Vector128<double> m11 = Vector128.Create(left.m1.GetElement(1));
        Vector128<double> r1 = MulAdd(m10, o0, m11 * o1);

        Vector128<double> m20 = Vector128.Create(left.m2.GetElement(0));
        Vector128<double> m21 = Vector128.Create(left.m2.GetElement(1));
        Vector128<double> r2 = MulAdd(m20, o0, MulAdd(m21, o1, o2));

        return new Matrix(r0, r1, r2);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial MatrixComplexity DetermineComplexity()
    {
        // TODO: customizable fuzziness?
        
        Vector128<double> test = Vector128.Create(1.0, 0);
        Vector128<double> f0 = FuzzyNotEqual(m0, test);
        Vector128<double> f1 = FuzzyNotEqual(Vector128.Shuffle(m1, Vector128.Create(1, 0)), test);
        uint m01 = (f0 | f1).ExtractMostSignificantBits();
        bool f2 = FuzzyNotZero(m2);

        uint translation = f2 ? 0b001 : 0u;
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
