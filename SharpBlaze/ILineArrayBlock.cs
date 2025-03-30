
namespace SharpBlaze;

public interface ILineArrayBlock<T> : ILineArray<T>
    where T : unmanaged, ILineArrayBlock<T>
{
    static abstract int BlockSize { get; }
    
    BumpToken<byte> GetFrontBlock();
    
    int GetFrontBlockLineCount();
}
