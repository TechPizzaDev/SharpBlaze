using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    public Span2D(Span<T> data, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, stride);

        _data = ref MemoryMarshal.GetReference(data.Slice(0, height * stride));
        _width = width;
        _height = height;
        _stride = stride;
    }

    public Span2D(Span<T> data, int width, int height)
    {
        _data = ref MemoryMarshal.GetReference(data.Slice(0, height * width));
        _width = width;
        _height = height;
        _stride = width;
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
            if ((uint)y >= (uint)_height)
                ThrowHelper.ThrowIndexOutOfRange();
            
            return MemoryMarshal.CreateSpan(
                ref Unsafe.Add(ref _data, y * _stride),
                _width);
        }
    }
    
    public static implicit operator ReadOnlySpan2D<T>(Span2D<T> span)
    {
        return new(ref span._data, span._width, span._height, span._stride);
    }
}
