using System.Runtime.CompilerServices;

namespace SharpBlaze;

/**
 * 24.8 fixed point number.
 */
public struct F24Dot8
{
    int _value;

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
        return (F24Dot8) Utils.Round(v * 256.0);
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

    public static implicit operator int(F24Dot8 value)
    {
        return value._value;
    }

    public static implicit operator F24Dot8(int value)
    {
        return new F24Dot8() { _value = value };
    }
}