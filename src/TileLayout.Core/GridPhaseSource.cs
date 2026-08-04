namespace TileLayout.Core
{
    public enum GridPhaseSourceKind
    {
        TargetCornerAnchor,
        DoorControlledBoundaryRedistribution,
        DoorControlledBoundaryPattern,
        BoundaryVertex,
        AdjacentBoundaryResidueMidpoint,
        RecommendedMinimumContact,
        ProjectAbsoluteMinimumContact
    }

    public sealed class GridPhaseSource
    {
        internal GridPhaseSource(
            TileLayoutAxis axis,
            GridPhaseSourceKind kind,
            double phaseOffset,
            string cornerId,
            string reason)
        {
            Axis = axis;
            Kind = kind;
            PhaseOffset = phaseOffset;
            CornerId = cornerId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public TileLayoutAxis Axis { get; }

        public GridPhaseSourceKind Kind { get; }

        public double PhaseOffset { get; }

        public string CornerId { get; }

        public string Reason { get; }

        public bool IsTargetCornerAnchor =>
            Kind == GridPhaseSourceKind.TargetCornerAnchor;
    }
}
