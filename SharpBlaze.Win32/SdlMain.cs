using System;
using Silk.NET.Maths;
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
            (int)width,
            (int)height,
            (uint)WindowFlags.Hidden);

        windowSurface = this.sdl.GetWindowSurface(window);

        drawSurface = this.sdl.CreateRGBSurfaceWithFormat(
            0, (int)width, (int)height, 32, (uint)PixelFormatEnum.Abgr8888);

        sdl.SetSurfaceBlendMode(drawSurface, BlendMode.None);
    }

    public int Run()
    {
        sdl.ShowWindow(window);

        bool mouseDown = false;
        int mouseX = 0;
        int mouseY = 0;

        Event ev = default;
        do
        {
            while (sdl.PollEvent(ref ev) != 0)
            {
                var evType = (EventType)ev.Type;
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
                        FloatPoint zoom = (mViewData.Scale * delta).Clamp(new(0.001), new(100000.0));

                        if (mViewData.Scale != zoom)
                        {
                            FloatPoint dd = (zoom - mViewData.Scale) / mViewData.Scale;
                            FloatPoint pn = mViewData.CoordinateSystemMatrix.Inverse().Map(ev.Wheel.MouseX, ev.Wheel.MouseY);

                            mViewData.Translation += (mViewData.Translation - pn) * dd;
                            mViewData.Scale = zoom;
                        }
                        break;
                }
            }

            Rasterize();

            (double avgRasterTime, double stDev, double median) = GetTimings();
            int fps = (int)(1000.0f / avgRasterTime);

            string title = $"FPS: {fps} - Raster time: {avgRasterTime:0.000}±{stDev:0.000}ms stddev / {median:0.000}ms median";
            sdl.SetWindowTitle(window, title);

            sdl.LockSurface(drawSurface);
            {
                int srcPitch = mImage.GetBytesPerRow();
                int srcHeight = mImage.GetImageHeight();
                Span<byte> data = new(mImage.GetImageData(), srcHeight * srcPitch);

                int dstPitch = drawSurface->Pitch;
                Span<byte> target = new(drawSurface->Pixels, drawSurface->H * dstPitch);

                if (srcPitch == dstPitch)
                {
                    data.CopyTo(target);
                }
                else
                {
                    for (int y = 0; y < srcHeight; y++)
                    {
                        Span<byte> src = data.Slice(y * srcPitch, srcPitch);
                        Span<byte> dst = target.Slice(y * dstPitch, dstPitch);
                        src.CopyTo(dst);
                    }
                }
            }
            sdl.UnlockSurface(drawSurface);

            sdl.UpperBlit(drawSurface, null, windowSurface, null);

            sdl.UpdateWindowSurface(window);
        }
        while (true);
    }

    public void Dispose()
    {
        sdl.DestroyWindow(window);

        GC.SuppressFinalize(this);
    }
}
