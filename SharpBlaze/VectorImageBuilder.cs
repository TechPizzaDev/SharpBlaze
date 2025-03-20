using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public class VectorImageBuilder
{
    private List<PathTag> _tags = new();
    private List<FloatPoint> _points = new();
    private List<(int tag, int point)> _closeOffsets = new();

    public void MoveTo(FloatPoint point)
    {
        _tags.Add(PathTag.Move);
        _points.Add(point);
    }

    public void LineTo(FloatPoint point)
    {
        _tags.Add(PathTag.Line);
        _points.Add(point);
    }

    public void QuadTo(FloatPoint point0, FloatPoint point1)
    {
        _tags.Add(PathTag.Quadratic);
        _points.Add(point0);
        _points.Add(point1);
    }

    public void CubicTo(FloatPoint point0, FloatPoint point1, FloatPoint point2)
    {
        _tags.Add(PathTag.Cubic);
        _points.Add(point0);
        _points.Add(point1);
        _points.Add(point2);
    }

    public void Close()
    {
        _tags.Add(PathTag.Close);
        _closeOffsets.Add((_tags.Count, _points.Count));
    }

    public void Clear()
    {
        _tags.Clear();
        _points.Clear();
        _closeOffsets.Clear();
    }

    public VectorImage ToVectorImage(uint color)
    {
        int geometryCapacity = _closeOffsets.Count + 1;
        Geometry[] geometries = new Geometry[geometryCapacity];
        int geometryCount = 0;

        int prevTagOffset = 0;
        int prevPointOffset = 0;
        IntRect fullBounds = new();

        foreach ((int tagOffset, int pointOffset) in _closeOffsets)
        {
            FillRule fillRule = FillRule.NonZero;

            Range tagRange = new(prevTagOffset, tagOffset);
            Range pointRange = new(prevPointOffset, pointOffset);
            if (TryCreateGeo(fillRule, color, tagRange, pointRange, ref fullBounds, out geometries[geometryCount]))
            {
                geometryCount++;
            }

            prevTagOffset = tagOffset;
            prevPointOffset = pointOffset;
        }

        Array.Resize(ref geometries, geometryCount);

        return new VectorImage(geometries, fullBounds);
    }

    private bool TryCreateGeo(
        FillRule fillRule, uint color,
        Range tagRange, Range pointRange, ref IntRect fullBounds, out Geometry geometry)
    {
        PathTag[] tags = CollectionsMarshal.AsSpan(_tags)[tagRange].ToArray();
        ReadOnlySpan<FloatPoint> pointSpan = CollectionsMarshal.AsSpan(_points)[pointRange];

        if (tags.Length == 0)
        {
            Debug.Assert(pointSpan.Length == 0);

            Unsafe.SkipInit(out geometry);
            return false;
        }

        FloatPoint[] points = new FloatPoint[pointSpan.Length];
        IntRect pBounds = GetBoundsAndCopy(pointSpan, points).ToExpandedIntRect();

        geometry = new Geometry(
            pBounds,
            tags,
            points, 
            Matrix.Identity,
            color,
            fillRule);

        fullBounds = IntRect.Union(fullBounds, pBounds);
        return true;
    }

    private static FloatRect GetBoundsAndCopy(ReadOnlySpan<FloatPoint> src, Span<FloatPoint> dst)
    {
        Vector128<double> min = Vector128<double>.Zero;
        Vector128<double> max = Vector128<double>.Zero;

        for (int i = 0; i < src.Length; i++)
        {
            Vector128<double> p = src[i].AsVector128();

            min = V128Helper.MinNative(min, p);
            max = V128Helper.MaxNative(max, p);

            dst[i] = new FloatPoint(p);
        }

        return new FloatRect(new FloatPoint(min), new FloatPoint(max));
    }
}
