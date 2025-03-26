using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public partial struct FloatRect
{
    public FloatRect(double x, double y, double width, double height)
    {
        Min = Vector128.Create(x, y);
        Max = Vector128.Create(x + width, y + height);
    }

    public FloatRect(Vector128<double> min, Vector128<double> max)
    {
        Min = min;
        Max = max;
    }

    public FloatRect(FloatPoint min, FloatPoint max)
    {
        Min = min.AsVector128();
        Max = max.AsVector128();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FloatRect(IntRect r)
    {
        Vector128<int> minMax = r.AsVector128();
        Min = V128Helper.ConvertToDoubleLower(minMax);
        Max = V128Helper.ConvertToDoubleUpper(minMax);
    }

    public Vector128<double> Min;
    public Vector128<double> Max;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasArea()
    {
        return Vector128.LessThanAll(Min, Max);
    }
}