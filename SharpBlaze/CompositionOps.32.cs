using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

public static unsafe partial class CompositionOps
{
    // This must only be included from CompositionOps.h


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha32(uint x, uint a)
    {
        const uint rgMask = 0x00ff00ff;
        const uint offset = 0x00800080;

        uint a0 = (x & rgMask) * a;
        uint a1 = (a0 + ((a0 >> 8) & rgMask) + offset) >> 8;
        uint a2 = a1 & rgMask;

        uint b0 = ((x >> 8) & rgMask) * a;
        uint b1 = (b0 + ((b0 >> 8) & rgMask) + offset);
        uint b2 = b1 & (~rgMask);

        return a2 | b2;
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ApplyAlpha(Vector128<uint> x, Vector128<uint> a)
    {
        var rgMask = Vector128.Create(0x00ff00ffU);
        var offset = Vector128.Create(0x00800080U);

        var a0 = (x & rgMask) * a;
        var a1 = (a0 + ((a0 >> 8) & rgMask) + offset) >> 8;
        var a2 = a1 & rgMask;
        
        var b0 = ((x >> 8) & rgMask) * a;
        var b1 = (b0 + ((b0 >> 8) & rgMask) + offset);
        var b2 = Vector128.AndNot(b1, rgMask);

        return a2 | b2;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ApplyAlpha(Vector512<uint> x, Vector512<uint> a)
    {
        var rgMask = Vector512.Create(0x00ff00ffU);
        var offset = Vector512.Create(0x00800080U);

        var a0 = (x & rgMask) * a;
        var a1 = (a0 + ((a0 >> 8) & rgMask) + offset) >> 8;
        var a2 = a1 & rgMask;
        
        var b0 = ((x >> 8) & rgMask) * a;
        var b1 = (b0 + ((b0 >> 8) & rgMask) + offset);
        var b2 = Vector512.AndNot(b1, rgMask);

        return a2 | b2;
    }
}