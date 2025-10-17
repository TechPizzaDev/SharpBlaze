using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public struct ViewData
{
    public Matrix CoordinateSystemMatrix;
    public FloatPoint Translation;
    public FloatPoint Scale;

    public ViewData()
    {
        Scale = new FloatPoint(1.0);
    }

    public void SetupCoordinateSystem(int width, int height, IntRect bounds)
    {
        (Vector128<double> min, Vector128<double> max) = V128Helper.ConvertToDouble(bounds.AsVector128());

        Vector128<int> isize = Vector128.CreateScalarUnsafe(width).WithElement(1, height);
        Vector128<double> size = V128Helper.ConvertToDoubleLower(isize);

        SetupCoordinateSystem(new FloatPoint(size), new FloatPoint(min), new FloatPoint(max));
    }

    public void SetupCoordinateSystem(FloatPoint size, FloatPoint min, FloatPoint max)
    {
        FloatPoint p = (max - min) * 0.5;
        FloatPoint s = size * 0.5;
        CoordinateSystemMatrix = Matrix.CreateTranslation(s - p);
    }

    public readonly Matrix GetMatrix()
    {
        Matrix m = Matrix.CreateScale(Scale);

        m *= Matrix.CreateTranslation(Translation);

        m *= CoordinateSystemMatrix;

        return m;
    }
}
