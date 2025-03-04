using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

public static class Utils
{
    internal const double DBL_EPSILON = 2.2204460492503131e-16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> MinNative(Vector128<double> left, Vector128<double> right)
    {
#if NET9_0_OR_GREATER
        return Vector128.MinNative(left, right);
#else
        if (Sse2.IsSupported)
        {
            return Sse2.Min(left, right);
        }
        return Vector128.Min(left, right);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> MaxNative(Vector128<double> left, Vector128<double> right)
    {
#if NET9_0_OR_GREATER
        return Vector128.MaxNative(left, right);
#else
        if (Sse2.IsSupported)
        {
            return Sse2.Max(left, right);
        }
        return Vector128.Max(left, right);
#endif
    }


    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Clamp<T>(T val, T min, T max)
        where T : INumber<T>
    {
        return T.Max(min, T.Min(val, max));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Clamp<T>(Vector128<T> val, Vector128<T> min, Vector128<T> max)
    {
        return Vector128.Max(min, Vector128.Min(val, max));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> Clamp<T>(Vector256<T> val, Vector256<T> min, Vector256<T> max)
    {
        return Vector256.Max(min, Vector256.Min(val, max));
    }


    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 Clamp(F24Dot8 val, F24Dot8 min, F24Dot8 max)
    {
        return Clamp(val._value, min._value, max._value);
    }


    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TileIndex Clamp(TileIndex val, TileIndex min, TileIndex max)
    {
        return Clamp(val._value, min._value, max._value);
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

        return A + ((B - A) * T.CreateTruncating(t));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> InterpolateLinear(Vector128<double> A, Vector128<double> B, Vector128<double> t)
    {
        Debug.Assert(Vector128.GreaterThanOrEqualAll(t, Vector128.Create(0.0)));
        Debug.Assert(Vector128.LessThanOrEqualAll(t, Vector128.Create(1.0)));

        return A + ((B - A) * t);
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


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe int* ZeroCoverX32()
    {
        return ZeroCoverHelper.mZeroCoverX32;
    }

    private static unsafe class ZeroCoverHelper
    {
        public static readonly int* mZeroCoverX32;

        static ZeroCoverHelper()
        {
            nuint byteCount = sizeof(int) * 32;
            mZeroCoverX32 = (int*) NativeMemory.AlignedAlloc(byteCount, 64);
            NativeMemory.Clear(mZeroCoverX32, byteCount);
        }
    }
}
