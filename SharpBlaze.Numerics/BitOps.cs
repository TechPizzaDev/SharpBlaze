using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

// BitVector is a fixed size bit array that fits into one register.
//
// Bit vector type should be either uint32 or uint64, depending on target CPU.
// It is tempting to just use an alias for something like uintptr_t, but it is
// easier for compiler-specific implementation to choose correct functions for
// builtins. See CountBits for GCC, for example. It has two implementations,
// for uint32 and uint64, calling either __builtin_popcount or
// __builtin_popcountl. And the rest of the API can use these functions
// without worrying that compiler will get confused which version to call.

public struct BitVector
{
    public nuint _value;
}

public static class BitOps
{
    /**
     * Returns the amount of BitVector values needed to contain at least a given
     * amount of bits.
     *
     * @param maxBitCount Maximum number of bits for which storage is needed. Must
     * be at least 1.
     */
    public static int BitVectorsForMaxBitCount(int maxBitCount)
    {
        Debug.Assert(maxBitCount != 0);

        int x = Unsafe.SizeOf<BitVector>() * 8;

        return (maxBitCount + x - 1) / x;
    }


    /**
     * Finds if bit at a given index is set to 1. If it is, this function returns
     * false. Otherwise, it sets bit at this index and returns true.
     *
     * @param vec Array of bit vectors. Must not be nullptr and must contain at
     * least (index + 1) amount of bits.
     *
     * @param index Bit index to test and set. Must be at least 0.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ConditionalSetBit(Span<BitVector> vec, uint index)
    {
        (uint vecIndex, uint localIndex) = Math.DivRem(index, (uint) Unsafe.SizeOf<nuint>() * 8U);

        nuint current = vec[(int) vecIndex]._value;
        nuint bit = ((nuint) 1) << (int) localIndex;

        if ((current & bit) == 0)
        {
            vec[(int) vecIndex]._value = current | bit;
            return true;
        }

        return false;
    }
}
