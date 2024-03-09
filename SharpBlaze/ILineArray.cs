namespace SharpBlaze;

public unsafe interface ILineArray<T>
    where T : unmanaged, ILineArray<T>
{
    static abstract void Construct(ref T placement,
        TileIndex rowCount, TileIndex columnCount,
        ThreadMemory memory);

    void AppendVerticalLine(ThreadMemory memory, F24Dot8 x, F24Dot8 y0, F24Dot8 y1);
    void AppendLineDownR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    void AppendLineUpR_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    void AppendLineDownL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    void AppendLineUpL_V(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    void AppendLineDownRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
    void AppendLineUpRL(ThreadMemory memory, F24Dot8 x0, F24Dot8 y0, F24Dot8 x1, F24Dot8 y1);
}
