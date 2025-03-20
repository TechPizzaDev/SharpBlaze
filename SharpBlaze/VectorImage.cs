using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpBlaze;


internal unsafe partial struct BinaryReader
{
    public BinaryReader(byte* binary, ulong length)
    {
        Bytes = binary;
        End = binary + length;
    }

    private byte* Bytes;
    private readonly byte* End;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong GetRemainingByteCount()
    {
        return (ulong) (End - Bytes);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        ulong r = (ulong) (End - Bytes);

        if (r >= 4)
        {
            uint n = Unsafe.ReadUnaligned<uint>(Bytes);

            Bytes += 4;

            return n;
        }

        return 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadInt8()
    {
        if (Bytes < End)
        {
            sbyte n = (sbyte) *Bytes;

            Bytes++;

            return n;
        }

        return 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadUInt8()
    {
        if (Bytes < End)
        {
            byte n = *Bytes;

            Bytes++;

            return n;
        }

        return 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        ulong r = (ulong) (End - Bytes);

        if (r >= 4)
        {
            int n = Unsafe.ReadUnaligned<int>(Bytes);

            Bytes += 4;

            return n;
        }

        return 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReadBinary(byte* d, ulong length)
    {
        Debug.Assert(GetRemainingByteCount() >= length);

        NativeMemory.Copy(Bytes, d, (nuint) length);

        Bytes += length;
    }

    public void ReadBinary(Span<byte> d)
    {
        Debug.Assert(GetRemainingByteCount() >= (uint) d.Length);

        new Span<byte>(Bytes, d.Length).CopyTo(d);

        Bytes += d.Length;
    }
}

public unsafe partial class VectorImage
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


    public OperationStatus Parse(byte* binary, ulong length)
    {
        Debug.Assert(binary != null);
        Debug.Assert(length > 0);

        if (length < 4 + 4 * 6)
        {
            return OperationStatus.NeedMoreData;
        }

        BinaryReader br = new(binary, length);

        // Read signature.
        byte B = br.ReadUInt8();
        byte v = br.ReadUInt8();
        byte e = br.ReadUInt8();
        byte c = br.ReadUInt8();

        if (!Signature.SequenceEqual([B, v, e, c]))
        {
            return OperationStatus.InvalidData;
        }

        // Read version.
        uint version = br.ReadUInt32();

        if (version != 1)
        {
            return OperationStatus.InvalidData;
        }

        // 4 bytes, total path count.
        uint count = br.ReadUInt32();

        // 16 bytes, full bounds.
        int iminx = br.ReadInt32();
        int iminy = br.ReadInt32();
        int imaxx = br.ReadInt32();
        int imaxy = br.ReadInt32();

        mBounds = IntRect.FromMinMax(iminx, iminy, imaxx, imaxy);

        // Each path entry is at least 32 bytes plus 4 bytes indicating path count
        // plus 16 bytes indicating full bounds plus 8 bytes for signature and
        // version.
        if (length < ((count * 32) + 4 + 16 + 8))
        {
            // File is smaller than it says it has paths in it.
            return OperationStatus.NeedMoreData;
        }

        Geometry[] geometries = new Geometry[count];

        int i = 0;
        for (; i < count; i++)
        {
            // 4 bytes, color as premultiplied RGBA8.
            uint color = br.ReadUInt32();

            // 16 bytes, path bounds.
            int pminx = br.ReadInt32();
            int pminy = br.ReadInt32();
            int pmaxx = br.ReadInt32();
            int pmaxy = br.ReadInt32();

            // 4 bytes, fill rule.
            FillRule fillRule = (FillRule) (br.ReadUInt32() & 1);

            // 4 bytes, tag count.
            uint tagCount = br.ReadUInt32();

            // 4 bytes, point count.
            uint pointCount = br.ReadUInt32();

            ulong memoryNeeded = tagCount + (pointCount * (uint) Unsafe.SizeOf<FloatPoint>());

            if (br.GetRemainingByteCount() < memoryNeeded)
            {
                // Less bytes left to read than the file says there are tags and
                // points stored.
                break;
            }

            PathTag[] tags = new PathTag[tagCount];
            FloatPoint[] points = new FloatPoint[pointCount];

            br.ReadBinary(MemoryMarshal.AsBytes(tags.AsSpan()));
            br.ReadBinary(MemoryMarshal.AsBytes(points.AsSpan()));

            geometries[i] = new Geometry(
                IntRect.FromMinMax(pminx, pminy, pmaxx, pmaxy),
                tags, points,
                Matrix.Identity,
                color,
                fillRule);
        }

        Array.Resize(ref geometries, i);
        mGeometries = geometries;

        return OperationStatus.Done;
    }

    public void Save(Stream stream)
    {
        using BinaryWriter writer = new(stream);

        writer.Write(Signature);
        writer.Write(Version);

        ReadOnlySpan<Geometry> geometries = mGeometries.Span;
        writer.Write(geometries.Length);

        Write(writer, mBounds);

        for (int i = 0; i < geometries.Length; i++)
        {
            ref readonly Geometry geo = ref geometries[i];

            writer.Write(geo.Color);

            Write(writer, geo.PathBounds);

            writer.Write((uint) geo.Rule);

            writer.Write(geo.Tags.Length);
            writer.Write(geo.Points.Length);

            writer.Write(MemoryMarshal.AsBytes(geo.Tags.Span));

            foreach (FloatPoint point in geo.Points.Span)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
        }
    }

    private static void Write(BinaryWriter writer, IntRect rect)
    {
        (int minX, int minY, int maxX, int maxY) = rect;
        writer.Write(minX);
        writer.Write(minY);
        writer.Write(maxX);
        writer.Write(maxY);
    }
}