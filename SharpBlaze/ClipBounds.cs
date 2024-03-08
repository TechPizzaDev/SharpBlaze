using System;
using System.Diagnostics;

namespace SharpBlaze;


/**
 * Keeps maximum point for clipping.
 */
public struct ClipBounds
{
    public ClipBounds(int maxx, int maxy)
    {
        Debug.Assert(maxx > 0);
        Debug.Assert(maxy > 0);

        MaxX = maxx;
        MaxY = maxy;
        FMax = new F24Dot8Point(maxx << 8, maxy << 8);
    }

    public readonly double MaxX;
    public readonly double MaxY;
    public readonly F24Dot8Point FMax;

    // Prevent creating this with empty bounds as this is most likely not an
    // intentional situation.
    [Obsolete]
    public ClipBounds() { }
}
