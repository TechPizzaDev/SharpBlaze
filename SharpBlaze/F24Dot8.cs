using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

/**
 * 24.8 fixed point number.
 */
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F24Dot8
{
    private readonly int _value;

    private F24Dot8(int value) => _value = value;

    /**
     * Value equivalent to one in 24.8 fixed point format.
     */
    public static F24Dot8 F24Dot8_1 => 1 << 8;


    /**
     * Value equivalent to two in 24.8 fixed point format.
     */
    public static F24Dot8 F24Dot8_2 => 2 << 8;


    /**
     * Converts double to 24.8 fixed point number. Does not check if a double is
     * small enough to be represented as 24.8 number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 DoubleToF24Dot8(double v)
    {
        return (F24Dot8) (int) Utils.Round(v * 256.0);
    }


    /**
     * Returns absolute value for a given 24.8 fixed point number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 F24Dot8Abs(F24Dot8 v)
    {
        int mask = v >> 31;
        return (v + mask) ^ mask;
    }

    public static implicit operator int(F24Dot8 value) => value._value;

    public static implicit operator uint(F24Dot8 value) => (uint) value._value;

    public static implicit operator F24Dot8(int value) => new(value);

    public static implicit operator F24Dot8(uint value) => new((int) value);
    
    public override string ToString()
    {
        return $"{_value / 256.0:F}";
    }

    private readonly string GetDebuggerDisplay()
    {
        return $"{_value} ({ToString()})";
    }
}