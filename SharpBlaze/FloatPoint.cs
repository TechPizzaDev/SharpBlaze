
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public struct FloatPoint
{
    public double X;
    public double Y;

    public FloatPoint(double x, double y)
    {
        X = x;
        Y = y;
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
}