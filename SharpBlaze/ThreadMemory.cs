
namespace SharpBlaze;

/**
 * Maintains per-thread memory.
 *
 * Each thread gets a separate instance of thread memory. There
 * are two types of per-thread memory. They are different in
 * terms of how long the allocations stay valid.
 *
 * Frame memory.
 *
 * Frame memory is allocated any time during frame and
 * released after frame ends. Once something is allocated from
 * frame memory, this allocation stays valid until frame is
 * complete.
 *
 *
 * Task memory.
 *
 * Task memory is allocated during a single task and released
 * once task ends. Allocations made from task memory become
 * invalid once thread finishes executing a current task and
 * another task picked up by the same thread will be able to
 * make allocations from the same memory region, potentially
 * overwriting contents written by previous task. Task memory
 * is suited for temporary objects that are required during
 * execution of a task and can be discarded once it finishes.
 *
 * All methods in this class indicate which memory type they
 * operate on.
 *
 * The system automatically releases both frame and task
 * memory at appropriate times.
 */
public unsafe partial class ThreadMemory
{

    public ThreadMemory()
    {
    }


    /**
     * Returns new tiled line array block. Returned memory is not zero-filled.
     *
     * Line blocks are always allocated from frame memory.
     */
    public partial LineArrayTiledBlock* FrameNewTiledBlock(LineArrayTiledBlock* next);


    /**
     * Returns new narrow line array block. Returned memory is not
     * zero-filled.
     *
     * Line blocks are always allocated from frame memory.
     */
    public partial LineArrayX16Y16Block* FrameNewX16Y16Block(LineArrayX16Y16Block* next);


    /**
     * Returns new wide line array block. Returned memory is
     * not zero-filled.
     *
     * Line blocks are always allocated from frame memory.
     */
    public partial LineArrayX32Y16Block* FrameNewX32Y16Block(LineArrayX32Y16Block* next);


    /**
     * Resets frame memory. All allocations made during frame
     * by thread this memory belongs to will become invalid once
     * this method returns.
     */
    public partial void ResetFrameMemory();


    /**
     * Resets task memory. All allocations made during execution
     * of a single task by a thread owning this memory will
     * become invalid once this method returns.
     *
     * This method is automatically called by Threads class
     * which manages execution of tasks, but task can call it as
     * well.
     */
    public partial void ResetTaskMemory();

    private LineBlockAllocator mFrameLineBlockAllocator = new();
    private BumpAllocator mFrameAllocator = new();
    private BumpAllocator mTaskAllocator = new();

    public BumpAllocator Frame => mFrameAllocator;
    public BumpAllocator Task => mTaskAllocator;


    public partial LineArrayTiledBlock* FrameNewTiledBlock(LineArrayTiledBlock* next)
    {
        return mFrameLineBlockAllocator.NewTiledBlock(next);
    }


    public partial LineArrayX16Y16Block* FrameNewX16Y16Block(LineArrayX16Y16Block* next)
    {
        return mFrameLineBlockAllocator.NewX16Y16Block(next);
    }


    public partial LineArrayX32Y16Block* FrameNewX32Y16Block(LineArrayX32Y16Block* next)
    {
        return mFrameLineBlockAllocator.NewX32Y16Block(next);
    }


    public partial void ResetFrameMemory()
    {
        mFrameLineBlockAllocator.Clear();
        mFrameAllocator.Free();
    }


    public partial void ResetTaskMemory()
    {
        mTaskAllocator.Free();
    }

}