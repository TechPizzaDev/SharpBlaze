using System;
using System.Runtime.Intrinsics;

namespace SharpBlaze;


public readonly struct IntRect
{
    public IntRect(int x, int y, int width, int height)
    {
        _value = Vector128.Create(x, y, x + width, y + height);
    }

    public IntRect(Vector128<int> value)
    {
        _value = value;
    }

    private readonly Vector128<int> _value;

    public int X => _value.GetElement(0);

    public int Y => _value.GetElement(1);

    public int Width => _value.GetElement(2) - _value.GetElement(0);

    public int Height => _value.GetElement(3) - _value.GetElement(1);

    public static IntRect FromMinMax(int x1, int y1, int x2, int y2)
    {
        return new(Vector128.Create(x1, y1, x2, y2));
    }

    public static IntRect FromMinMax(Vector128<int> minMax)
    {
        return new IntRect(minMax);
    }

    public static IntRect Union(IntRect a, IntRect b)
    {
        Vector128<int> min = Vector128.Min(a._value, b._value);
        Vector128<int> max = Vector128.Max(a._value, b._value);
        Vector128<int> mix = Vector128.ConditionalSelect(Vector128.Create(~0, ~0, 0, 0), min, max);
        return new IntRect(mix);
    }

    public static IntRect Intersect(IntRect a, IntRect b)
    {
        Vector128<int> min = Vector128.Min(a._value, b._value);
        Vector128<int> max = Vector128.Max(a._value, b._value);
        Vector128<int> mix = Vector128.ConditionalSelect(Vector128.Create(~0, ~0, 0, 0), max, min);
        return new IntRect(mix);
    }

    public Vector128<int> AsVector128()
    {
        return _value;
    }

    public bool Contains(IntRect other)
    {
        Vector128<int> min = Vector128.GreaterThanOrEqual(_value, other._value);
        Vector128<int> max = Vector128.LessThanOrEqual(_value, other._value);
        Vector128<int> mix = Vector128.ConditionalSelect(Vector128.Create(~0, ~0, 0, 0), min, max);
        return Vector128.EqualsAll(mix, Vector128<int>.AllBitsSet);
    }

    public void Deconstruct(out int X1, out int Y1, out int X2, out int Y2)
    {
        X1 = _value.GetElement(0);
        Y1 = _value.GetElement(1);
        X2 = _value.GetElement(2);
        Y2 = _value.GetElement(3);
    }

    public bool HasArea()
    {
        (Vector128<long> left, Vector128<long> right) = Vector128.Widen(_value);
        return Vector128.LessThanAll(left, right);
    }

    public static IntRect operator +(IntRect a, IntRect b) => new(a._value + b._value);
}