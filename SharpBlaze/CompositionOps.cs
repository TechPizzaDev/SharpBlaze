using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

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
    public static Vector128<uint> BlendSourceOver(Vector128<uint> d, Vector128<uint> s)
    {
        return s + ApplyAlpha(d, Vector128.Create(255u) - (s >> 24));
    }


    internal static void CompositeSpanSourceOver(int pos, int end, uint* d, uint alpha, uint color)
    {
        Debug.Assert(pos >= 0);
        Debug.Assert(pos < end);
        Debug.Assert(d != null);
        Debug.Assert(alpha <= 255);

        // For opaque colors, use opaque span composition version.
        Debug.Assert(alpha != 255 || (color >> 24) < 255);

        int x = pos;
        uint cba = ApplyAlpha(color, alpha);

        if (Vector128.IsHardwareAccelerated)
        {
            var vCba = Vector128.Create(cba);

            for (; x + Vector128<uint>.Count - 1 <= end; x += Vector128<uint>.Count)
            {
                var dd = Vector128.Load(d + x);

                BlendSourceOver(dd, vCba).Store(d + x);
            }
        }

        for (; x < end; x++)
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


    internal static void CompositeSpanSourceOverOpaque(int pos, int end, uint* d, uint alpha, uint color)
    {
        Debug.Assert(pos >= 0);
        Debug.Assert(pos < end);
        Debug.Assert(d != null);
        Debug.Assert(alpha <= 255);
        Debug.Assert((color >> 24) == 255);

        if (alpha == 255)
        {
            // Solid span, write only.
            int length = end - pos;
            if (length > 0)
            {
                new Span<uint>(d + pos, end - pos).Fill(color);
            }
        }
        else
        {
            // Transparent span.
            CompositeSpanSourceOver(pos, end, d, alpha, color);
        }
    }
}

public readonly unsafe struct SpanBlender : ISpanBlender
{
    public SpanBlender(uint color)
    {
        Color = color;
    }

    readonly uint Color = 0;


    public void CompositeSpan(int pos, int end, uint* d, uint alpha)
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


    public void CompositeSpan(int pos, int end, uint* d, uint alpha)
    {
        CompositionOps.CompositeSpanSourceOverOpaque(pos, end, d, alpha, Color);
    }
}