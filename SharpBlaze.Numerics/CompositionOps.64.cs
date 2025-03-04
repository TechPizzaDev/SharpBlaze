using System.Runtime.CompilerServices;

namespace SharpBlaze;

public static partial class CompositionOps
{
    // This must only be included from CompositionOps.h


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha64(uint x, byte a)
    {
        const ulong rgbaMask = 0x00ff_00ff_00ff_00ff;
        const ulong offset = 0x0080_0080_0080_0080;
        
        ulong a0 = ((x | (((ulong) x) << 24)) & rgbaMask) * a;
        ulong a1 = (a0 + ((a0 >> 8) & rgbaMask) + offset) >> 8;
        ulong a2 = a1 & rgbaMask;

        return ((uint) a2) | ((uint) (a2 >> 24));
    }
}
