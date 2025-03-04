using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SharpBlaze;

public struct F24Dot8Point 
{
    public F24Dot8 X;
    public F24Dot8 Y;

    public F24Dot8Point(F24Dot8 x, F24Dot8 y)
    {
        X = x;
        Y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector128<int> ToVector128()
    {
        return Vector128.Create((uint)X._value | ((ulong)Y._value << 32)).AsInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector256<int> ToVector256()
    {
        return Vector256.Create((uint)X._value | ((ulong)Y._value << 32)).AsInt32();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public F24Dot8Point Clamp(F24Dot8Point min, F24Dot8Point max)
    {
        return new F24Dot8Point(
            Utils.Clamp(X, min.X, max.X),
            Utils.Clamp(Y, min.Y, max.Y));
    }
}
