using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

using static Utils;

public static partial class CurveUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AcceptRoot(out double t, double root)
    {
        double c = Clamp(root, 0.0, 1.0);
        t = c;

        if (Math.Abs(root - c) <= DBL_EPSILON)
        {
            return 1;
        }
        return 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial int FindQuadraticRoots(double a, double b, double c, out DoubleX2 roots)
    {
        Unsafe.SkipInit(out roots);

        double delta = b * b - 4.0 * a * c;

        if (delta < 0.0)
        {
            return 0;
        }

        if (delta > 0.0)
        {
            double d = Math.Sqrt(delta);
            double q = -0.5 * (b + (b < 0.0 ? -d : d));
            double rv0 = q / a;
            double rv1 = c / q;

            if (FuzzyIsEqual(rv0, rv1))
            {
                return AcceptRoot(out roots[0], rv0);
            }

            double r0 = Math.Min(rv0, rv1);
            double r1 = Math.Max(rv0, rv1);

            int n = AcceptRoot(out roots[0], r0);

            n += AcceptRoot(out roots[n], r1);

            return n;
        }

        if (a != 0)
        {
            return AcceptRoot(out roots[0], -0.5 * b / a);
        }

        return 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AcceptRootWithin(out double t, double root)
    {
        Unsafe.SkipInit(out t);

        if (root <= DBL_EPSILON || root >= (1.0 - DBL_EPSILON))
        {
            return 0;
        }

        t = root;

        return 1;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int FindQuadraticRootsWithin(double a, double b, double c, out DoubleX2 roots)
    {
        Unsafe.SkipInit(out roots);

        double delta = b * b - 4.0 * a * c;

        if (delta < 0.0)
        {
            return 0;
        }

        if (delta > 0.0)
        {
            double d = Math.Sqrt(delta);
            double q = -0.5 * (b + (b < 0.0 ? -d : d));
            double rv0 = q / a;
            double rv1 = c / q;

            if (FuzzyIsEqual(rv0, rv1))
            {
                return AcceptRootWithin(out roots[0], rv0);
            }

            double r0 = rv0 <= rv1 ? rv0 : rv1;
            double r1 = rv0 <= rv1 ? rv1 : rv0;

            int n = AcceptRootWithin(out roots[0], r0);

            n += AcceptRootWithin(out roots[n], r1);

            return n;
        }

        if (a != 0)
        {
            return AcceptRootWithin(out roots[0], -0.5 * b / a);
        }

        return 0;
    }


    static bool FindQuadraticExtrema(double a, double b, double c, out double t)
    {
        Unsafe.SkipInit(out t);

        double aMinusB = a - b;
        double d = aMinusB - b + c;

        if (aMinusB == 0 || d == 0)
        {
            return false;
        }

        double tv = aMinusB / d;

        Debug.Assert(double.IsFinite(tv));

        if (tv <= 1e-15 || tv >= (1.0 - 1e-15))
        {
            return false;
        }

        t = tv;

        return true;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int FindCubicExtrema(double a, double b, double c, double d, out DoubleX2 t)
    {
        double A = d - a + 3.0 * (b - c);
        double B = 2.0 * (a - b - b + c);
        double C = b - a;

        return FindQuadraticRootsWithin(A, B, C, out t);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    public static partial int CutCubicAtYExtrema(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst)
    {
        int n = FindCubicExtrema(src[0].Y, src[1].Y, src[2].Y, src[3].Y, out DoubleX2 t);

        if (n == 1)
        {
            // One root, two output curves.

            Debug.Assert(t[0] > 0.0);
            Debug.Assert(t[0] < 1.0);

            CutCubicAt(src, dst, t[0]);
            
            // Make sure curve tangents at extrema are horizontal.
            double y = dst[3].Y;

            dst[2].Y = y;
            dst[4].Y = y;

            return 2;
        }

        if (n == 2)
        {
            // Two roots, three output curves.

            // Expect sorted roots from FindCubicExtrema.
            Debug.Assert(t[0] < t[1]);
            Debug.Assert(t[0] > 0.0);
            Debug.Assert(t[0] < 1.0);
            Debug.Assert(t[1] > 0.0);
            Debug.Assert(t[1] < 1.0);

            CutCubicAt(src, dst, t[0]);

            double d = 1.0 - t[0];

            Debug.Assert(double.IsFinite(d));

            // Clamp to make sure we don't go out of range due to limited precision.
            double tt = Clamp((t[1] - t[0]) / d, 0.0, 1.0);

            CutCubicAt(dst.Slice(3, 4), dst.Slice(3, 7), tt);

            // Make sure curve tangents at extremas are horizontal.
            double y0 = dst[3].Y;
            double y1 = dst[6].Y;

            dst[2].Y = y0;
            dst[4].Y = y0;
            dst[5].Y = y1;
            dst[7].Y = y1;

            return 3;
        }

        Debug.Assert(n == 0);

        dst[0] = src[0];
        dst[1] = src[1];
        dst[2] = src[2];
        dst[3] = src[3];

        return 1;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial int CutCubicAtXExtrema(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst)
    {
        int n = FindCubicExtrema(src[0].X, src[1].X, src[2].X, src[3].X, out DoubleX2 t);

        if (n == 1)
        {
            // One root, two output curves.

            Debug.Assert(t[0] > 0.0);
            Debug.Assert(t[0] < 1.0);

            CutCubicAt(src, dst, t[0]);

            // Make sure curve tangents at extrema are horizontal.
            double x = dst[3].X;

            dst[2].X = x;
            dst[4].X = x;

            return 2;
        }

        if (n == 2)
        {
            // Two roots, three output curves.

            // Expect sorted roots from FindCubicExtrema.
            Debug.Assert(t[0] < t[1]);
            Debug.Assert(t[0] > 0.0);
            Debug.Assert(t[0] < 1.0);
            Debug.Assert(t[1] > 0.0);
            Debug.Assert(t[1] < 1.0);

            CutCubicAt(src, dst, t[0]);

            double d = 1.0 - t[0];

            Debug.Assert(double.IsFinite(d));

            // Clamp to make sure we don't go out of range due to limited precision.
            double tt = Clamp((t[1] - t[0]) / d, 0.0, 1.0);

            CutCubicAt(dst.Slice(3, 4), dst.Slice(3, 7), tt);

            // Make sure curve tangents at extremas are horizontal.
            double x0 = dst[3].X;
            double x1 = dst[6].X;

            dst[2].X = x0;
            dst[4].X = x0;
            dst[5].X = x1;
            dst[7].X = x1;

            return 3;
        }

        Debug.Assert(n == 0);

        dst[0] = src[0];
        dst[1] = src[1];
        dst[2] = src[2];
        dst[3] = src[3];

        return 1;
    }


    static bool IsQuadraticMonotonic(double a, double b, double c)
    {
        double ab = a - b;
        double bc = b - c;

        if (ab < 0)
        {
            bc = -bc;
        }

        return ab != 0 && bc >= 0;
    }


    public static partial int CutQuadraticAtYExtrema(
        ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst)
    {
        double a = src[0].Y;
        double b = src[1].Y;
        double c = src[2].Y;

        if (IsQuadraticMonotonic(a, b, c))
        {
            dst[0] = src[0];
            dst[1] = src[1];
            dst[2] = src[2];

            return 1;
        }

        if (FindQuadraticExtrema(a, b, c, out double t))
        {
            CutQuadraticAt(src, dst, t);

            double y = dst[2].Y;

            dst[1].Y = y;
            dst[3].Y = y;

            return 2;
        }

        dst[0] = new FloatPoint(
            src[0].X,
            a
        );

        dst[1] = new FloatPoint(
            src[1].X,
            Math.Abs(a - b) < Math.Abs(b - c) ? a : c
        );

        dst[2] = new FloatPoint(
            src[2].X,
            c
        );

        return 1;
    }


    public static partial int CutQuadraticAtXExtrema(
        ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst)
    {
        double a = src[0].X;
        double b = src[1].X;
        double c = src[2].X;

        if (IsQuadraticMonotonic(a, b, c))
        {
            dst[0] = src[0];
            dst[1] = src[1];
            dst[2] = src[2];

            return 1;
        }

        if (FindQuadraticExtrema(a, b, c, out double t))
        {
            CutQuadraticAt(src, dst, t);

            double x = dst[2].X;

            dst[1].X = x;
            dst[3].X = x;

            return 2;
        }

        dst[0] = new FloatPoint(
            a,
            src[0].Y
        );

        dst[1] = new FloatPoint(
            Math.Abs(a - b) < Math.Abs(b - c) ? a : c,
            src[1].Y
        );

        dst[2] = new FloatPoint(
            c,
            src[2].Y
        );

        return 1;
    }
}
