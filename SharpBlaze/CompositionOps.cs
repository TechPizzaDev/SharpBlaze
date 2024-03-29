using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public static unsafe partial class CompositionOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha(uint x, uint a)
    {
        if (sizeof(nint) == 8)
        {
            return ApplyAlpha64(x, a);
        }
        else
        {
            return ApplyAlpha32(x, a);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint BlendSourceOver(uint d, uint s)
    {
        return s + ApplyAlpha(d, 255 - (s >> 24));
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CompositeSpanSourceOver(int pos, int end, uint* d, int alpha, uint color)
    {
        Debug.Assert(pos >= 0);
        Debug.Assert(pos < end);
        Debug.Assert(d != null);
        Debug.Assert(alpha <= 255);

        // For opaque colors, use opaque span composition version.
        Debug.Assert((color >> 24) < 255);

        int e = end;
        uint cba = ApplyAlpha(color, (uint) alpha);

        for (int x = pos; x < e; x++)
        {
            uint dd = d[x];

            if (dd == 0)
            {
                d[x] = cba;
            }
            else
            {
                d[x] = BlendSourceOver(dd, cba);
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CompositeSpanSourceOverOpaque(int pos, int end, uint* d, int alpha, uint color)
    {
        Debug.Assert(pos >= 0);
        Debug.Assert(pos < end);
        Debug.Assert(d != null);
        Debug.Assert(alpha <= 255);
        Debug.Assert((color >> 24) == 255);

        int e = end;

        if (alpha == 255)
        {
            // Solid span, write only.
            for (int x = pos; x < e; x++)
            {
                d[x] = color;
            }
        }
        else
        {
            // Transparent span.
            uint cba = ApplyAlpha(color, (uint) alpha);

            for (int x = pos; x < e; x++)
            {
                uint dd = d[x];

                if (dd == 0)
                {
                    d[x] = cba;
                }
                else
                {
                    d[x] = BlendSourceOver(dd, cba);
                }
            }
        }
    }
}

public readonly unsafe struct SpanBlender: ISpanBlender
{
    public SpanBlender(uint color)
    {
        Color = color;
    }

    readonly uint Color = 0;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompositeSpan(int pos, int end, uint* d, int alpha)
    {
        CompositionOps.CompositeSpanSourceOver(pos, end, d, alpha, Color);
    }
}

/**
 * Span blender which assumes source color is opaque.
 */
public readonly unsafe struct SpanBlenderOpaque : ISpanBlender
{
    public SpanBlenderOpaque(uint color)
    {
        Color = color;
    }

    readonly uint Color = 0;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompositeSpan(int pos, int end, uint* d, int alpha)
    {
        CompositionOps.CompositeSpanSourceOverOpaque(pos, end, d, alpha, Color);
    }
}