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
    private Executor executor;
    private VectorImage mVectorImage;

    protected Queue<double> samples = new();
    private double[] sampleBuffer = new double[60 * 10];

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
        executor = Executor.CreateOptimalExecutor();

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
        mImage.DrawImage(mVectorImage, matrix, executor);

        long raster_end = Stopwatch.GetTimestamp();
        //Console.WriteLine(clips + " - " + notClips + " - " + misses);

        double rasterTime = Stopwatch.GetElapsedTime(raster_start, raster_end).TotalMicroseconds;

        samples.Enqueue(rasterTime);

        while (samples.Count > sampleBuffer.Length)
        {
            samples.Dequeue();
        }
    }

    public (TimeSpan avgTime, TimeSpan stDev, TimeSpan median) GetTimings()
    {
        samples.CopyTo(sampleBuffer, 0);
        Span<double> span = sampleBuffer.AsSpan(0, samples.Count);

        double avgTime = 0;
        double sqSum = 0;
        foreach (double x in span)
        {
            avgTime += x;
            sqSum += x * x;
        }
        avgTime /= span.Length;
        double stDev = Math.Sqrt(sqSum / span.Length - avgTime * avgTime);
        
        span.Sort();
        double median = span[span.Length / 2];

        return (
            TimeSpan.FromMicroseconds(avgTime),
            TimeSpan.FromMicroseconds(stDev), 
            TimeSpan.FromMicroseconds(median));
    }

    public string GetWindowTitle()
    {
        (TimeSpan avgRasterTime, TimeSpan stDev, TimeSpan median) = GetTimings();
        double fps = 1.0 / avgRasterTime.TotalSeconds;

        string title =
            $"FPS: {fps:#0.0} - " +
            $"{avgRasterTime.TotalMilliseconds:#0.00}±" +
            $"{stDev.TotalMilliseconds:#0.00}ms stdev / {median.TotalMilliseconds:#0.00} median";

        return title;
    }
}