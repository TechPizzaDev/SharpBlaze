using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    
    internal ReadOnlySpan2D(ref readonly T data, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, stride);

        _data = ref data;
        _width = width;
        _height = height;
        _stride = stride;
    }
    
    public ReadOnlySpan2D(ReadOnlySpan<T> data, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, stride);
        
        _data = ref MemoryMarshal.GetReference(data.Slice(0, height * stride));
        _width = width;
        _height = height;
        _stride = stride;
    }
    
    public ReadOnlySpan<T> this[int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)y >= (uint)_height)
                ThrowHelper.ThrowIndexOutOfRange();
            
            return MemoryMarshal.CreateReadOnlySpan(
                in Unsafe.Add(ref Unsafe.AsRef(in _data), y * _stride),
                _width);
        }
    }
}