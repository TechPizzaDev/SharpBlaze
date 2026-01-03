using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze.Numerics;

public static class ScalarHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MinNative(double a, double b)
    {
#if NET10_0_OR_GREATER
        return double.MinNative(a, b);
#else
        if (Sse2.IsSupported)
        {
            return Sse2.MinScalar(
                Vector128.CreateScalarUnsafe(a),
                Vector128.CreateScalarUnsafe(b)
            ).ToScalar();
        }
        return Math.Min(a, b);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double MaxNative(double a, double b)
    {
#if NET10_0_OR_GREATER
        return double.MaxNative(a, b);
#else
        if (Sse2.IsSupported)
        {
            return Sse2.MaxScalar(
                Vector128.CreateScalarUnsafe(a),
                Vector128.CreateScalarUnsafe(b)
            ).ToScalar();
        }
        return Math.Max(a, b);
#endif
    }

    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Clamp<T>(T val, T min, T max)
        where T : INumber<T>
    {
#if NET10_0_OR_GREATER
        return T.ClampNative(val, min, max);
#elif NET9_0_OR_GREATER
        return T.Clamp(val, min, max);
#else
        return T.Min(T.Max(val, min), max);
#endif
    }

    /**
     * Returns value clamped to range between minimum and maximum values.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ClampNative(double val, double min, double max)
    {
#if NET10_0_OR_GREATER
        return double.ClampNative(val, min, max);
#else
        return MinNative(MaxNative(val, min), max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundToInt32(double v)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ConvertToInt32(Vector128.CreateScalarUnsafe(v));
        }
        
        double r = Math.Round(v);
#if NET9_0_OR_GREATER
        return double.ConvertToIntegerNative<int>(r);
#else
        return (int) r;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T MulAdd<T>(T a, T b, T c)
        where T : INumber<T>
    {
#if NET9_0_OR_GREATER
        return T.MultiplyAddEstimate(a, b, c);
#else
        return a * b + c;
#endif
    }
}
