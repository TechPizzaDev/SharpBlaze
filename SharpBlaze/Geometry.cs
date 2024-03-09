using System.Diagnostics;

namespace SharpBlaze;


/**
 * One renderable item.
 */
public readonly unsafe partial struct Geometry
{

    /**
     * Constructs geometry.
     *
     * @param pathBounds Bounding box of a path transformed by transformation
     * matrix. This rectangle potentially can exceed bounds of destination
     * image.
     *
     * @param tags Pointer to tags. Must not be nullptr.
     *
     * @param points Pointer to points. Must not be nullptr.
     *
     * @param tm Transformation matrix.
     *
     * @param tagCount A number of tags. Must be greater than 0.
     *
     * @param color RGBA color, 8 bits per channel, color components
     * premultiplied by alpha.
     *
     * @param rule Fill rule to use.
     */
    public Geometry(IntRect pathBounds, PathTag* tags,
         FloatPoint* points, in Matrix tm, int tagCount,
         int pointCount, uint color, FillRule rule)
    {
        PathBounds = pathBounds;
        Tags = tags;
        Points = points;
        TM = tm;
        TagCount = tagCount;
        PointCount = pointCount;
        Color = color;
        Rule = rule;

        Debug.Assert(tags != null);
        Debug.Assert(points != null);
        Debug.Assert(tagCount > 0);
        Debug.Assert(pointCount > 0);
    }


    public readonly IntRect PathBounds;
    public readonly PathTag* Tags = null;
    public readonly FloatPoint* Points = null;
    public readonly Matrix TM;
    public readonly int TagCount = 0;
    public readonly int PointCount = 0;
    public readonly uint Color = 0;
    public readonly FillRule Rule = FillRule.NonZero;
};
