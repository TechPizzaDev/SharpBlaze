
namespace SharpBlaze;

public unsafe sealed class SerialExecutor : Executor
{
    public override ThreadMemory MainMemory { get; } = new();

    public override int WorkerCount => 0;

    public override void For(int fromInclusive, int toExclusive, void* state, LoopBody loopBody)
    {
        SerialFor(fromInclusive, toExclusive, state, MainMemory, loopBody);
    }

    internal static void SerialFor(int fromInclusive, int toExclusive, void* state, ThreadMemory memory, LoopBody loopBody)
    {
        for (int i = fromInclusive; i < toExclusive; i++)
        {
            loopBody.Invoke(i, state, memory);

            memory.ResetTaskMemory();
        }
    }

    public override void ResetFrameMemory()
    {
        MainMemory.ResetFrameMemory();
    }
}
