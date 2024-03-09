using System.Runtime.CompilerServices;

namespace SharpBlaze;

using static Utils;

public partial struct Matrix
{

    /**
     * Constructs matrix as product of two given matrices.
     */
    public Matrix(in Matrix matrix1, in Matrix matrix2)
    {
        m[0][0] = matrix2.m[0][0] * matrix1.m[0][0] + matrix2.m[0][1] *
            matrix1.m[1][0];

        m[0][1] = matrix2.m[0][0] * matrix1.m[0][1] + matrix2.m[0][1] *
            matrix1.m[1][1];

        m[1][0] = matrix2.m[1][0] * matrix1.m[0][0] + matrix2.m[1][1] *
            matrix1.m[1][0];

        m[1][1] = matrix2.m[1][0] * matrix1.m[0][1] + matrix2.m[1][1] *
            matrix1.m[1][1];

        m[2][0] = matrix2.m[2][0] * matrix1.m[0][0] + matrix2.m[2][1] *
            matrix1.m[1][0] + matrix1.m[2][0];

        m[2][1] = matrix2.m[2][0] * matrix1.m[0][1] + matrix2.m[2][1] *
            matrix1.m[1][1] + matrix1.m[2][1];
    }


    /**
     * Constructs translation matrix with given position.
     */
    public static explicit operator Matrix(FloatPoint translation)
    {
        Matrix r;
        Unsafe.SkipInit(out r);
        r.m[0][0] = 1;
        r.m[0][1] = 0;
        r.m[1][0] = 0;
        r.m[1][1] = 1;
        r.m[2][0] = translation.X;
        r.m[2][1] = translation.Y;
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


    public readonly partial bool Invert(ref Matrix result)
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
        double det = GetDeterminant();

        if (FuzzyIsZero(det))
        {
            return Identity;
        }

        return new Matrix(
             m[1][1] / det,
            -m[0][1] / det,
            -m[1][0] / det,
             m[0][0] / det,
            (m[1][0] * m[2][1] - m[1][1] * m[2][0]) / det,
            (m[0][1] * m[2][0] - m[0][0] * m[2][1]) / det);
    }


    public readonly partial FloatRect Map(in FloatRect rect)
    {
        FloatPoint topLeft = Map(rect.MinX, rect.MinY);
        FloatPoint topRight = Map(rect.MaxX, rect.MinY);
        FloatPoint bottomLeft = Map(rect.MinX, rect.MaxY);
        FloatPoint bottomRight = Map(rect.MaxX, rect.MaxY);

        double minX = Min(topLeft.X, Min(topRight.X, Min(bottomLeft.X,
            bottomRight.X)));
        double maxX = Max(topLeft.X, Max(topRight.X, Max(bottomLeft.X,
            bottomRight.X)));
        double minY = Min(topLeft.Y, Min(topRight.Y, Min(bottomLeft.Y,
            bottomRight.Y)));
        double maxY = Max(topLeft.Y, Max(topRight.Y, Max(bottomLeft.Y,
            bottomRight.Y)));

        return new FloatRect(minX, minY, maxX - minX, maxY - minY);
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
            m[0][0] == 1 &&
            m[1][1] == 1 &&
            m[0][1] == 0 &&
            m[1][0] == 0 &&
            m[2][0] == 0 &&
            m[2][1] == 0;
    }


    public readonly partial bool IsEqual(in Matrix matrix)
    {
        return
            FuzzyIsEqual(m[0][0], matrix.m[0][0]) &&
            FuzzyIsEqual(m[0][1], matrix.m[0][1]) &&
            FuzzyIsEqual(m[1][0], matrix.m[1][0]) &&
            FuzzyIsEqual(m[1][1], matrix.m[1][1]) &&
            FuzzyIsEqual(m[2][0], matrix.m[2][0]) &&
            FuzzyIsEqual(m[2][1], matrix.m[2][1]);
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


    public partial void PostMultiply(in Matrix matrix)
    {
        double m00 = m[0][0];
        double m01 = m[0][1];
        double m10 = m[1][0];
        double m11 = m[1][1];

        m[0][0] = matrix.m[0][0] * m00 + matrix.m[0][1] * m10;
        m[0][1] = matrix.m[0][0] * m01 + matrix.m[0][1] * m11;
        m[1][0] = matrix.m[1][0] * m00 + matrix.m[1][1] * m10;
        m[1][1] = matrix.m[1][0] * m01 + matrix.m[1][1] * m11;
        m[2][0] = matrix.m[2][0] * m00 + matrix.m[2][1] * m10 + m[2][0];
        m[2][1] = matrix.m[2][0] * m01 + matrix.m[2][1] * m11 + m[2][1];
    }


    public partial void PreMultiply(in Matrix matrix)
    {
        double m00 = m[0][0];
        double m01 = m[0][1];
        double m10 = m[1][0];
        double m11 = m[1][1];

        m[0][0] = m00 * matrix.m[0][0] + m01 * matrix.m[1][0];
        m[0][1] = m00 * matrix.m[0][1] + m01 * matrix.m[1][1];
        m[1][0] = m10 * matrix.m[0][0] + m11 * matrix.m[1][0];
        m[1][1] = m10 * matrix.m[0][1] + m11 * matrix.m[1][1];
        m[2][0] = m[2][0] * matrix.m[0][0] + m[2][1] * matrix.m[1][0] + matrix.m[2][0];
        m[2][1] = m[2][0] * matrix.m[0][1] + m[2][1] * matrix.m[1][1] + matrix.m[2][1];
    }


    public readonly partial MatrixComplexity DetermineComplexity()
    {
        bool m00 = FuzzyNotEqual(m[0][0], 1.0);
        bool m01 = FuzzyNotZero(m[0][1]);
        bool m10 = FuzzyNotZero(m[1][0]);
        bool m11 = FuzzyNotEqual(m[1][1], 1.0);
        bool m20 = FuzzyNotZero(m[2][0]);
        bool m21 = FuzzyNotZero(m[2][1]);

        bool translation = m20 | m21;
        bool scale = m00 | m11;
        bool complex = m01 | m10;

        const int TranslationBit = 2;
        const int ScaleBit = 1;
        const int ComplexBit = 0;

        int mask =
            ((translation ? 1 : 0) << 2) |
            ((scale ? 1 : 0) << 1) |
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
