using System;
using System.Diagnostics.CodeAnalysis;

namespace SharpBlaze;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowIndexOutOfRange()
    {
        throw new IndexOutOfRangeException();
    }
}