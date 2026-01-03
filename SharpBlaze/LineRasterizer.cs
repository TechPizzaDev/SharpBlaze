using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public abstract class LineRasterizer
{
    public abstract IntRect Bounds { get; }

    public abstract void Rasterize(
        int geometryIndex,
        IntRect targetRect,
        ReadOnlySpan<F24Dot8> coversStart,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable);
}

public abstract class LineRasterizer<TAlpha, TBlender> : LineRasterizer
    where TAlpha : unmanaged, IEqualityOperators<TAlpha, TAlpha, bool>
    where TBlender : ISpanBlender<TAlpha>
{
    protected abstract TBlender CreateBlender(int geometryIndex, IntRect targetRect);

    public override sealed void Rasterize(
        int geometryIndex,
        IntRect targetRect,
        ReadOnlySpan<F24Dot8> coversStart,
        Span2D<BitVector> bitVectorTable,
        Span2D<CoverArea> coverAreaTable)
    {
        TBlender blender = CreateBlender(geometryIndex, targetRect);

        int left = targetRect.X;
        int right = targetRect.Right;
        int top = targetRect.Y;
        int height = targetRect.Height;

        for (int y = 0; y < height; y++)
        {
            F24Dot8 startCover = y < coversStart.Length ? coversStart[y] : F24Dot8.Zero;

            RenderOneLine(
                left,
                right,
                top + y,
                bitVectorTable[y],
                coverAreaTable[y],
                startCover,
                ref blender);
        }
    }

    public static void RenderOneLine(
        int left,
        int right,
        int y,
        ReadOnlySpan<BitVector> bitVectors,
        ReadOnlySpan<CoverArea> coverAreas,
        F24Dot8 startCover,
        ref TBlender blender)
    {
        // Cover accumulation.
        F24Dot8 cover = startCover;

        // Span state.
        int spanX = left;
        int spanEnd = left;
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
                int edgeX = left + index;
                int nextX = edgeX + 1;

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
                            blender.CompositeSpan(spanX, spanEnd, y, spanAlpha);
                        }

                        spanX = nextX;
                        spanEnd = spanX;
                        spanAlpha = default;
                    }
                    else if (spanAlpha == alpha)
                    {
                        spanEnd = nextX;
                    }
                    else
                    {
                        // Alpha is not zero, but not equal to previous span alpha.
                        if (spanAlpha != default)
                        {
                            blender.CompositeSpan(spanX, spanEnd, y, spanAlpha);
                        }

                        spanX = edgeX;
                        spanEnd = nextX;
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
                            blender.CompositeSpan(spanX, spanEnd, y, spanAlpha);
                        }

                        spanX = edgeX;
                        spanEnd = nextX;
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
                                spanEnd = nextX;
                            }
                            else
                            {
                                // Only gap alpha matches current span.
                                blender.CompositeSpan(spanX, edgeX, y, spanAlpha);

                                spanX = edgeX;
                                spanEnd = nextX;
                                spanAlpha = alpha;
                            }
                        }
                        else
                        {
                            if (spanAlpha != default)
                            {
                                blender.CompositeSpan(spanX, spanEnd, y, spanAlpha);
                            }

                            // Compose gap.
                            blender.CompositeSpan(spanEnd, edgeX, y, gapAlpha);

                            spanX = edgeX;
                            spanEnd = nextX;
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
            blender.CompositeSpan(spanX, spanEnd, y, spanAlpha);
        }

        if (cover != F24Dot8.Zero && spanEnd < right)
        {
            // Composite anything that goes to the edge of destination image.
            TAlpha alpha = blender.ApplyFillRule(cover << 9);

            blender.CompositeSpan(spanEnd, right, y, alpha);
        }
    }
}