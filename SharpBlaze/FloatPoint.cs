using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
    public readonly F24Dot8Point ToF24Dot8(F24Dot8Point min, F24Dot8Point max)
    {
        Vector128<double> scaled = AsVector128() * Vector128.Create(256.0);

        Vector128<int> conv;
        if (Sse2.IsSupported)
        {
            conv = Sse2.ConvertToVector128Int32(scaled);
        }
        else
        {
            Vector128<float> narrow = Vector128.Narrow(scaled, scaled);
#if NET9_0_OR_GREATER
            conv = Vector128.ConvertToInt32Native(narrow);
#else
            conv = Vector128.ConvertToInt32(narrow);
#endif
        }

        // Operating on vectors is cheaper than clamping extracted scalars later.
        conv = Utils.Clamp(conv, min.ToVector128(), max.ToVector128());
        
        return new F24Dot8Point(conv.GetElement(0), conv.GetElement(1));
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
