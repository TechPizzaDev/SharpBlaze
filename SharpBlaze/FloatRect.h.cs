
namespace SharpBlaze;


public partial struct FloatRect
{
    public FloatRect(double x, double y, double width,
        double height)
    {
        MinX = (x);
        MinY = (y);
        MaxX = (x + width);
        MaxY = (y + height);
    }


    public FloatRect(in IntRect r)
    {
        MinX = (r.MinX);
        MinY = (r.MinY);
        MaxX = (r.MaxX);
        MaxY = (r.MaxY);
    }


    public readonly partial IntRect ToExpandedIntRect();


    public double MinX;
    public double MinY;
    public double MaxX;
    public double MaxY;
}