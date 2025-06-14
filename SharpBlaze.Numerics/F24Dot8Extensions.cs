using System.Runtime.CompilerServices;

namespace SharpBlaze.Numerics;

public static class F24Dot8Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 ToF24D8(this int value)
    {
        return Unsafe.BitCast<int, F24Dot8>(value << 8);
    }
}
