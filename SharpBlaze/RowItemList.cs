using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpBlaze;


public unsafe partial struct RowItemList<T>
    where T : unmanaged
{
    public RowItemList()
    {
    }


    public struct Block : IConstructible<Block, int>
    {
        [InlineArray(ItemsPerBlock)]
        public struct Array
        {
            private T _e0;
        }

        public static void Construct(ref Block instance, in int args)
        {
            instance = new Block();
        }

        public Block()
        {
        }

        public const int ItemsPerBlock = 32;

        public Array Items;
        public Block* Previous = null;
        public Block* Next = null;

        // Always start with one. Blocks never sit allocated, but without
        // items.
        public int Count = 1;
    };

    public Block* First = null;

    // While inserting, new items are added to last block.
    public Block* Last = null;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ThreadMemory memory, in T value)
    {
        if (Last == null)
        {
            // Adding first item.
            Block* b = memory.FrameNew<Block, int>(0);

            Debug.Assert(First == null);

            b->Items[0] = value;

            First = b;
            Last = b;
        }
        else
        {
            // Inserting n-th item.
            Block* current = Last;
            int count = current->Count;

            if (count < Block.ItemsPerBlock)
            {
                current->Items[count] = value;

                current->Count = count + 1;
            }
            else
            {
                Block* b = memory.FrameNew<Block, int>(0);

                b->Items[0] = value;

                // Insert to doubly-linked list.
                current->Next = b;
                b->Previous = current;

                Last = b;
            }
        }
    }
}
