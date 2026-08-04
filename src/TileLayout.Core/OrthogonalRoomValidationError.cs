namespace TileLayout.Core
{
    public enum OrthogonalRoomValidationError
    {
        None = 0,
        IncorrectLineCount,
        NonFiniteCoordinate,
        NonCoplanar,
        DegenerateLine,
        NonAxisAlignedLine,
        AmbiguousToleranceCluster,
        DuplicateOrOverlappingLine,
        IntersectingOrTouchingBoundary,
        InvalidVertexDegree,
        MultipleDisconnectedLoops,
        NonPositiveArea,
        InvalidFinishedFace
    }
}
