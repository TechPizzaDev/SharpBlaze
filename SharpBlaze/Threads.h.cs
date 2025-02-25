using System;
using System.Threading;

namespace SharpBlaze;

using static Math;

/**
 * Manages a pool of threads used for parallelization of rasterization tasks.
 */
public sealed partial class ParallelExecutor : Executor
{
    public ParallelExecutor(int workerCount, bool allowInline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 0);

        mTaskData = new TaskList();

        mThreadData = new ThreadData[workerCount];

        this.allowInline = allowInline;

        for (int i = 0; i < mThreadData.Length; i++)
        {
            ThreadMemory memory = new();
            Thread thread = new(Worker);
            thread.IsBackground = true;
            thread.Start(new ThreadStart(mTaskData, memory));

            mThreadData[i] = new ThreadData(thread, memory);
        }
    }

    public ParallelExecutor() : this(GetHardwareThreadCount() - 1, true)
    {
    }


    private class TaskList
    {
        public int Cursor = 0;
        public int End = 0;
        public Action<int, ThreadMemory>? Fn = null;

        public readonly SemaphoreSlim RequiredWorkerCount = new(0);
        public readonly CountdownEvent FinalizedWorkers = new(0);
    }

    private record ThreadStart(TaskList Tasks, ThreadMemory Memory);

    private record struct ThreadData(Thread Thread, ThreadMemory Memory);

    private readonly TaskList mTaskData;
    private readonly ThreadData[] mThreadData;
    private readonly bool allowInline;

    public override ThreadMemory MainMemory { get; } = new();

    public override int WorkerCount => mThreadData.Length;


    public override void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody)
    {
        int count = toExclusive - fromInclusive;
        int threadCount = mThreadData.Length;

        if (threadCount == 0)
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
