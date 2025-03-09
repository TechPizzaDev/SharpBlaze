using System;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

/// <summary>
/// Descriptor for linearization into 8×32 pixel tiles.
/// </summary>
public struct TileDescriptor_8x32 : ITileDescriptor<TileDescriptor_8x32>
{
    /// <inheritdoc />
    public static int TileW => 8;

    /// <inheritdoc />
    public static int TileH => 32;

    /// <inheritdoc />
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t)
    {
        // Combine all 32 values.
        Vector512<int> v = Vector512.Create(t) | Vector512.Create(t.Slice(16));

        // Zero means there are no non-zero values there.
        return Vector512.EqualsAll(v, Vector512<int>.Zero);
    }

    /// <inheritdoc />
    public static void FillStartCovers(Span<int> p, int value)
    {
        Vector512<int> v = Vector512.Create(value);
        v.CopyTo(p);
        v.CopyTo(p.Slice(16));
    }

    /// <inheritdoc />
    public static void AccumulateStartCovers(Span<int> p, int value)
    {
        Vector512<int> v = Vector512.Create(value);
        
        (v + Vector512.Create<int>(p)).CopyTo(p);
        p = p.Slice(16);
        (v + Vector512.Create<int>(p)).CopyTo(p);
    }
}
