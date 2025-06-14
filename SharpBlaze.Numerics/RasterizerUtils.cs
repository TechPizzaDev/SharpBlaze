using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public static class RasterizerUtils
{
    /**
     * Given area, calculate alpha in range 0-255 using non-zero fill rule.
     *
     * This function implements `min(abs(area), 1.0)`.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AreaToAlphaNonZero(F24Dot8 area)
    {
        int aa = area.ToBits() >> 9;

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
    public static int AreaToAlphaEvenOdd(F24Dot8 area)
    {
        int aa = area.ToBits() >> 9;

        // Find absolute area value.
        int mask = aa >> 31;
        int aaabs = (aa + mask) ^ mask;

        int aac = aaabs & 511;

        if (aac > 256)
        {
            aac = 512 - aac;
        }

        return Math.Min(aac, 255);
    }


    /**
     * This function returns 1 if value is greater than zero and it is divisible
     * by 256 (equal to one in 24.8 format) without a reminder.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static F24Dot8 FindAdjustment(F24Dot8 value)
    {
        F24Dot8 e = F24Dot8.Epsilon;

        // Will be set to 0 is value is zero or less. Otherwise it will be 1.
        F24Dot8 lte0 = ~((value - e) >> 31) & e;

        // Will be set to 1 if value is divisible by 256 without a reminder.
        // Otherwise it will be 0.
        F24Dot8 db256 = (((value & F24Dot8.FromBits(255)) - e) >> 31) & e;

        // Return 1 if both bits (more than zero and disisible by 256) are set.
        return lte0 & db256;
    }
}