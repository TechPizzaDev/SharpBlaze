using System.Diagnostics;

namespace SharpBlaze;


/**
 * 8.8 fixed point number.
 */
public partial struct F8Dot8
{
    private short _value;

    public static implicit operator short(F8Dot8 value) => value._value;

    public static implicit operator F8Dot8(short value) => new() { _value = value };
}

public struct F8Dot8x2
{
    private uint _value;

    public static implicit operator uint(F8Dot8x2 value) => value._value;

    public static implicit operator F8Dot8x2(uint value) => new() { _value = value };
}

public struct F8Dot8x4
{
    private ulong _value;

    public static implicit operator ulong(F8Dot8x4 value) => value._value;

    public static implicit operator F8Dot8x4(ulong value) => new() { _value = value };
}


public partial struct F8Dot8
{
    public static F8Dot8x2 PackF24Dot8ToF8Dot8x2(F24Dot8 a, F24Dot8 b)
    {
        // Values must be small enough.
        Debug.Assert((a & 0xffff0000) == 0);
        Debug.Assert((b & 0xffff0000) == 0);

        return (F8Dot8x2) (uint) (a) | ((F8Dot8x2) (uint) (b) << 16);
    }


    public static F8Dot8x4 PackF24Dot8ToF8Dot8x4(F24Dot8 a, F24Dot8 b, F24Dot8 c, F24Dot8 d)
    {
        // Values must be small enough.
        Debug.Assert((a & 0xffff0000) == 0);
        Debug.Assert((b & 0xffff0000) == 0);
        Debug.Assert((c & 0xffff0000) == 0);
        Debug.Assert((d & 0xffff0000) == 0);

        return (F8Dot8x4) (uint) (a) | ((F8Dot8x4) (uint) (b) << 16) |
            ((F8Dot8x4) (uint) (c) << 32) | ((F8Dot8x4) (uint) (d) << 48);
    }


    public static F24Dot8 UnpackLoFromF8Dot8x2(F8Dot8x2 a)
    {
        return (F24Dot8) (a & 0xffff);
    }


    public static F24Dot8 UnpackHiFromF8Dot8x2(F8Dot8x2 a)
    {
        return (F24Dot8) (a >> 16);
    }
}