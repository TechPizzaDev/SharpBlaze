using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static Utils;

[InlineArray(2)]
public struct DoubleX2
{
    private double _e0;
}

[InlineArray(4)]
public struct DoubleX4
{
    private double _e0;
}

public static partial class CurveUtils
{
    /*
     * Roots must not be nullptr. Returns 0, 1 or 2.
     */
    public static partial int FindQuadraticRoots(
        double a, double b, double c,
        out DoubleX2 roots);


    /**
     * Finds extrema on X axis of a quadratic curve and splits it at this point,
     * producing up to 2 output curves. Returns the number of output curves. If
     * extrema was not found, this function returns 1 and destination array
     * contains points of the original input curve. Always returns 1 or 2.
     *
     * Curves are stored in output as follows.
     *
     * 1. dst[0], dst[1], dst[2]
     * 2. dst[2], dst[3], dst[4]
     *
     * @param src Input quadratic curve as 3 points.
     *
     * @param dst Pointer to memory for destination curves. Must be large enough
     * to keep 5 FloatPoint values.
     */
    public static partial int CutQuadraticAtXExtrema(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst);


    /**
     * Finds extremas on X axis of a cubic curve and splits it at these points,
     * producing up to 3 output curves. Returns the number of output curves. If no
     * extremas are found, this function returns 1 and destination array contains
     * points of the original input curve. Always returns 1, 2 or 3.
     *
     * Curves are stored in output as follows.
     *
     * 1. dst[0], dst[1], dst[2], dst[3]
     * 2. dst[3], dst[4], dst[5], dst[6]
     * 3. dst[6], dst[7], dst[8], dst[9]
     *
     * @param src Input cubic curve as 4 points.
     *
     * @param dst Pointer to memory for destination curves. Must be large enough
     * to keep 10 FloatPoint values.
     */
    public static partial int CutCubicAtXExtrema(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst);


    /**
     * Finds extrema on Y axis of a quadratic curve and splits it at this point,
     * producing up to 2 output curves. Returns the number of output curves. If
     * extrema was not found, this function returns 1 and destination array
     * contains points of the original input curve. Always returns 1 or 2.
     *
     * Curves are stored in output as follows.
     *
     * 1. dst[0], dst[1], dst[2]
     * 2. dst[2], dst[3], dst[4]
     *
     * @param src Input quadratic curve as 3 points.
     *
     * @param dst Pointer to memory for destination curves. Must be large enough
     * to keep 5 FloatPoint values.
     */
    public static partial int CutQuadraticAtYExtrema(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst);


    /**
     * Finds extremas on Y axis of a cubic curve and splits it at these points,
     * producing up to 3 output curves. Returns the number of output curves. If no
     * extremas are found, this function returns 1 and destination array contains
     * points of the original input curve. Always returns 1, 2 or 3.
     *
     * Curves are stored in output as follows.
     *
     * 1. dst[0], dst[1], dst[2], dst[3]
     * 2. dst[3], dst[4], dst[5], dst[6]
     * 3. dst[6], dst[7], dst[8], dst[9]
     *
     * @param src Input cubic curve as 4 points.
     *
     * @param dst Pointer to memory for destination curves. Must be large enough
     * to keep 10 FloatPoint values.
     */
    public static partial int CutCubicAtYExtrema(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst);


