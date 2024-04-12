using System;

namespace SharpBlaze;

public sealed class SerialExecutor : Executor
{
    public override ThreadMemory MainMemory { get; } = new();

    public override int WorkerCount => 0;

    public override void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody)
    {
        SerialFor(fromInclusive, toExclusive, MainMemory, loopBody);
    }

    internal static void SerialFor(int fromInclusive, int toExclusive, ThreadMemory memory, Action<int, ThreadMemory> loopBody)
    {
        for (int i = fromInclusive; i < toExclusive; i++)
        {
            loopBody.Invoke(i, memory);

            memory.ResetTaskMemory();
        }
    }

    public override void ResetFrameMemory()
    {
        MainMemory.ResetFrameMemory();
    }
}
