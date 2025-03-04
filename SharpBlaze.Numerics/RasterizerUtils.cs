using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

using static Utils;

public static class RasterizerUtils
{

    /**
     * Given area, calculate alpha in range 0-255 using non-zero fill rule.
     *
     * This function implements `min(abs(area), 1.0)`.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AreaToAlphaNonZero(int area)
    {
        int aa = area >> 9;

        // Find absolute area value.
        int mask = aa >> 31;
        int aaabs = (aa + mask) ^ mask;

        // Clamp absolute area value to be 255 or less.
        return Math.Min(aaabs, 255);
    }


    /**
     * Given area, calculate alpha in range 0-255 using even-odd fill rule.
     *
     * This function implements `abs(area - 2.0 × round(0.5 × area))`.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AreaToAlphaEvenOdd(int area)
    {
        int aa = area >> 9;

        // Find absolute area value.
        int mask = aa >> 31;
        int aaabs = (aa + mask) ^ mask;

        int aac = aaabs & 511;

        if (aac > 256)
        {
            return 512 - aac;
        }

        return Math.Min(aac, 255);
    }


    /**
     * This function returns 1 if value is greater than zero and it is divisible
     * by 256 (equal to one in 24.8 format) without a reminder.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FindAdjustment(F24Dot8 value)
    {
        // Will be set to 0 is value is zero or less. Otherwise it will be 1.
        int lte0 = ~((value - 1) >> 31) & 1;

        // Will be set to 1 if value is divisible by 256 without a reminder.
        // Otherwise it will be 0.
        int db256 = (((value & 255) - 1) >> 31) & 1;

        // Return 1 if both bits (more than zero and disisible by 256) are set.
        return lte0 & db256;
    }

}