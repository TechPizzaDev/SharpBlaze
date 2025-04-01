using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpBlaze;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public unsafe readonly struct BumpToken2D<T>
{
    private readonly T** _ptr;
    private readonly ushort _height;
    private readonly ushort _width;

    public bool HasValue => _ptr != null;

    public int Width => _width;

    public int Height => _height;

    internal BumpToken2D(T** ptr, int width, int height)
    {
        _ptr = ptr;
        _width = checked((ushort) width);
        _height = checked((ushort) height);
    }

    public T** GetPointer() => _ptr;

    public BumpToken<T> this[int y]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) y >= (uint) _height)
                ThrowHelper.ThrowIndexOutOfRange();

            T* ptr = _ptr[y];
            if (ptr == null)
                return default;
            
            return new BumpToken<T>(ptr, _width);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint) y >= (uint) _height)
                ThrowHelper.ThrowIndexOutOfRange();

            T* ptr = value.GetPointer();
            if (ptr != null && value.Length != _width)
                ThrowHelper.ThrowArgumentOutOfRange();

            _ptr[y] = ptr;
        }
    }
}
