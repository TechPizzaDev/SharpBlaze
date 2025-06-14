using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<F24Dot8> t)
    {
        // Combine all 32 values.
        ReadOnlySpan<int> bits = MemoryMarshal.Cast<F24Dot8, int>(t);
        Vector512<int> v = Vector512.Create(bits) | Vector512.Create(bits[16..]);

        // Zero means there are no non-zero values there.
        return Vector512.EqualsAll(v, Vector512<int>.Zero);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FillStartCovers(Span<F24Dot8> p, int value)
    {
        Vector512<int> v = Vector512.Create(value);
        Span<int> bits = MemoryMarshal.Cast<F24Dot8, int>(p);
        v.CopyTo(bits);
        v.CopyTo(bits[16..]);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AccumulateStartCovers(Span<F24Dot8> p, int value)
    {
        Vector512<int> v = Vector512.Create(value);
        
        Span<int> bits = MemoryMarshal.Cast<F24Dot8, int>(p); 
        (v + Vector512.Create<int>(bits)).CopyTo(bits);
        bits = bits[16..];
        (v + Vector512.Create<int>(bits)).CopyTo(bits);
    }
}
