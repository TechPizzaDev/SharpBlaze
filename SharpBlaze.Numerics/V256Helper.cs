using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze.Numerics;

public static class V256Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> Clamp<T>(Vector256<T> value, Vector256<T> min, Vector256<T> max)
    {
#if NET9_0_OR_GREATER
        return Vector256.ClampNative(value, min, max);
#else
        return Vector256.Min(Vector256.Max(value, min), max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<double> Create(Vector128<double> value)
    {
#if NET9_0_OR_GREATER
        return Vector256.Create(value);
#else
        return Vector256.Create(value, value);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<double> MulAdd(Vector256<double> a, Vector256<double> b, Vector256<double> c)
    {
#if NET9_0_OR_GREATER
        return Vector256.MultiplyAddEstimate(a, b, c);
#else
        return a * b + c;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<double> Round(Vector256<double> value)
    {
#if NET9_0_OR_GREATER
        return Vector256.Round(value);
#else
        return Vector256.Create(V128Helper.Round(value.GetLower()), V128Helper.Round(value.GetUpper()));
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> RoundToInt32(Vector256<double> value)
    {
        if (Avx.IsSupported)
        {
            return Avx.ConvertToVector128Int32(value);
        }

        Vector256<long> r = RoundToInt64(value);
        return Vector128.Narrow(r.GetLower(), r.GetUpper());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<long> RoundToInt64(Vector256<double> value)
    {
#if NET9_0_OR_GREATER
        return Vector256.ConvertToInt64Native(Vector256.Round(value));
#else
        return Vector256.ConvertToInt64(Round(value));
#endif
    }
}
