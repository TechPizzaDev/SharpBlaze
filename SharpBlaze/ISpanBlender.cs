namespace SharpBlaze;

public unsafe interface ISpanBlender
{
    void CompositeSpan(int pos, int end, uint* d, uint alpha);
}