    /**
     * Returns true if a given value is between a and b.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueBetweenAAndB(double a, double value, double b)
    {
        double min = ScalarHelper.MinNative(a, b);
        double max = ScalarHelper.MaxNative(a, b);
        return value >= min & value <= max; 
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> IsValueBetweenAAndB(Vector128<double> a, Vector128<double> value, Vector128<double> b)
    {
        Vector128<double> min = V128Helper.MinNative(a, b);
        Vector128<double> max = V128Helper.MaxNative(a, b);
        return 
            Vector128.GreaterThanOrEqual(value, min) & 
            Vector128.LessThanOrEqual(value, max);
    }


    /**
     * Returns true if given cubic curve is monotonic in X. This function only
     * checks if cubic control points are between end points. This means that this
     * function can return false when in fact curve does not change direction in X.
     *
     * Use this function for fast monotonicity checks.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CubicControlPointsBetweenEndPointsX(ReadOnlySpan<FloatPoint> pts)
    {
        double x0 = pts[0].X;
        double x1 = pts[1].X;
        double x2 = pts[2].X;
        double x3 = pts[3].X;
        return
            IsValueBetweenAAndB(x0, x1, x3) &
            IsValueBetweenAAndB(x0, x2, x3);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool QuadraticControlPointBetweenEndPointsX(ReadOnlySpan<FloatPoint> pts)
    {
        return IsValueBetweenAAndB(pts[0].X, pts[1].X, pts[2].X);
    }


    /**
     * Returns true if given cubic curve is monotonic in X or Y. This function only
     * checks if cubic control points are between end points. This means that this
     * function can return false when in fact curve does not change direction.
     *
     * Use this function for fast monotonicity checks.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> CubicControlPointsBetweenEndPoints(ReadOnlySpan<FloatPoint> pts)
    {
        Vector128<double> p0 = pts[0].AsVector128();
        Vector128<double> p1 = pts[1].AsVector128();
        Vector128<double> p2 = pts[2].AsVector128();
        Vector128<double> p3 = pts[3].AsVector128();
        return IsValueBetweenAAndB(p0, p1, p3) & IsValueBetweenAAndB(p0, p2, p3);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<double> QuadraticControlPointBetweenEndPoints(ReadOnlySpan<FloatPoint> pts)
    {
        Vector128<double> p0 = pts[0].AsVector128();
        Vector128<double> p1 = pts[1].AsVector128();
        Vector128<double> p2 = pts[2].AsVector128();
        return IsValueBetweenAAndB(p0, p1, p2);
    }


    public static void InterpolateQuadraticCoordinates(
        ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst, Vector128<double> t)
    {
        src = src[..3];
        dst = dst[..5];

        Vector128<double> ab = InterpolateLinear(src[0].AsVector128(), src[1].AsVector128(), t);
        Vector128<double> bc = InterpolateLinear(src[1].AsVector128(), src[2].AsVector128(), t);

        dst[0] = src[0];
        dst[1] = new FloatPoint(ab);
        dst[2] = new FloatPoint(InterpolateLinear(ab, bc, t));
        dst[3] = new FloatPoint(bc);
        dst[4] = src[2];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CutQuadraticAt(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst, double t)
    {
        Debug.Assert(t >= 0.0);
        Debug.Assert(t <= 1.0);

        InterpolateQuadraticCoordinates(src, dst, Vector128.Create(t));
    }


    private static void InterpolateCubicCoordinates(
        ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst, Vector128<double> t)
    {
        src = src[..4];
        dst = dst[..7];
        
        FloatPoint s0 = src[0];
        Vector128<double> s1 = src[1].AsVector128();
        Vector128<double> s2 = src[2].AsVector128();
        FloatPoint s3 = src[3];
        
        Vector128<double> ab = InterpolateLinear(s0.AsVector128(), s1, t);
        Vector128<double> bc = InterpolateLinear(s1, s2, t);
        Vector128<double> cd = InterpolateLinear(s2, s3.AsVector128(), t);
        Vector128<double> abc = InterpolateLinear(ab, bc, t);
        Vector128<double> bcd = InterpolateLinear(bc, cd, t);
        Vector128<double> abcd = InterpolateLinear(abc, bcd, t);

        dst[0] = s0;
        dst[1] = new FloatPoint(ab);
        dst[2] = new FloatPoint(abc);
        dst[3] = new FloatPoint(abcd);
        dst[4] = new FloatPoint(bcd);
        dst[5] = new FloatPoint(cd);
        dst[6] = s3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CutCubicAt(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst, double t)
    {
        Debug.Assert(t >= 0.0);
        Debug.Assert(t <= 1.0);

        InterpolateCubicCoordinates(src, dst, Vector128.Create(t));
    }
}
