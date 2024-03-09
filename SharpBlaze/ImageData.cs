using System.Diagnostics;

namespace SharpBlaze;


/**
 * A simple struct which keeps a pointer to image data and associated
 * properties. It does not allocate or free any memory.
 */
public unsafe struct ImageData
{

    /**
     * Construct image data.
     *
     * @param d Image data. It will be assigned, not copied. And it will not
     * be deallocated. This pointer must point to valid memory place as long
     * as image data struct is around.
     *
     * @param width Width in pixels. Must be at least 1.
     *
     * @param height Height in pixels. Must be at least 1.
     *
     * @param bytesPerRow Byte stride. Must be at least width × bpp.
     */
    public ImageData(byte* d, int width, int height,
         int bytesPerRow)
    {
        Data = d;
        Width = width;
        Height = height;
        BytesPerRow = bytesPerRow;

        Debug.Assert(width > 0);
        Debug.Assert(height > 0);

        // Do not assume any specific bpp, but assume it is at least 1 byte
        // per pixel.
        Debug.Assert(bytesPerRow >= width);
    }

    public byte* Data = null;
    public readonly int Width = 0;
    public readonly int Height = 0;
    public readonly int BytesPerRow = 0;
}