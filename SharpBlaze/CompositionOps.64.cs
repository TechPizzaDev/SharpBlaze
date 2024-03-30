using System.Runtime.CompilerServices;

namespace SharpBlaze;

public static unsafe partial class CompositionOps
{
    // This must only be included from CompositionOps.h


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha64(uint x, uint a)
    {
        ulong a0 = ((((ulong) (x)) | (((ulong) (x)) << 24)) & 0x00ff00ff00ff00ff) * a;
        ulong a1 = (a0 + ((a0 >> 8) & 0x00ff00ff00ff00ff) + 0x80008000800080) >> 8;
        ulong a2 = a1 & 0x00ff00ff00ff00ff;

        return ((uint) (a2)) | ((uint) (a2 >> 24));
    }
}