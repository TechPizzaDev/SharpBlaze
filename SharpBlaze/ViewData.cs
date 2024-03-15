namespace SharpBlaze;

public class ViewData
{
    public Matrix CoordinateSystemMatrix;
    public FloatPoint Translation;
    public double Scale = 1;

    public void SetupCoordinateSystem(int width, int height, VectorImage image)
    {
        IntRect bounds = image.GetBounds();

        Translation.X = -(bounds.MaxX - bounds.MinX) / 2.0;
        Translation.Y = -(bounds.MaxY - bounds.MinY) / 2.0;

        CoordinateSystemMatrix = Matrix.CreateTranslation(
            (width / 2.0),
            (height / 2.0));
    }

    public Matrix GetMatrix()
    {
        Matrix m = Matrix.CreateScale(Scale, Scale);

        m *= CoordinateSystemMatrix;

        m = Matrix.CreateTranslation(Translation) * m;

        return m;
    }
}
