using System;

namespace SharpBlaze;

public interface ISpanBlender
{
    void CompositeSpan(Span<uint> d, uint alpha);

    int ApplyFillRule(int value);
}
