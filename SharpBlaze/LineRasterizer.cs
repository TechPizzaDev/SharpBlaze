using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpBlaze;

public abstract class LineRasterizer
{
    internal abstract void Rasterize(
        int localRowIndex,
        in RasterizableGeometry raster,
        in Geometry geometry,
        Span2D<byte> rowView,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable);
}

public abstract class LineRasterizer<TColor, TAlpha, TBlender> : LineRasterizer
    where TColor : unmanaged
    where TAlpha : unmanaged, IEqualityOperators<TAlpha, TAlpha, bool>
    where TBlender : ISpanBlender<TColor, TAlpha>
{
    protected abstract TBlender CreateBlender(in Geometry geometry);
    
    internal override sealed void Rasterize(
        int localRowIndex,
        in RasterizableGeometry raster,
        in Geometry geometry,
        Span2D<byte> rowView,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        // Pointer to backdrop.
        ReadOnlySpan<F24Dot8> coversStart = raster.GetCoversForRow(localRowIndex);

        int height = rowView.Height;
        Span2D<BitVector> bitVectorView = bitVectorTable.Cut(bitVectorTable.Width, height);
        Span2D<CoverArea> coverAreaView = coverAreaTable.Cut(coverAreaTable.Width, height);

        TBlender blender = CreateBlender(geometry);

        for (int y = 0; y < height; y++)
        {
            Span<byte> row = rowView[y];
            F24Dot8 startCover = y < coversStart.Length ? coversStart[y] : F24Dot8.Zero;

            RenderOneLine(
                row,
                bitVectorView[y],
                coverAreaView[y],
                startCover,
                blender);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderOneLine(
        Span<byte> row,
        ReadOnlySpan<BitVector> bitVectors,
        ReadOnlySpan<CoverArea> coverAreas,
        F24Dot8 startCover,
        TBlender blender)
    {
        Span<TColor> d = MemoryMarshal.Cast<byte, TColor>(row);

        // Cover accumulation.
        F24Dot8 cover = startCover;

        // Span state.
        int spanX = 0;
        int spanEnd = 0;
        TAlpha spanAlpha = default;

        for (int i = 0; i < bitVectors.Length; i++)
        {
            nuint bitset = bitVectors[i]._value;

            while (bitset != 0)
            {
                nuint t = bitset & (nuint) (-(nint) bitset);
                int r = BitOperations.TrailingZeroCount(bitset);
                int index = (i * Unsafe.SizeOf<BitVector>() * 8) + r;

                bitset ^= t;

                // Note that index is in local geometry coordinates.
                int edgeX = index;
                int nextEdgeX = edgeX + 1;

                // Signed area for pixel at bit index.
                F24Dot8 area = coverAreas[index].Area + (cover << 9);

                // Area converted to alpha according to fill rule.
                TAlpha alpha = blender.ApplyFillRule(area);

                if (spanEnd == edgeX)
                {
                    // No gap between previous span and current pixel.
                    if (alpha == default)
                    {
                        if (spanAlpha != default)
                        {
                            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                        }

                        spanX = nextEdgeX;
                        spanEnd = spanX;
                        spanAlpha = default;
                    }
                    else if (spanAlpha == alpha)
                    {
                        spanEnd = nextEdgeX;
                    }
                    else
                    {
                        // Alpha is not zero, but not equal to previous span alpha.
                        if (spanAlpha != default)
                        {
                            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                        }

                        spanX = edgeX;
                        spanEnd = nextEdgeX;
                        spanAlpha = alpha;
                    }
                }
                else
                {
                    Debug.Assert(spanEnd < edgeX);

                    // There is a gap between last filled pixel and the new one.
                    if (cover == F24Dot8.Zero)
                    {
                        // Empty gap.
                        // Fill span if there is one and reset current span.
                        if (spanAlpha != default)
                        {
                            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                        }

                        spanX = edgeX;
                        spanEnd = nextEdgeX;
                        spanAlpha = alpha;
                    }
                    else
                    {
                        // Non empty gap.
                        // Attempt to merge gap with current span.
                        TAlpha gapAlpha = blender.ApplyFillRule(cover << 9);

                        // If alpha matches, extend current span.
                        if (spanAlpha == gapAlpha)
                        {
                            if (alpha == gapAlpha)
                            {
                                // Current pixel alpha matches as well.
                                spanEnd = nextEdgeX;
                            }
                            else
                            {
                                // Only gap alpha matches current span.
                                blender.CompositeSpan(d[spanX..edgeX], spanAlpha);

                                spanX = edgeX;
                                spanEnd = nextEdgeX;
                                spanAlpha = alpha;
                            }
                        }
                        else
                        {
                            if (spanAlpha != default)
                            {
                                blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
                            }

                            // Compose gap.
                            blender.CompositeSpan(d[spanEnd..edgeX], gapAlpha);

                            spanX = edgeX;
                            spanEnd = nextEdgeX;
                            spanAlpha = alpha;
                        }
                    }
                }

                cover += coverAreas[index].Delta;
            }
        }

        if (spanAlpha != default)
        {
            // Composite current span.
            blender.CompositeSpan(d[spanX..spanEnd], spanAlpha);
        }

        if (cover != F24Dot8.Zero && spanEnd < d.Length)
        {
            // Composite anything that goes to the edge of destination image.
            TAlpha alpha = blender.ApplyFillRule(cover << 9);

            blender.CompositeSpan(d[spanEnd..], alpha);
        }
    }
}