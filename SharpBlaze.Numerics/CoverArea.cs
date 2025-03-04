
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public struct CoverArea
{
    public int Delta;
    public int Area;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CoverArea(int delta, int area)
    {
        Delta = delta;
        Area = area;
    }
}
