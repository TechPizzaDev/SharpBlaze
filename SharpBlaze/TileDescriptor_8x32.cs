using System;
using System.Diagnostics;

namespace SharpBlaze;


/**
 * Descriptor for linearization into 8×32 pixel tiles.
 */
public struct TileDescriptor_8x32
{

    /**
     * Tile width in pixels.
     */
    public const int TileW = 8;


    /**
     * Tile height in pixels.
     */
    public const int TileH = 32;


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
        int v =
            t[0] | t[1] | t[2] | t[3] | t[4] | t[5] | t[6] | t[7] |
            t[8] | t[9] | t[10] | t[11] | t[12] | t[13] | t[14] | t[15] |
            t[16] | t[17] | t[18] | t[19] | t[20] | t[21] | t[22] | t[23] |
            t[24] | t[25] | t[26] | t[27] | t[28] | t[29] | t[30] | t[31];

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
        p[8] = value;
        p[9] = value;
        p[10] = value;
        p[11] = value;
        p[12] = value;
        p[13] = value;
        p[14] = value;
        p[15] = value;
        p[16] = value;
        p[17] = value;
        p[18] = value;
        p[19] = value;
        p[20] = value;
        p[21] = value;
        p[22] = value;
        p[23] = value;
        p[24] = value;
        p[25] = value;
        p[26] = value;
        p[27] = value;
        p[28] = value;
        p[29] = value;
        p[30] = value;
        p[31] = value;
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

        int p8 = p[8];
        int p9 = p[9];
        int p10 = p[10];
        int p11 = p[11];

        p[8] = value + p8;
        p[9] = value + p9;
        p[10] = value + p10;
        p[11] = value + p11;

        int p12 = p[12];
        int p13 = p[13];
        int p14 = p[14];
        int p15 = p[15];

        p[12] = value + p12;
        p[13] = value + p13;
        p[14] = value + p14;
        p[15] = value + p15;

        int p16 = p[16];
        int p17 = p[17];
        int p18 = p[18];
        int p19 = p[19];

        p[16] = value + p16;
        p[17] = value + p17;
        p[18] = value + p18;
        p[19] = value + p19;

        int p20 = p[20];
        int p21 = p[21];
        int p22 = p[22];
        int p23 = p[23];

        p[20] = value + p20;
        p[21] = value + p21;
        p[22] = value + p22;
        p[23] = value + p23;

        int p24 = p[24];
        int p25 = p[25];
        int p26 = p[26];
        int p27 = p[27];

        p[24] = value + p24;
        p[25] = value + p25;
        p[26] = value + p26;
        p[27] = value + p27;

        int p28 = p[28];
        int p29 = p[29];
        int p30 = p[30];
        int p31 = p[31];

        p[28] = value + p28;
        p[29] = value + p29;
        p[30] = value + p30;
        p[31] = value + p31;
    }


    public static ReadOnlySpan<int> ZeroCovers => new int[32];

    [Obsolete]
    public TileDescriptor_8x32() { }
}
