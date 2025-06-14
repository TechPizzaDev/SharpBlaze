using System;

namespace SharpBlaze;

public interface ISpanBlender<TColor, TAlpha>
{
    void CompositeSpan(Span<TColor> d, TAlpha alpha);

    /// <summary>
    /// Maps the coverage area of a pixel into an alpha value. 
    /// </summary>
    /// <param name="area">
    /// The signed coverage area in 24.8 fixed-point format.
    /// Value starts with no coverage at 0,
    /// reaching full coverage after ±131071.
    /// 
    /// Positive value is for lines that go up,
    /// negative value is for lines that go down.
    /// </param>
    /// <remarks>
    /// The full coverage threshold is (2.0*256*256),
    /// derived from the fixed-point sub-pixel grid.
    /// </remarks>
    /// <returns>
    /// The alpha value calculated from the area.
    /// </returns>
    TAlpha ApplyFillRule(F24Dot8 area);
}
