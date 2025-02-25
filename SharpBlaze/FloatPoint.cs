using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

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
    public readonly FloatPoint Clamp(FloatPoint min, FloatPoint max)
    {
        Vector128<double> left = Vector128.Max(AsVector128(), min.AsVector128());
        return new FloatPoint(Vector128.Min(left, max.AsVector128()));
    }

    public bool Equals(FloatPoint other)
    {
        return this == other;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FloatPoint other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator -(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(a.AsVector128() - b.AsVector128());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator +(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(a.AsVector128() + b.AsVector128());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator *(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(a.AsVector128() * b.AsVector128());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator /(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(a.AsVector128() / b.AsVector128());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FloatPoint a, FloatPoint b)
    {
        return Vector128.EqualsAll(a.AsVector128(), b.AsVector128());
    }

    public static bool operator !=(FloatPoint a, FloatPoint b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        string separator = NumberFormatInfo.GetInstance(null).NumberGroupSeparator;

        return $"<{X}{separator} {Y}>";
    }
}
