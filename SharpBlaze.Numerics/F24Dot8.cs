using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static Unsafe;

/**
 * 24.8 fixed point number.
 */
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct F24Dot8 : IEquatable<F24Dot8>
{
    internal readonly int _value;

    public static F24Dot8 Zero => default;
    
    public static F24Dot8 Epsilon => BitCast<int, F24Dot8>(1);

    /**
     * Value equivalent to one in 24.8 fixed point format.
     */
    public static F24Dot8 One => BitCast<int, F24Dot8>(1 << 8);

    public static F24Dot8 FromBits(int value) => BitCast<int, F24Dot8>(value);

    public static F24Dot8 FromBits(uint value) => BitCast<uint, F24Dot8>(value);

    public int ToBits() => _value;

    public int ToI32() => _value >> 8;

    /**
     * Converts double to 24.8 fixed point number. Does not check if a double is
     * small enough to be represented as 24.8 number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 FromF64(double v) => BitCast<int, F24Dot8>(ScalarHelper.RoundToInt32(v * 256.0));


    /**
     * Returns absolute value for a given 24.8 fixed point number.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 Abs(F24Dot8 v)
    {
        int mask = v._value >> 31;
        return BitCast<int, F24Dot8>((v._value + mask) ^ mask);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (F24Dot8 Q, F24Dot8 R) DivRem(F24Dot8 a, F24Dot8 b)
    {
        (int q, int r) = Math.DivRem(a._value, b._value);
        return (BitCast<int, F24Dot8>(q), BitCast<int, F24Dot8>(r));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator +(F24Dot8 a, F24Dot8 b) => BitCast<int, F24Dot8>(a._value + b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator ++(F24Dot8 a) => BitCast<int, F24Dot8>(a._value + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator -(F24Dot8 a, F24Dot8 b) => BitCast<int, F24Dot8>(a._value - b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator ~(F24Dot8 a) => BitCast<int, F24Dot8>(~a._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator *(F24Dot8 a, F24Dot8 b) => BitCast<int, F24Dot8>(a._value * b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator /(F24Dot8 a, F24Dot8 b) => BitCast<int, F24Dot8>(a._value / b._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator %(F24Dot8 a, F24Dot8 b) => BitCast<int, F24Dot8>(a._value % b._value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator &(F24Dot8 a, F24Dot8 b) => BitCast<int, F24Dot8>(a._value & b._value);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator <<(F24Dot8 a, int b) => BitCast<int, F24Dot8>(a._value << b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 operator >>(F24Dot8 a, int b) => BitCast<int, F24Dot8>(a._value >> b);

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
