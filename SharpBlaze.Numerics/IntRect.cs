using System.IO;
using System.Runtime.CompilerServices;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntRect FromMinMax(int x1, int y1, int x2, int y2)
    {
        return new(Vector128.Create(x1, y1, x2, y2));
    }

    public static IntRect FromMinMax(Vector128<int> minMax)
    {
        return new IntRect(minMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntRect Union(IntRect a, IntRect b)
    {
        Vector128<int> vA = a._value;
        Vector128<int> vB = b._value;
        
        Vector128<int> mix;
        if (Vector64.IsHardwareAccelerated)
        {
            Vector64<int> min = Vector64.Min(vA.GetLower(), vB.GetLower());
            Vector64<int> max = Vector64.Max(vA.GetUpper(), vB.GetUpper());
            mix = Vector128.Create(min, max);
        }
        else
        {
            Vector128<int> min = Vector128.Min(vA, vB);
            Vector128<int> max = Vector128.Max(vA, vB);
            mix = Vector128.ConditionalSelect(Vector128.Create(~0, ~0, 0, 0), min, max);
        }
        return new IntRect(mix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntRect Intersect(IntRect a, IntRect b)
    {
        Vector128<int> vA = a._value;
        Vector128<int> vB = b._value;
        
        Vector128<int> mix;
        if (Vector64.IsHardwareAccelerated)
        {
            Vector64<int> max = Vector64.Max(vA.GetLower(), vB.GetLower());
            Vector64<int> min = Vector64.Min(vA.GetUpper(), vB.GetUpper());
            mix = Vector128.Create(max, min);
        }
        else
        {
            Vector128<int> max = Vector128.Max(vA, vB);
            Vector128<int> min = Vector128.Min(vA, vB);
            mix = Vector128.ConditionalSelect(Vector128.Create(~0, ~0, 0, 0), max, min);
        }
        return new IntRect(mix);
    }

    public Vector128<int> AsVector128()
    {
        return _value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(IntRect other)
    {
        Vector128<int> vA = _value;
        Vector128<int> vB = other._value;

        Vector128<int> mix;
        if (Vector64.IsHardwareAccelerated)
        {
            Vector64<int> min = Vector64.LessThan(vB.GetLower(), vA.GetLower());
            Vector64<int> max = Vector64.GreaterThan(vB.GetUpper(), vA.GetUpper());
            mix = Vector128.Create(min, max);
        }
        else
        {
            Vector128<int> min = Vector128.LessThan(vB, vA);
            Vector128<int> max = Vector128.GreaterThan(vB, vA);
            mix = Vector128.ConditionalSelect(Vector128.Create(~0, ~0, 0, 0), min, max);
        }
        return Vector128.EqualsAll(mix, Vector128<int>.Zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out int X1, out int Y1, out int X2, out int Y2)
    {
        X1 = _value.GetElement(0);
        Y1 = _value.GetElement(1);
        X2 = _value.GetElement(2);
        Y2 = _value.GetElement(3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasArea()
    {
        Vector128<int> v = _value;
        
        if (Vector64.IsHardwareAccelerated)
        {
            return Vector64.LessThanAll(v.GetLower(), v.GetUpper());
        }

        Vector128<int> left = Vector128.Shuffle(v, Vector128.Create(0, 1, 0, 1));
        Vector128<int> right = Vector128.Shuffle(v, Vector128.Create(2, 3, 2, 3));
        return Vector128.LessThanAll(left, right);
    }
    
    public void Write(BinaryWriter writer)
    {
        (int minX, int minY, int maxX, int maxY) = this;
        writer.Write(minX);
        writer.Write(minY);
        writer.Write(maxX);
        writer.Write(maxY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntRect operator +(IntRect a, IntRect b) => new(a._value + b._value);
}
