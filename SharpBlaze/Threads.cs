using System;
using System.Diagnostics;
using System.Threading;

namespace SharpBlaze;

using static Math;

public sealed unsafe partial class ParallelExecutor
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

        if (!hasStartedThreads)
        {
            RunThreads();
        }

        mTaskData.Cursor = fromInclusive;
        mTaskData.End = toExclusive;
        mTaskData.Fn = loopBody;

        int threadCount = Min(mThreadData.Length, count);

        mTaskData.RequiredWorkerCount = threadCount;
        mTaskData.FinalizedWorkers = 0;

        Monitor.Enter(mTaskData.FinalizationMutex);

        // Wake all threads waiting on this condition variable.
        lock (mTaskData.CV)
        {
            Monitor.PulseAll(mTaskData.CV);
        }

        while (mTaskData.FinalizedWorkers < threadCount)
        {
            lock (mTaskData.FinalizationCV)
            {
                Monitor.Wait(mTaskData.FinalizationCV);
            }
        }

        Monitor.Exit(mTaskData.FinalizationMutex);

        // Cleanup.
        mTaskData.Cursor = 0;
        mTaskData.End = 0;
        mTaskData.Fn = null;

        mTaskData.RequiredWorkerCount = 0;
        mTaskData.FinalizedWorkers = 0;
    }


    public override void ResetFrameMemory()
    {
        for (int i = 0; i < mThreadData.Length; i++)
        {
            mThreadData[i].Memory.ResetFrameMemory();
        }
    }


    private void RunThreads()
    {
        for (int i = 0; i < mThreadData.Length; i++)
        {
            ThreadData d = mThreadData[i];

            d.Thread = new Thread(Worker);
            d.Thread.IsBackground = true;
            d.Thread.Start(d);
        }

        hasStartedThreads = true;
    }


    private static void Worker(object? p)
    {
        Debug.Assert(p != null);

        //#ifndef __EMSCRIPTEN__
        //        pthread_set_qos_class_self_np(QOS_CLASS_USER_INTERACTIVE, 0);
        //#endif // __EMSCRIPTEN__

        ThreadData d = (ThreadData) (p);

        // Loop forever waiting for next dispatch of tasks.
        for (; ; )
        {
            TaskList items = d.Tasks;

            Monitor.Enter(items.Mutex);

            while (items.RequiredWorkerCount < 1)
            {
                // Wait until required worker count becomes greater than zero.
                lock (items.CV)
                {
                    Monitor.Wait(items.CV);
                }
            }

            items.RequiredWorkerCount--;

            Monitor.Exit(items.Mutex);

            int end = items.End;

            while (true)
            {
                int index = Interlocked.Increment(ref items.Cursor) - 1;
                if (index >= end)
                {
                    break;
                }

                items.Fn.Invoke(index, d.Memory);
            }

            Monitor.Enter(items.FinalizationMutex);

            items.FinalizedWorkers++;

            Monitor.Exit(items.FinalizationMutex);

            lock (items.FinalizationCV)
            {
                Monitor.Pulse(items.FinalizationCV);
            }
        }
    }

}