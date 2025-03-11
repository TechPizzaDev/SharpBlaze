using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

using static Utils;
using static CurveUtils;
using static LinearizerUtils;

using static F24Dot8;

/**
 * Takes one geometry containing path as input and processes it as follows -
 *
 *   • Transforms path points by transformation matrix configured for
 *     geometry.
 *   • Clips all transformed segments (lines, quadratic curves and cubic
 *     curves) to destination image bounds.
 *   • Converts all segments, including curves, into series of lines in 24.8
 *     fixed point format.
 *   • Divides all lines that cross horizontal interval boundaries into two
 *     lines at points where they intersect intervals. Interval height is
 *     defined by T template parameter.
 *   • Inserts all lines to corresponding line arrays.
 *   • Lines that fall to the left of destination image bounds are processed
 *     as "start cover" contributors.
 *
 * Normally, only one Linearizer per path should be created, but it is not an
 * error to create more than one. For example, if only one path is being
 * rendered in the current pass. Or when there is one abnormally large path,
 * much larger than others being rendered in the same pass.
 *
 * To create a Linearizer, call Create static function. It will allocate a new
 * Linearizer object and process all segments. It returns already processed
 * geometry.
 *
 * Important notes about memory management -
 *
 *   • Linearizer itself is created in task memory. Thi means that once the
 *     current task completes, that memory will be discarded. You must
 *     retrieve all important information from Linearizer object before task
 *     completes.
 *   • All line segment blocks are allocated in frame memory. Thi means that
 *     line segments will persist until the frame is complete. You do not have
 *     to copy line segment blocks before task ends.
 *   • Start cover table is allocated in frame memory.
 *   • All start covers are allocated in frame memory.
 *   • You must not delete Linearizer returned by Create function.
 */
