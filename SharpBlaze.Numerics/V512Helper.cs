using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze.Numerics;

public static class V512Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> Clamp<T>(Vector512<T> value, Vector512<T> min, Vector512<T> max)
    {
#if NET9_0_OR_GREATER
        return Vector512.ClampNative(value, min, max);
#else
        return Vector512.Min(Vector512.Max(value, min), max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<double> Create(Vector128<double> value)
    {
        if (Avx512F.IsSupported)
        {
            Vector512<double> v512 = value.ToVector256Unsafe().ToVector512Unsafe();
            return Avx512F.Shuffle4x128(v512, v512, 0);
        }

#if NET9_0_OR_GREATER
        return Vector512.Create(value);
#else
        Vector256<double> v256 = Vector256.Create(value, value);
        return Vector512.Create(v256, v256);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<double> MulAdd(Vector512<double> a, Vector512<double> b, Vector512<double> c)
    {
#if NET9_0_OR_GREATER
        return Vector512.MultiplyAddEstimate(a, b, c);
#else
        return a * b + c;
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<double> Round(Vector512<double> value)
    {
#if NET9_0_OR_GREATER
        return Vector512.Round(value);
#else
        return Vector512.Create(V256Helper.Round(value.GetLower()), V256Helper.Round(value.GetUpper()));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> RoundToInt32(Vector512<double> value)
    {
        if (Avx512F.IsSupported)
        {
            return Avx512F.ConvertToVector256Int32(value);
        }

        Vector512<long> r = RoundToInt64(value);
        return Vector256.Narrow(r.GetLower(), r.GetUpper());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<long> RoundToInt64(Vector512<double> value)
    {
#if NET9_0_OR_GREATER
        return Vector512.ConvertToInt64Native(Vector512.Round(value));
#else
        return Vector512.ConvertToInt64(Round(value));
#endif
    }
}
