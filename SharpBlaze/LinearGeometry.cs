using System;

namespace SharpBlaze;

public readonly ref struct LinearGeometry(
    Matrix transform,
    ReadOnlySpan<PathTag> tags,
    ReadOnlySpan<FloatPoint> points)
{
    public readonly Matrix Transform = transform;
    public readonly ReadOnlySpan<PathTag> Tags = tags;
    public readonly ReadOnlySpan<FloatPoint> Points = points;
}
