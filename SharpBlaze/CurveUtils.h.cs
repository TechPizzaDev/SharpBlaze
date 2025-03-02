using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

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

[InlineArray(3)]
public struct FloatPointX3
{
    private FloatPoint _e0;
}

[InlineArray(4)]
public struct FloatPointX4
{
    private FloatPoint _e0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPointX3 Get3(int start)
    {
        Unsafe.SkipInit(out FloatPointX3 result);
        for (int i = 0; i < 3; i++)
        {
            result[i] = this[start + i];
        }
        return result;
    }
}

[InlineArray(5)]
public struct FloatPointX5
{
    private FloatPoint _e0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPointX3 Get3(int start)
    {
        Unsafe.SkipInit(out FloatPointX3 result);
        for (int i = 0; i < 3; i++)
        {
            result[i] = this[start + i];
        }
        return result;
    }
}

[InlineArray(7)]
public struct FloatPointX7
{
    private FloatPoint _e0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPointX4 Get4(int start)
    {
        Unsafe.SkipInit(out FloatPointX4 result);
        for (int i = 0; i < 4; i++)
        {
            result[i] = this[start + i];
        }
        return result;
    }
}

[InlineArray(10)]
public struct FloatPointX10
{
    private FloatPoint _e0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatPointX4 Get4(int start)
    {
        Unsafe.SkipInit(out FloatPointX4 result);
        for (int i = 0; i < 4; i++)
        {
            result[i] = this[start + i];
        }
        return result;
    }
}

public static unsafe partial class CurveUtils
{
    /*
     * Roots must not be nullptr. Returns 0, 1 or 2.
     */
    public static partial int FindQuadraticRoots(double a, double b, double c,
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
    public static partial int CutQuadraticAtXExtrema(in FloatPointX3 src, out FloatPointX5 dst);


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
    public static partial int CutCubicAtXExtrema(in FloatPointX4 src, out FloatPointX10 dst);


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
    public static partial int CutQuadraticAtYExtrema(in FloatPointX3 src, out FloatPointX5 dst);


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
    public static partial int CutCubicAtYExtrema(in FloatPointX4 src, out FloatPointX10 dst);



    /**
     * Returns true if a given value is between a and b.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValueBetweenAAndB(double a, double value, double b)
    {
        if (a <= b)
        {
            return a <= value && value <= b;
        }
        else
        {
            return a >= value && value >= b;
        }
    }


    /**
     * Returns true if given cubic curve is monotonic in X. This function only
     * checks if cubic control points are between end points. This means that this
     * function can return false when in fact curve does not change direction in X.
     *
     * Use this function for fast monotonicity checks.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CubicControlPointsBetweenEndPointsX(in FloatPointX4 pts)
    {
        return
            IsValueBetweenAAndB(pts[0].X, pts[1].X, pts[3].X) &&
            IsValueBetweenAAndB(pts[0].X, pts[2].X, pts[3].X);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool QuadraticControlPointBetweenEndPointsX(in FloatPointX3 pts)
    {
        return IsValueBetweenAAndB(pts[0].X, pts[1].X, pts[2].X);
    }


    /**
     * Returns true if given cubic curve is monotonic in Y. This function only
     * checks if cubic control points are between end points. This means that this
     * function can return false when in fact curve does not change direction in Y.
     *
     * Use this function for fast monotonicity checks.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CubicControlPointsBetweenEndPointsY(in FloatPointX4 pts)
    {
        return
            IsValueBetweenAAndB(pts[0].Y, pts[1].Y, pts[3].Y) &&
            IsValueBetweenAAndB(pts[0].Y, pts[2].Y, pts[3].Y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool QuadraticControlPointBetweenEndPointsY(in FloatPointX3 pts)
    {
        return IsValueBetweenAAndB(pts[0].Y, pts[1].Y, pts[2].Y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InterpolateQuadraticCoordinates(in FloatPointX3 src, out FloatPointX5 dst, Vector128<double> t)
    {
        Vector128<double> ab = InterpolateLinear(src[0].AsVector128(), src[1].AsVector128(), t);
        Vector128<double> bc = InterpolateLinear(src[1].AsVector128(), src[2].AsVector128(), t);
        
        Unsafe.SkipInit(out dst);
        dst[0] = src[0];
        dst[1] = new FloatPoint(ab);
        dst[2] = new FloatPoint(InterpolateLinear(ab, bc, t));
        dst[3] = new FloatPoint(bc);
        dst[4] = src[2];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CutQuadraticAt(in FloatPointX3 src, out FloatPointX5 dst, double t)
    {
        Debug.Assert(t >= 0.0);
        Debug.Assert(t <= 1.0);

        InterpolateQuadraticCoordinates(src, out dst, Vector128.Create(t));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InterpolateCubicCoordinates(in FloatPointX4 src, out FloatPointX7 dst, Vector128<double> t)
    {
        Vector128<double> ab = InterpolateLinear(src[0].AsVector128(), src[1].AsVector128(), t);
        Vector128<double> bc = InterpolateLinear(src[1].AsVector128(), src[2].AsVector128(), t);
        Vector128<double> cd = InterpolateLinear(src[2].AsVector128(), src[3].AsVector128(), t);
        Vector128<double> abc = InterpolateLinear(ab, bc, t);
        Vector128<double> bcd = InterpolateLinear(bc, cd, t);
        Vector128<double> abcd = InterpolateLinear(abc, bcd, t);

        Unsafe.SkipInit(out dst);
        dst[0] = src[0];
        dst[1] = new FloatPoint(ab);
        dst[2] = new FloatPoint(abc);
        dst[3] = new FloatPoint(abcd);
        dst[4] = new FloatPoint(bcd);
        dst[5] = new FloatPoint(cd);
        dst[6] = src[3];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CutCubicAt(in FloatPointX4 src, out FloatPointX7 dst, double t)
    {
        Debug.Assert(t >= 0.0);
        Debug.Assert(t <= 1.0);

        InterpolateCubicCoordinates(src, out dst, Vector128.Create(t));
    }

}