
namespace SharpBlaze;


public unsafe partial struct Rasterizer<T>
    where T : unmanaged, ITileDescriptor
{
    /**
     * Rasterize image.
     *
     * @param geometries Pointer to geometries to rasterize. Must not be nullptr.
     *
     * @param geometryCount A number of geometries in geometry array. Must be at
     * least 1.
     *
     * @param matrix Transformation matrix. All geometries will be pre-transformed
     * by this matrix when rasterizing.
     *
     * @param threads Threads to use.
     *
     * @param image Destination image. 
     */
    static void Rasterize(Geometry* geometries,
        int geometryCount, in Matrix matrix, in Threads threads,
        in ImageData image)
    {
        Rasterize(geometries, geometryCount, matrix, threads,
            image);
    }

}