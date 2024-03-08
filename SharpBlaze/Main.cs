using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace SharpBlaze;

public unsafe class Main
{
    public uint WindowWidth { get; }
    public uint WindowHeight { get; }

    protected byte* g_rawData;

    protected Queue<double> samples = new();

    public Main(uint width, uint height)
    {
        WindowWidth = width;
        WindowHeight = height;

        g_rawData = (byte*) NativeMemory.Alloc(WindowWidth * WindowHeight * 4);
    }

    public void Rasterize()
    {
        long raster_start = Stopwatch.GetTimestamp();



        long raster_end = Stopwatch.GetTimestamp();
        //Console.WriteLine(clips + " - " + notClips + " - " + misses);

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

    //public void CycleRasterizerImpl()
    //{
    //    var previousRasterizer = g_rasterizer;
    //    if (previousRasterizer is Avx2Rasterizer<FmaIntrinsic> or Avx2Rasterizer<FmaX86>)
    //    {
    //        g_rasterizer = V128Rasterizer<FmaX86>.Create(g_rasterizationTable, WindowWidth, WindowHeight);
    //    }
    //    else if (previousRasterizer is V128Rasterizer<FmaIntrinsic> or V128Rasterizer<FmaX86>)
    //    {
    //        g_rasterizer = ScalarRasterizer.Create(g_rasterizationTable, WindowWidth, WindowHeight);
    //    }
    //    else
    //    {
    //        g_rasterizer = Avx2Rasterizer<FmaX86>.Create(g_rasterizationTable, WindowWidth, WindowHeight);
    //    }
    //
    //    previousRasterizer.Dispose();
    //    Console.WriteLine($"Changed to {g_rasterizer}  (from {previousRasterizer})");
    //}
}