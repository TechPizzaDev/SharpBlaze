using System;
using System.Runtime.CompilerServices;

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
        get => new(T.TileW << 8);
    }

    /// <summary>
    /// Tile height in 24.8 fixed point format.
    /// </summary>
    static virtual F24Dot8 TileHF24Dot8
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(T.TileH << 8);
    }

    /// <summary>
    /// Converts X value expressed as 24.8 fixed point number to horizontal tile index.
    /// </summary>
    static virtual TileIndex F24Dot8ToTileColumnIndex(F24Dot8 x) => (TileIndex) (x / T.TileWF24Dot8);

    /// <summary>
    /// Converts Y value expressed as 24.8 fixed point number to vertical tile index.
    /// </summary>
    static virtual TileIndex F24Dot8ToTileRowIndex(F24Dot8 y) => (TileIndex) (y / T.TileHF24Dot8);

    /// <summary>
    /// Converts X value to horizontal tile index.
    /// </summary>
    static virtual TileIndex PointsToTileColumnIndex(int x) => (TileIndex) (x / T.TileW);

    /// <summary>
    /// Converts Y value to vertical tile index.
    /// </summary>
    static virtual TileIndex PointsToTileRowIndex(int y) => (TileIndex) (y / T.TileH);

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
    static virtual F24Dot8 TileColumnIndexToF24Dot8(TileIndex x) => new F24Dot8((int) x) * T.TileWF24Dot8;

    /// <summary>
    /// Returns given horizontal tile index to position in 24.8 format.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual F24Dot8 TileRowIndexToF24Dot8(TileIndex y) => new F24Dot8((int) y) * T.TileHF24Dot8;

    static abstract bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t);

    static abstract void FillStartCovers(Span<int> p, int value);

    static abstract void AccumulateStartCovers(Span<int> p, int value);
}
