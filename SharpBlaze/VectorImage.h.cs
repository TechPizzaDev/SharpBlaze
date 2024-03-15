using System.Diagnostics;

namespace SharpBlaze;


/**
 * Parser and maintainer of vector image.
 */
public unsafe partial class VectorImage
{
    private int mGeometryCount = 0;
    private IntRect mBounds;
    private Geometry* mGeometries = null;


    public int GetGeometryCount()
    {
        return mGeometryCount;
    }

    public IntRect GetBounds()
    {
        return mBounds;
    }


    public Geometry* GetGeometryAt(int index)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index < mGeometryCount);

        return mGeometries + index;
    }


    public Geometry* GetGeometries()
    {
        return mGeometries;
    }
}