using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

using static V128Helper;

[InlineArray(2)]
public struct F24Dot8PointX2
{
    private F24Dot8Point _e0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<int> FromFloatToV128(Vector128<double> p0, Vector128<double> p1)
    {
        Vector128<double> factor = Vector128.Create(256.0);
        Vector128<double> s0 = p0 * factor;
        Vector128<double> s1 = p1 * factor;
        return RoundToInt32(s0, s1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampFromFloat(
        FloatPoint p0, FloatPoint p1,
        F24Dot8Point min, F24Dot8Point max,
        Span<F24Dot8Point> result)
    {
        Vector128<int> p01 = FromFloatToV128(p0.AsVector128(), p1.AsVector128());
        
        Vector128<int> vMin = min.ToVector128();
        Vector128<int> vMax = max.ToVector128();
        p01 = Clamp(p01, vMin, vMax);
        
        p01.CopyTo(MemoryMarshal.Cast<F24Dot8Point, int>(result));
    }
}

[InlineArray(3)]
public struct F24Dot8PointX3
{
    private F24Dot8Point _e0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector128<int> P01, Vector128<int> P2) FromFloatToV128(
        Vector128<double> p0, Vector128<double> p1, Vector128<double> p2)
    {
        Vector128<double> factor = Vector128.Create(256.0);
        Vector128<double> s0 = p0 * factor;
        Vector128<double> s1 = p1 * factor;
        Vector128<double> s2 = p2 * factor;

        Vector128<int> r01 = RoundToInt32(s0, s1);
        Vector128<int> r22 = RoundToInt32(s2, s2);
        return (r01, r22);
    }

    public static void ClampFromFloat(
        ReadOnlySpan<FloatPoint> p,
        F24Dot8Point min,
        F24Dot8Point max,
        Span<F24Dot8Point> result)
    {
        p = p[..3];
        
        (Vector128<int> p01, Vector128<int> p22) = FromFloatToV128(
            p[0].AsVector128(),
            p[1].AsVector128(),
            p[2].AsVector128());

        Vector128<int> vMin = min.ToVector128();
        Vector128<int> vMax = max.ToVector128();
        p01 = Clamp(p01, vMin, vMax);
        p22 = Clamp(p22, vMin, vMax);

        Span<long> dst = MemoryMarshal.Cast<F24Dot8Point, long>(result)[..3];
        p01.AsInt64().CopyTo(dst);
        dst[2] = p22.AsInt64().ToScalar();
    }
}

[InlineArray(4)]
public struct F24Dot8PointX4
{
    private F24Dot8Point _e0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector128<int> P01, Vector128<int> P23) FromFloatToV128(
        Vector128<double> p0, Vector128<double> p1, Vector128<double> p2, Vector128<double> p3)
    {
        Vector128<double> factor = Vector128.Create(256.0);
        Vector128<double> s0 = p0 * factor;
        Vector128<double> s1 = p1 * factor;
        Vector128<double> s2 = p2 * factor;
        Vector128<double> s3 = p3 * factor;

        Vector128<int> r01 = RoundToInt32(s0, s1);
        Vector128<int> r23 = RoundToInt32(s2, s3);
        return (r01, r23);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FromV128(Vector128<int> p01, Vector128<int> p23, Span<F24Dot8Point> result)
    {
        Span<int> dst = MemoryMarshal.Cast<F24Dot8Point, int>(result)[..8];
        p01.CopyTo(dst);
        p23.CopyTo(dst[4..]);
    }

    public static void FromFloat(ReadOnlySpan<FloatPoint> p, Span<F24Dot8Point> result)
    {
        p = p[..4];
        
        (Vector128<int> p01, Vector128<int> p23) = FromFloatToV128(
            p[0].AsVector128(),
            p[1].AsVector128(),
            p[2].AsVector128(),
            p[3].AsVector128());
        
        FromV128(p01, p23, result);
    }

    public static void ClampFromFloat(
        ReadOnlySpan<FloatPoint> p,
        F24Dot8Point min,
        F24Dot8Point max,
        Span<F24Dot8Point> result)
    {
        p = p[..4];
        
        (Vector128<int> p01, Vector128<int> p23) = FromFloatToV128(
            p[0].AsVector128(),
            p[1].AsVector128(),
            p[2].AsVector128(),
            p[3].AsVector128());

        Vector128<int> vMin = min.ToVector128();
        Vector128<int> vMax = max.ToVector128();
        p01 = Clamp(p01, vMin, vMax);
        p23 = Clamp(p23, vMin, vMax);

        FromV128(p01, p23, result);
    }
}

public static class LinearizerUtils
{
    public static void UpdateCoverTable_Down(Span<int> covers, F24Dot8 y0, F24Dot8 y1)
    {
        Debug.Assert(y0 < y1);

        // Integer parts for top and bottom.
        int rowIndex0 = y0 >> 8;
        int rowIndex1 = (y1 - 1) >> 8;

        Debug.Assert(rowIndex0 >= 0);
        //ASSERT(rowIndex0 < T::TileH);
        Debug.Assert(rowIndex1 >= 0);
        //ASSERT(rowIndex1 < T::TileH);

        int fy0 = y0 - (rowIndex0 << 8);
        int fy1 = y1 - (rowIndex1 << 8);

        if (rowIndex0 == rowIndex1)
        {
            covers[rowIndex0] -= fy1 - fy0;
        }
        else
        {
            covers[rowIndex0] += fy0;

            for (int i = rowIndex0; i < rowIndex1; i++)
            {
                covers[i] -= 256;
            }

            covers[rowIndex1] -= fy1;
        }
    }


