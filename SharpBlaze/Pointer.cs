namespace SharpBlaze;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

public readonly unsafe struct Pointer<T>(T* value)
{
    public T* Value => value;

    public static implicit operator Pointer<T>(T* value)
    {
        return new Pointer<T>(value);
    }

    public static implicit operator T*(Pointer<T> value)
    {
        return value.Value;
    }
}
