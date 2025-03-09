using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace SharpBlaze;


/**
 * Descriptor for linearization into 8×16 pixel tiles.
 */
public struct TileDescriptor_8x16 : ITileDescriptor
{

    /**
     * Tile width in pixels.
     */
    public static int TileW => 8;


    /**
     * Tile height in pixels.
     */
    public static int TileH => 16;


    /**
     * Tile width in 24.8 fixed point format.
     */
    public static F24Dot8 TileWF24Dot8 => 1 << 11;


    /**
     * Tile height in 24.8 fixed point format.
     */
    public static F24Dot8 TileHF24Dot8 => 1 << 12;


    /**
     * Converts X value expressed as 24.8 fixed point number to horizontal tile
     * index.
     */
    public static TileIndex F24Dot8ToTileColumnIndex(F24Dot8 x)
    {
        return (TileIndex) (x >> 11);
    }


    /**
     * Converts Y value expressed as 24.8 fixed point number to vertical tile
     * index.
     */
    public static TileIndex F24Dot8ToTileRowIndex(F24Dot8 y)
    {
        return (TileIndex) (y >> 12);
    }


    /**
     * Converts X value to horizontal tile index.
     */
    public static TileIndex PointsToTileColumnIndex(int x)
    {
        return (TileIndex) (x >> 3);
    }


    /**
     * Converts Y value to vertical tile index.
     */
    public static TileIndex PointsToTileRowIndex(int y)
    {
        return (TileIndex) (y >> 4);
    }


    /**
     * Converts horizontal tile index to X value.
     */
    public static int TileColumnIndexToPoints(TileIndex x)
    {
        return (int) (x) << 3;
    }


    /**
     * Converts vertical tile index to Y value.
     */
    public static int TileRowIndexToPoints(TileIndex y)
    {
        return (int) (y) << 4;
    }


    /**
     * Returns given vertical tile index to position in 24.8 format.
     */
    public static F24Dot8 TileColumnIndexToF24Dot8(TileIndex x)
    {
        return (F24Dot8) (int) (x) << 11;
    }


    /**
     * Returns given horizontal tile index to position in 24.8 format.
     */
    public static F24Dot8 TileRowIndexToF24Dot8(TileIndex y)
    {
        return (F24Dot8) (int) (y) << 12;
    }

    
    public static bool CoverArrayContainsOnlyZeroes(ReadOnlySpan<int> t)
    {
        // Combine all 16 values.
        Vector512<int> v = Vector512.Create(t);

        // Zero means there are no non-zero values there.
        return Vector512.EqualsAll(v, Vector512<int>.Zero);
    }


    public static void FillStartCovers(Span<int> p, int value)
    {
        Vector512.Create(value).CopyTo(p);
    }


    public static void AccumulateStartCovers(Span<int> p, int value)
    {
        (Vector512.Create(value) + Vector512.Create<int>(p)).CopyTo(p);
    }


    [Obsolete]
    public TileDescriptor_8x16() { }
}
