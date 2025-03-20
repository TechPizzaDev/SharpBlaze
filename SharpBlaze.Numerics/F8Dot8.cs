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

    public static implicit operator short(F8Dot8 value) => value._value;

    public static implicit operator F24Dot8(F8Dot8 value) => new(value._value);

    public static explicit operator F8Dot8(short value) => new(value);

    public override string ToString()
    {
        return $"{_value / 256.0:F}";
    }

    private string GetDebuggerDisplay()
    {
        return $"{_value} ({ToString()})";
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F8Dot8x2
{
    private readonly uint _value;

    private F8Dot8x2(uint value) => _value = value;

    public F8Dot8 X => new((short) _value);
    public F8Dot8 Y => new((short) (_value >> 16));

    public static explicit operator uint(F8Dot8x2 value) => value._value;

    public static explicit operator F8Dot8x2(uint value) => new(value);

    private string GetDebuggerDisplay()
    {
        string separator = NumberFormatInfo.GetInstance(null).NumberGroupSeparator;

        return $"<{X}{separator} {Y}>";
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F8Dot8x4
{
    private readonly ulong _value;

    private F8Dot8x4(ulong value) => _value = value;

    public F8Dot8 X => new((short) _value);
    public F8Dot8 Y => new((short) (_value >> 16));
    public F8Dot8 Z => new((short) (_value >> 32));
    public F8Dot8 W => new((short) (_value >> 48));

    public static explicit operator ulong(F8Dot8x4 value) => value._value;

    public static explicit operator F8Dot8x4(ulong value) => new(value);

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
        Debug.Assert((a & 0xffff0000) == 0);
        Debug.Assert((b & 0xffff0000) == 0);

        return (F8Dot8x2) (uint) (a._value | (b._value << 16));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F8Dot8x4 Pack(F24Dot8 a, F24Dot8 b, F24Dot8 c, F24Dot8 d)
    {
        // Values must be small enough.
        Debug.Assert((a & 0xffff0000) == 0);
        Debug.Assert((b & 0xffff0000) == 0);
        Debug.Assert((c & 0xffff0000) == 0);
        Debug.Assert((d & 0xffff0000) == 0);

        uint lo = (uint) a._value | ((uint) b._value << 16);
        ulong hi = ((ulong) c._value << 32) | ((ulong) d._value << 48);
        
        return (F8Dot8x4) (lo | hi);
    }
}