public ref partial struct Linearizer<T, L>
    where T : unmanaged, ITileDescriptor<T>
    where L : unmanaged, ILineArray<L>
{
    /**
     * Returns tile bounds occupied by content this linearizer processed.
     */
    public readonly partial TileBounds GetTileBounds();


    /**
     * Returns A table of start cover arrays. The first item in this table
     * contains start covers of the first tile row, second item contains start
     * covers for the second row, etc. The number of items in the returned
     * table is equal to tile row count. Tile rows that did not have any
     * geometry to the left of destination image, will contain null in the
     * returned table.
     */
    public unsafe readonly partial int** GetStartCoverTable();


    /**
     * Returns line array at a given index.
     *
     * @param index Line array index. Must be at least zero and less than a
     * number of tile rows.
     */
    public partial ref L GetLineArrayAtIndex(TileIndex index);

    /**
     * Constructs Linearizer with bounds.
     */
    private Linearizer(TileBounds bounds, Span<L> lineArray)
    {
        mBounds = bounds;
        mLA = lineArray;
    }


    /**
     * Processes geometry assuming that all points within be strictly
     * contained within tile bounds. It still clamps all coordinates to make
     * sure crash does not happen, but no clipping is performed and
     * out-of-bounds geometry will look incorrect in the result.
     *
     * @param geometry Geometry to use as a source of segments. Must not be
     * null.
     *
     * @param memory Thread memory for allocations.
     */
    private unsafe partial void ProcessContained(in Geometry geometry, ThreadMemory memory);


    /**
     * Processes geometry assuming that parts of it may be out of bounds and
     * clipping should be performed. This method is generally slower than
     * ProcessContained because of doing extra work of determining how
     * individual segments contribute to the result.
     */
    private partial void ProcessUncontained(in Geometry geometry, ThreadMemory memory, ClipBounds clip,
        in Matrix matrix);


    private partial void AddUncontainedLine(ThreadMemory memory, ClipBounds clip,
        FloatPoint p0, FloatPoint p1);


    private partial void AddContainedLineF24Dot8(ThreadMemory memory, F24Dot8Point p0,
        F24Dot8Point p1);


    /**
     * Adds quadratic curve which potentially is not completely within
     * clipping bounds. Curve does not have to be monotonic.
     *
     * @param memory Thread memory.
     *
     * @param clip Clipping bounds.
     *
     * @param p Arbitrary quadratic curve.
     */
    private partial void AddUncontainedQuadratic(ThreadMemory memory, ClipBounds clip,
        scoped ReadOnlySpan<FloatPoint> p);


    /**
     * Adds quadratic curve which potentially is not completely within
     * clipping bounds. Curve must be monotonic.
     *
     * @param memory Thread memory.
     *
     * @param clip Clipping bounds.
     *
     * @param p Monotonic quadratic curve.
     */
    private partial void AddUncontainedMonotonicQuadratic(ThreadMemory memory,
        ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p);


    /**
     * Adds quadratic curve which potentially is not completely within
     * clipping bounds horizontally, but it is within top and bottom edges of
     * the clipping bounds. Curve must be monotonic.
     *
     * @param memory Thread memory.
     *
     * @param clip Clipping bounds.
     *
     * @param p Monotonic quadratic curve.
     */
    private partial void AddVerticallyContainedMonotonicQuadratic(ThreadMemory memory,
        ClipBounds clip, scoped Span<FloatPoint> p);


    /**
     * Adds quadratic curve completely contained within tile bounds. Curve
     * points are in 24.8 format.
     */
    private partial void AddContainedQuadraticF24Dot8(ThreadMemory memory,
        in F24Dot8PointX3 q);


    /**
     * Adds cubic curve which potentially is not completely within clipping
     * bounds. Curve does not have to be monotonic.
     *
     * @param memory Thread memory.
     *
     * @param clip Clipping bounds.
     *
     * @param p Arbitrary cubic curve.
     */
    private partial void AddUncontainedCubic(ThreadMemory memory,
        ClipBounds clip, 
        scoped ReadOnlySpan<FloatPoint> p);


    /**
     * Adds cubic curve which potentially is not completely within clipping
     * bounds. Curve must be monotonic.
     *
     * @param memory Thread memory.
     *
     * @param clip Clipping bounds.
     *
     * @param p Monotonic cubic curve.
     */
    private partial void AddUncontainedMonotonicCubic(ThreadMemory memory,
        ClipBounds clip,
        scoped ReadOnlySpan<FloatPoint> p);


    /**
     * Adds cubic curve which potentially is not completely within clipping
     * bounds horizontally, but it is within top and bottom edges of the
     * clipping bounds. Curve must be monotonic.
     *
     * @param memory Thread memory.
     *
     * @param clip Clipping bounds.
     *
     * @param p Monotonic cubic curve.
     */
    private partial void AddVerticallyContainedMonotonicCubic(ThreadMemory memory,
        ClipBounds clip, scoped Span<FloatPoint> p);


    private partial void AddPotentiallyUncontainedCubicF24Dot8(ThreadMemory memory,
        F24Dot8Point max, in F24Dot8PointX4 c);


    /**
     * Adds cubic curve completely contained within tile bounds. Curve points
     * are in 24.8 format.
     */
    private partial void AddContainedCubicF24Dot8(ThreadMemory memory,
        in F24Dot8PointX4 c);


    /**
     * Inserts vertical line to line array at a given index.
     */
    private partial void AppendVerticalLine(ThreadMemory memory, TileIndex rowIndex,
        F24Dot8 x, F24Dot8 y0, F24Dot8 y1);


    /**
     * ⬊
     */
    private partial void LineDownR(ThreadMemory memory, TileIndex rowIndex0,
        TileIndex rowIndex1, F24Dot8 dx, F24Dot8 dy,
        F24Dot8Point p0, F24Dot8Point p1);


    /**
     * ⬈
     */
    private partial void LineUpR(ThreadMemory memory, TileIndex rowIndex0,
        TileIndex rowIndex1, F24Dot8 dx, F24Dot8 dy,
        F24Dot8Point p0, F24Dot8Point p1);


    /**
     * ⬋
     */
    private partial void LineDownL(ThreadMemory memory, TileIndex rowIndex0,
        TileIndex rowIndex1, F24Dot8 dx, F24Dot8 dy,
        F24Dot8Point p0, F24Dot8Point p1);


    /**
     * ⬉
     */
    private partial void LineUpL(ThreadMemory memory, TileIndex rowIndex0,
        TileIndex rowIndex1, F24Dot8 dx, F24Dot8 dy,
        F24Dot8Point p0, F24Dot8Point p1);
    

    /**
     * Value indicating the maximum cover value for a single pixel. Since
     * rasterizer operates with 24.8 fixed point numbers, this means 256 × 256
     * subpixel grid.
     *
     * Positive value is for lines that go up.
     */
    private const int FullPixelCoverPositive = 256;


    /**
     * Minimum cover value.
     *
     * Negative value is for lines that go down.
     */
    private const int FullPixelCoverNegative = -256;


    // Initialized at the beginning, does not change later.
    private readonly TileBounds mBounds;

    // Keeps pointers to start cover arrays for each row of tiles. Allocated
    // in task memory and zero-filled when the first start cover array is
    // requested. Each entry is then allocated on demand in frame memory.
    private unsafe int** mStartCoverTable;

    private readonly Span<L> mLA;


    public static unsafe Linearizer<T, L> Create(
        ThreadMemory memory, in TileBounds bounds, bool contains, in Geometry geometry)
    {
        Span<L> lineArray = new(
            memory.TaskMallocArray<L>((int) bounds.RowCount),
            (int) bounds.RowCount);

        Linearizer<T, L> linearizer = new(bounds, lineArray);

        L.Construct(linearizer.mLA, bounds.ColumnCount, memory);

        if (contains)
        {
            linearizer.ProcessContained(geometry, memory);
        }
        else
        {
            int tx = T.TileColumnIndexToPoints(bounds.X);
            int ty = T.TileRowIndexToPoints(bounds.Y);
            int ch = T.TileColumnIndexToPoints(bounds.ColumnCount);
            int cv = T.TileRowIndexToPoints(bounds.RowCount);

            ClipBounds clip = new(ch, cv);

            Matrix matrix = geometry.TM;
            matrix *= Matrix.CreateTranslation(-tx, -ty);

            linearizer.ProcessUncontained(geometry, memory, clip, matrix);
        }

        return linearizer;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly partial TileBounds GetTileBounds()
    {
        return mBounds;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe readonly partial int** GetStartCoverTable()
    {
        return mStartCoverTable;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UnscopedRef]
    public partial ref L GetLineArrayAtIndex(TileIndex index)
    {
        return ref mLA[(int) index];
    }


    private unsafe partial void ProcessContained(in Geometry geometry, ThreadMemory memory)
    {
        // In this case path is known to be completely within destination image.
        // Some checks can be skipped.

        ReadOnlySpan<PathTag> tags = geometry.Tags.Span;

        F24Dot8Point* pp =
            memory.TaskMallocArray<F24Dot8Point>(geometry.Points.Length);

        F24Dot8Point origin;
        origin.X = T.TileColumnIndexToF24Dot8(mBounds.X);
        origin.Y = T.TileRowIndexToF24Dot8(mBounds.Y);

        F24Dot8Point size;
        size.X = T.TileColumnIndexToF24Dot8(mBounds.ColumnCount);
        size.Y = T.TileRowIndexToF24Dot8(mBounds.RowCount);

        SIMD.FloatPointsToF24Dot8Points(geometry.TM, new(pp, geometry.Points.Length), geometry.Points.Span, origin, size);

        F24Dot8Point moveTo = *pp++;

        for (int i = 1; i < tags.Length; i++)
        {
            switch (tags[i])
            {
                case PathTag.Move:
                {
                    // Complete previous path.
                    AddContainedLineF24Dot8(memory, pp[-1], moveTo);

                    moveTo = pp[0];

                    pp++;
                    break;
                }

                case PathTag.Line:
                {
                    AddContainedLineF24Dot8(memory, pp[-1], pp[0]);

                    pp++;
                    break;
                }

                case PathTag.Quadratic:
                {
                    AddContainedQuadraticF24Dot8(memory, *(F24Dot8PointX3*) (pp - 1));

                    pp += 2;
                    break;
                }

                case PathTag.Cubic:
                {
                    AddContainedCubicF24Dot8(memory, *(F24Dot8PointX4*) (pp - 1));

                    pp += 3;
                    break;
                }

                case PathTag.Close:
                {
                    break;
                }
            }
        }

        // Complete final path.
        AddContainedLineF24Dot8(memory, pp[-1], moveTo);
    }


    private partial void ProcessUncontained(in Geometry geometry, ThreadMemory memory,
        ClipBounds clip, in Matrix matrix)
    {
        ReadOnlySpan<PathTag> tags = geometry.Tags.Span;
        ReadOnlySpan<FloatPoint> points = geometry.Points.Span;

        Span<FloatPoint> segment = stackalloc FloatPoint[4];

        FloatPoint moveTo = matrix.Map(points[0]);
        points = points[1..];

        segment[0] = moveTo;

        for (int i = 1; i < tags.Length; i++)
        {
            switch (tags[i])
            {
                case PathTag.Move:
                {
                    // Complete previous path.
                    AddUncontainedLine(memory, clip, segment[0], moveTo);

                    moveTo = matrix.Map(points[0]);
                    points = points[1..];

                    segment[0] = moveTo;
                    break;
                }

                case PathTag.Line:
                {
                    FloatPoint p = matrix.Map(points[0]);
                    points = points[1..];

                    AddUncontainedLine(memory, clip, segment[0], p);

                    segment[0] = p;
                    break;
                }

                case PathTag.Quadratic:
                {
                    segment[1] = matrix.Map(points[0]);
                    segment[2] = matrix.Map(points[1]);
                    points = points[2..];

                    AddUncontainedQuadratic(memory, clip, segment.Slice(0,3));

                    segment[0] = segment[2];
                    break;
                }

                case PathTag.Cubic:
                {
                    segment[1] = matrix.Map(points[0]);
                    segment[2] = matrix.Map(points[1]);
                    segment[3] = matrix.Map(points[2]);
                    points = points[3..];

                    AddUncontainedCubic(memory, clip, segment);

                    segment[0] = segment[3];
                    break;
                }

                case PathTag.Close:
                {
                    break;
                }
            }
        }

        // Complete final path.
        AddUncontainedLine(memory, clip, segment[0], moveTo);
    }


    private partial void AddUncontainedLine(ThreadMemory memory,
        ClipBounds clip, FloatPoint p0, FloatPoint p1)
    {
        Debug.Assert(p0.IsFinite());
        Debug.Assert(p1.IsFinite());

        double y0 = p0.Y;
        double y1 = p1.Y;

        if (y0 == y1)
        {
            // Horizontal line, completely discarded.
            return;
        }

        if (y0 <= 0 && y1 <= 0)
        {
            // Line is on top, completely discarded.
            return;
        }

        if (y0 >= clip.Max.Y && y1 >= clip.Max.Y)
        {
            // Line is on bottom, completely discarded.
            return;
        }

        double x0 = p0.X;
        double x1 = p1.X;

        if (x0 >= clip.Max.X && x1 >= clip.Max.X)
        {
            // Line is on the right, completely discarded.
            return;
        }

        F24Dot8Point a, b;
        if (x0 == x1)
        {
            // Vertical line.
            a = p0.ToF24Dot8(default, clip.FMax);
            F24Dot8 p1y = Clamp(DoubleToF24Dot8(y1), 0, clip.FMax.Y);

            if (a.X == 0)
            {
                UpdateStartCovers(memory, a.Y, p1y);
                return;
            }
            
            b = new F24Dot8Point(a.X, p1y);
            goto Add;
        }

        // Vertical clipping.
        //
        // Use absolute delta-y, but not delta-x. Absolute delta-y is needed for
        // calculating vertical t value at min-y and max-y. Meanwhile delta-x
        // needs to be exact since it is multiplied by t and it can go left or
        // right.
        double deltay_v = Math.Abs(y1 - y0);
        double deltax_v = x1 - x0;

        // These will point to line start/end after vertical clipping.
        double rx0 = x0;
        double ry0 = y0;
        double rx1 = x1;
        double ry1 = y1;

        if (y1 > y0)
        {
            // Line is going ↓.
            if (y0 < 0)
            {
                // Cut at min-y.
                double t = -y0 / deltay_v;

                rx0 = x0 + (deltax_v * t);
                ry0 = 0;
            }

            if (y1 > clip.Max.Y)
            {
                // Cut again at max-y.
                double t = (clip.Max.Y - y0) / deltay_v;

                rx1 = x0 + (deltax_v * t);
                ry1 = clip.Max.Y;
            }
        }
        else
        {
            // Line is going ↑.
            if (y0 > clip.Max.Y)
            {
                // Cut at max-y.
                double t = (y0 - clip.Max.Y) / deltay_v;

                rx0 = x0 + (deltax_v * t);
                ry0 = clip.Max.Y;
            }

            if (y1 < 0)
            {
                // Cut again at min-y.
                double t = y0 / deltay_v;

                rx1 = x0 + (deltax_v * t);
                ry1 = 0;
            }
        }

        // Find out if remaining line is on the right.
        if (rx0 >= clip.Max.X && rx1 >= clip.Max.X)
        {
            // Line is on the right, completely discarded.
            return;
        }

        if (rx0 > 0 && rx1 > 0 && rx0 < clip.Max.X && rx1 < clip.Max.X)
        {
            // Inside.
            (a, b) = F24Dot8PointX2.ClampFromFloat(
                new FloatPoint(rx0, ry0),
                new FloatPoint(rx1, ry1),
                default, clip.FMax);

            goto Add;
        }

        if (rx0 <= 0 && rx1 <= 0)
        {
            // Left.
            F24Dot8 y0f = Clamp(DoubleToF24Dot8(ry0), 0, clip.FMax.Y);
            F24Dot8 y1f = Clamp(DoubleToF24Dot8(ry1), 0, clip.FMax.Y);

            UpdateStartCovers(memory, y0f, y1f);
            return;
        }

        // Horizontal clipping.
        double deltay_h = ry1 - ry0;
        double deltax_h = Math.Abs(rx1 - rx0);

        if (rx1 > rx0)
        {
            // Line is going →.
            double bx1 = rx1;
            double by1 = ry1;

            if (rx1 > clip.Max.X)
            {
                // Cut off at max-x.
                double t = (clip.Max.X - rx0) / deltax_h;

                by1 = ry0 + (deltay_h * t);
                bx1 = clip.Max.X;
            }

            if (rx0 < 0)
            {
                // Split at min-x.
                double t = -rx0 / deltax_h;

                F24Dot8 fa = Clamp(DoubleToF24Dot8(ry0), 0, clip.FMax.Y);

                a.X = 0;
                a.Y = Clamp(DoubleToF24Dot8(ry0 + (deltay_h * t)), 0, clip.FMax.Y);

                b = new FloatPoint(bx1, by1).ToF24Dot8(default, clip.FMax);

                UpdateStartCovers(memory, fa, a.Y);
            }
            else
            {
                (a, b) = F24Dot8PointX2.ClampFromFloat(
                    new FloatPoint(rx0, ry0),
                    new FloatPoint(bx1, by1),
                    default, clip.FMax);
            }
        }
        else
        {
            // Line is going ←.
            double bx0 = rx0;
            double by0 = ry0;

            if (rx0 > clip.Max.X)
            {
                // Cut off at max-x.
                double t = (rx0 - clip.Max.X) / deltax_h;

                by0 = ry0 + (deltay_h * t);
                bx0 = clip.Max.X;
            }

            if (rx1 < 0)
            {
                // Split at min-x.
                double t = rx0 / deltax_h;

                a = new FloatPoint(bx0, by0).ToF24Dot8(default, clip.FMax);

                b.X = 0;
                b.Y = Clamp(DoubleToF24Dot8(ry0 + (deltay_h * t)), 0, clip.FMax.Y);

                F24Dot8 c = Clamp(DoubleToF24Dot8(ry1), 0, clip.FMax.Y);

                UpdateStartCovers(memory, b.Y, c);
            }
            else
            {
                (a, b) = F24Dot8PointX2.ClampFromFloat(
                    new FloatPoint(bx0, by0),
                    new FloatPoint(rx1, ry1),
                    default, clip.FMax);
            }
        }
        
        Add:
        AddContainedLineF24Dot8(memory, a, b);
    }


    static F24Dot8 MaximumDelta => 2048 << 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial void AddContainedLineF24Dot8(ThreadMemory memory, F24Dot8Point p0, F24Dot8Point p1)
    {
        Debug.Assert(p0.X >= 0);
        Debug.Assert(p0.X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));

        Debug.Assert(p0.Y >= 0);
        Debug.Assert(p0.Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(p1.X >= 0);
        Debug.Assert(p1.X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));

        Debug.Assert(p1.Y >= 0);
        Debug.Assert(p1.Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        if (p0.Y == p1.Y)
        {
            // Ignore horizontal lines.
            return;
        }

        if (p0.X == p1.X)
        {
            // Special case, vertical line, simplifies this thing a lot.
            if (p0.Y < p1.Y)
            {
                // Line is going down ↓
                Vertical_Down(memory, p0.Y, p1.Y, p0.X);
            }
            else
            {
                // Line is going up ↑

                // Y values are not equal, as this case is checked already.

                Debug.Assert(p0.Y != p1.Y);

                Vertical_Up(memory, p0.Y, p1.Y, p0.X);
            }
            return;
        }
        
        LimitAddContainedLineF24Dot8(memory, p0, p1);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LimitAddContainedLineF24Dot8(ThreadMemory memory, F24Dot8Point p0, F24Dot8Point p1)
    {
        // First thing is to limit line size.
        F24Dot8 dx = Abs(p1.X - p0.X);
        F24Dot8 dy = Abs(p1.Y - p0.Y);

        if (dx > MaximumDelta || dy > MaximumDelta)
        {
            F24Dot8Point m = new(
                (p0.X + p1.X) >> 1,
                (p0.Y + p1.Y) >> 1
            );

            AddContainedLineF24Dot8(memory, p0, m);
            AddContainedLineF24Dot8(memory, m, p1);
            return;
        }

        // Line is short enough to be handled using 32 bit fixed point arithmetic.
        if (p0.Y < p1.Y)
        {
            // Line is going down ↓
            TileIndex rowIndex0 = T.F24Dot8ToTileRowIndex(p0.Y);
            TileIndex rowIndex1 = T.F24Dot8ToTileRowIndex(p1.Y - 1);

            Debug.Assert(rowIndex0 <= rowIndex1);

            if (rowIndex0 == rowIndex1)
            {
                // Entire line is completely within horizontal band. For curves
                // this is common case.
                F24Dot8 ty = T.TileRowIndexToF24Dot8(rowIndex0);
                F24Dot8 y0 = p0.Y - ty;
                F24Dot8 y1 = p1.Y - ty;

                mLA[(int) rowIndex0].AppendLineDownRL(memory, p0.X, y0, p1.X, y1);
            }
            else if (p0.X < p1.X)
            {
                // Line is going from left to right →
                LineDownR(memory, rowIndex0, rowIndex1, dx, dy, p0, p1);
            }
            else
            {
                // Line is going right to left ←
                LineDownL(memory, rowIndex0, rowIndex1, dx, dy, p0, p1);
            }
        }
        else
        {
            // Line is going up ↑

            // Y values are not equal, as this case is checked already.

            Debug.Assert(p0.Y > p1.Y);

            TileIndex rowIndex0 = T.F24Dot8ToTileRowIndex(p0.Y - 1);
            TileIndex rowIndex1 = T.F24Dot8ToTileRowIndex(p1.Y);

            Debug.Assert(rowIndex1 <= rowIndex0);

            if (rowIndex0 == rowIndex1)
            {
                // Entire line is completely within horizontal band. For curves
                // this is common case.
                F24Dot8 ty = T.TileRowIndexToF24Dot8(rowIndex0);
                F24Dot8 y0 = p0.Y - ty;
                F24Dot8 y1 = p1.Y - ty;

                mLA[(int) rowIndex0].AppendLineUpRL(memory, p0.X, y0, p1.X, y1);
            }
            else if (p0.X < p1.X)
            {
                // Line is going from left to right →
                LineUpR(memory, rowIndex0, rowIndex1, dx, dy, p0, p1);
            }
            else
            {
                // Line is going right to left ←
                LineUpL(memory, rowIndex0, rowIndex1, dx, dy, p0, p1);
            }
        }
    }


    private partial void AddUncontainedQuadratic(
        ThreadMemory memory,
        ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        Vector128<double> pmin = MinNative(
            MinNative(p[0].AsVector128(), p[1].AsVector128()),
            p[2].AsVector128());

        if (Vector128.GreaterThanOrEqualAny(pmin, clip.Max.AsVector128()))
        {
            // X: Curve is to the right of clipping bounds.
            // Y: Curve is below clipping bounds.
            return;
        }

        Vector128<double> pmax = MaxNative(
            MaxNative(p[0].AsVector128(), p[1].AsVector128()),
            p[2].AsVector128());

        double maxy = pmax.GetElement(1);
        if (maxy <= 0)
        {
            // Curve is above clipping bounds.
            return;
        }

        // First test if primitive intersects with any of horizontal axes of
        // clipping bounds.
        if (pmin.GetElement(1) >= 0 && maxy <= clip.Max.Y)
        {
            // Primitive is within clipping bounds vertically.
            double maxx = pmax.GetElement(0);

            if (maxx <= 0)
            {
                // And it is completely to the left of clipping bounds without
                // intersecting anything.
                F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
                F24Dot8 b = Clamp(DoubleToF24Dot8(p[2].Y), 0, clip.FMax.Y);

                UpdateStartCovers(memory, a, b);
                return;
            }

            if (maxx <= clip.Max.X && pmin.GetElement(0) >= 0)
            {
                // Curve is completely inside.
                F24Dot8PointX3.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX3 q);

                AddContainedQuadraticF24Dot8(memory, q);
                return;
            }
        }

        // Remaining option is that primitive potentially intersects clipping bounds.
        // First is to monotonize curve and attempt to clip it.

        bool monoInX = QuadraticControlPointBetweenEndPointsX(p);
        bool monoInY = QuadraticControlPointBetweenEndPointsY(p);

        if (monoInX && monoInY)
        {
            // Already monotonic in both directions. Quite common case, especially
            // with quadratics, return early.
            AddUncontainedMonotonicQuadratic(memory, clip, p);
            return;
        }

        if (monoInY)
        {
            // Here we know it has control points outside of end point range
            // in X direction.
            AddUncontainedQuadratic_YMono(memory, clip, p);
        }
        else
        {
            AddUncontainedQuadratic_YExtrema(memory, clip, p);
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddUncontainedQuadratic_YMono(ThreadMemory memory,
        ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        Span<FloatPoint> monoX = stackalloc FloatPoint[5];
        
        int nX = CutQuadraticAtXExtrema(p, monoX);

        for (int j = 0; j < nX; j++)
        {
            AddUncontainedMonotonicQuadratic(memory, clip, monoX.Slice(j * 2, 3));
        }
    }
    
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddUncontainedQuadratic_YExtrema(ThreadMemory memory,
        ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        Span<FloatPoint> monoX = stackalloc FloatPoint[5];
        
        Span<FloatPoint> monoY = stackalloc FloatPoint[5];
        int nY = CutQuadraticAtYExtrema(p, monoY);
            
        for (int i = 0; i < nY; i++)
        {
            ReadOnlySpan<FloatPoint> my = monoY.Slice(i * 2, 3);

            if (QuadraticControlPointBetweenEndPointsX(my))
            {
                AddUncontainedMonotonicQuadratic(memory, clip, my);
            }
            else
            {
                int nX = CutQuadraticAtXExtrema(my, monoX);

                for (int j = 0; j < nX; j++)
                {
                    AddUncontainedMonotonicQuadratic(memory, clip, monoX.Slice(j * 2, 3));
                }
            }
        }
    }


    [SkipLocalsInit]
    private partial void AddUncontainedMonotonicQuadratic(
        ThreadMemory memory, ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        Debug.Assert(p[0].IsFinite());
        Debug.Assert(p[1].IsFinite());
        Debug.Assert(p[2].IsFinite());
        
        // Assuming curve is monotonic.
        Debug.Assert(p[1].X <= Math.Max(p[0].X, p[2].X));
        Debug.Assert(p[1].X >= Math.Min(p[0].X, p[2].X));

        Debug.Assert(p[1].Y <= Math.Max(p[0].Y, p[2].Y));
        Debug.Assert(p[1].Y >= Math.Min(p[0].Y, p[2].Y));

        double sx = p[0].X;
        double px = p[2].X;

        if (sx >= clip.Max.X && px >= clip.Max.X)
        {
            // Completely on the right.
            return;
        }

        double sy = p[0].Y;
        double py = p[2].Y;

        if (sy <= 0 && py <= 0)
        {
            // Completely on top.
            return;
        }

        if (sy >= clip.Max.Y && py >= clip.Max.Y)
        {
            // Completely on bottom.
            return;
        }

        Span<FloatPoint> pts = stackalloc FloatPoint[3];
        p.CopyTo(pts);

        Span<FloatPoint> tmp = stackalloc FloatPoint[5];
        
        if (sy > py)
        {
            // Curve is going ↑.
            if (sy > clip.Max.Y)
            {
                // Cut-off at bottom.
                if (CutMonotonicQuadraticAtY(pts, clip.Max.Y, out double t))
                {
                    // Cut quadratic at t and keep upper part of curve (since we
                    // are handling ascending curve and cutting at off bottom).
                    CutQuadraticAt(pts, tmp, t);

                    pts[0] = tmp[2];
                    pts[1] = tmp[3];
                    // pts[2] already contains tmp[4].
                }
            }

            if (py < 0)
            {
                // Cut-off at top.
                if (CutMonotonicQuadraticAtY(pts, 0, out double t))
                {
                    // Cut quadratic at t and keep bottom part of curve (since we are
                    // handling ascending curve and cutting off at top).
                    CutQuadraticAt(pts, tmp, t);

                    // pts[0] already contains tmp[0].
                    pts[1] = tmp[1];
                    pts[2] = tmp[2];
                }
            }

            AddVerticallyContainedMonotonicQuadratic(memory, clip, pts);
        }
        else if (sy < py)
        {
            // Curve is going ↓.
            if (py > clip.Max.Y)
            {
                // Cut-off at bottom.
                if (CutMonotonicQuadraticAtY(pts, clip.Max.Y, out double t))
                {
                    // Cut quadratic at t and keep upper part of curve (since we are
                    // handling descending curve and cutting at off bottom).
                    CutQuadraticAt(pts, tmp, t);

                    // pts[0] already contains tmp[0].
                    pts[1] = tmp[1];
                    pts[2] = tmp[2];
                }
            }

            if (sy < 0)
            {
                // Cut-off at top.
                if (CutMonotonicQuadraticAtY(pts, 0, out double t))
                {
                    // Cut quadratic at t and keep bottom part of curve (since we are
                    // handling descending curve and cutting off at top).
                    CutQuadraticAt(pts, tmp, t);

                    pts[0] = tmp[2];
                    pts[1] = tmp[3];
                    // pts[2] already contains tmp[4].
                }
            }

            AddVerticallyContainedMonotonicQuadratic(memory, clip, pts);
        }
    }


    private partial void AddVerticallyContainedMonotonicQuadratic(
        ThreadMemory memory, ClipBounds clip, scoped Span<FloatPoint> p)
    {
        Debug.Assert(p[0].IsFinite());
        Debug.Assert(p[1].IsFinite());
        Debug.Assert(p[2].IsFinite());

        // Assuming curve is monotonic.
        Debug.Assert(p[1].X <= Math.Max(p[0].X, p[2].X));
        Debug.Assert(p[1].X >= Math.Min(p[0].X, p[2].X));

        Debug.Assert(p[1].Y <= Math.Max(p[0].Y, p[2].Y));
        Debug.Assert(p[1].Y >= Math.Min(p[0].Y, p[2].Y));

        Span<FloatPoint> tmp = stackalloc FloatPoint[5];
        
        double sx = p[0].X;
        double px = p[2].X;

        if (sx > px)
        {
            // Curve is going ←.
            if (px >= clip.Max.X)
            {
                // Completely on right.
                return;
            }

            if (sx <= 0)
            {
                // Completely on left.

                F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
                F24Dot8 b = Clamp(DoubleToF24Dot8(p[2].Y), 0, clip.FMax.Y);

                UpdateStartCovers(memory, a, b);
                return;
            }

            if (sx > clip.Max.X)
            {
                // Cut-off at right.
                if (CutMonotonicQuadraticAtX(p, clip.Max.X, out double t))
                {
                    // Cut quadratic at t and keep left part of curve (since we are
                    // handling right-to-left curve and cutting at off right part).
                    CutQuadraticAt(p, tmp, t);

                    p[0] = tmp[2];
                    p[1] = tmp[3];
                    // p[2] already contains tmp[4].
                }
            }

            if (px < 0)
            {
                // Split at min-x.
                if (CutMonotonicQuadraticAtX(p, 0, out double t))
                {
                    // Cut quadratic in two parts and keep both since we also need
                    // the part on the left side of bounding box.
                    CutQuadraticAt(p, tmp, t);

                    F24Dot8PointX3.ClampFromFloat(
                        tmp[..3], default, clip.FMax, out F24Dot8PointX3 q);

                    F24Dot8 c = Clamp(DoubleToF24Dot8(tmp[4].Y), 0, clip.FMax.Y);

                    AddContainedQuadraticF24Dot8(memory, q);

                    UpdateStartCovers(memory, q[2].Y, c);
                    return;
                }
            }

            // At this point we have entire curve inside bounding box.
            {
                F24Dot8PointX3.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX3 q);

                AddContainedQuadraticF24Dot8(memory, q);
            }
        }
        else if (sx < px)
        {
            // Curve is going →.
            if (sx >= clip.Max.X)
            {
                // Completely on right.
                return;
            }

            if (px <= 0)
            {
                // Completely on left.

                F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
                F24Dot8 b = Clamp(DoubleToF24Dot8(p[2].Y), 0, clip.FMax.Y);

                UpdateStartCovers(memory, a, b);
                return;
            }

            if (px > clip.Max.X)
            {
                // Cut-off at right.
                if (CutMonotonicQuadraticAtX(p, clip.Max.X, out double t))
                {
                    // Cut quadratic at t and keep left part of curve (since we are
                    // handling left-to-right curve and cutting at off right part).
                    CutQuadraticAt(p, tmp, t);

                    // p[0] already contains tmp[0].
                    p[1] = tmp[1];
                    p[2] = tmp[2];
                }
            }

            if (sx < 0)
            {
                // Split at min-x.
                if (CutMonotonicQuadraticAtX(p, 0, out double t))
                {
                    // Chop quadratic in two equal parts and keep both since we also
                    // need the part on the left side of bounding box.
                    CutQuadraticAt(p, tmp, t);

                    F24Dot8 a = Clamp(DoubleToF24Dot8(tmp[0].Y), 0, clip.FMax.Y);

                    F24Dot8PointX3.ClampFromFloat(
                        tmp.Slice(2, 3), default, clip.FMax, out F24Dot8PointX3 q);

                    UpdateStartCovers(memory, a, q[0].Y);

                    AddContainedQuadraticF24Dot8(memory, q);
                    return;
                }
            }

            // At this point we have entire curve inside bounding box.
            {
                F24Dot8PointX3.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX3 q);

                AddContainedQuadraticF24Dot8(memory, q);
            }
        }
        else
        {
            // Vertical line.
            if (px < clip.Max.X)
            {
                if (px <= 0)
                {
                    // Vertical line on the left.
                    F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
                    F24Dot8 b = Clamp(DoubleToF24Dot8(p[2].Y), 0, clip.FMax.Y);

                    UpdateStartCovers(memory, a, b);
                }
                else
                {
                    // Vertical line inside clip rect.
                    F24Dot8PointX3.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX3 q);

                    AddContainedQuadraticF24Dot8(memory, q);
                }
            }
        }
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private partial void AddContainedQuadraticF24Dot8(ThreadMemory memory, in F24Dot8PointX3 q)
    {
        Debug.Assert(q[0].X >= 0);
        Debug.Assert(q[0].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(q[0].Y >= 0);
        Debug.Assert(q[0].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(q[1].X >= 0);
        Debug.Assert(q[1].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(q[1].Y >= 0);
        Debug.Assert(q[1].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(q[2].X >= 0);
        Debug.Assert(q[2].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(q[2].Y >= 0);
        Debug.Assert(q[2].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        if (IsQuadraticFlatEnough(q))
        {
            AddContainedLineF24Dot8(memory, q[0], q[2]);
        }
        else
        {
            F24Dot8PointX5 split;
            SplitQuadratic(out split, q);

            AddContainedQuadraticF24Dot8(memory, split.Get3(0));
            AddContainedQuadraticF24Dot8(memory, split.Get3(2));
        }
    }


    private partial void AddUncontainedCubic(
        ThreadMemory memory,
        ClipBounds clip, 
        scoped ReadOnlySpan<FloatPoint> p)
    {
        Vector128<double> pmin = MinNative(
            MinNative(p[0].AsVector128(), p[1].AsVector128()),
            MinNative(p[2].AsVector128(), p[3].AsVector128()));

        if (Vector128.GreaterThanOrEqualAny(pmin, clip.Max.AsVector128()))
        {
            // X: Curve is to the right of clipping bounds.
            // Y: Curve is below clipping bounds.
            return;
        }

        Vector128<double> pmax = MaxNative(
            MaxNative(p[0].AsVector128(), p[1].AsVector128()),
            MaxNative(p[2].AsVector128(), p[3].AsVector128()));

        double maxy = pmax.GetElement(1);

        if (maxy <= 0)
        {
            // Curve is above clipping bounds.
            return;
        }

        // First test if primitive intersects with any of horizontal axes of
        // clipping bounds.
        if (pmin.GetElement(1) >= 0 && maxy <= clip.Max.Y)
        {
            // Primitive is within clipping bounds vertically.
            double maxx = pmax.GetElement(0);

            if (maxx <= 0)
            {
                // And it is completely to the left of clipping bounds without
                // intersecting anything.

                F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
                F24Dot8 b = Clamp(DoubleToF24Dot8(p[3].Y), 0, clip.FMax.Y);

                UpdateStartCovers(memory, a, b);
                return;
            }

            if (maxx <= clip.Max.X && pmin.GetElement(0) >= 0)
            {
                F24Dot8PointX4.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX4 c);

                AddContainedCubicF24Dot8(memory, c);
                return;
            }
        }

        // Remaining option is that primitive potentially intersects clipping bounds.
        //
        // Actual clipper expects monotonic cubics, so monotonize input.

        bool monoInX = CubicControlPointsBetweenEndPointsX(p);
        bool monoInY = CubicControlPointsBetweenEndPointsY(p);

        if (monoInX & monoInY)
        {
            // Already monotonic in both directions. Quite common case, return early.
            AddUncontainedMonotonicCubic(memory, clip, p);
            return;
        }

        if (monoInY)
        {
            // Here we know it has control points outside of end point range
            // in X direction.
            AddUncontainedCubic_YMono(memory, clip, p);
        }
        else
        {
            AddUncontainedCubic_YExtrema(memory, clip, p);
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddUncontainedCubic_YMono(ThreadMemory memory, ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        Span<FloatPoint> monoX = stackalloc FloatPoint[10];
        int nX = CutCubicAtXExtrema(p, monoX);

        for (int i = 0; i < nX; i++)
        {
            AddUncontainedMonotonicCubic(memory, clip, monoX.Slice(i * 3, 4));
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddUncontainedCubic_YExtrema(
        ThreadMemory memory, ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        Span<FloatPoint> monoX = stackalloc FloatPoint[10];
        
        Span<FloatPoint> monoY = stackalloc FloatPoint[10];
        int nY = CutCubicAtYExtrema(p, monoY);

        for (int i = 0; i < nY; i++)
        {
            ReadOnlySpan<FloatPoint> my = monoY.Slice(i * 3, 4);

            if (CubicControlPointsBetweenEndPointsX(my))
            {
                AddUncontainedMonotonicCubic(memory, clip, my);
            }
            else
            {
                int nX = CutCubicAtXExtrema(my, monoX);

                for (int j = 0; j < nX; j++)
                {
                    AddUncontainedMonotonicCubic(memory, clip, monoX.Slice(j * 3, 4));
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial void AddUncontainedMonotonicCubic(
        ThreadMemory memory,
        ClipBounds clip,
        scoped ReadOnlySpan<FloatPoint> p)
    {
        Debug.Assert(p[0].IsFinite());
        Debug.Assert(p[1].IsFinite());
        Debug.Assert(p[2].IsFinite());
        Debug.Assert(p[3].IsFinite());

        // Assuming curve is monotonic.

        double sx = p[0].X;
        double px = p[3].X;

        if (sx >= clip.Max.X &&
            px >= clip.Max.X)
        {
            // Completely on the right.
            return;
        }

        double sy = p[0].Y;
        double py = p[3].Y;

        if (sy <= 0 && py <= 0)
        {
            // Completely on top.
            return;
        }

        if (sy >= clip.Max.Y &&
            py >= clip.Max.Y)
        {
            // Completely on bottom.
            return;
        }

        AddUncontainedMonotonicCubic_Curve(memory, clip, p);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddUncontainedMonotonicCubic_Curve(ThreadMemory memory,
        ClipBounds clip, 
        scoped ReadOnlySpan<FloatPoint> p)
    {
        Span<FloatPoint> pts = stackalloc FloatPoint[4];
        p.CopyTo(pts);
        
        double sy = pts[0].Y;
        double py = pts[3].Y;

        Span<FloatPoint> tmp = stackalloc FloatPoint[7];
        
        if (sy > py)
        {
            // Curve is ascending.
            if (sy > clip.Max.Y)
            {
                // Cut-off at bottom.
                if (CutMonotonicCubicAtY(p, clip.Max.Y, out double t))
                {
                    // Cut cubic at t and keep upper part of curve (since we are
                    // handling ascending curve and cutting at off bottom).
                    CutCubicAt(p, tmp, t);

                    pts[0] = tmp[3];
                    pts[1] = tmp[4];
                    pts[2] = tmp[5];
                    // pts[3] already contains tmp[6].
                }
            }

            if (py < 0)
            {
                // Cut-off at top.
                if (CutMonotonicCubicAtY(p, 0, out double t))
                {
                    // Cut cubic at t and keep bottom part of curve (since we are
                    // handling ascending curve and cutting off at top).
                    CutCubicAt(p, tmp, t);

                    // pts[0] already contains tmp[0].
                    pts[1] = tmp[1];
                    pts[2] = tmp[2];
                    pts[3] = tmp[3];
                }
            }
        }
        else if (sy < py)
        {
            // Curve is descending.
            if (py > clip.Max.Y)
            {
                // Cut-off at bottom.
                if (CutMonotonicCubicAtY(p, clip.Max.Y, out double t))
                {
                    // Cut cubic at t and keep upper part of curve (since we are
                    // handling descending curve and cutting at off bottom).
                    CutCubicAt(p, tmp, t);

                    // pts[0] already contains tmp[0].
                    pts[1] = tmp[1];
                    pts[2] = tmp[2];
                    pts[3] = tmp[3];
                }
            }

            if (sy < 0)
            {
                // Cut-off at top.
                if (CutMonotonicCubicAtY(p, 0, out double t))
                {
                    // Cut cubic at t and keep bottom part of curve (since we are
                    // handling descending curve and cutting off at top).
                    CutCubicAt(p, tmp, t);

                    pts[0] = tmp[3];
                    pts[1] = tmp[4];
                    pts[2] = tmp[5];
                    // pts[3] already contains tmp[6].
                }
            }
        }
        else
        {
            return;
        }
        AddVerticallyContainedMonotonicCubic(memory, clip, pts);
    }


    private partial void AddVerticallyContainedMonotonicCubic(
        ThreadMemory memory, ClipBounds clip, scoped Span<FloatPoint> p)
    {
        Debug.Assert(p[0].IsFinite());
        Debug.Assert(p[1].IsFinite());
        Debug.Assert(p[2].IsFinite());
        Debug.Assert(p[3].IsFinite());
        
        double sx = p[0].X;
        double px = p[3].X;

        if (sx > px)
        {
            // Curve is going from right to left.
            if (px >= clip.Max.X)
            {
                // Completely on right.
                return;
            }

            AddVerticallyContainedMonotonicCubic_RightToLeft(memory, clip, p);
        }
        else if (sx < px)
        {
            // Curve is going from left to right.
            if (sx >= clip.Max.X)
            {
                // Completely on right.
                return;
            }

            AddVerticallyContainedMonotonicCubic_LeftToRight(memory, clip, p);
        }
        else if (px < clip.Max.X)
        {
            AddVerticallyContainedMonotonicCubic_VerticalLine(memory, clip, p);
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddVerticallyContainedMonotonicCubic_RightToLeft(
        ThreadMemory memory, ClipBounds clip, scoped Span<FloatPoint> p)
    {
        double sx = p[0].X;
        double px = p[3].X;

        if (sx <= 0)
        {
            // Completely on left.

            F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
            F24Dot8 b = Clamp(DoubleToF24Dot8(p[3].Y), 0, clip.FMax.Y);

            UpdateStartCovers(memory, a, b);
            return;
        }
        
        Span<FloatPoint> tmp = stackalloc FloatPoint[7];

        if (sx > clip.Max.X)
        {
            // Cut-off at right.
            if (CutMonotonicCubicAtX(p, clip.Max.X, out double t))
            {
                // Cut cubic at t and keep left part of curve (since we are
                // handling right-to-left curve and cutting at off right part).
                CutCubicAt(p, tmp, t);

                p[0] = tmp[3];
                p[1] = tmp[4];
                p[2] = tmp[5];
                // p[3] already contains tmp[6].
            }
        }

        if (px < 0)
        {
            // Cut-off at left.
            if (CutMonotonicCubicAtX(p, 0, out double t))
            {
                // Cut cubic in two equal parts and keep both since we also
                // need the part on the left side of bounding box.
                CutCubicAt(p, tmp, t);

                // Since curve is going from right to left, the first one will
                // be inside and the second one will be on the left.
                F24Dot8PointX4.FromFloat(
                    tmp[0], tmp[1], tmp[2], tmp[3],
                    out F24Dot8PointX4 c);

                AddPotentiallyUncontainedCubicF24Dot8(memory, clip.FMax, c);

                F24Dot8 y0 = Clamp(c[3].Y, 0, clip.FMax.Y);
                F24Dot8 y1 = Clamp(DoubleToF24Dot8(tmp[6].Y), 0, clip.FMax.Y);

                UpdateStartCovers(memory, y0, y1);
                return;
            }
        }

        // At this point we have entire curve inside bounding box.
        {
            F24Dot8PointX4.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX4 c);

            AddContainedCubicF24Dot8(memory, c);
        }
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddVerticallyContainedMonotonicCubic_LeftToRight(
        ThreadMemory memory, ClipBounds clip, scoped Span<FloatPoint> p)
    {
        double sx = p[0].X;
        double px = p[3].X;

        if (px <= 0)
        {
            // Completely on left.

            F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
            F24Dot8 b = Clamp(DoubleToF24Dot8(p[3].Y), 0, clip.FMax.Y);

            UpdateStartCovers(memory, a, b);
            return;
        }

        Span<FloatPoint> tmp = stackalloc FloatPoint[7];
        
        if (px > clip.Max.X)
        {
            // Cut-off at right.
            if (CutMonotonicCubicAtX(p, clip.Max.X, out double t))
            {
                // Cut cubic at t and keep left part of curve (since we are
                // handling left-to-right curve and cutting at off right part).
                CutCubicAt(p, tmp, t);

                // p[0] already contains tmp[0].
                p[1] = tmp[1];
                p[2] = tmp[2];
                p[3] = tmp[3];
            }
        }

        if (sx < 0)
        {
            // Cut-off at left.
            if (CutMonotonicCubicAtX(p, 0, out double t))
            {
                // Cut cubic in two equal parts and keep both since we also
                // need the part on the left side of bounding box.
                CutCubicAt(p, tmp, t);

                // Since curve is going from left to right, the first one will
                // be on the left and the second one will be inside.
                F24Dot8PointX4.FromFloat(
                    tmp[3], tmp[4], tmp[5], tmp[6],
                    out F24Dot8PointX4 c);

                F24Dot8 y0 = Clamp(DoubleToF24Dot8(tmp[0].Y), 0, clip.FMax.Y);
                F24Dot8 y1 = Clamp(c[0].Y, 0, clip.FMax.Y);

                UpdateStartCovers(memory, y0, y1);

                AddPotentiallyUncontainedCubicF24Dot8(memory, clip.FMax, c);
                return;
            }
        }

        // At this point we have entire curve inside bounding box.
        {
            F24Dot8PointX4.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX4 c);

            AddContainedCubicF24Dot8(memory, c);
        }
    }


    private void AddVerticallyContainedMonotonicCubic_VerticalLine(
        ThreadMemory memory, ClipBounds clip, scoped ReadOnlySpan<FloatPoint> p)
    {
        double px = p[3].X;

        // Vertical line.
        if (px <= 0)
        {
            // Vertical line on the left.
            F24Dot8 a = Clamp(DoubleToF24Dot8(p[0].Y), 0, clip.FMax.Y);
            F24Dot8 b = Clamp(DoubleToF24Dot8(p[3].Y), 0, clip.FMax.Y);

            UpdateStartCovers(memory, a, b);
        }
        else
        {
            // Vertical line inside clip rect.
            F24Dot8PointX4.ClampFromFloat(p, default, clip.FMax, out F24Dot8PointX4 c);

            AddContainedCubicF24Dot8(memory, c);
        }
    }


    private partial void AddPotentiallyUncontainedCubicF24Dot8(
        ThreadMemory memory, F24Dot8Point max, in F24Dot8PointX4 c)
    {
        // When cubic curve is not completely within destination image (determined
        // by testing if control points are inside of it), curve is clipped and
        // only parts within destination image need to be added to the output. To
        // make curve cutting simpler, before cutting, cubic curve is monotonized.
        // This allows to only look for one root when intersecting it with
        // vertical or horizontal lines (bounding box edges). However, there is
        // one small issue with cubic curves - there are situations where cubic
        // curve is monotonic despite the fact that it has control points outside
        // of bounding box defined by the first and the last points of that curve.
        // This leads to a problem when converting curve to 24.8 format. When it
        // is done points are clamped to destination image bounds so that when
        // later inserting generated segments to segment lists, bounds check can
        // be skipped. It is quicker to make sure curve fits into destination
        // image and then just assume that any segments that come out of it will
        // automatically fit into destination image. But if curve has control
        // points out of bounds even after monotonization, clamping control points
        // to image bounds will simply result in wrong output.
        //
        // This method tries to implement the solution. It checks if there are
        // points of a given curve outside of image bounds and if there are, it
        // splits curve in half and repeats recursively until curve gets too small
        // to subdivide. This solves the problem because after each subdivision,
        // curve control point bounding box gets tighter.

        Vector256<int> vC = Unsafe.BitCast<F24Dot8PointX4, Vector256<int>>(c);

        if (Vector256.LessThanAny(vC, Vector256<int>.Zero) ||
            Vector256.GreaterThanAny(vC, max.ToVector256()))
        {
            AddUncontainedCubicF24Dot8(memory, max, c);
        }
        else
        {
            AddContainedCubicF24Dot8(memory, c);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddUncontainedCubicF24Dot8(
        ThreadMemory memory, F24Dot8Point max, in F24Dot8PointX4 c)
    {
        // Potentially needs splitting unless it is already too small.
        F24Dot8 dx =
            Abs(c[0].X - c[1].X) +
            Abs(c[1].X - c[2].X) +
            Abs(c[2].X - c[3].X);

        F24Dot8 dy =
            Abs(c[0].Y - c[1].Y) +
            Abs(c[1].Y - c[2].Y) +
            Abs(c[2].Y - c[3].Y);

        if (dx + dy < F24Dot8_1)
        {
            F24Dot8PointX4 pc = c.Clamp(default, max);

            AddContainedCubicF24Dot8(memory, pc);
        }
        else
        {
            SplitCubic(out F24Dot8PointX7 pc, c);

            AddPotentiallyUncontainedCubicF24Dot8(memory, max, pc.Get4(0));

            AddPotentiallyUncontainedCubicF24Dot8(memory, max, pc.Get4(3));
        }
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private partial void AddContainedCubicF24Dot8(ThreadMemory memory, in F24Dot8PointX4 c)
    {
        Debug.Assert(c[0].X >= 0);
        Debug.Assert(c[0].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(c[0].Y >= 0);
        Debug.Assert(c[0].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(c[1].X >= 0);
        Debug.Assert(c[1].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(c[1].Y >= 0);
        Debug.Assert(c[1].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(c[2].X >= 0);
        Debug.Assert(c[2].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(c[2].Y >= 0);
        Debug.Assert(c[2].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(c[3].X >= 0);
        Debug.Assert(c[3].X <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));
        Debug.Assert(c[3].Y >= 0);
        Debug.Assert(c[3].Y <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        if (IsCubicFlatEnough(c))
        {
            AddContainedLineF24Dot8(memory, c[0], c[3]);
        }
        else
        {
            SplitCubic(out F24Dot8PointX7 split, c);

            AddContainedCubicF24Dot8(memory, split.Get4(0));
            AddContainedCubicF24Dot8(memory, split.Get4(3));
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private partial void AppendVerticalLine(ThreadMemory memory,
        TileIndex rowIndex, F24Dot8 x, F24Dot8 y0, F24Dot8 y1)
    {
        Debug.Assert(rowIndex < mBounds.RowCount);

        Debug.Assert(x >= 0);
        Debug.Assert(x <= T.TileColumnIndexToF24Dot8(mBounds.ColumnCount));

        Debug.Assert(y0 >= 0);
        Debug.Assert(y0 <= T.TileHF24Dot8);

        Debug.Assert(y1 >= 0);
        Debug.Assert(y1 <= T.TileHF24Dot8);

        mLA[(int) rowIndex].AppendVerticalLine(memory, x, y0, y1);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private partial void LineDownR(ThreadMemory memory,
        TileIndex rowIndex0, TileIndex rowIndex1, F24Dot8 dx,
        F24Dot8 dy, F24Dot8Point p0, F24Dot8Point p1)
    {
        Debug.Assert(rowIndex0 < rowIndex1);
        Debug.Assert((p1.Y - p0.Y) == dy);
        Debug.Assert((p1.X - p0.X) == dx);

        F24Dot8 fy0 = p0.Y - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = p1.Y - T.TileRowIndexToF24Dot8(rowIndex1);

        F24Dot8 p = (T.TileHF24Dot8 - fy0) * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = p0.X + delta;

        mLA[(int) rowIndex0].AppendLineDownR_V(memory, p0.X, fy0, cx, T.TileHF24Dot8);

        TileIndex idy = rowIndex0 + 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = T.TileHF24Dot8 * dx;

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

            for (; idy != rowIndex1; idy++)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx + delta;

                mLA[(int) idy].AppendLineDownR_V(memory, cx, 0, nx, T.TileHF24Dot8);

                cx = nx;
            }
        }

        mLA[(int) rowIndex1].AppendLineDownR_V(memory, cx, 0, p1.X, fy1);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private partial void LineUpR(ThreadMemory memory,
        TileIndex rowIndex0, TileIndex rowIndex1, F24Dot8 dx,
        F24Dot8 dy, F24Dot8Point p0, F24Dot8Point p1)
    {
        Debug.Assert(rowIndex0 > rowIndex1);
        Debug.Assert((p0.Y - p1.Y) == dy);
        Debug.Assert((p1.X - p0.X) == dx);

        F24Dot8 fy0 = p0.Y - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = p1.Y - T.TileRowIndexToF24Dot8(rowIndex1);

        F24Dot8 p = fy0 * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = p0.X + delta;

        mLA[(int) rowIndex0].AppendLineUpR_V(memory, p0.X, fy0, cx, 0);

        TileIndex idy = rowIndex0 - 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = T.TileHF24Dot8 * dx;

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

            for (; idy != rowIndex1; idy--)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx + delta;

                mLA[(int) idy].AppendLineUpR_V(memory, cx, T.TileHF24Dot8, nx, 0);

                cx = nx;
            }
        }

        mLA[(int) rowIndex1].AppendLineUpR_V(memory, cx, T.TileHF24Dot8, p1.X, fy1);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private partial void LineDownL(ThreadMemory memory,
        TileIndex rowIndex0, TileIndex rowIndex1, F24Dot8 dx,
        F24Dot8 dy, F24Dot8Point p0, F24Dot8Point p1)
    {
        Debug.Assert(rowIndex0 < rowIndex1);
        Debug.Assert((p1.Y - p0.Y) == dy);
        Debug.Assert((p0.X - p1.X) == dx);

        F24Dot8 fy0 = p0.Y - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = p1.Y - T.TileRowIndexToF24Dot8(rowIndex1);

        F24Dot8 p = (T.TileHF24Dot8 - fy0) * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = p0.X - delta;

        mLA[(int) rowIndex0].AppendLineDownL_V(memory, p0.X, fy0, cx, T.TileHF24Dot8);

        TileIndex idy = rowIndex0 + 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = T.TileHF24Dot8 * dx;

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

            for (; idy != rowIndex1; idy++)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx - delta;

                mLA[(int) idy].AppendLineDownL_V(memory, cx, 0, nx, T.TileHF24Dot8);

                cx = nx;
            }
        }

        mLA[(int) rowIndex1].AppendLineDownL_V(memory, cx, 0, p1.X, fy1);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private partial void LineUpL(ThreadMemory memory,
        TileIndex rowIndex0, TileIndex rowIndex1, F24Dot8 dx,
        F24Dot8 dy, F24Dot8Point p0, F24Dot8Point p1)
    {
        Debug.Assert(rowIndex0 > rowIndex1);
        Debug.Assert((p0.Y - p1.Y) == dy);
        Debug.Assert((p0.X - p1.X) == dx);

        F24Dot8 fy0 = p0.Y - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = p1.Y - T.TileRowIndexToF24Dot8(rowIndex1);

        F24Dot8 p = fy0 * dx;
        F24Dot8 delta = p / dy;

        F24Dot8 cx = p0.X - delta;

        mLA[(int) rowIndex0].AppendLineUpL_V(memory, p0.X, fy0, cx, 0);

        TileIndex idy = rowIndex0 - 1;

        if (idy != rowIndex1)
        {
            F24Dot8 mod = (p % dy) - dy;

            p = T.TileHF24Dot8 * dx;

            F24Dot8 lift = p / dy;
            F24Dot8 rem = p % dy;

            for (; idy != rowIndex1; idy--)
            {
                delta = lift;
                mod += rem;

                if (mod >= 0)
                {
                    mod -= dy;
                    delta++;
                }

                F24Dot8 nx = cx - delta;

                mLA[(int) idy].AppendLineUpL_V(memory, cx, T.TileHF24Dot8, nx, 0);

                cx = nx;
            }
        }

        mLA[(int) rowIndex1].AppendLineUpL_V(memory, cx, T.TileHF24Dot8, p1.X, fy1);
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Vertical_Down(ThreadMemory memory, F24Dot8 y0, F24Dot8 y1, F24Dot8 x)
    {
        Debug.Assert(y0 < y1);

        TileIndex rowIndex0 = T.F24Dot8ToTileRowIndex(y0);
        TileIndex rowIndex1 = T.F24Dot8ToTileRowIndex(y1 - 1);
        F24Dot8 fy0 = y0 - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - T.TileRowIndexToF24Dot8(rowIndex1);

        if (rowIndex0 == rowIndex1)
        {
            AppendVerticalLine(memory, rowIndex0, x, fy0, fy1);
        }
        else
        {
            AppendVerticalLine(memory, rowIndex0, x, fy0, T.TileHF24Dot8);

            for (TileIndex i = rowIndex0 + 1; i < rowIndex1; i++)
            {
                AppendVerticalLine(memory, i, x, 0, T.TileHF24Dot8);
            }

            AppendVerticalLine(memory, rowIndex1, x, 0, fy1);
        }
    }


    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Vertical_Up(ThreadMemory memory, F24Dot8 y0, F24Dot8 y1, F24Dot8 x)
    {
        Debug.Assert(y0 > y1);

        TileIndex rowIndex0 = T.F24Dot8ToTileRowIndex(y0 - 1);
        TileIndex rowIndex1 = T.F24Dot8ToTileRowIndex(y1);
        F24Dot8 fy0 = y0 - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - T.TileRowIndexToF24Dot8(rowIndex1);

        if (rowIndex0 == rowIndex1)
        {
            AppendVerticalLine(memory, rowIndex0, x, fy0, fy1);
        }
        else
        {
            AppendVerticalLine(memory, rowIndex0, x, fy0, 0);

            for (TileIndex i = rowIndex0 - 1; i > rowIndex1; i--)
            {
                AppendVerticalLine(memory, i, x, T.TileHF24Dot8, 0);
            }

            AppendVerticalLine(memory, rowIndex1, x, T.TileHF24Dot8, fy1);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe Span<int> GetStartCoversForRowAtIndex(ThreadMemory memory, int index)
    {
        Debug.Assert(mStartCoverTable != null);
        Debug.Assert((uint) index < mBounds.RowCount);

        int* p = mStartCoverTable[index];

        if (p == null)
        {
            p = memory.FrameMallocArrayZeroFill<int>(T.TileH);
            mStartCoverTable[index] = p;
        }

        return new Span<int>(p, T.TileH);
    }


    private unsafe void UpdateStartCovers(ThreadMemory memory, F24Dot8 y0, F24Dot8 y1)
    {
        Debug.Assert(y0 >= 0);
        Debug.Assert(y0 <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        Debug.Assert(y1 >= 0);
        Debug.Assert(y1 <= T.TileRowIndexToF24Dot8(mBounds.RowCount));

        if (y0 == y1)
        {
            // Not contributing to mask.
            return;
        }

        if (mStartCoverTable == null)
        {
            // Allocate pointers to row masks.
            mStartCoverTable = memory.FrameMallocPointersZeroFill<int>((int) mBounds.RowCount);
        }

        if (y0 < y1)
        {
            UpdateStartCovers_Down(memory, y0, y1);
        }
        else
        {
            UpdateStartCovers_Up(memory, y0, y1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateStartCovers_Down(ThreadMemory memory, F24Dot8 y0, F24Dot8 y1)
    {
        // Line is going down.
        TileIndex rowIndex0 = T.F24Dot8ToTileRowIndex(y0);
        TileIndex rowIndex1 = T.F24Dot8ToTileRowIndex(y1 - 1);
        F24Dot8 fy0 = y0 - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - T.TileRowIndexToF24Dot8(rowIndex1);

        Span<int> cmFirst = GetStartCoversForRowAtIndex(memory, (int) rowIndex0);

        if (rowIndex0 == rowIndex1)
        {
            UpdateCoverTable_Down(cmFirst, fy0, fy1);
        }
        else
        {
            UpdateCoverTable_Down(cmFirst, fy0, T.TileHF24Dot8);

            for (TileIndex i = rowIndex0 + 1; i < rowIndex1; i++)
            {
                UpdateStartCoversFull(memory, (int) i, FullPixelCoverNegative);
            }

            Span<int> cmLast = GetStartCoversForRowAtIndex(memory, (int) rowIndex1);

            UpdateCoverTable_Down(cmLast, 0, fy1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void UpdateStartCovers_Up(ThreadMemory memory, F24Dot8 y0, F24Dot8 y1)
    {
        // Line is going up.
        TileIndex rowIndex0 = T.F24Dot8ToTileRowIndex(y0 - 1);
        TileIndex rowIndex1 = T.F24Dot8ToTileRowIndex(y1);
        F24Dot8 fy0 = y0 - T.TileRowIndexToF24Dot8(rowIndex0);
        F24Dot8 fy1 = y1 - T.TileRowIndexToF24Dot8(rowIndex1);

        Span<int> cmFirst = GetStartCoversForRowAtIndex(memory, (int) rowIndex0);

        if (rowIndex0 == rowIndex1)
        {
            UpdateCoverTable_Up(cmFirst, fy0, fy1);
        }
        else
        {
            UpdateCoverTable_Up(cmFirst, fy0, 0);

            for (TileIndex i = rowIndex0 - 1; i > rowIndex1; i--)
            {
                UpdateStartCoversFull(memory, (int) i, FullPixelCoverPositive);
            }

            Span<int> cmLast = GetStartCoversForRowAtIndex(memory, (int) rowIndex1);

            UpdateCoverTable_Up(cmLast, T.TileHF24Dot8, fy1);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void UpdateStartCoversFull(ThreadMemory memory, int index, int value)
    {
        Debug.Assert(mStartCoverTable != null);
        Debug.Assert((uint) index < mBounds.RowCount);

        int* p = mStartCoverTable[index];

        if (p != null)
        {
            // Accumulate.
            T.AccumulateStartCovers(new Span<int>(p, T.TileH), value);
        }
        else
        {
            // Store first.
            p = memory.FrameMallocArray<int>(T.TileH);

            T.FillStartCovers(new Span<int>(p, T.TileH), value);

            mStartCoverTable[index] = p;
        }
    }
}