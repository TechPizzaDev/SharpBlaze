using System.Diagnostics;
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

    byte* Bytes;
    readonly byte* End;

    public readonly partial ulong GetRemainingByteCount();
    public partial sbyte ReadInt8();
    public partial byte ReadUInt8();
    public partial int ReadInt32();
    public partial uint ReadUInt32();
    public partial void ReadBinary(byte* d, ulong length);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial ulong GetRemainingByteCount()
    {
        return (ulong) (End - Bytes);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public partial uint ReadUInt32()
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
    public partial sbyte ReadInt8()
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
    public partial byte ReadUInt8()
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
    public partial int ReadInt32()
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
    public partial void ReadBinary(byte* d, ulong length)
    {
        Debug.Assert(GetRemainingByteCount() >= length);

        NativeMemory.Copy(Bytes, d, (nuint) length);

        Bytes += length;
    }
}

public unsafe partial class VectorImage
{
    public VectorImage()
    {
        mBounds = new(0, 0, 0, 0);
    }


    ~VectorImage()
    {
        Free();
    }


    public partial void Parse(byte* binary, ulong length)
    {
        Debug.Assert(binary != null);
        Debug.Assert(length > 0);

        Free();

        BinaryReader br = new(binary, length);

        // Read signature.
        byte B = br.ReadUInt8();
        byte v = br.ReadUInt8();
        byte e = br.ReadUInt8();
        byte c = br.ReadUInt8();

        if (B != 'B' || v != 'v' || e != 'e' || c != 'c')
        {
            return;
        }

        // Read version.
        uint version = br.ReadUInt32();

        if (version != 1)
        {
            return;
        }

        // 4 bytes, total path count.
        uint count = br.ReadUInt32();

        // 16 bytes, full bounds.
        int iminx = br.ReadInt32();
        int iminy = br.ReadInt32();
        int imaxx = br.ReadInt32();
        int imaxy = br.ReadInt32();

        mBounds = new IntRect(iminx, iminy, imaxx - iminx, imaxy - iminy);

        // Each path entry is at least 32 bytes plus 4 bytes indicating path count
        // plus 16 bytes indicating full bounds plus 8 bytes for signature and
        // version.
        if (length < ((count * 32) + 4 + 16 + 8))
        {
            // File is smaller than it says it has paths in it.
            return;
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

            *geometry = new Geometry(new IntRect(pminx, pminy, pmaxx - pminx, pmaxy - pminy),
                tags, points, Matrix.Identity, (int) tagCount, (int) pointCount, color,
                fillRule);

            mGeometryCount++;
        }
    }


    private partial void Free()
    {
        int count = mGeometryCount;

        for (int i = 0; i < count; i++)
        {
            NativeMemory.Free((void*) mGeometries[i].Tags);
            NativeMemory.Free((void*) mGeometries[i].Points);
        }

        NativeMemory.Free(mGeometries);

        mGeometries = null;
        mGeometryCount = 0;
    }
}