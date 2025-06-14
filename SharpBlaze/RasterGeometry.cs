using System;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

internal unsafe readonly struct RasterizableGeometry
{
    public RasterizableGeometry(
        int geometry,
        TileBounds bounds,
        BumpToken2D<byte> lines,
        BumpToken<int> firstBlockLineCounts,
        BumpToken2D<F24Dot8> startCoverTable)
    {
        Geometry = geometry;
        Bounds = bounds;
        Lines = lines;
        FirstBlockLineCounts = firstBlockLineCounts;
        StartCoverTable = startCoverTable;
    }

    public readonly int Geometry;
    public readonly TileBounds Bounds;
    public readonly BumpToken2D<byte> Lines;
    public readonly BumpToken<int> FirstBlockLineCounts;
    public readonly BumpToken2D<F24Dot8> StartCoverTable;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasLinesForRow(int rowIndex)
    {
        return Lines[rowIndex].HasValue;
    }
        

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* GetLinesForRow<T>(int rowIndex)
    {
        BumpToken<byte> token = Lines[rowIndex];
        if (token.HasValue && sizeof(T) > token.Length)
        {
            ThrowHelper.ThrowInvalidOperation();
        }
        return (T*) token.GetPointer();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetFirstBlockLineCountForRow(int rowIndex)
    {
        return FirstBlockLineCounts.AsSpan()[rowIndex];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<F24Dot8> GetCoversForRow(int rowIndex)
    {
        if (!StartCoverTable.HasValue)
        {
            // No table at all.
            return default;
        }

        return StartCoverTable[rowIndex];
    }
}
