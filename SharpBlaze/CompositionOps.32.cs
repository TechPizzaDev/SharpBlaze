using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

public static unsafe partial class CompositionOps
{
    // This must only be included from CompositionOps.h


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha32(uint x, uint a)
    {
        const int rgMask = 0x00ff00ff;

        uint a0 = (x & rgMask) * a;
        uint a1 = (a0 + ((a0 >> 8) & rgMask) + 0x00800080) >> 8;
        uint a2 = a1 & rgMask;

        uint b0 = ((x >> 8) & rgMask) * a;
        uint b1 = (b0 + ((b0 >> 8) & rgMask) + 0x00800080);
        uint b2 = b1 & 0xff00ff00;

        return a2 | b2;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ApplyAlpha(Vector128<uint> x, Vector128<uint> a)
    {
        var rgMask = Vector128.Create(0x00ff00ffU);

        var a0 = (x & rgMask) * a;
        var a1 = (a0 + ((a0 >> 8) & rgMask) + Vector128.Create(0x00800080U)) >> 8;
        var a2 = a1 & rgMask;
        
        var b0 = ((x >> 8) & rgMask) * a;
        var b1 = (b0 + ((b0 >> 8) & rgMask) + Vector128.Create(0x00800080U));
        var b2 = b1 & Vector128.Create(0xff00ff00U);

        return a2 | b2;
    }
}