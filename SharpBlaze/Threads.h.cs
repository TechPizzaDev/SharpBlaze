using System;
using System.Threading;

namespace SharpBlaze;

using static Math;

/**
 * Manages a pool of threads used for parallelization of rasterization tasks.
 */
public sealed unsafe partial class ParallelExecutor : Executor
{
    // Big tasks are usually split up by the library into smaller sub-tasks,
    // so the cost of waking worker threads can become big overhead.
    // There are cases where large geometries likely take more time than
    // wake-up so keep the threshold for inline work low.
    private const int InlineThresholdFactor = 4;

    public ParallelExecutor(int workerCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 0);

        mTaskData = new TaskList();

        mThreadData = new ThreadData[workerCount];

        for (int i = 0; i < mThreadData.Length; i++)
        {
            mThreadData[i] = new ThreadData(mTaskData);
        }
    }

    public ParallelExecutor() : this(GetHardwareThreadCount() - 1)
    {
    }



    private class TaskList
    {
        public int Cursor = 0;
        public int End = 0;
        public Action<int, ThreadMemory>? Fn = null;

        public readonly object Mutex = new();
        public volatile int RequiredWorkerCount = 0;

        public readonly object FinalizationMutex = new();
        public volatile int FinalizedWorkers = 0;
    };

    private class ThreadData(TaskList tasks)
    {
        public readonly ThreadMemory Memory = new();
        public readonly TaskList Tasks = tasks;
        public Thread? Thread;
    }

    private readonly TaskList mTaskData;
    private readonly ThreadData[] mThreadData;
    private bool hasStartedThreads;

    public override ThreadMemory MainMemory { get; } = new();

    public override int WorkerCount => mThreadData.Length;


    public override void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody)
    {
        int count = toExclusive - fromInclusive;
        int threadCount = mThreadData.Length;

        if (threadCount == 0 || count <= threadCount * InlineThresholdFactor)
        {
            SerialExecutor.SerialFor(fromInclusive, toExclusive, MainMemory, loopBody);
            return;
        }

        int run = Max(Min(64, count / (threadCount * 32)), 1);

        if (run == 1)
        {
            void p(int index, ThreadMemory memory)
            {
                loopBody.Invoke(index, memory);

                memory.ResetTaskMemory();
            }

            Run(fromInclusive, toExclusive, p);
        }
        else
        {
            int iterationCount = (count / run) + Min(count % run, 1);

            void p(int index, ThreadMemory memory)
            {
                int idx = fromInclusive + run * index;
                int maxidx = Min(toExclusive, idx + run);

                for (int i = idx; i < maxidx; i++)
                {
                    loopBody.Invoke(i, memory);

                    memory.ResetTaskMemory();
                }
            }

            Run(0, iterationCount, p);
        }
    }
}
