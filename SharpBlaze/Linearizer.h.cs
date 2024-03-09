using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public static class Linearizer
{

    /**
     * Calculates column count for a given image width in pixels.
     *
     * @param width Image width in pixels. Must be at least 1.
     */
    public static TileIndex CalculateColumnCount<T>(int width) where T : ITileDescriptor
    {
        Debug.Assert(width > 0);

        return T.PointsToTileColumnIndex(width + T.TileW - 1);
    }


    /**
     * Calculates row count for a given image height in pixels.
     *
     * @param height Image height in pixels. Must be at least 1.
     */
    public static TileIndex CalculateRowCount<T>(int height) where T : ITileDescriptor
    {
        Debug.Assert(height > 0);

        return T.PointsToTileRowIndex(height + T.TileH - 1);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TileBounds CalculateTileBounds<T>(int minx, int miny, int maxx, int maxy) where T : ITileDescriptor
    {
        Debug.Assert(minx >= 0);
        Debug.Assert(miny >= 0);
        Debug.Assert(minx < maxx);
        Debug.Assert(miny < maxy);

        TileIndex x = T.PointsToTileColumnIndex(minx);
        TileIndex y = T.PointsToTileRowIndex(miny);

        TileIndex horizontalCount = T.PointsToTileColumnIndex(
            maxx + T.TileW - 1) - x;

        TileIndex verticalCount = T.PointsToTileRowIndex(
            maxy + T.TileH - 1) - y;

        return new TileBounds(x, y, horizontalCount, verticalCount);
    }
}