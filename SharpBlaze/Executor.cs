using System;

namespace SharpBlaze;

public abstract class Executor
{
    public abstract ThreadMemory MainMemory { get; }

    public abstract int ThreadCount { get; }

    public abstract void For(int fromInclusive, int toExclusive, Action<int, ThreadMemory> loopBody);

    public abstract void ResetFrameMemory();
}
