using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

/**
 * A helper class for managing an image to draw on.
 */
public unsafe class DestinationImage<T> : IDisposable
    where T : unmanaged, ITileDescriptor<T>
{
    public DestinationImage()
    {
    }

    private ImageData mImageData;
    private ulong mImageDataSize;
    
    public void Dispose()
    {
        if (mImageData.Data != null)
        {
            NativeMemory.Free(mImageData.Data);
            mImageData = default;
        }
    }


    public IntSize UpdateSize(IntSize size, int pixelSize)
    {
        Debug.Assert(size.Width > 0);
        Debug.Assert(size.Height > 0);

        // Round-up width to tile width.
        TileIndex w = Linearizer.CalculateColumnCount<T>(size.Width) * (TileIndex) T.TileW;

        // Calculate how many bytes are required for the image.
        ulong bytes = w * (ulong) pixelSize * (ulong) size.Height;
        void* data = mImageData.Data;
        
        if (mImageDataSize < bytes)
        {
            const ulong ImageSizeRounding = 1024 * 32;
            const ulong ImageSizeRoundingMask = ImageSizeRounding - 1;

            ulong m = bytes + ImageSizeRoundingMask;
            ulong bytesRounded = m & ~ImageSizeRoundingMask;

            data = NativeMemory.Realloc(data, checked((nuint) bytesRounded));
            mImageDataSize = bytesRounded;
        }

        mImageData = new ImageData(
            data, 
            (int) w,
            size.Height, 
            checked((nint) w * pixelSize),
            pixelSize);
        
        return new IntSize(mImageData.Width, mImageData.Height);
    }

    public void DrawImage(VectorImage image, in Matrix matrix, Executor executor)
    {
        ReadOnlyMemory<Geometry> geometries = image.GetGeometries();
        if (geometries.IsEmpty)
        {
            return;
        }

        Rasterizer<T>.Rasterize(geometries.Span, matrix, executor, mImageData, new LinearRasterizer());
        
        GC.KeepAlive(image);

        // Free all the memory allocated by threads.
        executor.ResetFrameMemory();
    }

    sealed class SpanRasterizer : LineRasterizer<uint, byte, SpanBlender>
    {
        protected override SpanBlender CreateBlender(in Geometry geometry)
        {
            return new SpanBlender(geometry.Color, geometry.Rule);
        }
    }


    sealed class LinearRasterizer : LineRasterizer<Vector4, float, LinearRasterizer.Blender>
    {
        protected override Blender CreateBlender(in Geometry geometry)
        {
            Vector4 color = new Vector4(
                geometry.Color & 0xff,
                (geometry.Color >> 8) & 0xff,
                (geometry.Color >> 16) & 0xff,
                geometry.Color >> 24) / 255.0f;

            return new Blender(color, geometry.Rule);
        }


        public readonly struct Blender : ISpanBlender<Vector4, float>
        {
            public Blender(Vector4 color, FillRule fillRule)
            {
                Color = color;
                FillRule = fillRule;

                IsSolid = Vector128.GreaterThanOrEqualAll(
                    Color.AsVector128(),
                    Vector128.Create(0, 0, 0, 1f));
            }

            readonly Vector4 Color;
            readonly FillRule FillRule;
            readonly bool IsSolid;


            public void CompositeSpan(Span<Vector4> d, float alpha)
            {
                if (d.Length >= 4 && alpha == 1.0f && IsSolid)
                {
                    d.Fill(Color);
                }
                else
                {
                    CompositeTransparentSpan(d, alpha);
                }
            }


            private void CompositeTransparentSpan(Span<Vector4> d, float alpha)
            {
                Vector4 color = Color * alpha;
                Vector4 a = new(1.0f - color.W);

                for (int i = 0; i < d.Length; i++)
                {
                    d[i] = color + d[i] * a;
                }
            }

            public float ApplyFillRule(F24Dot8 area)
            {
                float a = area.ToBits() * (1f / (512f * 256f));
                return FillRule switch
                {
                    FillRule.EvenOdd => float.Abs(a - 2.0f * float.Round(0.5f * a)),
                    _ => float.Min(float.Abs(a), 1.0f),
                };
            }
        }
    }

    public ImageData GetImageData()
    {
        return mImageData;
    }
}
