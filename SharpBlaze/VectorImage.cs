using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;

namespace SharpBlaze;

public partial class VectorImage
{
    public static ReadOnlySpan<byte> Signature => "Bvec"u8;

    public static uint Version => 1u;

    public VectorImage()
    {
        mBounds = new(0, 0, 0, 0);
    }

    public VectorImage(ReadOnlyMemory<Geometry> geometries, IntRect bounds)
    {
        mGeometries = geometries;
        mBounds = bounds;
    }

    private const int HeaderSize = 4 * 7;
    
    private static OperationStatus Parse(
        ReadOnlySpan<byte> data, out int bytesConsumed, out uint count, out IntRect bounds)
    {
        count = 0;
        bounds = default;
        
        bytesConsumed = 0;
        if (data.Length < HeaderSize)
        {
            return OperationStatus.NeedMoreData;
        }

        // Read signature.
        bytesConsumed += 4;
        if (!Signature.SequenceEqual(data[..4]))
        {
            return OperationStatus.InvalidData;
        }

        // Read version.
        bytesConsumed += 4;
        if (BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != 1)
        {
            return OperationStatus.InvalidData;
        }

        // 4 bytes, total path count.
        count = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);

        // 16 bytes, full bounds.
        int iminx = BinaryPrimitives.ReadInt32LittleEndian(data[12..]);
        int iminy = BinaryPrimitives.ReadInt32LittleEndian(data[16..]);
        int imaxx = BinaryPrimitives.ReadInt32LittleEndian(data[20..]);
        int imaxy = BinaryPrimitives.ReadInt32LittleEndian(data[24..]);
        
        bounds = IntRect.FromMinMax(iminx, iminy, imaxx, imaxy);

        bytesConsumed += 4 * 5;
        return OperationStatus.Done;
    }

    public static VectorImage Parse(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[HeaderSize];
        stream.ReadExactly(buffer);

        OperationStatus status = Parse(
            buffer, out int bytesConsumed, out uint count, out IntRect bounds);
        
        Debug.Assert(buffer.Length == bytesConsumed);
        ThrowHelper.ThrowOnInvalid(status);
        
        Geometry[] geometries = new Geometry[count];

        for (int i = 0; i < geometries.Length; i++)
        {
            Geometry.Read(stream, out geometries[i]);
        }

        return new VectorImage(geometries, bounds);
    }

    public void Save(Stream stream)
    {
        using BinaryWriter writer = new(stream);

        writer.Write(Signature);
        writer.Write(Version);

        ReadOnlySpan<Geometry> geometries = mGeometries.Span;
        writer.Write(geometries.Length);

        mBounds.Write(writer);

        foreach (ref readonly Geometry geo in geometries)
        {
            geo.Write(writer);
        }
    }
}
