
namespace SharpBlaze;

public readonly struct TileIndex
{
    internal readonly uint _value;

    private TileIndex(uint value)
    {
        _value = value;
    }

    public static implicit operator uint(TileIndex value) => value._value;

    public static explicit operator int(TileIndex value) => (int) value._value;

    public static implicit operator TileIndex(uint value) => new(value);

    public static explicit operator TileIndex(int value) => new((uint) value);

    public override string ToString()
    {
        return _value.ToString();
    }
}
