
namespace SharpBlaze;


public partial struct FloatRect
{
    public readonly partial IntRect ToExpandedIntRect()
    {
        int minx = (int) (Utils.Floor(Min.X));
        int miny = (int) (Utils.Floor(Min.Y));
        int maxx = (int) (Utils.Ceil(Max.X));
        int maxy = (int) (Utils.Ceil(Max.Y));

        return new IntRect(minx, miny, maxx - minx, maxy - miny);
    }
}