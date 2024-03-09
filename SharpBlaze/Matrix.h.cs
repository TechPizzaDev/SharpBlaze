using System.Runtime.CompilerServices;

namespace SharpBlaze;

using static Utils;


/**
 * Describes how complex 3x2 matrix is.
 */
public enum MatrixComplexity : byte
{

    /**
     * Identity matrix. Transforming point by this matrix will result in
     * identical point.
     */
    Identity = 0,


    /**
     * Matrix only contains translation and no scale or other components.
     */
    TranslationOnly,


    /**
     * Matrix only contains scale, but no translation or other components.
     */
    ScaleOnly,


    /**
     * Matrix contains a combination of translation and scale.
     */
    TranslationScale,


    /**
     * Matrix potentially contains a combination of scale, translation,
     * rotation and skew.
     */
    Complex
}


/**
 * A class encapsulating a 3x2 matrix.
 */
public partial struct Matrix
{
    /**
     * Pre-constructed identity matrix.
     */
    public static Matrix Identity => new();


    /**
     * Creates a translation matrix from the given vector.
     */
    public static partial Matrix CreateTranslation(FloatPoint translation);


    /**
     * Creates a translation matrix from the given x and y values.
     */
    public static partial Matrix CreateTranslation(double x, double y);


    /**
     * Creates a scale matrix from the given vector.
     */
    public static partial Matrix CreateScale(FloatPoint scale);


    /**
     * Creates a scale matrix from the given x and y values.
     */
    public static partial Matrix CreateScale(double x, double y);


    /**
     * Creates scale matrix that from a single scale value which is used as
     * scale factor for both x and y.
     */
    public static partial Matrix CreateScale(double scale);


    /**
     * Creates a skew matrix from the given angles in degrees.
     */
    public static partial Matrix CreateSkew(double degreesX, double degreesY);


    /**
     * Creates a 3x2 rotation matrix using the given rotation in degrees.
     */
    public static partial Matrix CreateRotation(double degrees);


    /**
     * Linearly interpolates from matrix1 to matrix2, based on the third
     * parameter.
     */
    public static partial Matrix Lerp(in Matrix matrix1, in Matrix matrix2,
        double t);


    /**
     * Returns whether the matrix is the identity matrix.
     */
    public readonly partial bool IsIdentity();


    /**
     * Calculates the determinant for this matrix.
     */
    public readonly partial double GetDeterminant();


    /**
     * Attempts to invert this matrix. If the operation succeeds, the inverted
     * matrix is stored in the result parameter and true is returned.
     * Otherwise, identity matrix is stored in the result parameter and false
     * is returned.
     */
    public readonly partial bool Invert(ref Matrix result);


    /**
     * Attempts to invert this matrix. If the operation succeeds, the inverted
     * matrix is returned. Otherwise, identity matrix is returned.
     */
    public readonly partial Matrix Inverse();


    /**
     * Maps given point by this matrix.
     */
    public readonly partial FloatPoint Map(FloatPoint point);


    /**
     * Maps given point by this matrix.
     */
    public readonly partial FloatPoint Map(double x, double y);


    /**
     * Maps given rectangle by this matrix.
     */
    public readonly partial FloatRect Map(in FloatRect rect);


    /**
     * Maps all four corner points of a given rectangle and returns a new
     * rectangle which fully contains transformed points.
     */
    public readonly partial IntRect MapBoundingRect(in IntRect rect);


    /**
     * Post-multiplies this matrix by a given matrix.
     */
    public partial void PostMultiply(in Matrix matrix);


    /**
     * Pre-multiplies this matrix by a given matrix.
     */
    public partial void PreMultiply(in Matrix matrix);


    /**
     * Returns M11 element of matrix.
     */
    public readonly partial double M11();


    /**
     * Sets M11 element of matrix.
     */
    public partial void SetM11(double value);


    /**
     * Returns M12 element of matrix.
     */
    public readonly partial double M12();


    /**
     * Sets M12 element of matrix.
     */
    public partial void SetM12(double value);


    /**
     * Returns M21 element of matrix.
     */
    public readonly partial double M21();


    /**
     * Sets M21 element of matrix.
     */
    public partial void SetM21(double value);


    /**
     * Returns M22 element of matrix.
     */
    public readonly partial double M22();


    /**
     * Sets M22 element of matrix.
     */
    public partial void SetM22(double value);


    /**
     * Returns M31 element of matrix.
     */
    public readonly partial double M31();


    /**
     * Sets M31 element of matrix.
     */
    public partial void SetM31(double value);


    /**
     * Returns M32 element of matrix.
     */
    public readonly partial double M32();


    /**
     * Sets M32 element of matrix.
     */
    public partial void SetM32(double value);


    /**
     * Returns true if this matrix contains the same values as a given matrix.
     */
    public readonly partial bool IsEqual(in Matrix matrix);


    /**
     * Returns translation components of this matrix as point.
     */
    public readonly partial FloatPoint GetTranslation();


    /**
     * Pre-multiplies this matrix by translation matrix constructed with given
     * translation values.
     */
    public partial void PreTranslate(FloatPoint translation);


    /**
     * Post-multiplies this matrix by translation matrix constructed with
     * given translation values.
     */
    public partial void PostTranslate(FloatPoint translation);


