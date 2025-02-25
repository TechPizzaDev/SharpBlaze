namespace SharpBlaze;

public class ViewData
{
    public Matrix CoordinateSystemMatrix;
    public FloatPoint Translation;
    public FloatPoint Scale = new(1);

    public void SetupCoordinateSystem(int width, int height, VectorImage image)
    {
        IntRect bounds = image.GetBounds();

        double x = (bounds.MaxX - bounds.MinX) / 2.0;
        double y = (bounds.MaxY - bounds.MinY) / 2.0;

        CoordinateSystemMatrix = Matrix.CreateTranslation(
            (width / 2.0) - x,
            (height / 2.0) - y);
    }

    public Matrix GetMatrix()
    {
        Matrix m = Matrix.CreateScale(Scale);

        m *= Matrix.CreateTranslation(Translation);

        m *= CoordinateSystemMatrix;

        return m;
    }
}
