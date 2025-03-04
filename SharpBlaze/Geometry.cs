using System;
using System.Diagnostics;

namespace SharpBlaze;


/**
 * One renderable item.
 */
public readonly struct Geometry
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
    public Geometry(
        IntRect pathBounds, 
        ReadOnlyMemory<PathTag> tags,
        ReadOnlyMemory<FloatPoint> points, 
        in Matrix tm, 
        uint color, 
        FillRule rule)
    {
        Debug.Assert(!tags.IsEmpty);
        Debug.Assert(!points.IsEmpty);

        PathBounds = pathBounds;
        Tags = tags;
        Points = points;
        TM = tm;
        Color = color;
        Rule = rule;
    }


    public readonly IntRect PathBounds;
    public readonly ReadOnlyMemory<PathTag> Tags = null;
    public readonly ReadOnlyMemory<FloatPoint> Points = null;
    public readonly Matrix TM;
    public readonly uint Color = 0;
    public readonly FillRule Rule = FillRule.NonZero;
};
