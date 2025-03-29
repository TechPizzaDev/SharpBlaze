using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public readonly ref struct ReadOnlySpan2D<T>
{
    private readonly ref readonly T _data;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;

    public int Width => _width;
    public int Height => _height;
    public int Stride => _stride;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan2D(ref readonly T data, int width, int height, int stride)
    {
        Debug.Assert((uint) width <= (uint) stride);

        _data = ref data;
        _width = width;
        _height = height;
        _stride = stride;
    }

    public ReadOnlySpan2D(ReadOnlySpan<T> data, int width, int height, int stride)
    {
        if ((uint) width > (uint) stride)
            ThrowHelper.ThrowArgumentOutOfRange();

        _data = ref MemoryMarshal.GetReference(data[..(height * stride)]);
        _width = width;
        _height = height;
        _stride = stride;
    }

    public ReadOnlySpan<T> this[int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) y >= (uint) _height)
                ThrowHelper.ThrowIndexOutOfRange();

            return MemoryMarshal.CreateReadOnlySpan(
                in Unsafe.Add(ref Unsafe.AsRef(in _data), y * _stride),
                _width);
        }
    }
}
