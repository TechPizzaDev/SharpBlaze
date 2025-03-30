using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpBlaze;

public unsafe partial class BumpAllocator
{
    public BumpAllocator()
    {
    }


    /**
     * Allocates given amount of bytes. Does not zero-fill allocated memory.
     *
     * @param size A number of bytes to allocate. Must be at least 1.
     */
    private partial void* Malloc(int size);


    /**
     * Resets this allocator to the initial state.
     */
    public partial void Free();

    /**
     * Represents a single block of raw memory in a linked list. One such
     * block manages relatively large amount of memory.
     */
    private struct Block
    {
        // Entire arena.
        public byte* Bytes;

        // Next block in all block list.
        public Block* Next;

        public int Position;
        public int BlockSize;
    }

    Block* mMasterActiveList = null;
    Block* mMasterFreeList = null;


    /**
     * Returns allocation size rounded up so that the next allocation from the
     * same block will be aligned oto 16 byte boundary.
     */
    private static int RoundUpAllocationSizeForNextAllocation(int size)
    {
        Debug.Assert(size > 0);

        int m = size + 15;

        return m & ~15;
    }


    private partial void* Malloc(int size)
    {
        Block* mal = mMasterActiveList;

        if (mal != null)
        {
            int remainingSize = mal->BlockSize - mal->Position;

            if (remainingSize >= size)
            {
                void* p = mal->Bytes + mal->Position;

                mal->Position += RoundUpAllocationSizeForNextAllocation(size);

                return p;
            }
        }

        return MallocFromNewBlock(size);
    }

    public BumpToken<T> Alloc<T>(int length)
        where T : unmanaged
    {
        void* ptr = Malloc(length * sizeof(T));
        return new((T*) ptr, length);
    }
    
    public BumpToken2D<T> Alloc2D<T>(int width, int height)
        where T : unmanaged
    {
        int size = height * sizeof(T*);
        void* ptr = Malloc(size);
        new Span<byte>(ptr, size).Clear();
        return new((T**) ptr, width, height);
    }
}