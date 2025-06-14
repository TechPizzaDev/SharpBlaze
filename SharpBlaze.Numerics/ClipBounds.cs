using System;
using System.Diagnostics;
using SharpBlaze.Numerics;

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

        Max = new FloatPoint(maxx, maxy);
        FMax = new F24Dot8Point(maxx.ToF24D8(), maxy.ToF24D8());
    }

    public readonly FloatPoint Max;
    public readonly F24Dot8Point FMax;

    // Prevent creating this with empty bounds as this is most likely not an
    // intentional situation.
    [Obsolete]
    public ClipBounds() { }
}
