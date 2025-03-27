using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SharpBlaze;

public unsafe struct RowItemList<T>
    where T : unmanaged
{
    public RowItemList()
    {
    }


    public struct Block
    {
        [InlineArray(ItemsPerBlock)]
        public struct Array
        {
            private T _e0;
        }

        public Block()
        {
        }

        public const int ItemsPerBlock = 32;

        public Array Items;

        public Block* Next = null;

        // Always start with one. Blocks never sit allocated, but without
        // items.
        public int Count = 1;

        [UnscopedRef]
        public Span<T> AsSpan()
        {
            return Items[..Count];
        }
    };

    public Block* First = null;

    // While inserting, new items are added to last block.
    public Block* Last = null;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ThreadMemory memory, in T value)
    {
        if (Last != null)
        {
            // Inserting n-th item.
            Block* current = Last;
            int count = current->Count;

            if (count < Block.ItemsPerBlock)
            {
                current->Items[count] = value;

                current->Count = count + 1;
                return;
            }
        }
        AppendSlow(memory, value);
    }

    private void AppendSlow(ThreadMemory memory, in T value)
    {
        Block* b = memory.FrameMalloc<Block>();
        *b = new Block();

        b->Items[0] = value;

        if (Last == null)
        {
            // Adding first item.
            Debug.Assert(First == null);

            First = b;
        }
        else
        {
            // Insert to linked list.
            Last->Next = b;
        }
        Last = b;
    }
}
