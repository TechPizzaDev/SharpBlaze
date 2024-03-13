using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct FloatPoint
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
    public static FloatPoint operator -(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(
            a.X - b.X,
            a.Y - b.Y
        );
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator +(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(
            a.X + b.X,
            a.Y + b.Y
        );
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FloatPoint operator *(FloatPoint a, FloatPoint b)
    {
        return new FloatPoint(
            a.X * b.X,
            a.Y * b.Y
        );
    }

    private readonly string GetDebuggerDisplay()
    {
        string separator = NumberFormatInfo.GetInstance(null).NumberGroupSeparator;

        return $"<{X}{separator} {Y}>";
    }
}