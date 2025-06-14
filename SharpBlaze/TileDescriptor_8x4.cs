using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<F24Dot8> t)
    {
        // Combine all 8 values.
        Vector128<int> v = Vector128.Create(MemoryMarshal.Cast<F24Dot8, int>(t));

        // Zero means there are no non-zero values there.
        return Vector128.EqualsAll(v, Vector128<int>.Zero);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillStartCovers(Span<F24Dot8> p, int value)
    {
        Vector128.Create(value).CopyTo(MemoryMarshal.Cast<F24Dot8, int>(p));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AccumulateStartCovers(Span<F24Dot8> p, int value)
    {
        Span<int> bits = MemoryMarshal.Cast<F24Dot8, int>(p);
        (Vector128.Create(value) + Vector128.Create<int>(bits)).CopyTo(bits);
    }
}
