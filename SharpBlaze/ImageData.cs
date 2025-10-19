using System.Diagnostics;

namespace SharpBlaze;

/**
 * A simple struct which keeps a pointer to image data and associated
 * properties. It does not allocate or free any memory.
 */
public unsafe readonly struct ImageData
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
     * @param stride Byte stride. Must be at least width × bpp.
     */
    public ImageData(void* data, int width, int height, nint stride, int bytesPerPixel)
    {
        Data = data;
        Width = width;
        Height = height;
        Stride = stride;
        BytesPerPixel = bytesPerPixel;

        Debug.Assert(width > 0);
        Debug.Assert(height > 0);

        // Do not assume any specific bpp, but assume it is at least 1 byte
        // per pixel.
        Debug.Assert(stride >= (nint) width * bytesPerPixel);
    }

    public readonly void* Data;
    public readonly int Width;
    public readonly int Height;
    public readonly nint Stride;
    public readonly int BytesPerPixel;

    public IntRect Bounds => new(0, 0, Width, Height);

    public Span2D<T> GetSpan2D<T>()
        where T : unmanaged
    {
        ulong width = (uint) Width * (ulong) (uint) BytesPerPixel / (ulong) sizeof(T);

        return new Span2D<T>((T*) Data, checked((int) width), Height, Stride);
    }
}
