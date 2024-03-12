
namespace SharpBlaze;

public struct TileIndex
{
    uint _value;

    public static implicit operator uint(TileIndex value)
    {
        return value._value;
    }

    public static explicit operator int(TileIndex value)
    {
        return (int) value._value;
    }

    public static implicit operator TileIndex(uint value)
    {
        return new TileIndex() { _value = value };
    }

    public static explicit operator TileIndex(int value)
    {
        return new TileIndex() { _value = (uint) value };
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}
