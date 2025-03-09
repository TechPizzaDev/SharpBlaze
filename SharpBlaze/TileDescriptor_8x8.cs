using System;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

/// <summary>
/// Descriptor for linearization into 8×8 pixel tiles.
/// </summary>
public struct TileDescriptor_8x8 : ITileDescriptor<TileDescriptor_8x8>
{
    /// <inheritdoc />
    public static int TileW => 8;

    /// <inheritdoc />
    public static int TileH => 8;

    /// <inheritdoc />
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t)
    {
        // Combine all 8 values.
        Vector256<int> v = Vector256.Create(t);

        // Zero means there are no non-zero values there.
        return Vector256.EqualsAll(v, Vector256<int>.Zero);
    }

    /// <inheritdoc />
    public static void FillStartCovers(Span<int> p, int value)
    {
        Vector256.Create(value).CopyTo(p);
    }

    /// <inheritdoc />
    public static void AccumulateStartCovers(Span<int> p, int value)
    {
        (Vector256.Create(value) + Vector256.Create<int>(p)).CopyTo(p);
    }
}
