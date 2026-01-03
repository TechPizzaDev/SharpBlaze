using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SharpBlaze;

public static partial class CompositionOps
{
    // This must only be included from CompositionOps.h

    const uint HalfOffset = 0x0080_0080;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha32(uint x, byte a)
    {
        const uint rgMask = 0x00ff_00ff;

        uint a0 = (x & rgMask) * a;
        uint a1 = (a0 + ((a0 >> 8) & rgMask) + HalfOffset) >> 8;
        uint a2 = a1 & rgMask;

        uint b0 = ((x >> 8) & rgMask) * a;
        uint b1 = (b0 + ((b0 >> 8) & rgMask) + HalfOffset);
        uint b2 = b1 & (~rgMask);

        return a2 | b2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ApplyAlpha(Vector128<uint> x, Vector128<ushort> a)
    {
        Vector128<ushort> offset = Vector128.Create(HalfOffset).AsUInt16();

        Vector128<ushort> a0 = Vector128.WidenLower(x.AsByte()) * a;
        Vector128<ushort> a1 = (a0 + (a0 >> 8) + offset) >> 8;

        Vector128<ushort> b0 = Vector128.WidenUpper(x.AsByte()) * a;
        Vector128<ushort> b1 = (b0 + (b0 >> 8) + offset) >> 8;

        return Vector128.Narrow(a1, b1).AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ApplyAlpha_Avx2(Vector128<uint> x, Vector256<ushort> a)
    {
        Vector256<ushort> x16 = Avx2.ConvertToVector256Int16(x.AsByte()).AsUInt16();
        Vector256<ushort> a0 = x16 * a;

        Vector256<ushort> offset = Vector256.Create(HalfOffset).AsUInt16();
        Vector256<ushort> a1 = ((a0 + (a0 >> 8) + offset) >> 8).AsUInt16();
        
        Vector128<byte> a2 = Avx512BW.VL.IsSupported
            ? Avx512BW.VL.ConvertToVector128Byte(a1)
            : Vector128.Narrow(a1.GetLower(), a1.GetUpper());

        return a2.AsUInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ApplyAlpha_Avx512BW(Vector256<uint> x, Vector512<ushort> a)
    {
        Vector512<ushort> x16 = Avx512BW.ConvertToVector512UInt16(x.AsByte());
        Vector512<ushort> a0 = x16 * a;
        
        Vector512<ushort> a1 = (a0 + (a0 >> 8) + Vector512.Create(HalfOffset).AsUInt16()) >> 8;
        Vector256<byte> a2 = Avx512BW.ConvertToVector256Byte(a1);

        return a2.AsUInt32();
    }
}
