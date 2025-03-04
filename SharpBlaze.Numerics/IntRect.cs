using System;

namespace SharpBlaze;


public struct IntRect
{
    public IntRect(int x, int y, int width, int height)
    {
        MinX = (x);
        MinY = (y);
        MaxX = (x + width);
        MaxY = (y + height);
    }

    public int MinX;
    public int MinY;
    public int MaxX;
    public int MaxY;

    public static IntRect FromMinMax(int x1, int y1, int x2, int y2)
    {
        return new IntRect()
        {
            MinX = x1,
            MinY = y1,
            MaxX = x2,
            MaxY = y2
        };
    }

    public static IntRect Union(IntRect a, IntRect b)
    {
        int x1 = Math.Min(a.MinX, b.MinX);
        int x2 = Math.Max(a.MaxX, b.MaxX);
        int y1 = Math.Min(a.MinY, b.MinY);
        int y2 = Math.Max(a.MaxY, b.MaxY);

        return FromMinMax(x1, y1, x2, y2);
    }
}