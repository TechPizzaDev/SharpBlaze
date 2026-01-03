using System;

namespace SharpBlaze;

public readonly struct SpanBlender : ISpanBlender<byte>
{
    public SpanBlender(in ImageData image, uint color, FillRule fillRule)
    {
        Image = image;
        Color = color;
        FillRule = fillRule;
    }

    readonly ImageData Image;
    readonly uint Color = 0;
    readonly FillRule FillRule;


    public void CompositeSpan(int x0, int x1, int y, byte alpha)
    {
        Span<uint> d = Image.GetSpan2D<uint>()[y][x0..x1];

        if (Color >= 0xff000000 && alpha == 255)
        {
            // Solid span, write only.
            d.Fill(Color);
        }
        else
        {
            // Transparent span.
            CompositionOps.CompositeSpanSourceOver(d, alpha, Color);
        }
    }

    public byte ApplyFillRule(F24Dot8 area)
    {
        if (FillRule == FillRule.EvenOdd)
        {
            return (byte) RasterizerUtils.AreaToAlphaEvenOdd(area);
        }
        return (byte) RasterizerUtils.AreaToAlphaNonZero(area);
    }

    public void Dispose()
    {
    }
}
