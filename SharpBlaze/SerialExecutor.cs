using System;

namespace SharpBlaze;

public sealed class SerialExecutor : Executor
{
    public override ThreadMemory MainMemory { get; } = new();

    public override int ThreadCount => 1;

    public override void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody)
    {
        for (int i = fromInclusive; i < toExclusive; i++)
        {
            loopBody.Invoke(i, MainMemory);

            MainMemory.ResetTaskMemory();
        }
    }

    public override void ResetFrameMemory()
    {
        MainMemory.ResetFrameMemory();
    }
}
