
namespace SharpBlaze;


public struct IntRect
{
    public IntRect(int x, int y, int width,
        int height)
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
}