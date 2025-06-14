using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<F24Dot8> t)
    {
        // Combine all 8 values.
        Vector256<int> v = Vector256.Create(MemoryMarshal.Cast<F24Dot8, int>(t));

        // Zero means there are no non-zero values there.
        return Vector256.EqualsAll(v, Vector256<int>.Zero);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillStartCovers(Span<F24Dot8> p, int value)
    {
        Vector256.Create(value).CopyTo(MemoryMarshal.Cast<F24Dot8, int>(p));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AccumulateStartCovers(Span<F24Dot8> p, int value)
    {
        Span<int> bits = MemoryMarshal.Cast<F24Dot8, int>(p);
        (Vector256.Create(value) + Vector256.Create<int>(bits)).CopyTo(bits);
    }
}
