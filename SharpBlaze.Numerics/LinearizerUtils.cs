using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

[InlineArray(2)]
public struct F24Dot8PointX2
{
    private F24Dot8Point _e0;
   
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out F24Dot8Point a, out F24Dot8Point b)
    {
        a = this[0];
        b = this[1];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<int> FromFloatToV128(Vector128<double> p0, Vector128<double> p1)
    {
        Vector128<double> factor = Vector128.Create(256.0);
        Vector128<double> s0 = p0 * factor;
        Vector128<double> s1 = p1 * factor;
        return ConvertToInt32(s0, s1);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<int> ConvertToInt32(Vector128<double> p0, Vector128<double> p1)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            Vector128<long> r0 = AdvSimd.Arm64.ConvertToInt64RoundToEven(p0);
            Vector128<long> r1 = AdvSimd.Arm64.ConvertToInt64RoundToEven(p1);
            return AdvSimd.ExtractNarrowingUpper(AdvSimd.ExtractNarrowingLower(r0), r1);
        }
        else if (Sse2.IsSupported)
        {
            Vector128<long> r0 = Sse2.ConvertToVector128Int32(p0).AsInt64();
            Vector128<long> r1 = Sse2.ConvertToVector128Int32(p1).AsInt64();
            return Sse2.UnpackLow(r0, r1).AsInt32();
        }

        Vector128<float> narrow01 = Vector128.Narrow(p0, p1);
#if NET9_0_OR_GREATER
        return Vector128.ConvertToInt32Native(narrow01);
#else
        return Vector128.ConvertToInt32(narrow01);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8PointX2 ClampFromFloat(
        FloatPoint p0, FloatPoint p1,
        F24Dot8Point min, F24Dot8Point max)
    {
        Vector128<int> p01 = FromFloatToV128(p0.AsVector128(), p1.AsVector128());
        Vector128<int> vMin = min.ToVector128();
        Vector128<int> vMax = max.ToVector128();
        
        p01 = Utils.Clamp(p01, vMin, vMax);
        return FromV128(p01);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static F24Dot8PointX2 FromV128(Vector128<int> p01)
    {
        Unsafe.SkipInit(out F24Dot8PointX2 result);
        result[0] = new(p01.GetElement(0), p01.GetElement(1));
        result[1] = new(p01.GetElement(2), p01.GetElement(3));
        return result;
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

        Vector128<float> narrow01 = Vector128.Narrow(s0, s1);
        Vector128<float> narrow22 = Vector128.Narrow(s2, s2);
        
#if NET9_0_OR_GREATER
        Vector128<int> conv01 = Vector128.ConvertToInt32Native(narrow01);
        Vector128<int> conv22 = Vector128.ConvertToInt32Native(narrow22);
#else
        Vector128<int> conv01 = Vector128.ConvertToInt32(narrow01);
        Vector128<int> conv22 = Vector128.ConvertToInt32(narrow22);
#endif
        return (conv01, conv22);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FromV128(Vector128<int> p01, Vector128<int> p2, out F24Dot8PointX3 result)
    {
        Unsafe.SkipInit(out result);
        result[0] = new(p01.GetElement(0), p01.GetElement(1));
        result[1] = new(p01.GetElement(2), p01.GetElement(3));
        result[2] = new(p2.GetElement(0), p2.GetElement(1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampFromFloat(
        ReadOnlySpan<FloatPoint> p,
        F24Dot8Point min, 
        F24Dot8Point max,
        out F24Dot8PointX3 result)
    {
        (Vector128<int> p01, Vector128<int> p22) = FromFloatToV128(
            p[0].AsVector128(),
            p[1].AsVector128(),
            p[2].AsVector128());

        Vector128<int> vMin = min.ToVector128();
        Vector128<int> vMax = max.ToVector128();
        p01 = Utils.Clamp(p01, vMin, vMax);
        p22 = Utils.Clamp(p22, vMin, vMax);

        FromV128(p01, p22, out result);
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

        Vector128<float> narrow01 = Vector128.Narrow(s0, s1);
        Vector128<float> narrow23 = Vector128.Narrow(s2, s3);
        
#if NET9_0_OR_GREATER
        Vector128<int> conv01 = Vector128.ConvertToInt32Native(narrow01);
        Vector128<int> conv23 = Vector128.ConvertToInt32Native(narrow23);
#else
        Vector128<int> conv01 = Vector128.ConvertToInt32(narrow01);
        Vector128<int> conv23 = Vector128.ConvertToInt32(narrow23);
#endif
        return (conv01, conv23);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FromV128(Vector128<int> p01, Vector128<int> p23, out F24Dot8PointX4 result)
    {
        Unsafe.SkipInit(out result);
        result[0] = new(p01.GetElement(0), p01.GetElement(1));
        result[1] = new(p01.GetElement(2), p01.GetElement(3));
        result[2] = new(p23.GetElement(0), p23.GetElement(1));
        result[3] = new(p23.GetElement(2), p23.GetElement(3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FromFloat(
        FloatPoint p0, FloatPoint p1, FloatPoint p2, FloatPoint p3,
        out F24Dot8PointX4 result)
    {
        (Vector128<int> p01, Vector128<int> p23) = FromFloatToV128(
            p0.AsVector128(), 
            p1.AsVector128(),
            p2.AsVector128(), 
            p3.AsVector128());
        FromV128(p01, p23, out result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampFromFloat(
        ReadOnlySpan<FloatPoint> p,
        F24Dot8Point min, 
        F24Dot8Point max,
        out F24Dot8PointX4 result)
    {
        (Vector128<int> p01, Vector128<int> p23) = FromFloatToV128(
            p[0].AsVector128(), 
            p[1].AsVector128(), 
            p[2].AsVector128(), 
            p[3].AsVector128());

        Vector128<int> vMin = min.ToVector128();
        Vector128<int> vMax = max.ToVector128();
        p01 = Utils.Clamp(p01, vMin, vMax);
        p23 = Utils.Clamp(p23, vMin, vMax);

        FromV128(p01, p23, out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly F24Dot8PointX4 Clamp(F24Dot8Point min, F24Dot8Point max)
    {
        Vector256<int> value = Unsafe.BitCast<F24Dot8PointX4, Vector256<int>>(this);

        Vector256<int> vMin = min.ToVector256();
        Vector256<int> vMax = max.ToVector256();
        Vector256<int> clamped = Utils.Clamp(value, vMin, vMax);

        return Unsafe.BitCast<Vector256<int>, F24Dot8PointX4>(clamped);
    }
}

[InlineArray(5)]
public struct F24Dot8PointX5
{
    private F24Dot8Point _e0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public F24Dot8PointX3 Get3(int start)
    {
        Unsafe.SkipInit(out F24Dot8PointX3 result);
        for (int i = 0; i < 3; i++)
        {
            result[i] = this[start + i];
        }
        return result;
    }
}

[InlineArray(7)]
public struct F24Dot8PointX7
{
    private F24Dot8Point _e0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public F24Dot8PointX4 Get4(int start)
    {
        Unsafe.SkipInit(out F24Dot8PointX4 result);
        for (int i = 0; i < 4; i++)
        {
            result[i] = this[start + i];
        }
        return result;
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
            covers[rowIndex0] -= 256 - fy0;

            for (int i = rowIndex0 + 1; i < rowIndex1; i++)
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SplitQuadratic(out F24Dot8PointX5 r, in F24Dot8PointX3 s)
    {
        F24Dot8 m0x = (s[0].X + s[1].X) >> 1;
        F24Dot8 m0y = (s[0].Y + s[1].Y) >> 1;
        
        F24Dot8 m1x = (s[1].X + s[2].X) >> 1;
        F24Dot8 m1y = (s[1].Y + s[2].Y) >> 1;
        
        F24Dot8 mx = (m0x + m1x) >> 1;
        F24Dot8 my = (m0y + m1y) >> 1;

        Unsafe.SkipInit(out r);
        r[0] = s[0];
        r[1].X = m0x;
        r[1].Y = m0y;
        r[2].X = mx;
        r[2].Y = my;
        r[3].X = m1x;
        r[3].Y = m1y;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SplitCubic(out F24Dot8PointX7 r, in F24Dot8PointX4 s)
    {
        F24Dot8 m0x = (s[0].X + s[1].X) >> 1;
        F24Dot8 m0y = (s[0].Y + s[1].Y) >> 1;
        
        F24Dot8 m1x = (s[1].X + s[2].X) >> 1;
        F24Dot8 m1y = (s[1].Y + s[2].Y) >> 1;
        
        F24Dot8 m2x = (s[2].X + s[3].X) >> 1;
        F24Dot8 m2y = (s[2].Y + s[3].Y) >> 1;
        
        F24Dot8 m3x = (m0x + m1x) >> 1;
        F24Dot8 m3y = (m0y + m1y) >> 1;
        
        F24Dot8 m4x = (m1x + m2x) >> 1;
        F24Dot8 m4y = (m1y + m2y) >> 1;
        
        F24Dot8 mx = (m3x + m4x) >> 1;
        F24Dot8 my = (m3y + m4y) >> 1;

        Unsafe.SkipInit(out r);
        r[0] = s[0];
        r[1].X = m0x;
        r[1].Y = m0y;
        r[2].X = m3x;
        r[2].Y = m3y;
        r[3].X = mx;
        r[3].Y = my;
        r[4].X = m4x;
        r[4].Y = m4y;
        r[5].X = m2x;
        r[5].Y = m2y;
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
    public static bool CutMonotonicQuadraticAtX(ReadOnlySpan<FloatPoint> quadratic, double x, out double t)
    {
        return CutMonotonicQuadraticAt(
            quadratic[0].X, quadratic[1].X, quadratic[2].X, x, out t);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicQuadraticAtY(ReadOnlySpan<FloatPoint> quadratic, double y, out double t)
    {
        return CutMonotonicQuadraticAt(
            quadratic[0].Y, quadratic[1].Y, quadratic[2].Y, y, out t);
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicCubicAt(out double t, DoubleX4 pts)
    {
        const double Tolerance = 1e-7;

        Unsafe.SkipInit(out t);

        double p0 = pts[0];
        double p3 = pts[3];

        double negative = 0;
        double positive = 0;

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
            double m = (positive + negative) / 2.0;
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

        t = (negative + positive) / 2.0;

        return true;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CutMonotonicCubicAtY(ReadOnlySpan<FloatPoint> pts, double y, out double t)
    {
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsQuadraticFlatEnough(in F24Dot8PointX3 q)
    {
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


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCubicFlatEnough(F24Dot8PointX4 c)
    {
        F24Dot8 tolerance = F24Dot8.F24Dot8_1 >> 1;
        F24Dot8 c2 = new(2);
        F24Dot8 c3 = new(3);
        
        return
            F24Dot8.Abs(c2 * c[0].X - c3 * c[1].X + c[3].X) <= tolerance &&
            F24Dot8.Abs(c2 * c[0].Y - c3 * c[1].Y + c[3].Y) <= tolerance &&
            F24Dot8.Abs(c[0].X - c3 * c[2].X + c2 * c[3].X) <= tolerance &&
            F24Dot8.Abs(c[0].Y - c3 * c[2].Y + c2 * c[3].Y) <= tolerance;
    }

}