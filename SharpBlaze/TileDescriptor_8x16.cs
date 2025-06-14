using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<F24Dot8> t)
    {
        // Combine all 16 values.
        Vector512<int> v = Vector512.Create(MemoryMarshal.Cast<F24Dot8, int>(t));

        // Zero means there are no non-zero values there.
        return Vector512.EqualsAll(v, Vector512<int>.Zero);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillStartCovers(Span<F24Dot8> p, int value)
    {
        Vector512.Create(value).CopyTo(MemoryMarshal.Cast<F24Dot8, int>(p));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AccumulateStartCovers(Span<F24Dot8> p, int value)
    {
        Span<int> bits = MemoryMarshal.Cast<F24Dot8, int>(p);
        (Vector512.Create(value) + Vector512.Create<int>(bits)).CopyTo(bits);
    }
}
