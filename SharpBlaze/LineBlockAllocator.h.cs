using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharpBlaze;

public unsafe partial class LineBlockAllocator
{
    public LineBlockAllocator()
    {
    }


    /**
     * Returns new tiled line array block. Returned memory is not zero-filled.
     */
    public partial LineArrayTiledBlock* NewTiledBlock(LineArrayTiledBlock* next);


    /**
     * Returns new narrow line array block. Returned memory is not
     * zero-filled.
     */
    public partial LineArrayX16Y16Block* NewX16Y16Block(LineArrayX16Y16Block* next);


    /**
     * Returns new wide line array block. Returned memory is not zero-filled.
     */
    public partial LineArrayX32Y16Block* NewX32Y16Block(LineArrayX32Y16Block* next);


    /**
     * Resets this allocator to initial state. Should be called
     * after frame ends.
     */
    public partial void Clear();

    // If these get bigger, there is probably too much wasted memory for most
    // input paths.
    static LineBlockAllocator()
    {
        Debug.Assert(sizeof(LineArrayTiledBlock) <= 1024);
        Debug.Assert(sizeof(LineArrayX16Y16Block) <= 1024);
        Debug.Assert(sizeof(LineArrayX32Y16Block) <= 1024);

        Debug.Assert(sizeof(Arena) == Arena.Size);
        Debug.Assert(sizeof(ArenaLinks) == (sizeof(void*) * 2));
    }

    // Points to the current arena.
    private byte* mCurrent = null;
    private byte* mEnd = null;

    [StructLayout(LayoutKind.Explicit)]
    private struct Arena
    {
        // Each arena is 32 kilobytes.
        public const int Size = 1024 * 32;

        [FieldOffset(0)]
        public fixed byte Memory[Size];

        [FieldOffset(0)]
        public ArenaLinks Links;
    }

    private struct ArenaLinks
    {
        // Points to the next item in free list.
        public Arena* NextFree;

        // Points to the next item in all block list.
        public Arena* NextAll;
    }

    private Arena* mAllArenas = null;
    private Arena* mFreeArenas = null;


    public partial LineArrayTiledBlock* NewTiledBlock(LineArrayTiledBlock* next)
    {
        var block = MallocBlock<LineArrayTiledBlock>();
        *block = new(next);
        return block;
    }


    public partial LineArrayX16Y16Block* NewX16Y16Block(LineArrayX16Y16Block* next)
    {
        var block = MallocBlock<LineArrayX16Y16Block>();
        *block = new(next);
        return block;
    }


    public partial LineArrayX32Y16Block* NewX32Y16Block(LineArrayX32Y16Block* next)
    {
        var block = MallocBlock<LineArrayX32Y16Block>();
        *block = new(next);
        return block;
    }


    private T* MallocBlock<T>() where T : unmanaged
    {
        byte* current = mCurrent;

        if (current < mEnd)
        {
            T* b = (T*) (current);

            mCurrent = (byte*) (b + 1);

            return b;
        }

        return MallocBlockFromNewArena<T>();
    }

    private T* MallocBlockFromNewArena<T>() where T : unmanaged
    {
        NewArena();

        T* b = (T*) (mCurrent);

        mCurrent = (byte*) (b + 1);

        return b;
    }
}
