using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze.Numerics;

public static class V128Helper
{
    private const ulong PositiveInfinityBits = 0x7FF0_0000_0000_0000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFiniteAll(Vector128<double> value)
    {
        Vector128<ulong> bits = value.AsUInt64();
        Vector128<ulong> top = ~bits & Vector128.Create(PositiveInfinityBits);
        return !Vector128.EqualsAll(top, Vector128<ulong>.Zero);
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> Clamp<T>(Vector128<T> val, Vector128<T> min, Vector128<T> max)
    {
#if NET9_0_OR_GREATER
        return Vector128.Clamp(val, min, max);
#else
        return Vector128.Min(Vector128.Max(val, min), max);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> Round(Vector128<double> value)
    {
#if NET9_0_OR_GREATER
        return Vector128.Round(value);
#else
        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.RoundToNearest(value);
        }
        if (PackedSimd.IsSupported)
        {
            return PackedSimd.RoundToNearest(value);
        }
        
        const double IntBoundary = 4503599627370496.0; // 2^52
        Vector128<double> t = CopySign(Vector128.Create(IntBoundary), value);
        return CopySign((value + t) - t, value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<long> RoundToInt64(Vector128<double> value)
    {
        if (Avx512DQ.VL.IsSupported)
        {
            return Avx512DQ.VL.ConvertToVector128Int64(value);
        }
        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.ConvertToInt64RoundToEven(value);
        }
        
#if NET9_0_OR_GREATER
        return Vector128.ConvertToInt64Native(Vector128.Round(value));
#else
        return Vector128.ConvertToInt64(Round(value));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> CopySign(Vector128<double> value, Vector128<double> sign)
    {
#if NET9_0_OR_GREATER
        return Vector128.CopySign(value, sign);
#else
        return Vector128.ConditionalSelect(Vector128.Create(-0.0), sign, value);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<double> CopySign(Vector256<double> value, Vector256<double> sign)
    {
#if NET9_0_OR_GREATER
        return Vector256.CopySign(value, sign);
#else
        return Vector256.ConditionalSelect(Vector256.Create(-0.0), sign, value);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> RoundToInt32(Vector128<double> value)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ConvertToVector128Int32(value);
        }
        if (PackedSimd.IsSupported)
        {
            return PackedSimd.ConvertToInt32Saturate(PackedSimd.RoundToNearest(value));
        }

        Vector128<long> r = RoundToInt64(value);
        return Vector128.Narrow(r, r);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> RoundToInt32(Vector128<double> p0, Vector128<double> p1)
    {
        if (Sse2.IsSupported)
        {
            Vector128<int> n0 = Sse2.ConvertToVector128Int32(p0);
            Vector128<int> n1 = Sse2.ConvertToVector128Int32(p1);
            return Sse2.UnpackLow(n0.AsInt64(), n1.AsInt64()).AsInt32();
        }
        if (PackedSimd.IsSupported)
        {
            Vector128<int> n0 = PackedSimd.ConvertToInt32Saturate(PackedSimd.RoundToNearest(p0));
            Vector128<int> n1 = PackedSimd.ConvertToInt32Saturate(PackedSimd.RoundToNearest(p1));
            return n0.AsInt64().WithElement(1, n1.AsInt64().ToScalar()).AsInt32();
        }

        // Cannot narrow f64 to f32 due to precision loss, so convert to i64 first.
        Vector128<long> r0 = RoundToInt64(p0);
        Vector128<long> r1 = RoundToInt64(p1);
        return Vector128.Narrow(r0, r1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> MulAdd(Vector128<double> a, Vector128<double> b, Vector128<double> c)
    {
#if NET9_0_OR_GREATER
        return Vector128.MultiplyAddEstimate(a, b, c);
#else
        return a * b + c;
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
    public static (Vector128<double> Lower, Vector128<double> Upper) ConvertToDouble(Vector128<int> value)
    {
        return (ConvertToDoubleLower(value), ConvertToDoubleUpper(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> ConvertToDoubleLower(Vector128<int> value)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ConvertToVector128Double(value);
        }
        return Vector128.ConvertToDouble(Vector128.WidenLower(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> ConvertToDoubleUpper(Vector128<int> value)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ConvertToVector128Double(Sse2.ShiftRightLogical128BitLane(value, 8));
        }
        return Vector128.ConvertToDouble(Vector128.WidenUpper(value));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> Shuffle(Vector128<double> a, Vector128<double> b, [ConstantExpected] byte control)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.Shuffle(a, b, control);
        }
        return Vector128.Create(
            a.GetElement(control & 0b01),
            b.GetElement((control & 0b10) >> 1));
    }
}
