using System.Runtime.CompilerServices;

namespace SharpBlaze;

public static unsafe partial class CompositionOps
{
    // This must only be included from CompositionOps.h


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ApplyAlpha32(uint x, uint a)
    {
         uint a0 = (x & 0x00ff00ff) * a;
         uint a1 = (a0 + ((a0 >> 8) & 0x00ff00ff) + 0x00800080) >> 8;
         uint a2 = a1 & 0x00ff00ff;

         uint b0 = ((x >> 8) & 0x00ff00ff) * a;
         uint b1 = (b0 + ((b0 >> 8) & 0x00ff00ff) + 0x00800080);
         uint b2 = b1 & 0xff00ff00;

        return a2 | b2;
    }
}