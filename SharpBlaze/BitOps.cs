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
    internal nuint _value;
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
    public static unsafe int BitVectorsForMaxBitCount(int maxBitCount)
    {
        Debug.Assert(maxBitCount != 0);

        int x = sizeof(BitVector) * 8;

        return (maxBitCount + x - 1) / x;
    }


    /**
     * Calculates how many bits are set to 1 in bitmap.
     *
     * @param vec An array of BitVector values containing bits.
     *
     * @param count A number of values in vec. Note that this is not maximum
     * amount of bits to scan, but the amount of BitVector numbers vec contains.
     */
    public static unsafe int CountBitsInVector(BitVector* vec, int count)
    {
        int num = 0;

        for (int i = 0; i < count; i++)
        {
            nuint value = vec[i]._value;

            if (value != 0)
            {
                num += BitOperations.PopCount(value);
            }
        }

        return num;
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
    public static unsafe bool ConditionalSetBit(Span<BitVector> vec, uint index)
    {
        (uint vecIndex, uint localIndex) = Math.DivRem(index, (uint) sizeof(nuint) * 8U);

        nuint current = vec[(int) vecIndex]._value;
        nuint bit = ((nuint) 1) << (int) localIndex;

        if ((current & bit) == 0)
        {
            vec[(int) vecIndex]._value = current | bit;
            return true;
        }

        return false;
    }


    /**
     * Returns index to the first bit vector value which contains at least one bit
     * set to 1. If the entire array contains only zero bit vectors, an index to
     * the last bit vector will be returned.
     *
     * @param vec Bit vector array. Must not be nullptr.
     *
     * @param maxBitVectorCount A number of items in bit vector array. This
     * function always returns value less than this.
     */
    public static unsafe int FindFirstNonZeroBitVector(BitVector* vec, int maxBitVectorCount)
    {
        Debug.Assert(vec != null);
        Debug.Assert(maxBitVectorCount > 0);

        int i = 0;

        for (; i < maxBitVectorCount; i++)
        {
            if (vec[i]._value != 0)
            {
                return i;
            }
        }

        return i;
    }
}
