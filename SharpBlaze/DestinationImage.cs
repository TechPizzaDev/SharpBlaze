using System;
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

    private byte* mImageData = null;
    private IntSize mImageSize;
    private int mBytesPerRow = 0;
    private int mImageDataSize = 0;


    ~DestinationImage()
    {
        NativeMemory.Free(mImageData);
    }


    public IntSize UpdateSize(IntSize size)
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


    public void ClearImage()
    {
        NativeMemory.Clear(mImageData, (nuint) (mImageSize.Width * 4 * mImageSize.Height));
    }


    public void DrawImage(VectorImage image, in Matrix matrix, Executor executor)
    {
        if (image.GetGeometryCount() < 1)
        {
            return;
        }

        ImageData d = new(mImageData, mImageSize.Width, mImageSize.Height,
           mBytesPerRow);

        Rasterizer<T>.Rasterize(image.GetGeometrySpan(), matrix, executor, d);

        GC.KeepAlive(image);

        // Free all the memory allocated by threads.
        executor.ResetFrameMemory();
    }


    public IntSize GetImageSize()
    {
        return mImageSize;
    }


    public int GetImageWidth()
    {
        return mImageSize.Width;
    }


    public int GetImageHeight()
    {
        return mImageSize.Height;
    }


    public byte* GetImageData()
    {
        return mImageData;
    }


    public int GetBytesPerRow()
    {
        return mBytesPerRow;
    }
}
