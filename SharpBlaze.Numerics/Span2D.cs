using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public readonly ref struct Span2D<T>
{
    private readonly ref T _data;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;

    public int Width => _width;
    public int Height => _height;
    public int Stride => _stride;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span2D(ref T data, int width, int height, int stride)
    {
        Debug.Assert((uint) width <= (uint) stride);

        _data = ref data;
        _width = width;
        _height = height;
        _stride = stride;
    }

    public Span2D(Span<T> data, int width, int height, int stride)
    {
        if ((uint) width > (uint) stride)
            ThrowHelper.ThrowArgumentOutOfRange();

        // Last row does not need full stride.
        int length = (height - 1) * stride + width;

        _data = ref MemoryMarshal.GetReference(data[..length]);
        _width = width;
        _height = height;
        _stride = stride;
    }

    public Span2D(Span<T> data, int width, int height) : this(data, width, height, width)
    {
    }

    public unsafe Span2D(T* data, int width, int height)
    {
        _data = ref *data;
        _width = width;
        _height = height;
        _stride = width;
    }

    public Span<T> this[int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) y >= (uint) _height)
                ThrowHelper.ThrowIndexOutOfRange();

            return MemoryMarshal.CreateSpan(
                ref Unsafe.Add(ref _data, y * _stride),
                _width);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span2D<T> Cut(int width)
    {
        if ((uint) width > (uint) _width)
            ThrowHelper.ThrowArgumentOutOfRange();
        
        return new(ref _data, width, _height, _stride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span2D<T> Cut(int width, int height)
    {
        if ((uint) width > (uint) _width)
            ThrowHelper.ThrowArgumentOutOfRange();

        if ((uint) height > (uint) _height)
            ThrowHelper.ThrowArgumentOutOfRange();

        return new(ref _data, width, height, _stride);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan2D<T>(Span2D<T> span)
    {
        return new(ref span._data, span._width, span._height, span._stride);
    }
}
