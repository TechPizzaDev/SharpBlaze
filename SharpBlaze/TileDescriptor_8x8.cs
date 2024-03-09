using System;
using System.Diagnostics;

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
        int v =
            t[0] | t[1] | t[2] | t[3] | t[4] | t[5] | t[6] | t[7];

        // Zero means there are no non-zero values there.
        return v == 0;
    }


    public static unsafe void FillStartCovers(int* p, int value)
    {
        Debug.Assert(p != null);

        p[0] = value;
        p[1] = value;
        p[2] = value;
        p[3] = value;
        p[4] = value;
        p[5] = value;
        p[6] = value;
        p[7] = value;
    }


    public static unsafe void AccumulateStartCovers(int* p, int value)
    {
        int p0 = p[0];
        int p1 = p[1];
        int p2 = p[2];
        int p3 = p[3];

        p[0] = value + p0;
        p[1] = value + p1;
        p[2] = value + p2;
        p[3] = value + p3;

        int p4 = p[4];
        int p5 = p[5];
        int p6 = p[6];
        int p7 = p[7];

        p[4] = value + p4;
        p[5] = value + p5;
        p[6] = value + p6;
        p[7] = value + p7;
    }


    public static ReadOnlySpan<int> ZeroCovers => new int[8];

    [Obsolete]
    public TileDescriptor_8x8() { }
}
