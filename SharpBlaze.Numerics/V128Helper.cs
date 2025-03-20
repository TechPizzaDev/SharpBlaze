using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze.Numerics;

public static class V128Helper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Vector128<double> Lower, Vector128<double> Upper) Widen(Vector128<int> value)
    {
        Vector128<double> lo, hi;
        if (Sse2.IsSupported)
        {
            lo = Sse2.ConvertToVector128Double(value);
            Vector128<float> tmp = value.AsSingle();
            hi = Sse2.ConvertToVector128Double(Sse.MoveHighToLow(tmp, tmp).AsInt32());
        }
        else
        {
            lo = Vector128.ConvertToDouble(Vector128.WidenLower(value));
            hi = Vector128.ConvertToDouble(Vector128.WidenUpper(value));
        }
        return (lo, hi);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> WidenLower(Vector128<int> value)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.ConvertToVector128Double(value);
        }
        return Vector128.ConvertToDouble(Vector128.WidenLower(value));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> WidenUpper(Vector128<int> value)
    {
        if (Sse2.IsSupported)
        {
            Vector128<float> tmp = value.AsSingle();
            return Sse2.ConvertToVector128Double(Sse.MoveHighToLow(tmp, tmp).AsInt32());
        }
        return Vector128.ConvertToDouble(Vector128.WidenUpper(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Narrow(Vector128<double> p0, Vector128<double> p1)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            Vector128<long> r0 = AdvSimd.Arm64.ConvertToInt64RoundToEven(p0);
            Vector128<long> r1 = AdvSimd.Arm64.ConvertToInt64RoundToEven(p1);
            return AdvSimd.ExtractNarrowingUpper(AdvSimd.ExtractNarrowingLower(r0), r1);
        }
        else if (Sse2.IsSupported)
        {
            Vector128<long> r0 = Sse2.ConvertToVector128Int32(p0).AsInt64();
            Vector128<long> r1 = Sse2.ConvertToVector128Int32(p1).AsInt64();
            return Sse2.UnpackLow(r0, r1).AsInt32();
        }

        // Cannot narrow to f32 directly or we lose precision.
        Vector128<long> n0, n1;
#if NET9_0_OR_GREATER
        n0 = Vector128.ConvertToInt64Native(p0);
        n1 = Vector128.ConvertToInt64Native(p1);
#else
        n0 = Vector128.ConvertToInt64(p0);
        n1 = Vector128.ConvertToInt64(p1);
#endif
        return Vector128.Narrow(n0, n1);
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
}
