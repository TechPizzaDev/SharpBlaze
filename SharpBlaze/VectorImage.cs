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
}

public unsafe partial class VectorImage
{
    public static ReadOnlySpan<byte> Signature => "Bvec"u8;

    public static uint Version => 1u;

    public VectorImage()
    {
        mBounds = new(0, 0, 0, 0);
    }

    public VectorImage(Geometry* geometries, int geometryCount, IntRect bounds)
    {
        mGeometries = geometries;
        mGeometryCount = geometryCount;
        mBounds = bounds;
    }

    ~VectorImage()
    {
        Free();
    }


    public OperationStatus Parse(byte* binary, ulong length)
    {
        Debug.Assert(binary != null);
        Debug.Assert(length > 0);

        Free();

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

        mGeometries = (Geometry*) NativeMemory.Alloc((nuint) (sizeof(Geometry) * count));

        for (uint i = 0; i < count; i++)
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

            ulong memoryNeeded = tagCount + (pointCount * 16);

            if (br.GetRemainingByteCount() < memoryNeeded)
            {
                // Less bytes left to read than the file says there are tags and
                // points stored.
                break;
            }

            PathTag* tags = (PathTag*) NativeMemory.Alloc(tagCount);
            FloatPoint* points = (FloatPoint*) NativeMemory.Alloc(pointCount * 16);

            br.ReadBinary((byte*) tags, tagCount);
            br.ReadBinary((byte*) points, pointCount * 16);

            Geometry* geometry = mGeometries + mGeometryCount;

            *geometry = new Geometry(IntRect.FromMinMax(pminx, pminy, pmaxx, pmaxy),
                tags, points, Matrix.Identity, (int) tagCount, (int) pointCount, color,
                fillRule);

            mGeometryCount++;
        }

        return OperationStatus.Done;
    }

    public void Save(Stream stream)
    {
        using BinaryWriter writer = new(stream);

        writer.Write(Signature);
        writer.Write(Version);

        writer.Write(mGeometryCount);

        writer.Write(mBounds.MinX);
        writer.Write(mBounds.MinY);
        writer.Write(mBounds.MaxX);
        writer.Write(mBounds.MaxY);

        for (int i = 0; i < mGeometryCount; i++)
        {
            Geometry* geo = &mGeometries[i];

            writer.Write(geo->Color);

            writer.Write(geo->PathBounds.MinX);
            writer.Write(geo->PathBounds.MinY);
            writer.Write(geo->PathBounds.MaxX);
            writer.Write(geo->PathBounds.MaxY);

            writer.Write((uint) geo->Rule);

            writer.Write(geo->TagCount);
            writer.Write(geo->PointCount);

            writer.Write(new Span<byte>(geo->Tags, geo->TagCount));

            for (int j = 0; j < geo->PointCount; j++)
            {
                FloatPoint point = geo->Points[j];
                writer.Write(point.X);
                writer.Write(point.Y);
            }
        }
    }


    private void Free()
    {
        int count = mGeometryCount;

        for (int i = 0; i < count; i++)
        {
            NativeMemory.Free(mGeometries[i].Tags);
            NativeMemory.Free(mGeometries[i].Points);
        }

        NativeMemory.Free(mGeometries);

        mGeometries = null;
        mGeometryCount = 0;
    }
}