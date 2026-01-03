using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F24Dot8Point
{
    public readonly F24Dot8 X;
    public readonly F24Dot8 Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public F24Dot8Point(F24Dot8 x, F24Dot8 y)
    {
        X = x;
        Y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<int> ToVector128()
    {
        return Vector128.Create(Unsafe.BitCast<F24Dot8Point, long>(this)).AsInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector256<int> ToVector256()
    {
        return Vector256.Create(Unsafe.BitCast<F24Dot8Point, long>(this)).AsInt32();
    }

    public override string ToString()
    {
        string separator = NumberFormatInfo.CurrentInfo.NumberGroupSeparator;
        return $"<{X}{separator} {Y}>";
    }

    private string GetDebuggerDisplay()
    {
        string separator = NumberFormatInfo.CurrentInfo.NumberGroupSeparator;
        return $"<{X}{separator} {Y}> (<{X.ToBits()}{separator} {Y.ToBits()}>)";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> ReadAsVector128(ReadOnlySpan<F24Dot8Point> span)
    {
        return MemoryMarshal.Read<Vector128<int>>(MemoryMarshal.AsBytes(span));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8Point operator +(F24Dot8Point a, F24Dot8Point b) => new(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8Point operator >>(F24Dot8Point a, int b) => new(a.X >> b, a.Y >> b);
}
