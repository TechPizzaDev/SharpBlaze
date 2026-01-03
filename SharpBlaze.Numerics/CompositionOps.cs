using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

public static partial class CompositionOps
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ApplyAlpha(uint x, byte a)
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
    public static Vector128<uint> BlendSourceOver(Vector128<uint> d, Vector128<uint> s, Vector256<ushort> a)
    {
        Vector128<uint> mixed = Avx2.IsSupported ? ApplyAlpha_Avx2(d, a) : ApplyAlpha(d, a.GetLower());
        return s + mixed;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<uint> BlendSourceOver(Vector256<uint> d, Vector256<uint> s, Vector512<ushort> a)
    {
        return s + ApplyAlpha_Avx512BW(d, a);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void CompositeSpanSourceOver(Span<uint> d, byte alpha, uint color)
    {
        // For opaque colors, use opaque span composition version.
        Debug.Assert(alpha != 255 || (color >> 24) < 255);

        uint cba = ApplyAlpha(color, alpha);
        byte a = (byte) (255u - (cba >> 24));

        if (Avx512BW.IsSupported)
        {
            Vector256<uint> v_cba = Vector256.Create(cba);
            Vector512<ushort> v_a = Vector512.Create((ushort) a);

            while (d.Length >= Vector256<uint>.Count)
            {
                Vector256<uint> dd = Vector256.Create<uint>(d);
                BlendSourceOver(dd, v_cba, v_a).CopyTo(d);
                d = d.Slice(Vector256<uint>.Count);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<uint> v_cba = Vector128.Create(cba);
            Vector256<ushort> v_a = Vector256.Create((ushort) a);

            while (d.Length >= Vector128<uint>.Count)
            {
                Vector128<uint> dd = Vector128.Create<uint>(d);
                BlendSourceOver(dd, v_cba, v_a).CopyTo(d);
                d = d.Slice(Vector128<uint>.Count);
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