    /**
     * Pre-multiplies this matrix by translation matrix constructed with given
     * translation values.
     */
    public partial void PreTranslate(double x, double y);


    /**
     * Post-multiplies this matrix by translation matrix constructed with
     * given translation values.
     */
    public partial void PostTranslate(double x, double y);


    /**
     * Pre-multiplies this matrix by scale matrix constructed with given scale
     * values.
     */
    public partial void PreScale(FloatPoint scale);


    /**
     * Post-multiplies this matrix by scale matrix constructed with given
     * scale values.
     */
    public partial void PostScale(FloatPoint scale);


    /**
     * Pre-multiplies this matrix by scale matrix constructed with given scale
     * values.
     */
    public partial void PreScale(double x, double y);


    /**
     * Post-multiplies this matrix by scale matrix constructed with given
     * scale values.
     */
    public partial void PostScale(double x, double y);


    /**
     * Pre-multiplies this matrix by scale matrix constructed with given scale
     * value.
     */
    public partial void PreScale(double scale);


    /**
     * Post-multiplies this matrix by scale matrix constructed with given
     * scale value.
     */
    public partial void PostScale(double scale);


    /**
     * Pre-multiplies this matrix with rotation matrix constructed with given
     * rotation in degrees.
     */
    public partial void PreRotate(double degrees);


    /**
     * Post-multiplies this matrix with rotation matrix constructed with given
     * rotation in degrees.
     */
    public partial void PostRotate(double degrees);


    /**
     * Determine matrix complexity.
     */
    public readonly partial MatrixComplexity DetermineComplexity();


    [InlineArray(2)]
    private struct Row
    {
        private double _e0;
    }

    [InlineArray(3)]
    private struct Rows
    {
        private Row _e0;
    }

    private Rows m;

    

    /**
     * Constructs identity 3x2 matrix.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix()
    {
        m[0][0] = 1;
        m[0][1] = 0;
        m[1][0] = 0;
        m[1][1] = 1;
        m[2][0] = 0;
        m[2][1] = 0;
    }


    /**
     * Constructs a copy of a given 3x2 matrix.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix(in Matrix matrix)
    {
        m[0][0] = matrix.m[0][0];
        m[0][1] = matrix.m[0][1];
        m[1][0] = matrix.m[1][0];
        m[1][1] = matrix.m[1][1];
        m[2][0] = matrix.m[2][0];
        m[2][1] = matrix.m[2][1];
    }


    /**
     * Contructs 3x2 matrix from given components.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix(double m11, double m12, double m21,
        double m22, double m31, double m32)
    {
        m[0][0] = m11;
        m[0][1] = m12;
        m[1][0] = m21;
        m[1][1] = m22;
        m[2][0] = m31;
        m[2][1] = m32;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial Matrix CreateTranslation(FloatPoint translation)
    {
        return CreateTranslation(translation.X, translation.Y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial Matrix CreateTranslation(double x, double y)
    {
        return new Matrix(1, 0, 0, 1, x, y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial Matrix CreateScale(FloatPoint scale)
    {
        return CreateScale(scale.X, scale.Y);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial Matrix CreateScale(double x, double y)
    {
        return new Matrix(x, 0, 0, y, 0, 0);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial Matrix CreateScale(double scale)
    {
        return new Matrix(scale, 0, 0, scale, 0, 0);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static partial Matrix CreateSkew(double degreesX, double degreesY)
    {
        if (FuzzyIsZero(degreesX) && FuzzyIsZero(degreesY))
        {
            return Identity;
        }

        double xTan = Tan(Deg2Rad(degreesX));
        double yTan = Tan(Deg2Rad(degreesY));

        return new Matrix(1.0, yTan, xTan, 1.0, 0.0, 0.0);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double GetDeterminant()
    {
        return m[0][0] * m[1][1] - m[0][1] * m[1][0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial FloatPoint Map(FloatPoint point)
    {
        return new FloatPoint(
            m[0][0] * point.X + m[1][0] * point.Y + m[2][0],
            m[0][1] * point.X + m[1][1] * point.Y + m[2][1]
        );
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial FloatPoint Map(double x, double y)
    {
        return new FloatPoint(
            m[0][0] * x + m[1][0] * y + m[2][0],
            m[0][1] * x + m[1][1] * y + m[2][1]
        );
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M11()
    {
        return m[0][0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void SetM11(double value)
    {
        m[0][0] = value;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M12()
    {
        return m[0][1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void SetM12(double value)
    {
        m[0][1] = value;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M21()
    {
        return m[1][0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void SetM21(double value)
    {
        m[1][0] = value;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M22()
    {
        return m[1][1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void SetM22(double value)
    {
        m[1][1] = value;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M31()
    {
        return m[2][0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void SetM31(double value)
    {
        m[2][0] = value;
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M32()
    {
        return m[2][1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial void SetM32(double value)
    {
        m[2][1] = value;
    }

   
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Matrix self, in Matrix matrix)
    {
        return self.IsEqual(matrix);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Matrix self, in Matrix matrix)
    {
        return !self.IsEqual(matrix);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial FloatPoint GetTranslation()
    {
        return new FloatPoint(
            m[2][0],
            m[2][1]
        );
    }

}