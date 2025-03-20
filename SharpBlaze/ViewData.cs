using System.Runtime.CompilerServices;
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
        Scale = new(1);
    }

    public void SetupCoordinateSystem(int width, int height, IntRect bounds)
    {
        (Vector128<double> min, Vector128<double> max) = V128Helper.Widen(bounds.AsVector128());

        Unsafe.SkipInit(out Vector128<int> isize);
        isize = isize.WithElement(0, width);
        isize = isize.WithElement(1, height);
        Vector128<double> size = V128Helper.WidenLower(isize);

        SetupCoordinateSystem(size, min, max);
    }

    public void SetupCoordinateSystem(Vector128<double> size, Vector128<double> min, Vector128<double> max)
    {
        Vector128<double> p = (max - min) * 0.5;
        Vector128<double> s = size * 0.5;
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
