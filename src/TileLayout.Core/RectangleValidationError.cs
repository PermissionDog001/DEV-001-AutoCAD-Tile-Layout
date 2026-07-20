namespace TileLayout.Core
{
    public enum RectangleValidationError
    {
        None = 0,
        IncorrectLineCount,
        NonFiniteCoordinate,
        NonCoplanar,
        DegenerateLine,
        NonAxisAlignedLine,
        NonPositiveDimensions,
        NonClosedBoundary,
        DuplicateOrMissingSide
    }
}
