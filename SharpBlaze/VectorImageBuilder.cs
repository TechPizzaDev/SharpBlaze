﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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
        _closeOffsets.Add((_tags.Count, _points.Count));
    }

    public void Clear()
    {
        _tags.Clear();
        _points.Clear();
        _closeOffsets.Clear();
    }

    public unsafe VectorImage ToVectorImage(uint color)
    {
        int geometryCapacity = _closeOffsets.Count + 1;
        Geometry* geometries = (Geometry*) NativeMemory.Alloc((uint) geometryCapacity, (uint) sizeof(Geometry));
        Span<Geometry> geometrySpan = new(geometries, geometryCapacity);
        int geometryCount = 0;

        int prevTagOffset = 0;
        int prevPointOffset = 0;
        IntRect fullBounds = new();

        foreach ((int tagOffset, int pointOffset) in _closeOffsets)
        {
            FillRule fillRule = FillRule.NonZero;

            Range tagRange = new(prevTagOffset, tagOffset);
            Range pointRange = new(prevPointOffset, pointOffset);
            if (TryCreateGeo(fillRule, color, tagRange, pointRange, ref fullBounds, out geometrySpan[geometryCount]))
            {
                geometryCount++;
            }

            prevTagOffset = tagOffset;
            prevPointOffset = pointOffset;
        }

        return new VectorImage(geometries, geometryCount, fullBounds);
    }

    private unsafe bool TryCreateGeo(
        FillRule fillRule, uint color,
        Range tagRange, Range pointRange, ref IntRect fullBounds, out Geometry geometry)
    {
        Span<PathTag> tagSpan = CollectionsMarshal.AsSpan(_tags)[tagRange];
        Span<FloatPoint> pointSpan = CollectionsMarshal.AsSpan(_points)[pointRange];

        if (tagSpan.Length == 0)
        {
            Debug.Assert(pointSpan.Length == 0);

            Unsafe.SkipInit(out geometry);
            return false;
        }

        PathTag* tags = (PathTag*) NativeMemory.Alloc((uint) tagSpan.Length, sizeof(PathTag));
        tagSpan.CopyTo(new Span<PathTag>(tags, tagSpan.Length));

        FloatPoint* points = (FloatPoint*) NativeMemory.Alloc((uint) pointSpan.Length, (uint) sizeof(FloatPoint));
        IntRect pBounds = GetBoundsAndCopy(pointSpan, new Span<FloatPoint>(points, pointSpan.Length)).ToExpandedIntRect();

        geometry = new Geometry(
            pBounds,
            tags, points, Matrix.Identity, tagSpan.Length, pointSpan.Length, color,
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

            min = Vector128.Min(min, p);
            max = Vector128.Max(max, p);

            dst[i] = new FloatPoint(p);
        }

        return new FloatRect(new FloatPoint(min), new FloatPoint(max));
    }
}
