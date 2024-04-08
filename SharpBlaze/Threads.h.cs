using System;
using System.Threading;

namespace SharpBlaze;

using static Math;

/**
 * Manages a pool of threads used for parallelization of rasterization tasks.
 */
public sealed unsafe partial class ParallelExecutor : Executor
{
    public ParallelExecutor(int threadCount)
    {
        mTaskData = new TaskList();

        mThreadData = new ThreadData[threadCount];

        for (int i = 0; i < mThreadData.Length; i++)
        {
            mThreadData[i] = new ThreadData(mTaskData);
        }
    }

    public ParallelExecutor() : this(Min(GetHardwareThreadCount(), 128))
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

    private class ThreadData
    {
        public ThreadData(TaskList tasks)
        {
            Tasks = tasks;
        }

        public ThreadMemory Memory = new();
        public TaskList Tasks;
        public Thread Thread;
    }

    private readonly TaskList mTaskData;
    private readonly ThreadData[] mThreadData;
    private bool hasStartedThreads;

    public override ThreadMemory MainMemory => mThreadData[0].Memory;

    public override int ThreadCount => mThreadData.Length;


    public override void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody)
    {
        int count = toExclusive - fromInclusive;
        if (count <= 0)
        {
            return;
        }

        int run = Max(Min(64, count / (mThreadData.Length * 32)), 1);

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
