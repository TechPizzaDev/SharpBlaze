using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

/**
 * 8.8 fixed point number.
 */
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly partial struct F8Dot8
{
    private readonly short _value;

    internal F8Dot8(short value) => _value = value;

    public short ToBits() => _value;

    public static implicit operator F24Dot8(F8Dot8 value) => F24Dot8.FromBits(value._value);

    public static F8Dot8 FromBits(short value) => Unsafe.BitCast<short, F8Dot8>(value);

    public override string ToString()
    {
        return $"{_value / 256.0:F}";
    }

    private string GetDebuggerDisplay()
    {
        return $"{ToString()} ({_value})";
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F8Dot8x2(uint value)
{
    public F8Dot8 X => new((short) value);
    public F8Dot8 Y => new((short) (value >> 16));

    private string GetDebuggerDisplay()
    {
        string separator = NumberFormatInfo.GetInstance(null).NumberGroupSeparator;

        return $"<{X}{separator} {Y}>";
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F8Dot8x4(ulong value)
{
    public F8Dot8 X => new((short) value);
    public F8Dot8 Y => new((short) (value >> 16));
    public F8Dot8 Z => new((short) (value >> 32));
    public F8Dot8 W => new((short) (value >> 48));

    private string GetDebuggerDisplay()
    {
        string separator = NumberFormatInfo.GetInstance(null).NumberGroupSeparator;

        return $"<{X}{separator} {Y}{separator} {Z}{separator} {W}>";
    }
}

public readonly partial struct F8Dot8
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F8Dot8x2 Pack(F24Dot8 a, F24Dot8 b)
    {
        // Values must be small enough.
        Debug.Assert((a.ToBits() & 0xffff0000) == 0);
        Debug.Assert((b.ToBits() & 0xffff0000) == 0);

        return new F8Dot8x2((uint) (a._value | (b._value << 16)));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F8Dot8x4 Pack(F24Dot8 a, F24Dot8 b, F24Dot8 c, F24Dot8 d)
    {
        // Values must be small enough.
        Debug.Assert((a.ToBits() & 0xffff0000) == 0);
        Debug.Assert((b.ToBits() & 0xffff0000) == 0);
        Debug.Assert((c.ToBits() & 0xffff0000) == 0);
        Debug.Assert((d.ToBits() & 0xffff0000) == 0);

        uint lo = (uint) a._value | ((uint) b._value << 16);
        ulong hi = ((ulong) c._value << 32) | ((ulong) d._value << 48);

        return new F8Dot8x4(lo | hi);
    }
}
