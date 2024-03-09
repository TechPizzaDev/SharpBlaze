
namespace SharpBlaze;


/**
 * Bézier path command.
 */
public enum PathTag : byte
{
    Move = 0,
    Line,
    Quadratic,
    Cubic,
    Close
}