    public static void UpdateCoverTable_Up(Span<int> covers, F24Dot8 y0, F24Dot8 y1)
    {
        Debug.Assert(y0 > y1);

        // Integer parts for top and bottom.
        int rowIndex0 = (y0 - 1) >> 8;
        int rowIndex1 = y1 >> 8;

        Debug.Assert(rowIndex0 >= 0);
        //ASSERT(rowIndex0 < T::TileH);
        Debug.Assert(rowIndex1 >= 0);
        //ASSERT(rowIndex1 < T::TileH);

        int fy0 = y0 - (rowIndex0 << 8);
        int fy1 = y1 - (rowIndex1 << 8);

        if (rowIndex0 == rowIndex1)
        {
            covers[rowIndex0] += fy0 - fy1;
        }
        else
        {
            covers[rowIndex0] += fy0;

            for (int i = rowIndex0 - 1; i > rowIndex1; i--)
            {
                covers[i] += 256;
            }

            covers[rowIndex1] += 256 - fy1;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateCoverTable(Span<int> covers, F24Dot8 y0, F24Dot8 y1)
    {
        if (y0 < y1)
        {
            UpdateCoverTable_Down(covers, y0, y1);
        }
        else
        {
            UpdateCoverTable_Up(covers, y0, y1);
        }
    }


    /**
     * Split quadratic curve in half.
     *
     * @param r Resulting curves. First curve will be represented as elements at
     * indices 0, 1 and 2. Second curve will be represented as elements at indices
     * 2, 3 and 4.
     *
     * @param s Source curve defined by three points.
     */
    public static void SplitQuadratic(Span<F24Dot8Point> r, ReadOnlySpan<F24Dot8Point> s)
    {
        s = s[..3];
        r = r[..5];
        
        F24Dot8Point m0 = (s[0] + s[1]) >> 1;
        F24Dot8Point m1 = (s[1] + s[2]) >> 1;
        F24Dot8Point m2 = (m0 + m1) >> 1;

        r[0] = s[0];
        r[1] = m0;
        r[2] = m2;
        r[3] = m1;
        r[4] = s[2];
    }


    /**
     * Split cubic curve in half.
     *
     * @param r Resulting curves. First curve will be represented as elements at
     * indices 0, 1, 2 and 3. Second curve will be represented as elements at
     * indices 3, 4, 5 and 6.
     *
     * @param s Source curve defined by four points.
     */
    public static void SplitCubic(Span<F24Dot8Point> r, ReadOnlySpan<F24Dot8Point> s)
    {
        s = s[..4];
        r = r[..7];

        F24Dot8Point m0 = (s[0] + s[1]) >> 1;
        F24Dot8Point m1 = (s[1] + s[2]) >> 1;
        F24Dot8Point m2 = (s[2] + s[3]) >> 1;
        F24Dot8Point m3 = (m0 + m1) >> 1;
        F24Dot8Point m4 = (m1 + m2) >> 1;
        F24Dot8Point m5 = (m3 + m4) >> 1;
        
        r[0] = s[0];
        r[1] = m0;
        r[2] = m3;
        r[3] = m5;
        r[4] = m4;
        r[5] = m2;
        r[6] = s[3];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicQuadraticAt(double c0, double c1, double c2, double target, out double t)
    {
        double A = c0 - c1 - c1 + c2;
        double B = 2 * (c1 - c0);
        double C = c0 - target;

        int count = CurveUtils.FindQuadraticRoots(A, B, C, out DoubleX2 roots);

        t = roots[0];

        return count > 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicQuadraticAtX(ReadOnlySpan<FloatPoint> q, double x, out double t)
    {
        q = q[..3];
        
        return CutMonotonicQuadraticAt(q[0].X, q[1].X, q[2].X, x, out t);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicQuadraticAtY(ReadOnlySpan<FloatPoint> q, double y, out double t)
    {
        q = q[..3];
        
        return CutMonotonicQuadraticAt(q[0].Y, q[1].Y, q[2].Y, y, out t);
    }

    
    public static bool CutMonotonicCubicAt(out double t, DoubleX4 pts)
    {
        const double Tolerance = 1e-7;

        Unsafe.SkipInit(out t);

        double p0 = pts[0];
        double p3 = pts[3];

        double negative;
        double positive;

        if (p0 < 0)
        {
            if (p3 < 0)
            {
                return false;
            }

            negative = 0;
            positive = 1.0;
        }
        else if (p0 > 0)
        {
            if (p3 > 0)
            {
                return false;
            }

            negative = 1.0;
            positive = 0;
        }
        else
        {
            t = 0;
            return true;
        }

        double p1 = pts[1];
        double p2 = pts[2];

        do
        {
            double m = (positive + negative) * 0.5;
            double y01 = Utils.InterpolateLinear(p0, p1, m);
            double y12 = Utils.InterpolateLinear(p1, p2, m);
            double y23 = Utils.InterpolateLinear(p2, p3, m);
            double y012 = Utils.InterpolateLinear(y01, y12, m);
            double y123 = Utils.InterpolateLinear(y12, y23, m);
            double y0123 = Utils.InterpolateLinear(y012, y123, m);

            if (y0123 == 0.0)
            {
                t = m;
                return true;
            }

            if (y0123 < 0.0)
            {
                negative = m;
            }
            else
            {
                positive = m;
            }
        }
        while (Math.Abs(positive - negative) > Tolerance);

        t = (negative + positive) * 0.5;

        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicCubicAtY(ReadOnlySpan<FloatPoint> pts, double y, out double t)
    {
        pts = pts[..4];
        
        Unsafe.SkipInit(out DoubleX4 c);
        c[0] = pts[0].Y - y;
        c[1] = pts[1].Y - y;
        c[2] = pts[2].Y - y;
        c[3] = pts[3].Y - y;

        return CutMonotonicCubicAt(out t, c);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicCubicAtX(ReadOnlySpan<FloatPoint> pts, double x, out double t)
    {
        pts = pts[..4];
        
        Unsafe.SkipInit(out DoubleX4 c);
        c[0] = pts[0].X - x;
        c[1] = pts[1].X - x;
        c[2] = pts[2].X - x;
        c[3] = pts[3].X - x;

        return CutMonotonicCubicAt(out t, c);
    }


    /**
     * Returns true if a given quadratic curve is flat enough to be interpreted as
     * line for rasterizer.
     */
    public static bool IsQuadraticFlatEnough(ReadOnlySpan<F24Dot8Point> q)
    {
        q = q[..3];
        
        if (q[0].X == q[2].X &&
            q[0].Y == q[2].Y)
        {
            return true;
        }

        // Find middle point between start and end point.
        F24Dot8 mx = (q[0].X + q[2].X) >> 1;
        F24Dot8 my = (q[0].Y + q[2].Y) >> 1;

        // Calculate cheap distance between middle point and control point.
        F24Dot8 dx = F24Dot8.Abs(mx - q[1].X);
        F24Dot8 dy = F24Dot8.Abs(my - q[1].Y);

        // Add both distances together and compare with allowed error.
        F24Dot8 dc = dx + dy;

        // 32 in 24.8 fixed point format is equal to 0.125.
        return dc <= 32;
    }


    public static bool IsCubicFlatEnough(ReadOnlySpan<F24Dot8Point> c)
    {
        c = c[..4];

        F24Dot8 tolerance = F24Dot8.F24Dot8_1 >> 1;
        F24Dot8 c2 = new(2);
        F24Dot8 c3 = new(3);
        
        F24Dot8Point p0 = c[0];
        F24Dot8Point p3 = c[3];
        return
            F24Dot8.Abs(c2 * p0.X - c3 * c[1].X + p3.X) <= tolerance &&
            F24Dot8.Abs(c2 * p0.Y - c3 * c[1].Y + p3.Y) <= tolerance &&
            F24Dot8.Abs(p0.X - c3 * c[2].X + c2 * p3.X) <= tolerance &&
            F24Dot8.Abs(p0.Y - c3 * c[2].Y + c2 * p3.Y) <= tolerance;
    }
}
