namespace TileLayout.Core
{
    public sealed class LayoutCandidateMetrics
    {
        internal LayoutCandidateMetrics(
            long interiorNonFullTileCount,
            double interiorNonFullTileArea,
            double internalTransitionSeamLength,
            long boundaryNonFullTileCount,
            long belowDefaultMinimumBoundaryTileCount,
            double minimumBoundaryBandWidth,
            int phaseResetCount,
            long continuousIrregularTileCount,
            int firstSightlinePenalty,
            int keyAlignmentCount)
        {
            InteriorNonFullTileCount = interiorNonFullTileCount;
            InteriorNonFullTileArea = interiorNonFullTileArea;
            InternalTransitionSeamLength = internalTransitionSeamLength;
            BoundaryNonFullTileCount = boundaryNonFullTileCount;
            BelowDefaultMinimumBoundaryTileCount =
                belowDefaultMinimumBoundaryTileCount;
            MinimumBoundaryBandWidth = minimumBoundaryBandWidth;
            PhaseResetCount = phaseResetCount;
            ContinuousIrregularTileCount = continuousIrregularTileCount;
            FirstSightlinePenalty = firstSightlinePenalty;
            KeyAlignmentCount = keyAlignmentCount;
        }

        public long InteriorNonFullTileCount { get; }

        public double InteriorNonFullTileArea { get; }

        public double InternalTransitionSeamLength { get; }

        public long BoundaryNonFullTileCount { get; }

        public long BelowDefaultMinimumBoundaryTileCount { get; }

        public double MinimumBoundaryBandWidth { get; }

        public int PhaseResetCount { get; }

        public long ContinuousIrregularTileCount { get; }

        public int FirstSightlinePenalty { get; }

        public int KeyAlignmentCount { get; }
    }
}
