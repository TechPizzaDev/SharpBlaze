using System;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

/// <summary>
/// Descriptor for linearization into 8×16 pixel tiles.
/// </summary>
public struct TileDescriptor_8x16 : ITileDescriptor<TileDescriptor_8x16>
{
    /// <inheritdoc />
    public static int TileW => 8;

    /// <inheritdoc />
    public static int TileH => 16;

    /// <inheritdoc />
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t)
    {
        // Combine all 16 values.
        Vector512<int> v = Vector512.Create(t);

        // Zero means there are no non-zero values there.
        return Vector512.EqualsAll(v, Vector512<int>.Zero);
    }

    /// <inheritdoc />
    public static void FillStartCovers(Span<int> p, int value)
    {
        Vector512.Create(value).CopyTo(p);
    }

    /// <inheritdoc />
    public static void AccumulateStartCovers(Span<int> p, int value)
    {
        (Vector512.Create(value) + Vector512.Create<int>(p)).CopyTo(p);
    }
}
