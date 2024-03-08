using System.Diagnostics;

namespace SharpBlaze;

/**
 * Represents a rectangle in destination image coordinates, measured in tiles.
 */
public struct TileBounds
{
    public TileBounds(
        TileIndex x,
        TileIndex y,
        TileIndex horizontalCount,
        TileIndex verticalCount)
    {
        Debug.Assert(ColumnCount > 0);
        Debug.Assert(RowCount > 0);

        X = x;
        Y = y;
        ColumnCount = horizontalCount;
        RowCount = verticalCount;
    }

    // Minimum horizontal and vertical tile indices.
    public TileIndex X;
    public TileIndex Y;

    // Horizontal and vertical tile counts. Total number of tiles covered
    // by a geometry can be calculated by multiplying these two values.
    public TileIndex ColumnCount;
    public TileIndex RowCount;
};
