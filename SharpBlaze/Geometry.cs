using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        in Matrix transform,
        uint color,
        FillRule rule)
    {
        Debug.Assert(!tags.IsEmpty);
        Debug.Assert(!points.IsEmpty);

        PathBounds = pathBounds;
        Tags = tags;
        Points = points;
        Transform = transform;
        Color = color;
        Rule = rule;
    }


    public readonly IntRect PathBounds;
    public readonly ReadOnlyMemory<PathTag> Tags = null;
    public readonly ReadOnlyMemory<FloatPoint> Points = null;
    public readonly Matrix Transform;
    public readonly uint Color = 0;
    public readonly FillRule Rule = FillRule.NonZero;

    public static void Read(Stream stream, out Geometry geometry)
    {
        Span<byte> buffer = stackalloc byte[GeometryHeader.Size];
        stream.ReadExactly(buffer);

        OperationStatus status = GeometryHeader.Read(
            buffer, out int bytesConsumed, out GeometryHeader header);

        Debug.Assert(bytesConsumed == buffer.Length);
        ThrowHelper.ThrowOnInvalid(status);

        Read(stream, header, out geometry);
    }

    public static void Read(
        Stream stream,
        GeometryHeader header,
        out Geometry geometry)
    {
        // TODO: allocate arrays in arenas

        PathTag[] tags = new PathTag[header.TagCount];
        Debug.Assert(sizeof(PathTag) == sizeof(byte));

        Span<byte> tagBytes = MemoryMarshal.AsBytes(tags.AsSpan());
        stream.ReadExactly(tagBytes);

        FloatPoint[] points = new FloatPoint[header.PointCount];
        if (BitConverter.IsLittleEndian)
        {
            Span<byte> pointBytes = MemoryMarshal.AsBytes(points.AsSpan());
            stream.ReadExactly(pointBytes);
        }
        else
        {
            ReadPoints(stream, points);
        }

        geometry = new Geometry(
            header.PathBounds,
            tags,
            points,
            Matrix.Identity,
            header.Color,
            header.Rule);
    }

    [SkipLocalsInit]
    private static void ReadPoints(Stream stream, Span<FloatPoint> points)
    {
        const int pSize = sizeof(double) * 2;
        const int bufSize = 64;

        Span<byte> buffer = stackalloc byte[pSize * bufSize];

        while (points.Length >= 0)
        {
            int count = Math.Min(points.Length, bufSize);

            Span<byte> span = buffer[..(pSize * count)];
            stream.ReadExactly(span);

            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> src = span.Slice(i * pSize, pSize);

                points[i] = new FloatPoint(
                    BinaryPrimitives.ReadDoubleLittleEndian(src),
                    BinaryPrimitives.ReadDoubleLittleEndian(src[sizeof(double)..]));
            }

            points = points[count..];
        }
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Color);

        PathBounds.Write(writer);

        writer.Write((uint) Rule);

        writer.Write(Tags.Length);
        writer.Write(Points.Length);

        writer.Write(MemoryMarshal.AsBytes(Tags.Span));

        foreach (FloatPoint point in Points.Span)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
        }
    }
};
