using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpBlaze;

public unsafe partial class BumpAllocator
{
    const int kMinimumMasterBlockSize = 1024 * 128;

    /**
     * Returns block allocation size aligned to 32 kilobyte boundary.
     */
    static int RoundUpBlockSize(int size)
    {
        Debug.Assert(size > 0);

        int m = size + 32767;

        return m & ~32767;
    }


    ~BumpAllocator()
    {
        FreeBlockChain(mMasterActiveList);
        FreeBlockChain(mMasterFreeList);
    }


    private partial void FreeBlockChain(Block* block)
    {
        while (block != null)
        {
            Block* next = block->Next;

            NativeMemory.Free(block->Bytes);
            NativeMemory.Free(block);

            block = next;
        }
    }


    private partial void* MallocFromNewBlock(int size)
    {
        Debug.Assert(size > 0);

        ref Block* ptr = ref mMasterFreeList;

        while (ptr != null)
        {
            Block* b = ptr;

            Debug.Assert(b->Position == 0);

            if (b->BlockSize >= size)
            {
                ptr = b->Next;

                // Block is large enough. Remove from free list and insert to
                // active block list.
                void* p = b->Bytes;

                b->Position = RoundUpAllocationSizeForNextAllocation(size);
                b->Next = mMasterActiveList;

                mMasterActiveList = b;

                return p;
            }

            ptr = ref b->Next;
        }

        // A new block is needed.
        Block* block = (Block*) (NativeMemory.Alloc((nuint) sizeof(Block)));

        block->BlockSize = Math.Max(kMinimumMasterBlockSize,
            RoundUpBlockSize(size));

        block->Bytes = (byte*) (NativeMemory.Alloc((nuint) block->BlockSize));

        Debug.Assert(block->Bytes != null);

        // Assign position to allocation size because we will return base pointer
        // later without adjusting current position.

        block->Position = RoundUpAllocationSizeForNextAllocation(size);

        // Insert to main list.
        block->Next = mMasterActiveList;

        mMasterActiveList = block;

        return block->Bytes;
    }


    public partial void Free()
    {
        Block* b = mMasterActiveList;

        while (b != null)
        {
            Block* next = b->Next;

            b->Next = mMasterFreeList;
            b->Position = 0;

            mMasterFreeList = b;

            b = next;
        }

        mMasterActiveList = null;
    }

}