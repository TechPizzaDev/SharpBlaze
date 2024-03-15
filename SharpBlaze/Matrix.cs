using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

using static Utils;

public partial struct Matrix
{

    /**
     * Constructs matrix as product of two given matrices.
     */
    public Matrix(in Matrix matrix1, in Matrix matrix2)
    {
        m[0] = Vector128.Create(
            matrix2.m[0][0] * matrix1.m[0][0] + matrix2.m[0][1] * matrix1.m[1][0],
            matrix2.m[0][0] * matrix1.m[0][1] + matrix2.m[0][1] * matrix1.m[1][1]);

        m[1] = Vector128.Create(
            matrix2.m[1][0] * matrix1.m[0][0] + matrix2.m[1][1] * matrix1.m[1][0],
            matrix2.m[1][0] * matrix1.m[0][1] + matrix2.m[1][1] * matrix1.m[1][1]);

        m[2] = Vector128.Create(
            matrix2.m[2][0] * matrix1.m[0][0] + matrix2.m[2][1] * matrix1.m[1][0] + matrix1.m[2][0],
            matrix2.m[2][0] * matrix1.m[0][1] + matrix2.m[2][1] * matrix1.m[1][1] + matrix1.m[2][1]);
    }


    /**
     * Constructs translation matrix with given position.
     */
    public static explicit operator Matrix(FloatPoint translation)
    {
        Matrix r;
        Unsafe.SkipInit(out r);
        r.m[0] = Vector128.Create(1.0, 0);
        r.m[1] = Vector128.Create(0.0, 1);
        r.m[2] = Vector128.Create(translation.X, translation.Y);
        return r;
    }


    public static partial Matrix CreateRotation(double degrees)
    {
        if (FuzzyIsZero(degrees))
        {
            return Identity;
        }

        double c = 0;
        double s = 0;

        if (degrees == 90.0 || degrees == -270.0)
        {
            s = 1;
        }
        else if (degrees == 180.0 || degrees == -180.0)
        {
            c = -1;
        }
        else if (degrees == -90.0 || degrees == 270.0)
        {
            s = -1;
        }
        else
        {
            // Arbitrary rotation.
            double radians = Deg2Rad(degrees);

            c = Cos(radians);
            s = Sin(radians);
        }

        return new Matrix(c, s, -s, c, 0, 0);
    }


    public static partial Matrix Lerp(in Matrix matrix1, in Matrix matrix2,
        double t)
    {
        return new Matrix(
            matrix1.m[0][0] + (matrix2.m[0][0] - matrix1.m[0][0]) * t,
            matrix1.m[0][1] + (matrix2.m[0][1] - matrix1.m[0][1]) * t,
            matrix1.m[1][0] + (matrix2.m[1][0] - matrix1.m[1][0]) * t,
            matrix1.m[1][1] + (matrix2.m[1][1] - matrix1.m[1][1]) * t,
            matrix1.m[2][0] + (matrix2.m[2][0] - matrix1.m[2][0]) * t,
            matrix1.m[2][1] + (matrix2.m[2][1] - matrix1.m[2][1]) * t);
    }


    public readonly partial bool Invert(out Matrix result)
    {
        double det = GetDeterminant();

        if (FuzzyIsZero(det))
        {
            result = Identity;
            return false;
        }

        result = new Matrix(
             m[1][1] / det,
            -m[0][1] / det,
            -m[1][0] / det,
             m[0][0] / det,
            (m[1][0] * m[2][1] - m[1][1] * m[2][0]) / det,
            (m[0][1] * m[2][0] - m[0][0] * m[2][1]) / det);

        return true;
    }


    public readonly partial Matrix Inverse()
    {
        Invert(out Matrix result);
        return result;
    }


    public readonly partial FloatRect Map(in FloatRect rect)
    {
        Vector128<double> topLeft = Map(rect.Min).AsVector128();
        Vector128<double> topRight = Map(rect.Max.X, rect.Min.Y).AsVector128();
        Vector128<double> bottomLeft = Map(rect.Min.X, rect.Max.Y).AsVector128();
        Vector128<double> bottomRight = Map(rect.Max).AsVector128();

        Vector128<double> min = Vector128.Min(topLeft, Vector128.Min(topRight, Vector128.Min(bottomLeft, bottomRight)));
        Vector128<double> max = Vector128.Max(topLeft, Vector128.Max(topRight, Vector128.Max(bottomLeft, bottomRight)));

        return new FloatRect(new FloatPoint(min), new FloatPoint(max));
    }


    public readonly partial IntRect MapBoundingRect(in IntRect rect)
    {
        FloatRect r = Map(new FloatRect(rect));

        return r.ToExpandedIntRect();
    }


    public readonly partial bool IsIdentity()
    {
        // Look at diagonal elements first to return if scale is not 1.
        return
            Vector128.EqualsAll(m[0], Vector128.Create(1.0, 0)) &&
            Vector128.EqualsAll(m[1], Vector128.Create(0, 1.0)) &&
            Vector128.EqualsAll(m[2], Vector128<double>.Zero);
    }


