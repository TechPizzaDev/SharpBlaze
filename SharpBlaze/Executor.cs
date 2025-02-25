using System;

namespace SharpBlaze;

public abstract class Executor
{
    public unsafe delegate void LoopBody(int index, void* state, ThreadMemory memory);
    
    public abstract ThreadMemory MainMemory { get; }

    public abstract int WorkerCount { get; }

    public unsafe abstract void For(int fromInclusive, int toExclusive, void* state, LoopBody loopBody);

    public abstract void ResetFrameMemory();

    public static Executor CreateOptimalExecutor()
    {
        // Subtract one since ParallelExecutor can execute work on the calling thread.
        int workerCount = ParallelExecutor.GetHardwareThreadCount() - 1;
        if (workerCount == 0)
        {
            return new SerialExecutor();
        }
        return new ParallelExecutor(workerCount, true);
    }
}
