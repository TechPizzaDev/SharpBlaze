using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SharpBlaze;

internal static class ThrowHelper
{
    public static void ThrowOnInvalid(OperationStatus status)
    {
        if (status == OperationStatus.InvalidData)
        {
            ThrowInvalidData();
        }
        else if (status != OperationStatus.Done)
        {
            ThrowUnreachableException(status);
        }
    }

    private static void ThrowInvalidData()
    {
        throw new InvalidDataException();
    }

    [DoesNotReturn]
    private static void ThrowUnreachableException<T>(T value)
    {
        throw new UnreachableException(value?.ToString());
    }
}