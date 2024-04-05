using System;

namespace SharpBlaze;


public partial struct FloatRect
{
    public readonly IntRect ToExpandedIntRect()
    {
        int minx = (int) (Math.Floor(Min.X));
        int miny = (int) (Math.Floor(Min.Y));
        int maxx = (int) (Math.Ceiling(Max.X));
        int maxy = (int) (Math.Ceiling(Max.Y));

        return IntRect.FromMinMax(minx, miny, maxx, maxy);
    }
}