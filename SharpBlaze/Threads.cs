using System;
using System.Diagnostics;
using System.Threading;

namespace SharpBlaze;

using static Utils;

public unsafe partial class Threads
{

    public static int GetHardwareThreadCount()
    {
        return Max(Environment.ProcessorCount, 1);
    }


    private void Run(int count, Action<int, ThreadMemory> loopBody)
    {
        Debug.Assert(loopBody != null);

        if (count < 1)
        {
            return;
        }

        if (count == 1)
        {
            loopBody.Invoke(0, mMainMemory);
            return;
        }

        mTaskData.Cursor = 0;
        mTaskData.Count = count;
        mTaskData.Fn = loopBody;

        int threadCount = Min(mThreadCount, count);

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
        mTaskData.Count = 0;

        mTaskData.Fn = null;

        mTaskData.RequiredWorkerCount = 0;
        mTaskData.FinalizedWorkers = 0;
    }


    public void ResetFrameMemory()
    {
        for (int i = 0; i < mThreadCount; i++)
        {
            mThreadData[i].Memory.ResetFrameMemory();
        }

        mMainMemory.ResetFrameMemory();
    }


    private void RunThreads()
    {
        if (mThreadData != null)
        {
            return;
        }

        mTaskData = new TaskList();

        int cpuCount = Min(GetHardwareThreadCount(), 128);

        mThreadCount = cpuCount;

        mThreadData = new ThreadData[cpuCount];

        for (int i = 0; i < cpuCount; i++)
        {
            mThreadData[i] = new ThreadData(mTaskData);
        }

        for (int i = 0; i < cpuCount; i++)
        {
            ThreadData d = mThreadData[i];

            d.Thread = new(Worker);
            d.Thread.Start(d);
        }
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

            int count = items.Count;

            for (; ; )
            {
                int index = Interlocked.Increment(ref items.Cursor) - 1;

                if (index >= count)
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