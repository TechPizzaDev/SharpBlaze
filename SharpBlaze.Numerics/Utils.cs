using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public static class Utils
{
    internal const double DBL_EPSILON = 2.2204460492503131e-16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> ClampNative<T>(Vector256<T> val, Vector256<T> min, Vector256<T> max)
    {
#if NET9_0_OR_GREATER
        return Vector256.ClampNative(val, min, max);
#else
        return Vector256.Min(Vector256.Max(val, min), max);
#endif
    }


    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 Clamp(F24Dot8 val, F24Dot8 min, F24Dot8 max)
    {
        return F24Dot8.FromBits(ScalarHelper.Clamp(val._value, min._value, max._value));
    }


    /**
     * Linearly interpolate between A and B.
     * If t is 0, returns A.
     * If t is 1, returns B.
     * If t is something else, returns value linearly interpolated between A and B.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InterpolateLinear<T, V>(T A, T B, V t)
        where T : INumber<T>
        where V : INumber<V>
    {
        Debug.Assert(t >= V.Zero);
        Debug.Assert(t <= V.One);

        return ScalarHelper.MulAdd(B - A, T.CreateTruncating(t), A);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> InterpolateLinear(Vector128<double> A, Vector128<double> B, Vector128<double> t)
    {
        Debug.Assert(Vector128.GreaterThanOrEqualAll(t, Vector128.Create(0.0)));
        Debug.Assert(Vector128.LessThanOrEqualAll(t, Vector128.Create(1.0)));

        return V128Helper.MulAdd(B - A, t, A);
    }


    /**
     * Returns true if two given numbers are considered equal.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyIsEqual(double a, double b)
    {
        return (Math.Abs(a - b) < DBL_EPSILON);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> FuzzyIsEqual(Vector128<double> a, Vector128<double> b)
    {
        return Vector128.LessThan(Vector128.Abs(a - b), Vector128.Create(DBL_EPSILON));
    }


    /**
     * Returns true if a number can be considered being equal to zero.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyIsZero(double d)
    {
        return Math.Abs(d) < DBL_EPSILON;
    }


    /**
     * Returns true if two given numbers are not considered equal.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyNotEqual(double a, double b)
    {
        return (Math.Abs(a - b) >= DBL_EPSILON);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> FuzzyNotEqual(Vector128<double> a, Vector128<double> b)
    {
        return Vector128.GreaterThanOrEqual(Vector128.Abs(a - b), Vector128.Create(DBL_EPSILON));
    }


    /**
     * Returns true if a number can not be considered being equal to zero.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyNotZero(double d)
    {
        return Math.Abs(d) >= DBL_EPSILON;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyNotZero(Vector128<double> d)
    {
        return Vector128.GreaterThanOrEqualAll(Vector128.Abs(d), Vector128.Create(DBL_EPSILON));
    }


    /**
     * Convert degrees to radians.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Deg2Rad(double x)
    {
        // pi / 180
        return x * 0.01745329251994329576923690768489;
    }


    /**
     * Convert radians to degrees.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Rad2Deg(double x)
    {
        // 180 / pi.
        return x * 57.295779513082320876798154814105;
    }
}
