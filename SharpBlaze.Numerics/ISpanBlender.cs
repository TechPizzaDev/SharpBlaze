using System;

namespace SharpBlaze;

public interface ISpanBlender<TColor, TAlpha>
{
    void CompositeSpan(Span<TColor> d, TAlpha alpha);

    TAlpha ApplyFillRule(int value);
}
