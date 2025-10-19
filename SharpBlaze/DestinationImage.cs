using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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


    public bool UpdateSize(IntSize size, int pixelSize)
    {
        Debug.Assert(size.Width > 0);
        Debug.Assert(size.Height > 0);

        // Round-up width to tile width.
        TileIndex w = Linearizer.CalculateColumnCount<T>(size.Width) * (TileIndex) T.TileW;

        // Calculate how many bytes are required for the image.
        ulong bytes = w * (ulong) pixelSize * (ulong) size.Height;
        void* data = mImageData.Data;

        bool resized = false;
        if (mImageDataSize < bytes)
        {
            const ulong ImageSizeRounding = 1024 * 32;
            const ulong ImageSizeRoundingMask = ImageSizeRounding - 1;

            ulong m = bytes + ImageSizeRoundingMask;
            ulong bytesRounded = m & ~ImageSizeRoundingMask;

            data = NativeMemory.Realloc(data, checked((nuint) bytesRounded));
            mImageDataSize = bytesRounded;
            resized = true;
        }

        mImageData = new ImageData(
            data,
            (int) w,
            size.Height,
            checked((nint) w * pixelSize),
            pixelSize);

        return resized;
    }

    public void DrawImage(VectorImage image, in Matrix matrix, Executor executor)
    {
        ReadOnlyMemory<Geometry> geometries = image.GetGeometries();
        if (geometries.IsEmpty)
        {
            return;
        }

        Rasterizer<T>.Rasterize(
            geometries.Span,
            matrix,
            executor,
            new LinearRasterizer(geometries, mImageData));

        GC.KeepAlive(image);

        // Free all the memory allocated by threads.
        executor.ResetFrameMemory();
    }

    sealed class SpanRasterizer(ReadOnlyMemory<Geometry> geometries, ImageData image) : LineRasterizer<byte, SpanBlender>
    {
        public override IntRect Bounds => image.Bounds;

        protected override SpanBlender CreateBlender(int geometryIndex, IntRect targetRect)
        {
            ref readonly Geometry geometry = ref geometries.Span[geometryIndex];

            return new SpanBlender(image, geometry.Color, geometry.Rule);
        }
    }


    sealed class LinearRasterizer(ReadOnlyMemory<Geometry> geometries, ImageData image) : LineRasterizer<float, LinearRasterizer.Blender>
    {
        public override IntRect Bounds => image.Bounds;

        protected override Blender CreateBlender(int geometryIndex, IntRect targetRect)
        {
            ref readonly Geometry geometry = ref geometries.Span[geometryIndex];

            uint c = geometry.Color;
            Vector4 color = new Vector4(
                c & 0xff,
                (c >> 8) & 0xff,
                (c >> 16) & 0xff,
                c >> 24) / 255.0f;

            return new Blender(image, color, geometry.Rule);
        }


        public readonly struct Blender : ISpanBlender<float>
        {
            public Blender(in ImageData image, Vector4 color, FillRule fillRule)
            {
                Image = image;
                Color = color;
                FillRule = fillRule;

                IsSolid = Vector128.GreaterThanOrEqualAll(
                    Color.AsVector128(),
                    Vector128.Create(0, 0, 0, 1f));
            }

            readonly ImageData Image;
            readonly Vector4 Color;
            readonly FillRule FillRule;
            readonly bool IsSolid;

            public void CompositeSpan(int x0, int x1, int y, float alpha)
            {
                Span<Vector4> d = Image.GetSpan2D<Vector4>()[y][x0..x1];

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

            public void Dispose()
            {
            }
        }
    }

    public ImageData GetImageData()
    {
        return mImageData;
    }
}
