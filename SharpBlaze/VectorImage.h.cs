using System;

namespace SharpBlaze;


/**
 * Parser and maintainer of vector image.
 */
public unsafe partial class VectorImage
{
    private IntRect mBounds;
    private ReadOnlyMemory<Geometry> mGeometries = null;

    public IntRect GetBounds()
    {
        return mBounds;
    }
    
    public ReadOnlyMemory<Geometry> GetGeometries()
    {
        return mGeometries;
    }
}