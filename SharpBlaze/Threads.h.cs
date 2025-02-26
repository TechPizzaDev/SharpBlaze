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


    private unsafe class TaskList
    {
        public int Cursor = 0;
        public int End = 0;
        public LoopBody? Fn = null;
        public void* State;

        public readonly SemaphoreSlim RequiredWorkerCount = new(0);
        public readonly CountdownEvent FinalizedWorkers = new(0);
    }

    private record ThreadStart(TaskList Tasks, ThreadMemory Memory);

    private record struct ThreadData(Thread Thread, ThreadMemory Memory);

    private readonly TaskList mTaskData;
    private readonly ThreadData[] mThreadData;
    private readonly bool allowInline;

    public override ThreadMemory MainMemory { get; } = new();

    public override int WorkerCount => mThreadData.Length + (allowInline ? 1 : 0);

    private unsafe struct RunState(void* state, LoopBody loopBody)
    {
        public required int Run;
        public required int FromInclusive;
        public required int ToExclusive;

        public void Invoke(int index, ThreadMemory memory)
        {
            loopBody.Invoke(index, state, memory);
            
            memory.ResetTaskMemory();
        }
    }

    public unsafe override void For(int fromInclusive, int toExclusive, void* state, LoopBody loopBody)
    {
        int count = toExclusive - fromInclusive;
        int threadCount = WorkerCount;

        int run = Max(Min(64, count / (threadCount * 32)), 1);
        
        RunState runState = new(state, loopBody)
        {
            Run = run,
            FromInclusive = fromInclusive,
            ToExclusive = toExclusive,
        };

        if (run == 1)
        {
            static void RunBody(int index, void* state, ThreadMemory memory)
            {
                RunState* s = (RunState*) state;
                
                s->Invoke(index, memory);
            }

            Run(fromInclusive, toExclusive, &runState, RunBody);
        }
        else
        {
            int iterationCount = (count / run) + Min(count % run, 1);

            static void RunBody(int index, void* state, ThreadMemory memory)
            {
                RunState* s = (RunState*) state;
                
                int start = s->FromInclusive + s->Run * index;
                int end = Min(s->ToExclusive, start + s->Run);

                for (int i = start; i < end; i++)
                {
                    s->Invoke(i, memory);
                }
            }
            
            Run(0, iterationCount, &runState, RunBody);
        }
    }
}
