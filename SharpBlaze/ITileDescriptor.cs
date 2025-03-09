using System;

namespace SharpBlaze;

public unsafe interface ITileDescriptor
{
    /**
     * Tile width in pixels.
     */
    static abstract int TileW { get; }

    /**
     * Tile height in pixels.
     */
    static abstract int TileH { get; }

    /**
     * Tile width in 24.8 fixed point format.
     */
    static abstract F24Dot8 TileWF24Dot8 { get; }


    /**
     * Tile height in 24.8 fixed point format.
     */
    static abstract F24Dot8 TileHF24Dot8 { get; }

    /**
     * Converts X value expressed as 24.8 fixed point number to horizontal tile
     * index.
     */
    static abstract TileIndex F24Dot8ToTileColumnIndex(F24Dot8 x);

    /**
     * Converts Y value expressed as 24.8 fixed point number to vertical tile
     * index.
     */
    static abstract TileIndex F24Dot8ToTileRowIndex(F24Dot8 y);

    /**
     * Converts X value to horizontal tile index.
     */
    static abstract TileIndex PointsToTileColumnIndex(int x);

    /**
     * Converts Y value to vertical tile index.
     */
    static abstract TileIndex PointsToTileRowIndex(int y);

    /**
     * Converts horizontal tile index to X value.
     */
    static abstract int TileColumnIndexToPoints(TileIndex x);

    /**
     * Converts vertical tile index to Y value.
     */
    static abstract int TileRowIndexToPoints(TileIndex y);

    /**
     * Returns given vertical tile index to position in 24.8 format.
     */
    static abstract F24Dot8 TileColumnIndexToF24Dot8(TileIndex x);

    /**
     * Returns given horizontal tile index to position in 24.8 format.
     */
    static abstract F24Dot8 TileRowIndexToF24Dot8(TileIndex y);

    static abstract bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t);

    static abstract void FillStartCovers(Span<int> p, int value);

    static abstract void AccumulateStartCovers(Span<int> p, int value);
}
