using System;
using System.Diagnostics;
using System.Threading;

namespace SharpBlaze;

using static Math;

public sealed partial class ParallelExecutor
{
    public static int GetHardwareThreadCount()
    {
        return Max(Environment.ProcessorCount, 1);
    }


    private void Run(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody)
    {
        Debug.Assert(loopBody != null);

        int count = toExclusive - fromInclusive;
        if (count < 1)
        {
            return;
        }

        if (count == 1)
        {
            loopBody.Invoke(fromInclusive, MainMemory);
            return;
        }

        mTaskData.Cursor = fromInclusive;
        mTaskData.End = toExclusive;
        mTaskData.Fn = loopBody;

        int threadCount = Min(mThreadData.Length, count);

        mTaskData.FinalizedWorkers.Reset(threadCount);

        // Wake all threads waiting on this condition variable.
        mTaskData.RequiredWorkerCount.Release(threadCount);

        if (allowInline)
        {
            do
            {
                // Only do inline work if we won't steal from the awoken threads;
                // this thread needs to be waiting on workers before they finish.
                int remTasks = mTaskData.End - mTaskData.Cursor;
                if (remTasks <= threadCount)
                {
                    break;
                }
            }
            while (WorkerStep(mTaskData, MainMemory));
        }

        mTaskData.FinalizedWorkers.Wait();

        // Cleanup.
        mTaskData.Cursor = 0;
        mTaskData.End = 0;
        mTaskData.Fn = null;
    }


    public override void ResetFrameMemory()
    {
        for (int i = 0; i < mThreadData.Length; i++)
        {
            mThreadData[i].Memory.ResetFrameMemory();
        }

        MainMemory.ResetFrameMemory();
    }


    private static void Worker(object? p)
    {
        Debug.Assert(p != null);

        //#ifndef __EMSCRIPTEN__
        //        pthread_set_qos_class_self_np(QOS_CLASS_USER_INTERACTIVE, 0);
        //#endif // __EMSCRIPTEN__

        (TaskList? items, ThreadMemory? memory) = (ThreadStart) p;

        // Loop forever waiting for next dispatch of tasks.
        while (true)
        {
            // Wait until required worker count becomes greater than zero.
            items.RequiredWorkerCount.Wait();

            while (WorkerStep(items, memory))
            {
            }

            items.FinalizedWorkers.Signal();
        }
    }

    private static bool WorkerStep(TaskList items, ThreadMemory memory)
    {
        int index = Interlocked.Increment(ref items.Cursor) - 1;
        if (index >= items.End)
        {
            return false;
        }

        items.Fn.Invoke(index, memory);
        return true;
    }
}