    public readonly partial bool IsEqual(in Matrix matrix)
    {
        return
            Vector128.EqualsAll(FuzzyIsEqual(m[0], matrix.m[0]), Vector128<double>.AllBitsSet) &&
            Vector128.EqualsAll(FuzzyIsEqual(m[1], matrix.m[1]), Vector128<double>.AllBitsSet) &&
            Vector128.EqualsAll(FuzzyIsEqual(m[2], matrix.m[2]), Vector128<double>.AllBitsSet);
    }


    public partial void PreTranslate(FloatPoint translation)
    {
        PreMultiply((Matrix) (translation));
    }


    public partial void PostTranslate(FloatPoint translation)
    {
        PostMultiply((Matrix) (translation));
    }


    public partial void PreTranslate(double x, double y)
    {
        PreTranslate(new FloatPoint(
            x, y
        ));
    }


    public partial void PostTranslate(double x, double y)
    {
        PostTranslate(new FloatPoint(
            x, y
        ));
    }


    public partial void PreScale(FloatPoint scale)
    {
        PreMultiply(CreateScale(scale));
    }


    public partial void PostScale(FloatPoint scale)
    {
        PostMultiply(CreateScale(scale));
    }


    public partial void PreScale(double x, double y)
    {
        PreScale(new FloatPoint(
            x, y
        ));
    }


    public partial void PostScale(double x, double y)
    {
        PostScale(new FloatPoint(
            x, y
        ));
    }


    public partial void PreScale(double scale)
    {
        PreScale(new FloatPoint(
            scale, scale
        ));
    }


    public partial void PostScale(double scale)
    {
        PostScale(new FloatPoint(
            scale, scale
        ));
    }


    public partial void PreRotate(double degrees)
    {
        PreMultiply(CreateRotation(degrees));
    }


    public partial void PostRotate(double degrees)
    {
        PostMultiply(CreateRotation(degrees));
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void PostMultiply(in Matrix matrix)
    {
        double m00 = m[0][0];
        double m01 = m[0][1];
        double m10 = m[1][0];
        double m11 = m[1][1];

        m[0] = Vector128.Create(
            matrix.m[0][0] * m00 + matrix.m[0][1] * m10,
            matrix.m[0][0] * m01 + matrix.m[0][1] * m11);
        m[1] = Vector128.Create(
            matrix.m[1][0] * m00 + matrix.m[1][1] * m10,
            matrix.m[1][0] * m01 + matrix.m[1][1] * m11);
        m[2] = Vector128.Create(
            matrix.m[2][0] * m00 + matrix.m[2][1] * m10 + m[2][0],
            matrix.m[2][0] * m01 + matrix.m[2][1] * m11 + m[2][1]);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void PreMultiply(in Matrix matrix)
    {
        Vector128<double> o0 = matrix.m[0];
        Vector128<double> o1 = matrix.m[1];

        Vector128<double> m00 = Vector128.Create(m[0][0]);
        Vector128<double> m01 = Vector128.Create(m[0][1]);
        m[0] = m00 * o0 + m01 * o1;

        Vector128<double> m10 = Vector128.Create(m[1][0]);
        Vector128<double> m11 = Vector128.Create(m[1][1]);
        m[1] = m10 * o0 + m11 * o1;

        Vector128<double> m20 = Vector128.Create(m[2][0]);
        Vector128<double> m21 = Vector128.Create(m[2][1]);
        m[2] = m20 * o0 + m21 * o1 + matrix.m[2];
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial MatrixComplexity DetermineComplexity()
    {
        Vector128<double> m0 = FuzzyNotEqual(m[0], Vector128.Create(1.0, 0));
        Vector128<double> m1 = FuzzyNotEqual(m[1], Vector128.Create(0, 1.0));
        uint m01 = (m0 | Vector128.Shuffle(m1, Vector128.Create(1, 0))).ExtractMostSignificantBits();
        bool m2 = FuzzyNotZero(m[2]);

        bool translation = m2;
        bool scale = (m01 & 0b01) != 0;
        bool complex = (m01 & 0b10) != 0;

        const int TranslationBit = 2;
        const int ScaleBit = 1;
        const int ComplexBit = 0;

        int mask =
            ((translation ? 1 : 0) << TranslationBit) |
            ((scale ? 1 : 0) << ScaleBit) |
            ((complex ? 1 : 0) << ComplexBit);

        switch (mask)
        {
            case 0:
                return MatrixComplexity.Identity;
            case (1 << TranslationBit):
                return MatrixComplexity.TranslationOnly;
            case (1 << ScaleBit):
                return MatrixComplexity.ScaleOnly;
            case ((1 << TranslationBit) | (1 << ScaleBit)):
                return MatrixComplexity.TranslationScale;
            default:
                break;
        }

        return MatrixComplexity.Complex;
    }
}
