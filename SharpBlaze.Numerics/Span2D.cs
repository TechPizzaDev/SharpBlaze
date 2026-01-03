using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpBlaze.Numerics;

namespace SharpBlaze;

public readonly ref struct Span2D<T>
{
    internal readonly ref T _data;
    private readonly nint _stride;
    private readonly int _width;
    private readonly int _height;

    public int Width => _width;
    public int Height => _height;
    public nint Stride => _stride;

    public bool IsContiguous
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nint) _width * Unsafe.SizeOf<T>() == _stride;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width == 0 || _height == 0;
    }

    internal ulong Area
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ulong) (uint) _width * (uint) _height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span2D(ref T data, int width, int height, nint stride)
    {
        Debug.Assert(width >= 0 && height >= 0 && stride >= 0);
        Debug.Assert((uint) width <= stride);

        _data = ref data;
        _width = width;
        _height = height;
        _stride = stride;
    }

    public Span2D(ref T data) : this(ref data, 1, 1, Unsafe.SizeOf<T>())
    {
    }

    public Span2D(Span<T> data, int width, int height, nint stride)
    {
        if ((uint) width > (nuint) stride)
        {
            ThrowHelper.ThrowArgumentOutOfRange();
        }

        if (height > 0)
        {
            // Last row does not need full stride.
            uint strideCount = (uint) (height - 1);

            uint elemSize = (uint) Unsafe.SizeOf<T>();
            ulong lastByteLength = (ulong) (uint) width * elemSize;

            ulong byteLength = strideCount * (nuint) stride + lastByteLength;
            if (byteLength > (ulong) (uint) data.Length * elemSize)
            {
                ThrowHelper.ThrowArgumentOutOfRange();
            }
        }
        else if (height < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRange();
        }

        _data = ref MemoryMarshal.GetReference(data);
        _width = width;
        _height = height;
        _stride = stride;
    }

    public Span2D(Span<T> data, int width, int height) :
        this(data, width, height, checked((nint) width * Unsafe.SizeOf<T>()))
    {
    }

    public Span2D(Span<T> data) : this(data, data.Length, 1)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Span2D(void* data, int width, int height, nint stride)
    {
        Debug.Assert((uint) width <= (nuint) stride);

        _data = ref *(T*) data;
        _width = width;
        _height = height;
        _stride = stride;
    }

    public Span<T> this[int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) y >= (uint) _height)
                ThrowHelper.ThrowIndexOutOfRange();

            nuint offset = (uint) y * (nuint) _stride;
            ref T data = ref Unsafe.AddByteOffset(ref _data, offset);

            return MemoryMarshal.CreateSpan(ref data, _width);
        }
    }

    public T[] ToArray()
    {
        T[] array = new T[Area];
        CopyTo(new Span2D<T>(array, _width, _height));
        return array;
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
    public Span2D<T> Slice(int x, int y, int width, int height)
    {
        if ((uint) x + (ulong) (uint) width > (uint) _width)
            ThrowHelper.ThrowArgumentOutOfRange();

        if ((uint) y + (ulong) (uint) height > (uint) _height)
            ThrowHelper.ThrowArgumentOutOfRange();

        nuint offset = (uint) y * (nuint) _stride;
        ref T data = ref Unsafe.Add(ref Unsafe.AddByteOffset(ref _data, offset), (uint) x);

        return new(ref data, width, height, _stride);
    }


    public void CopyTo(Span2D<T> destination)
    {
        Marshal2D.Invoke(this, destination, new CopyOp<T>());
    }

    public void Clear()
    {
        Marshal2D.Invoke(this, this, new ClearOp<T>());
    }

    public void Fill(T value)
    {
        Marshal2D.Invoke(this, this, new FillOp<T>(value));
    }

    public Span2D<U> Cast<U>()
    {
        ulong width = (uint) _width * (ulong) Unsafe.SizeOf<T>() / (ulong) Unsafe.SizeOf<U>();

        return new Span2D<U>(
            ref Unsafe.As<T, U>(ref _data),
            checked((int) width),
            _height,
            _stride);
    }
}

internal readonly struct CopyOp<T> : ISpanOp<T>
{
    public void Invoke(Span<T> src, Span<T> dst) => src.CopyTo(dst);
}

internal readonly struct ClearOp<T> : ISpanOp<T>
{
    public void Invoke(Span<T> src, Span<T> dst) => src.Clear();
}

internal readonly struct FillOp<T>(T value) : ISpanOp<T>
{
    public void Invoke(Span<T> src, Span<T> dst) => src.Fill(value);
}

internal interface ISpanOp<T>
{
    void Invoke(Span<T> src, Span<T> dst);
}
