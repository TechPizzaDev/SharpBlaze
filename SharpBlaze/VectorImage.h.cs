using System.Diagnostics;

namespace SharpBlaze;


/**
 * Parser and maintainer of vector image.
 */
public unsafe partial class VectorImage
{
    public partial void Parse(byte* binary, ulong length);
    public partial int GetGeometryCount();
    public partial IntRect GetBounds();
    public partial Geometry* GetGeometryAt(int index);
    public partial Geometry* GetGeometries();

    private partial void Free();

    private int mGeometryCount = 0;
    private IntRect mBounds;
    private Geometry* mGeometries = null;


    public partial int GetGeometryCount()
    {
        return mGeometryCount;
    }

    public partial IntRect GetBounds()
    {
        return mBounds;
    }


    public partial Geometry* GetGeometryAt(int index)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index < mGeometryCount);

        return mGeometries + index;
    }


    public partial Geometry* GetGeometries()
    {
        return mGeometries;
    }
}