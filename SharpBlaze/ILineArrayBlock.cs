namespace SharpBlaze;

public unsafe interface ILineArrayBlock<T> : ILineArray<T>
    where T : unmanaged, ILineArrayBlock<T>
{
    void* GetFrontBlock();
    int GetFrontBlockLineCount();
}
