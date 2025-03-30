using System;

namespace SharpBlaze;

public unsafe readonly struct BumpToken<T>
{
    private readonly T* _ptr;
    private readonly int _length;
    
    public bool HasValue => _ptr != null;
    
    public int Length => _length;

    internal BumpToken(T* ptr, int length)
    {
        _ptr = ptr;
        _length = length;
    }

    public T* GetPointer() => _ptr;

    public Span<T> AsSpan() => new(_ptr, _length);
    
    public static implicit operator ReadOnlySpan<T>(BumpToken<T> token) => new(token._ptr, token._length);
}
