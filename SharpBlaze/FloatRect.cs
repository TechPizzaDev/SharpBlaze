
namespace SharpBlaze;


public partial struct FloatRect
{
    public readonly partial IntRect ToExpandedIntRect()
    {
        int minx = (int) (Utils.Floor(MinX));
        int miny = (int) (Utils.Floor(MinY));
        int maxx = (int) (Utils.Ceil(MaxX));
        int maxy = (int) (Utils.Ceil(MaxY));

        return new IntRect(minx, miny, maxx - minx, maxy - miny);
    }
}