
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SharpBlaze;

using static Utils;

/**
 * Manages a pool of threads used for parallelization of rasterization tasks.
 */
public unsafe partial class Threads
{
    public Threads()
    {
    }


    private class TaskList
    {
        public int Cursor = 0;
        public int Count = 0;
        public Action<int, ThreadMemory>? Fn = null;

        public object CV = new();
        public object Mutex = new();
        public int RequiredWorkerCount = 0;

        public object FinalizationCV => FinalizationMutex;
        public object FinalizationMutex = new();
        public int FinalizedWorkers = 0;
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

    private TaskList? mTaskData = null;
    private ThreadData[]? mThreadData = null;
    private int mThreadCount = 0;
    private ThreadMemory mMainMemory = new();


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ParallelFor(int count, Action<int, ThreadMemory> loopBody)
    {
        RunThreads();

        int run = Max(Min(64, count / (mThreadCount * 32)), 1);

        if (run == 1)
        {
            void p(int index, ThreadMemory memory)
            {
                loopBody.Invoke(index, memory);

                memory.ResetTaskMemory();
            }

            Run(count, p);
        }
        else
        {
            int iterationCount = (count / run) + Min(count % run, 1);

            void p(int index, ThreadMemory memory)
            {
                int idx = run * index;
                int maxidx = Min(count, idx + run);

                for (int i = idx; i < maxidx; i++)
                {
                    loopBody.Invoke(i, memory);

                    memory.ResetTaskMemory();
                }
            }

            Run(iterationCount, p);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void* MallocMain(int size)
    {
        return mMainMemory.FrameMalloc(size);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* MallocMain<T>() where T : unmanaged
    {
        return mMainMemory.FrameMalloc<T>();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T* NewMain<T, TArgs>(in TArgs args) where T : unmanaged, IConstructible<T, TArgs>
    {
        T* instance = MallocMain<T>();
        T.Construct(ref *instance, args);
        return instance;
    }

}