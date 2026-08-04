namespace TileLayout.Core
{
    public sealed class CandidateGenerationReport
    {
        internal CandidateGenerationReport(
            int xPhaseCount,
            int yPhaseCount,
            int phaseCombinationCount,
            int generatedAlternativeCount,
            int duplicatePhaseCount,
            int dominatedCandidateCount,
            int retainedCandidateCount,
            bool xPhaseLimitReached,
            bool yPhaseLimitReached,
            bool combinationLimitReached,
            bool retentionLimitReached,
            int optimizationTargetCornerCount = 0,
            int xTargetAnchorPhaseCount = 0,
            int yTargetAnchorPhaseCount = 0,
            int doubleAnchorCombinationCount = 0,
            int singleAnchorCombinationCount = 0,
            int mergedPhaseSourceCount = 0,
            bool anchorCombinationLimitReached = false,
            bool wallCornerSearchEnabled = false,
            bool phaseSearchEnabled = false)
        {
            XPhaseCount = xPhaseCount;
            YPhaseCount = yPhaseCount;
            PhaseCombinationCount = phaseCombinationCount;
            GeneratedAlternativeCount = generatedAlternativeCount;
            DuplicatePhaseCount = duplicatePhaseCount;
            DominatedCandidateCount = dominatedCandidateCount;
            RetainedCandidateCount = retainedCandidateCount;
            XPhaseLimitReached = xPhaseLimitReached;
            YPhaseLimitReached = yPhaseLimitReached;
            CombinationLimitReached = combinationLimitReached;
            RetentionLimitReached = retentionLimitReached;
            OptimizationTargetCornerCount = optimizationTargetCornerCount;
            XTargetAnchorPhaseCount = xTargetAnchorPhaseCount;
            YTargetAnchorPhaseCount = yTargetAnchorPhaseCount;
            DoubleAnchorCombinationCount = doubleAnchorCombinationCount;
            SingleAnchorCombinationCount = singleAnchorCombinationCount;
            MergedPhaseSourceCount = mergedPhaseSourceCount;
            AnchorCombinationLimitReached = anchorCombinationLimitReached;
            WallCornerSearchEnabled = wallCornerSearchEnabled;
            PhaseSearchEnabled = phaseSearchEnabled;
        }

        public static CandidateGenerationReport Empty =>
            new CandidateGenerationReport(0, 0, 0, 0, 0, 0, 0,
                false, false, false, false);

        public int XPhaseCount { get; }

        public int YPhaseCount { get; }

        public int PhaseCombinationCount { get; }

        public int GeneratedAlternativeCount { get; }

        public int DuplicatePhaseCount { get; }

        public int DominatedCandidateCount { get; }

        public int RetainedCandidateCount { get; }

        public bool XPhaseLimitReached { get; }

        public bool YPhaseLimitReached { get; }

        public bool CombinationLimitReached { get; }

        public bool RetentionLimitReached { get; }

        public int OptimizationTargetCornerCount { get; }

        public int XTargetAnchorPhaseCount { get; }

        public int YTargetAnchorPhaseCount { get; }

        public int DoubleAnchorCombinationCount { get; }

        public int SingleAnchorCombinationCount { get; }

        public int MergedPhaseSourceCount { get; }

        public bool AnchorCombinationLimitReached { get; }

        /// <summary>
        /// Indicates whether the optional bounded wall-corner phase search
        /// actually ran for this report. False means the G1 candidate path was
        /// intentionally kept and zero phase-search statistics are not a
        /// truncation result.
        /// </summary>
        public bool WallCornerSearchEnabled { get; }

        /// <summary>
        /// Indicates whether the bounded whole-room phase search ran. G1 can
        /// run this search without enabling G3 wall-corner anchor priority.
        /// </summary>
        public bool PhaseSearchEnabled { get; }

        public bool IsTruncated => XPhaseLimitReached
            || YPhaseLimitReached
            || CombinationLimitReached
            || RetentionLimitReached
            || AnchorCombinationLimitReached;
    }
}
