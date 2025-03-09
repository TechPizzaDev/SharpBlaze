using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

/**
 * 24.8 fixed point number.
 */
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F24Dot8 : IEquatable<F24Dot8>
{
    internal readonly int _value;

    public F24Dot8(int value) => _value = value;

    /**
     * Value equivalent to one in 24.8 fixed point format.
     */
    public static F24Dot8 F24Dot8_1 => new(1 << 8);


    /**
     * Value equivalent to two in 24.8 fixed point format.
     */
    public static F24Dot8 F24Dot8_2 => new(2 << 8);


    /**
     * Converts double to 24.8 fixed point number. Does not check if a double is
     * small enough to be represented as 24.8 number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 DoubleToF24Dot8(double v)
    {
        return new(ConvertToInt32(v * 256.0));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ConvertToInt32(double v)
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


    /**
     * Returns absolute value for a given 24.8 fixed point number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 Abs(F24Dot8 v)
    {
        int mask = v._value >> 31;
        return new F24Dot8((v._value + mask) ^ mask);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (F24Dot8 Q, F24Dot8 R) DivRem(F24Dot8 a, F24Dot8 b)
    {
        (int q, int r) = Math.DivRem(a._value, b._value);
        return (new F24Dot8(q), new F24Dot8(r));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator +(F24Dot8 a, F24Dot8 b) => new(a._value + b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator -(F24Dot8 a, F24Dot8 b) => new(a._value - b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator *(F24Dot8 a, F24Dot8 b) => new(a._value * b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator /(F24Dot8 a, F24Dot8 b) => new(a._value / b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator %(F24Dot8 a, F24Dot8 b) => new(a._value % b._value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator <<(F24Dot8 a, int b) => new(a._value << b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator >>(F24Dot8 a, int b) => new(a._value >> b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(F24Dot8 a, F24Dot8 b) => a._value == b._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(F24Dot8 a, F24Dot8 b) => a._value != b._value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(F24Dot8 a, F24Dot8 b) => a._value < b._value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(F24Dot8 a, F24Dot8 b) => a._value <= b._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(F24Dot8 a, int b) => a._value <= b;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(F24Dot8 a, F24Dot8 b) => a._value > b._value;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(F24Dot8 a, F24Dot8 b) => a._value >= b._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(F24Dot8 a, int b) => a._value >= b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(F24Dot8 value) => value._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator uint(F24Dot8 value) => (uint) value._value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator F24Dot8(int value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator F24Dot8(uint value) => new((int) value);

    public bool Equals(F24Dot8 other) => _value == other._value;

    public override bool Equals(object? obj)
    {
        return obj is F24Dot8 other && Equals(other);
    }

    public override int GetHashCode() => _value;
    
    public override string ToString()
    {
        return $"{_value / 256.0:F}";
    }

    private string GetDebuggerDisplay()
    {
        return $"{_value} ({ToString()})";
    }
}
