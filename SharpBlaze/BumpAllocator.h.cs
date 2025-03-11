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
     * Allocates memory for one element of type T. Does not zero-fill
     * allocated memory and does not call any constructors.
     */
    public partial T* Malloc<T>() where T : unmanaged;


    /**
     * Allocates memory for a given amount of pointers of type T. Does not
     * zero-fill allocated memory. Note that this method does not allocate
     * any objects, only arrays of pointers.
     *
     * @param count A number of pointers to allocate. Must be at least 1.
     */
    public partial T** MallocPointers<T>(int count) where T : unmanaged;


    /**
     * Allocates memory for a given amount of pointers of type T and fills the
     * entire block of allocated memory with zeroes. Note that this method
     * does not allocate any objects, only arrays of pointers.
     *
     * @param count A number of pointers to allocate. Must be at least 1.
     */
    public partial T** MallocPointersZeroFill<T>(int count) where T : unmanaged;


    /**
     * Allocates memory for an array of values of type T. Does not zero-fill
     * allocated memory and does not call any constructors.
     *
     * @param count A number of pointers to allocate. Must be at least 1.
     */
    public partial T* MallocArray<T>(int count) where T : unmanaged;


    /**
     * Allocates memory for an array of values of type T. Fills allocated
     * memory with zeroes, but does not call any constructors.
     *
     * @param count A number of pointers to allocate. Must be at least 1.
     */
    public partial T* MallocArrayZeroFill<T>(int count) where T : unmanaged;


    /**
     * Allocates given amount of bytes. Does not zero-fill allocated memory.
     *
     * @param size A number of bytes to allocate. Must be at least 1.
     */
    public partial void* Malloc(int size);


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



    public partial T* Malloc<T>() where T : unmanaged
    {
        return (T*) (Malloc(sizeof(T)));
    }


    public partial T** MallocPointers<T>(int count) where T : unmanaged
    {
        Debug.Assert(count > 0);

        return (T**) (Malloc(sizeof(T*) * count));
    }


    public partial T** MallocPointersZeroFill<T>(int count) where T : unmanaged
    {
        Debug.Assert(count > 0);

        int b = sizeof(T*) * count;

        T** p = (T**) (Malloc(b));

        NativeMemory.Clear(p, (nuint) b);

        return p;
    }


    public partial T* MallocArray<T>(int count) where T : unmanaged
    {
        Debug.Assert(count > 0);

        return (T*) (Malloc(sizeof(T) * count));
    }


    public partial T* MallocArrayZeroFill<T>(int count) where T : unmanaged
    {
        Debug.Assert(count > 0);

        int b = sizeof(T) * count;

        T* p = (T*) (Malloc(b));

        NativeMemory.Clear(p, (nuint) b);

        return p;
    }


    public partial void* Malloc(int size)
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

    public Span<T> Alloc<T>(int length)
        where T : unmanaged
    {
        void* ptr = Malloc(length * sizeof(T));
        return new(ptr, length);
    }
}