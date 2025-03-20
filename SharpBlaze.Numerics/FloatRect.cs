using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public partial struct FloatRect
{
    public readonly IntRect ToExpandedIntRect()
    {
        Vector128<double> rmin = Vector128.Floor(Min);
        Vector128<double> rmax = Vector128.Ceiling(Max);
        return IntRect.FromMinMax(V128Helper.Narrow(rmin, rmax));
    }
}