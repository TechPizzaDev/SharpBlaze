using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Silk.NET.SDL;

namespace SharpBlaze.Win32;

public unsafe class SdlMain : Main, IDisposable
{
    private readonly Sdl sdl;

    private Window* window;
    private Surface* windowSurface;
    private Surface* drawSurface;

    public SdlMain(Sdl sdl, uint width, uint height) : base(width, height)
    {
        this.sdl = sdl;

        window = this.sdl.CreateWindow(
            "Rasterizer",
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            (int) width,
            (int) height,
            (uint) WindowFlags.Hidden);
    }

    public int Run()
    {
        sdl.ShowWindow(window);

        windowSurface = sdl.GetWindowSurface(window);
        int width = 0;
        int height = 0;
        sdl.GetWindowSize(window, &width, &height);
        drawSurface = sdl.CreateRGBSurfaceWithFormat(0, width, height, 32, (uint) PixelFormatEnum.Abgr8888);

        sdl.SetSurfaceBlendMode(drawSurface, BlendMode.None);

        bool mouseDown = false;
        int mouseX = 0;
        int mouseY = 0;

        Event ev = default;
        do
        {
            while (sdl.PollEvent(ref ev) != 0)
            {
                var evType = (EventType) ev.Type;
                switch (evType)
                {
                    case EventType.Quit:
                        return 0;

                    case EventType.Mousebuttondown:
                        mouseDown = true;
                        mouseX = ev.Button.X;
                        mouseY = ev.Button.Y;
                        break;

                    case EventType.Mousebuttonup:
                        mouseDown = false;
                        break;

                    case EventType.Mousemotion:
                        if (mouseDown)
                        {
                            mViewData.Translation += new FloatPoint(
                                ev.Motion.X - mouseX,
                                ev.Motion.Y - mouseY);
                        }
                        mouseX = ev.Motion.X;
                        mouseY = ev.Motion.Y;
                        break;

                    case EventType.Mousewheel:
                        FloatPoint delta = new(1.0 + ev.Wheel.PreciseY * 0.1);
                        FloatPoint scale = (mViewData.Scale * delta).Clamp(new(0.001), new(100000.0));
                        if (mViewData.Scale == scale)
                        {
                            break;
                        }

                        FloatPoint dd = (scale - mViewData.Scale) / mViewData.Scale;
                        FloatPoint pn = mViewData.CoordinateSystemMatrix.Inverse().Map(ev.Wheel.MouseX, ev.Wheel.MouseY);

                        mViewData.Translation += (mViewData.Translation - pn) * dd;
                        mViewData.Scale = scale;
                        break;
                }
            }

            Rasterize();

            sdl.SetWindowTitle(window, GetWindowTitle());

            sdl.LockSurface(drawSurface);
            {
                ImageData image = mImage.GetImageData();
                Span2D<byte> source = image.GetSpan2D<byte>();

                Span2D<uint> target = new(
                    drawSurface->Pixels,
                    drawSurface->W,
                    drawSurface->H,
                    drawSurface->Pitch);

                // TODO: Respect endianness?

                if (source.Stride == target.Stride)
                {
                    source.CopyTo(target.Cast<byte>());
                }
                else
                {
                    for (int y = 0; y < source.Height; y++)
                    {
                        Span<float> src = MemoryMarshal.Cast<byte, float>(source[y]);
                        Span<uint> dst = target[y];

                        if (Avx512F.IsSupported)
                        {
                            ConvertAvx512F(src, dst);
                        }
                        else
                        {
                            ConvertV128(src, dst);
                        }
                    }
                }
            }
            sdl.UnlockSurface(drawSurface);

            sdl.UpperBlit(drawSurface, null, windowSurface, null);

            sdl.UpdateWindowSurface(window);
        }
        while (true);
    }

    private static void ConvertAvx512F(ReadOnlySpan<float> src, Span<uint> dst)
    {
        while (src.Length >= Vector512<float>.Count)
        {
            Vector512<uint> u32 = Avx512F.ConvertToVector512UInt32(Vector512.Create(src) * 255f);
            Vector128<byte> rgba8 = Avx512F.ConvertToVector128ByteWithSaturation(u32);
            rgba8.AsUInt32().CopyTo(dst);

            src = src[Vector512<float>.Count..];
            dst = dst[4..];
        }
        ConvertScalar(src, dst);
    }

    private static void ConvertV128(ReadOnlySpan<float> src, Span<uint> dst)
    {
        while (src.Length >= Vector128<float>.Count * 4)
        {
            Vector128<uint> u32_0 = Vector128.ConvertToUInt32(Vector128.Create(src[..4]) * 255f);
            Vector128<uint> u32_1 = Vector128.ConvertToUInt32(Vector128.Create(src[4..8]) * 255f);
            Vector128<uint> u32_2 = Vector128.ConvertToUInt32(Vector128.Create(src[8..12]) * 255f);
            Vector128<uint> u32_3 = Vector128.ConvertToUInt32(Vector128.Create(src[12..16]) * 255f);

            Vector128<ushort> u16_0 = Vector128.Narrow(u32_0, u32_1).AsUInt16();
            Vector128<ushort> u16_1 = Vector128.Narrow(u32_2, u32_3).AsUInt16();

            Vector128<byte> rgba8 = Vector128.Narrow(u16_0, u16_1);
            rgba8.AsUInt32().CopyTo(dst);

            src = src[(Vector128<float>.Count * 4)..];
            dst = dst[4..];
        }
        ConvertScalar(src, dst);
    }

    private static void ConvertScalar(ReadOnlySpan<float> src, Span<uint> dst)
    {
        while (src.Length >= Vector128<float>.Count)
        {
            Vector128<uint> u32 = Vector128.ConvertToUInt32(Vector128.Create(src) * 255f);
            Vector128<ushort> u16 = Vector128.Narrow(u32, u32).AsUInt16();
            Vector128<byte> u8 = Vector128.Narrow(u16, u16);
            dst[0] = u8.AsUInt32().ToScalar();

            src = src[Vector128<float>.Count..];
            dst = dst[1..];
        }
    }

    public void Dispose()
    {
        sdl.DestroyWindow(window);

        GC.SuppressFinalize(this);
    }
}
