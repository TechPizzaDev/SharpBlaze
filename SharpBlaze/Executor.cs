using System;

namespace SharpBlaze;

public abstract class Executor
{
    public abstract ThreadMemory MainMemory { get; }

    public abstract int WorkerCount { get; }

    public abstract void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody);

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
