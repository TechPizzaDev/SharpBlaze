using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

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
    TranslationOnly = 1,


    /**
     * Matrix only contains scale, but no translation or other components.
     */
    ScaleOnly = 2,


    /**
     * Matrix contains a combination of translation and scale.
     */
    TranslationScale = 3,


    /**
     * Matrix potentially contains a combination of scale, translation,
     * rotation and skew.
     */
    Complex = 4
}


/**
 * A class encapsulating a 3x2 matrix.
 */
public readonly partial struct Matrix
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
    public readonly partial bool Invert(out Matrix result);


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
     * Returns M11 element of matrix.
     */
    public readonly partial double M11();


    /**
     * Returns M12 element of matrix.
     */
    public readonly partial double M12();


    /**
     * Returns M21 element of matrix.
     */
    public readonly partial double M21();


    /**
     * Returns M22 element of matrix.
     */
    public readonly partial double M22();


    /**
     * Returns M31 element of matrix.
     */
    public readonly partial double M31();


    /**
     * Returns M32 element of matrix.
     */
    public readonly partial double M32();


    /**
     * Returns true if this matrix contains the same values as a given matrix.
     */
    public readonly partial bool IsEqual(in Matrix matrix);


    /**
     * Returns translation components of this matrix as point.
     */
    public readonly partial FloatPoint GetTranslation();


    /**
     * Determine matrix complexity.
     */
    public readonly partial MatrixComplexity DetermineComplexity();


    [InlineArray(3)]
    private struct Rows
    {
        private Vector128<double> _e0;
    }

    private readonly Rows m;



    /**
     * Constructs identity 3x2 matrix.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix()
    {
        m[0] = Vector128.Create(1.0, 0);
        m[1] = Vector128.Create(0.0, 1);
        m[2] = Vector128<double>.Zero;
    }


    /**
     * Contructs 3x2 matrix from given vectors.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix(Vector128<double> m1, Vector128<double> m2, Vector128<double> m3)
    {
        m[0] = m1;
        m[1] = m2;
        m[2] = m3;
    }


    /**
     * Contructs 3x2 matrix from given components.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix(double m11, double m12, double m21,
        double m22, double m31, double m32)
    {
        m[0] = Vector128.Create(m11, m12);
        m[1] = Vector128.Create(m21, m22);
        m[2] = Vector128.Create(m31, m32);
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

        double xTan = Math.Tan(Deg2Rad(degreesX));
        double yTan = Math.Tan(Deg2Rad(degreesY));

        return new Matrix(1.0, yTan, xTan, 1.0, 0.0, 0.0);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double GetDeterminant()
    {
        Vector128<double> m0 = m[0];
        Vector128<double> m1 = Vector128.Shuffle(m[1], Vector128.Create(1, 0));
        Vector128<double> d = m0 * m1;
        return d[0] - d[1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial FloatPoint Map(FloatPoint point)
    {
        return new FloatPoint(
            m[0] * Vector128.Create(point.X) + 
            m[1] * Vector128.Create(point.Y) + 
            m[2]);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial FloatPoint Map(double x, double y)
    {
        return Map(new FloatPoint(x, y));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<double> M1()
    {
        return m[0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M11()
    {
        return m[0][0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M12()
    {
        return m[0][1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<double> M2()
    {
        return m[1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M21()
    {
        return m[1][0];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M22()
    {
        return m[1][1];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<double> M3()
    {
        return m[2];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M31()
    {
        return m[2][0];
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial double M32()
    {
        return m[2][1];
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
        return new FloatPoint(m[2]);
    }

}