using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

public static unsafe partial class CompositionOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha(uint x, byte a)
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
    public static uint BlendSourceOver(uint d, uint s, byte a)
    {
        return s + ApplyAlpha(d, a);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> BlendSourceOver(Vector128<uint> d, uint s, byte a)
    {
        Vector128<uint> mixed = Avx2.IsSupported ? ApplyAlpha_Avx2(d, a) : ApplyAlpha(d, a);
        return Vector128.Create(s) + mixed;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<uint> BlendSourceOver_Avx512BW(Vector256<uint> d, uint s, byte a)
    {
        return Vector256.Create(s) + ApplyAlpha_Avx512BW(d, a);
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
        uint cba = ApplyAlpha(color, (byte) alpha);
        byte a = (byte) (255u - (cba >> 24));
        
        if (Avx512BW.IsSupported)
        {
            for (; x + Vector256<uint>.Count <= end; x += Vector256<uint>.Count)
            {
                Vector256<uint> dd = Vector256.Load(d + x);
                BlendSourceOver_Avx512BW(dd, cba, a).Store(d + x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x + Vector128<uint>.Count <= end; x += Vector128<uint>.Count)
            {
                Vector128<uint> dd = Vector128.Load(d + x);
                BlendSourceOver(dd, cba, a).Store(d + x);
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
                d[x] = BlendSourceOver(dd, cba, a);
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
