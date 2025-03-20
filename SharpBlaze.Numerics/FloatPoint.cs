using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

[DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
public struct FloatPoint : IEquatable<FloatPoint>
{
    public double X;
    public double Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPoint(double x, double y)
    {
        X = x;
        Y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPoint(double value)
    {
        X = value;
        Y = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPoint(Vector128<double> xy)
    {
        this = Unsafe.BitCast<Vector128<double>, FloatPoint>(xy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<double> AsVector128()
    {
        return Unsafe.BitCast<FloatPoint, Vector128<double>>(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> ToF24Dot8(Vector128<double> value, Vector128<int> min, Vector128<int> max)
    {
        Vector128<double> scaled = value * 256.0;

        Vector128<int> conv = V128Helper.RoundToInt32(scaled);
        
        // Operating on vectors is cheaper than clamping extracted scalars later.
        return V128Helper.Clamp(conv, min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly FloatPoint Clamp(FloatPoint min, FloatPoint max)
    {
        return new FloatPoint(
            V128Helper.Clamp(AsVector128(), min.AsVector128(), max.AsVector128()));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsFinite() => V128Helper.IsFiniteAll(AsVector128());

    public readonly bool Equals(FloatPoint other) => this == other;

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FloatPoint other && Equals(other);
    }

    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator -(FloatPoint a, FloatPoint b) => new(a.AsVector128() - b.AsVector128());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator +(FloatPoint a, FloatPoint b) => new(a.AsVector128() + b.AsVector128());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator *(FloatPoint a, FloatPoint b) => new(a.AsVector128() * b.AsVector128());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator *(FloatPoint a, double b) => new(a.AsVector128() * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator /(FloatPoint a, FloatPoint b) => new(a.AsVector128() / b.AsVector128());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator /(FloatPoint a, double b) => new(a.AsVector128() / b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FloatPoint a, FloatPoint b)
    {
        return Vector128.EqualsAll(a.AsVector128(), b.AsVector128());
    }

    public static bool operator !=(FloatPoint a, FloatPoint b) => !(a == b);

    public override readonly string ToString()
    {
        string separator = NumberFormatInfo.GetInstance(null).NumberGroupSeparator;

        return $"<{X}{separator} {Y}>";
    }
}
