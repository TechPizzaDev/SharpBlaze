using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SharpBlaze;

public unsafe class Main
{
    public uint WindowWidth { get; }
    public uint WindowHeight { get; }

    public ViewData mViewData;
    public DestinationImage<TileDescriptor_8x16> mImage;
    VectorImage mVectorImage;

    protected Queue<double> samples = new();

    public Main(uint width, uint height)
    {
        WindowWidth = width;
        WindowHeight = height;

        mVectorImage = new VectorImage();

        byte[] bytes = File.ReadAllBytes("../../../../Images/Paris-30k.vectorimage");
        fixed (byte* pBytes = bytes)
        {
            mVectorImage.Parse(pBytes, (ulong) bytes.Length);
        }

        mImage = new DestinationImage<TileDescriptor_8x16>();

        mViewData = new ViewData();
        mViewData.SetupCoordinateSystem((int) WindowWidth, (int) WindowHeight, mVectorImage);
    }

    public void Rasterize()
    {
        IntSize imageSize = mImage.UpdateSize(new IntSize(
            (int) WindowWidth, (int) WindowHeight
        ));

        long raster_start = Stopwatch.GetTimestamp();

        Matrix matrix = mViewData.GetMatrix();

        mImage.ClearImage();
        mImage.DrawImage(mVectorImage, matrix);

        long raster_end = Stopwatch.GetTimestamp();
        //Console.WriteLine(clips + " - " + notClips + " - " + misses);

        int pixelCount = mImage.GetBytesPerRow() * mImage.GetImageHeight();
        byte* pixels = mImage.GetImageData();
        byte* pixelsEnd = pixels + pixelCount;
        while (pixels < pixelsEnd)
        {
            byte r = pixels[2];
            byte b = pixels[0];

            pixels[0] = r;
            pixels[2] = b;

            pixels += 4;
        }

        double rasterTime = Stopwatch.GetElapsedTime(raster_start, raster_end).TotalMilliseconds;

        samples.Enqueue(rasterTime);

        while (samples.Count > 60 * 10)
        {
            samples.Dequeue();
        }
    }

    public (double avgTime, double stDev, double median) GetTimings()
    {
        double avgRasterTime = samples.Sum() / samples.Count;
        double sqSum = samples.Select(x => x * x).Sum();
        double stDev = Math.Sqrt(sqSum / samples.Count - avgRasterTime * avgRasterTime);

        double median = samples.Order().ElementAt(samples.Count / 2);

        return (avgRasterTime, median, stDev);
    }
}