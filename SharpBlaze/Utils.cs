using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

public static class Utils
{
    internal const float FLT_EPSILON = 1.1920929e-07F;

    internal const double DBL_EPSILON = 2.2204460492503131e-16;

    public static int BIT_SIZE_OF<T>() => Unsafe.SizeOf<T>() << 3;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Round(double v)
    {
        return Math.Round(v);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Round(float v)
    {
        return MathF.Round(v);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Min<T>(T a, T b)
        where T : INumber<T>
    {
        return T.Min(a, b);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Max<T>(T a, T b)
        where T : INumber<T>
    {
        return T.Max(a, b);
    }


    /**
     * Finds the smallest of the three values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Min3<T>(T a, T b, T c)
        where T : INumber<T>
    {
        return T.Min(a, T.Min(b, c));
    }


    /**
     * Finds the greatest of the three values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Max3<T>(T a, T b, T c)
        where T : INumber<T>
    {
        return T.Max(a, T.Max(b, c));
    }


    /**
     * Rounds-up a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Ceil(float v)
    {
        return MathF.Ceiling(v);
    }


    /**
     * Rounds-up a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Ceil(double v)
    {
        return Math.Ceiling(v);
    }


    /**
     * Rounds-down a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Floor(float v)
    {
        return MathF.Floor(v);
    }


    /**
     * Rounds-down a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Floor(double v)
    {
        return Math.Floor(v);
    }


    /**
     * Returns square root of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sqrt(float v)
    {
        return MathF.Sqrt(v);
    }


    /**
     * Returns square root of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqrt(double v)
    {
        return Math.Sqrt(v);
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
        return Clamp((int) val, (int) min, (int) max);
    }


    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TileIndex Clamp(TileIndex val, TileIndex min, TileIndex max)
    {
        return Clamp((uint) val, (uint) min, (uint) max);
    }


    /**
     * Returns absolute of a given value.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Abs<T>(T t)
        where T : INumber<T>
    {
        return t >= T.Zero ? t : -t;
    }


    /**
     * Returns true if a given floating point value is not a number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(float x)
    {
        return float.IsNaN(x);
    }


    /**
     * Returns true if a given double precision floating point value is not a
     * number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNaN(double x)
    {
        return double.IsNaN(x);
    }


    /**
     * Returns true if a given double precision floating point number is finite.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool DoubleIsFinite(double x)
    {
        // 0 × finite → 0
        // 0 × infinity → NaN
        // 0 × NaN → NaN
        double p = x * 0;

        return !IsNaN(p);
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
     * Returns true if two given numbers are considered equal.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyIsEqual(float a, float b)
    {
        return (MathF.Abs(a - b) < FLT_EPSILON);
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
     * Returns true if two given numbers are not considered equal.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyNotEqual(float a, float b)
    {
        return (MathF.Abs(a - b) >= FLT_EPSILON);
    }


    /**
     * Returns true if a number can be considered being equal to zero.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyIsZero(float f)
    {
        return MathF.Abs(f) < FLT_EPSILON;
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
     * Returns true if a number can not be considered being equal to zero.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FuzzyNotZero(float f)
    {
        return MathF.Abs(f) >= FLT_EPSILON;
    }


    /**
     * Finds the greatest of the four values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Max4<T>(T a, T b, T c, T d)
        where T : INumber<T>
    {
        return T.Max(a, T.Max(b, T.Max(c, d)));
    }


    /**
     * Finds the smallest of the four values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Min4<T>(T a, T b, T c, T d)
        where T : INumber<T>
    {
        return T.Min(a, T.Min(b, T.Min(c, d)));
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


    /**
     * Calculates sine of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(float v)
    {
        return MathF.Sin(v);
    }


    /**
     * Calculates sine of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sin(double v)
    {
        return Math.Sin(v);
    }


    /**
     * Calculate cosine of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(float v)
    {
        return MathF.Cos(v);
    }


    /**
     * Calculate cosine of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Cos(double v)
    {
        return Math.Cos(v);
    }


    /**
     * Returns tangent of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Tan(float v)
    {
        return MathF.Tan(v);
    }


    /**
     * Returns tangent of a given number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Tan(double v)
    {
        return Math.Tan(v);
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