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
            int keyAlignmentCount,
            long belowProjectAbsoluteMinimumBoundaryTileCount = 0,
            long projectReviewBoundaryTileCount = 0,
            int optimizationTargetCornerCount = 0,
            int exactGridIntersectionCornerCount = 0,
            int exactSeamAlignedCornerCount = 0,
            int safeDoubleWallCornerAlignmentCount = 0,
            int safeSingleWallCornerAlignmentCount = 0,
            long entranceVisualBelowRecommendedBoundaryTileCount = 0,
            long entranceBlindBelowRecommendedBoundaryTileCount = 0)
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
            BelowProjectAbsoluteMinimumBoundaryTileCount =
                belowProjectAbsoluteMinimumBoundaryTileCount;
            ProjectReviewBoundaryTileCount = projectReviewBoundaryTileCount;
            OptimizationTargetCornerCount = optimizationTargetCornerCount;
            ExactGridIntersectionCornerCount =
                exactGridIntersectionCornerCount;
            ExactSeamAlignedCornerCount = exactSeamAlignedCornerCount;
            SafeDoubleWallCornerAlignmentCount = safeDoubleWallCornerAlignmentCount;
            SafeSingleWallCornerAlignmentCount = safeSingleWallCornerAlignmentCount;
            EntranceVisualBelowRecommendedBoundaryTileCount =
                entranceVisualBelowRecommendedBoundaryTileCount;
            EntranceBlindBelowRecommendedBoundaryTileCount =
                entranceBlindBelowRecommendedBoundaryTileCount;
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

        public long BelowProjectAbsoluteMinimumBoundaryTileCount { get; }

        public long ProjectReviewBoundaryTileCount { get; }

        public int OptimizationTargetCornerCount { get; }

        public int ExactGridIntersectionCornerCount { get; }

        public int ExactSeamAlignedCornerCount { get; }

        public int SafeDoubleWallCornerAlignmentCount { get; }

        public int SafeSingleWallCornerAlignmentCount { get; }

        public long EntranceVisualBelowRecommendedBoundaryTileCount { get; }

        public long EntranceBlindBelowRecommendedBoundaryTileCount { get; }
    }
}
