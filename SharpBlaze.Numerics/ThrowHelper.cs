using System;
using System.Diagnostics.CodeAnalysis;

namespace SharpBlaze.Numerics;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException();
    }
    
    [DoesNotReturn]
    public static void ThrowArgumentOutOfRange()
    {
        throw new ArgumentOutOfRangeException();
    }
    
    [DoesNotReturn]
    public static void ThrowDestinationTooShort()
    {
        throw new ArgumentException("Destination is too short.");
    }
}