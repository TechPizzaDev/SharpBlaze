using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace SharpBlaze;


/**
 * Descriptor for linearization into 8×8 pixel tiles.
 */
public struct TileDescriptor_8x8 : ITileDescriptor
{
    /**
     * Tile width in pixels.
     */
    public static int TileW => 8;


    /**
     * Tile height in pixels.
     */
    public static int TileH => 8;


    /**
     * Tile width in 24.8 fixed point format.
     */
    public static F24Dot8 TileWF24Dot8 => 1 << 11;


    /**
     * Tile height in 24.8 fixed point format.
     */
    public static F24Dot8 TileHF24Dot8 => 1 << 11;


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
        return (TileIndex) (y >> 11);
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
        return (TileIndex) (y >> 3);
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
        return (int) (y) << 3;
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
        return (F24Dot8) (int) (y) << 11;
    }


    public static unsafe bool CoverArrayContainsOnlyZeroes(int* t)
    {
        Debug.Assert(t != null);

        // Combine all 8 values.
        var v = Vector256.Load(t);

        // Zero means there are no non-zero values there.
        return Vector256.EqualsAll(v, Vector256<int>.Zero);
    }


    public static unsafe void FillStartCovers(int* p, int value)
    {
        Debug.Assert(p != null);

        Vector256.Create(value).Store(p);
    }


    public static unsafe void AccumulateStartCovers(int* p, int value)
    {
        (Vector256.Create(value) + Vector256.Load(p)).Store(p);
    }

    [Obsolete]
    public TileDescriptor_8x8() { }
}
