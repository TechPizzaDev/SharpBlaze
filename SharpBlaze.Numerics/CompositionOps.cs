using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

public static partial class CompositionOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha(uint x, byte a)
    {
        if (Unsafe.SizeOf<nint>() == 8)
        {
            return ApplyAlpha64(x, a);
        }
        return ApplyAlpha32(x, a);
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

    public static void CompositeSpanSourceOver(Span<uint> d, byte alpha, uint color)
    {
        // For opaque colors, use opaque span composition version.
        Debug.Assert(alpha != 255 || (color >> 24) < 255);

        uint cba = ApplyAlpha(color, alpha);
        byte a = (byte) (255u - (cba >> 24));

        if (Avx512BW.IsSupported)
        {
            while (d.Length >= Vector256<uint>.Count)
            {
                Vector256<uint> dd = Vector256.Create<uint>(d);
                BlendSourceOver_Avx512BW(dd, cba, a).CopyTo(d);
                d = d[Vector256<uint>.Count..];
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            while (d.Length >= Vector128<uint>.Count)
            {
                Vector128<uint> dd = Vector128.Create<uint>(d);
                BlendSourceOver(dd, cba, a).CopyTo(d);
                d = d[Vector128<uint>.Count..];
            }
        }

        for (int x = 0; x < d.Length; x++)
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
}