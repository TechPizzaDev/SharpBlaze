namespace SharpBlaze;

public partial struct FloatRect
{
    public FloatRect(double x, double y, double width, double height)
    {
        Min = new FloatPoint(x, y);
        Max = new FloatPoint(x + width, y + height);
    }

    public FloatRect(FloatPoint min, FloatPoint max)
    {
        Min = min;
        Max = max;
    }

    public FloatRect(in IntRect r)
    {
        Min = new FloatPoint(r.MinX, r.MinY);
        Max = new FloatPoint(r.MaxX, r.MaxY);
    }

    public FloatPoint Min;
    public FloatPoint Max;
}