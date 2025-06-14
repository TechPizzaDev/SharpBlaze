using System;
using System.Runtime.CompilerServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

/// <summary>
/// Descriptor for linearization into tiles.
/// </summary>
/// <typeparam name="T">Self type.</typeparam>
public interface ITileDescriptor<T>
    where T : ITileDescriptor<T>
{
    /// <summary>
    /// Tile width in pixels.
    /// </summary>
    static abstract int TileW { get; }

    /// <summary>
    /// Tile height in pixels.
    /// </summary>
    static abstract int TileH { get; }

    /// <summary>
    /// Tile width in 24.8 fixed point format.
    /// </summary>
    static virtual F24Dot8 TileWF24Dot8
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => T.TileW.ToF24D8();
    }

    /// <summary>
    /// Tile height in 24.8 fixed point format.
    /// </summary>
    static virtual F24Dot8 TileHF24Dot8
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => T.TileH.ToF24D8();
    }

    /// <summary>
    /// Converts X value expressed as 24.8 fixed point number to horizontal tile index.
    /// </summary>
    static virtual TileIndex F24Dot8ToTileColumnIndex(F24Dot8 x) => (uint) x.ToBits() / (uint) T.TileWF24Dot8.ToBits();

    /// <summary>
    /// Converts Y value expressed as 24.8 fixed point number to vertical tile index.
    /// </summary>
    static virtual TileIndex F24Dot8ToTileRowIndex(F24Dot8 y) => (uint) y.ToBits() / (uint) T.TileHF24Dot8.ToBits();

    /// <summary>
    /// Converts X value to horizontal tile index.
    /// </summary>
    static virtual TileIndex PointsToTileColumnIndex(int x) => (uint) x / (uint) T.TileW;

    /// <summary>
    /// Converts Y value to vertical tile index.
    /// </summary>
    static virtual TileIndex PointsToTileRowIndex(int y) => (uint) y / (uint) T.TileH;

    /// <summary>
    /// Converts horizontal tile index to X value.
    /// </summary>
    static virtual int TileColumnIndexToPoints(TileIndex x) => (int) (x * (uint) T.TileW);

    /// <summary>
    /// Converts vertical tile index to Y value.
    /// </summary>
    static virtual int TileRowIndexToPoints(TileIndex y) => (int) (y * (uint) T.TileH);

    /// <summary>
    /// Returns given vertical tile index to position in 24.8 format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual F24Dot8 TileColumnIndexToF24Dot8(TileIndex x) => F24Dot8.FromBits(x * (uint) T.TileWF24Dot8.ToBits());

    /// <summary>
    /// Returns given horizontal tile index to position in 24.8 format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual F24Dot8 TileRowIndexToF24Dot8(TileIndex y) => F24Dot8.FromBits(y * (uint) T.TileHF24Dot8.ToBits());

    static abstract bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<F24Dot8> t);

    static abstract void FillStartCovers(Span<F24Dot8> p, int value);

    static abstract void AccumulateStartCovers(Span<F24Dot8> p, int value);
}
