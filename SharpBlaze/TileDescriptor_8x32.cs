using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace SharpBlaze;


/**
 * Descriptor for linearization into 8×32 pixel tiles.
 */
public struct TileDescriptor_8x32 : ITileDescriptor
{

    /**
     * Tile width in pixels.
     */
    public static int TileW => 8;


    /**
     * Tile height in pixels.
     */
    public static int TileH => 32;


    /**
     * Tile width in 24.8 fixed point format.
     */
    public static F24Dot8 TileWF24Dot8 => 1 << 11;


    /**
     * Tile height in 24.8 fixed point format.
     */
    public static F24Dot8 TileHF24Dot8 => 1 << 13;


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
        return (TileIndex) (y >> 13);
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
        return (TileIndex) (y >> 5);
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
        return (int) (y) << 5;
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
        return (F24Dot8) (int) (y) << 13;
    }


    public static unsafe bool CoverArrayContainsOnlyZeroes(int* t)
    {
        Debug.Assert(t != null);

        // Combine all 32 values.
        var v = Vector512.Load(t) | Vector512.Load(t + 16);

        // Zero means there are no non-zero values there.
        return Vector512.EqualsAll(v, Vector512<int>.Zero);
    }


    public static unsafe void FillStartCovers(int* p, int value)
    {
        Debug.Assert(p != null);

        var v = Vector512.Create(value);
        v.Store(p);
        v.Store(p + 16);
    }


    public static unsafe void AccumulateStartCovers(int* p, int value)
    {
        var v = Vector512.Create(value);
        
        (v + Vector512.Load(p)).Store(p);
        (v + Vector512.Load(p + 16)).Store(p + 16);
    }


    [Obsolete]
    public TileDescriptor_8x32() { }
}
