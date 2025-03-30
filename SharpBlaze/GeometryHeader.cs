using System;
using System.Buffers;
using System.Buffers.Binary;

namespace SharpBlaze;

public readonly struct GeometryHeader
{
    internal const int Size = 4 * 8;
    
    public readonly IntRect PathBounds;
    public readonly uint Color;
    public readonly FillRule Rule;
    public readonly uint TagCount;
    public readonly uint PointCount;

    public GeometryHeader(IntRect pathBounds, uint color, FillRule rule, uint tagCount, uint pointCount)
    {
        PathBounds = pathBounds;
        Color = color;
        Rule = rule;
        TagCount = tagCount;
        PointCount = pointCount;
    }

    public static OperationStatus Read(
        ReadOnlySpan<byte> data,
        out int bytesConsumed,
        out GeometryHeader header)
    {
        bytesConsumed = 0;
        header = default;

        if (data.Length < Size)
        {
            return OperationStatus.NeedMoreData;
        }
        bytesConsumed += 4 * 6;

        // 4 bytes, color as premultiplied RGBA8.
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(data);

        // 16 bytes, path bounds.
        int pminx = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        int pminy = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
        int pmaxx = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
        int pmaxy = BinaryPrimitives.ReadInt32LittleEndian(data[16..]);

        // 4 bytes, fill rule.
        uint fillRule = BinaryPrimitives.ReadUInt32LittleEndian(data[20..]);
        if (fillRule > 1)
        {
            return OperationStatus.InvalidData;
        }
        bytesConsumed += 4 * 2;

        // 4 bytes, tag count.
        uint tagCount = BinaryPrimitives.ReadUInt32LittleEndian(data[24..]);

        // 4 bytes, point count.
        uint pointCount = BinaryPrimitives.ReadUInt32LittleEndian(data[28..]);

        header = new GeometryHeader(
            IntRect.FromMinMax(pminx, pminy, pmaxx, pmaxy),
            color,
            (FillRule) fillRule,
            tagCount,
            pointCount);

        return OperationStatus.Done;
    }
}