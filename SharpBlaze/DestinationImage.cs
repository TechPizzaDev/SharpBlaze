using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpBlaze;


/**
 * A helper class for managing an image to draw on.
 */
public unsafe partial class DestinationImage<T>
    where T : unmanaged, ITileDescriptor
{
    public DestinationImage()
    {
    }

    public partial IntSize UpdateSize(IntSize size);

    public partial void ClearImage();

    public partial void DrawImage(VectorImage image, in Matrix matrix);

    public partial IntSize GetImageSize();
    public partial int GetImageWidth();
    public partial int GetImageHeight();
    public partial byte* GetImageData();
    public partial int GetBytesPerRow();

    private byte* mImageData = null;
    private IntSize mImageSize;
    private int mBytesPerRow = 0;
    private int mImageDataSize = 0;
    private Threads mThreads = new();


    ~DestinationImage()
    {
        NativeMemory.Free(mImageData);
    }


    public partial IntSize UpdateSize(IntSize size)
    {
        Debug.Assert(size.Width > 0);
        Debug.Assert(size.Height > 0);

        // Round-up width to tile width.
        TileIndex w = Linearizer.CalculateColumnCount<T>(size.Width) * (TileIndex) T.TileW;

        // Calculate how many bytes are required for the image.
        int bytes = (int) w * 4 * size.Height;

        if (mImageDataSize < bytes)
        {
            const int ImageSizeRounding = 1024 * 32;
            const int ImageSizeRoundingMask = ImageSizeRounding - 1;

            int m = bytes + ImageSizeRoundingMask;

            int bytesRounded = m & ~ImageSizeRoundingMask;

            NativeMemory.Free(mImageData);

            mImageData = (byte*) (NativeMemory.Alloc((nuint) bytesRounded));
            mImageDataSize = bytesRounded;
        }

        mImageSize.Width = (int) w;
        mImageSize.Height = size.Height;
        mBytesPerRow = (int) (w * 4);

        return mImageSize;
    }


    public partial void ClearImage()
    {
        NativeMemory.Clear(mImageData, (nuint) (mImageSize.Width * 4 * mImageSize.Height));
    }


    public partial void DrawImage(VectorImage image, in Matrix matrix)
    {
        if (image.GetGeometryCount() < 1)
        {
            return;
        }

        ImageData d = new(mImageData, mImageSize.Width, mImageSize.Height,
           mBytesPerRow);

        Rasterizer<T>.Rasterize(image.GetGeometries(), image.GetGeometryCount(), matrix,
            mThreads, d);

        // Free all the memory allocated by threads.
        mThreads.ResetFrameMemory();
    }


    public partial IntSize GetImageSize()
    {
        return mImageSize;
    }


    public partial int GetImageWidth()
    {
        return mImageSize.Width;
    }


    public partial int GetImageHeight()
    {
        return mImageSize.Height;
    }


    public partial byte* GetImageData()
    {
        return mImageData;
    }


    public partial int GetBytesPerRow()
    {
        return mBytesPerRow;
    }
}
