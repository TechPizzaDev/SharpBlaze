using System;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

/// <summary>
/// Descriptor for linearization into 8×8 pixel tiles.
/// </summary>
public struct TileDescriptor_8x4 : ITileDescriptor<TileDescriptor_8x4>
{
    /// <inheritdoc />
    public static int TileW => 8;

    /// <inheritdoc />
    public static int TileH => 4;

    /// <inheritdoc />
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t)
    {
        // Combine all 8 values.
        Vector128<int> v = Vector128.Create(t);

        // Zero means there are no non-zero values there.
        return Vector128.EqualsAll(v, Vector128<int>.Zero);
    }

    /// <inheritdoc />
    public static void FillStartCovers(Span<int> p, int value)
    {
        Vector128.Create(value).CopyTo(p);
    }

    /// <inheritdoc />
    public static void AccumulateStartCovers(Span<int> p, int value)
    {
        (Vector128.Create(value) + Vector128.Create<int>(p)).CopyTo(p);
    }
}
