
namespace SharpBlaze;

public struct F24Dot8Point 
{
    public F24Dot8 X;
    public F24Dot8 Y;

    public F24Dot8Point(F24Dot8 x, F24Dot8 y)
    {
        X = x;
        Y = y;
    }

    public static F24Dot8Point FloatPointToF24Dot8Point(FloatPoint p) 
    {
        return new F24Dot8Point(
            F24Dot8.DoubleToF24Dot8(p.X),
            F24Dot8.DoubleToF24Dot8(p.Y)
        );
    }


    public static F24Dot8Point FloatPointToF24Dot8Point(double x, double y)
    {
        return new F24Dot8Point(
            F24Dot8.DoubleToF24Dot8(x),
            F24Dot8.DoubleToF24Dot8(y)
        );
    }
}
