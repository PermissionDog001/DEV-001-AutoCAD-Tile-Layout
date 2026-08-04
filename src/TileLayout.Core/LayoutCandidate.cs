using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class LayoutCandidate
    {
        private readonly IReadOnlyList<BoundaryBandPlan> axisPlans;

        internal LayoutCandidate(
            string id,
            bool isDefault,
            bool isFlippedAlternative,
            string selectionReason,
            IList<BoundaryBandPlan> axisPlans,
            IList<LineSegment3D> divisionLines,
            IReadOnlyList<TileFootprint> tiles,
            IList<CandidateDiagnostic> diagnostics,
            LayoutCandidateMetrics metrics,
            LayoutCandidateStructure structure = null,
            IList<TileFootprintAssessment> tileAssessments = null,
            IList<WallCornerAssessment> wallCornerAssessments = null,
            IList<GridPhaseSource> phaseSources = null)
        {
            Id = id;
            IsDefault = isDefault;
            IsFlippedAlternative = isFlippedAlternative;
            SelectionReason = selectionReason;
            this.axisPlans =
                new ReadOnlyCollection<BoundaryBandPlan>(axisPlans);
            DivisionLines = new ReadOnlyCollection<LineSegment3D>(divisionLines);
            Tiles = tiles;
            Diagnostics =
                new ReadOnlyCollection<CandidateDiagnostic>(diagnostics);
            Metrics = metrics;
            Structure = structure ?? LayoutCandidateStructure.Rectangular;
            TileAssessments = new ReadOnlyCollection<TileFootprintAssessment>(
                tileAssessments ?? new List<TileFootprintAssessment>());
            WallCornerAssessments =
                new ReadOnlyCollection<WallCornerAssessment>(
                    wallCornerAssessments
                        ?? new List<WallCornerAssessment>());
            PhaseSources = new ReadOnlyCollection<GridPhaseSource>(
                phaseSources ?? new List<GridPhaseSource>());

            var rejectionReasons = new List<CandidateDiagnostic>();
            foreach (CandidateDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Severity == CandidateDiagnosticSeverity.Rejection)
                {
                    rejectionReasons.Add(diagnostic);
                }
            }

            RejectionReasons =
                new ReadOnlyCollection<CandidateDiagnostic>(rejectionReasons);
        }

        public string Id { get; }

        public bool IsDefault { get; }

        public bool IsFlippedAlternative { get; }

        public bool IsRejected => RejectionReasons.Count > 0;

        public bool RequiresPolicyDecision
        {
            get
            {
                foreach (CandidateDiagnostic diagnostic in Diagnostics)
                {
                    if (diagnostic.Code ==
                        CandidateDiagnosticCode.BelowDefaultMinimumRequiresPolicy
                        || diagnostic.Code ==
                            CandidateDiagnosticCode.BelowRecommendedMinimumRequiresReview
                        || diagnostic.Code ==
                            CandidateDiagnosticCode
                                .SmallBoundaryCutWithoutOppositeFullOrSeam
                        || diagnostic.Code ==
                            CandidateDiagnosticCode.MultipleCandidatesRequireSelection)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool RequiresProjectPolicy => Diagnostics.Any(diagnostic =>
            diagnostic.Code == CandidateDiagnosticCode.BelowDefaultMinimumRequiresPolicy);

        public bool RequiresUserReview => Diagnostics.Any(diagnostic =>
            diagnostic.Code == CandidateDiagnosticCode.BelowRecommendedMinimumRequiresReview
            || diagnostic.Code == CandidateDiagnosticCode
                .SmallBoundaryCutWithoutOppositeFullOrSeam);

        public string SelectionReason { get; }

        public IReadOnlyList<BoundaryBandPlan> AxisPlans => axisPlans;

        public IReadOnlyList<LineSegment3D> DivisionLines { get; }

        public IReadOnlyList<TileFootprint> Tiles { get; }

        public IReadOnlyList<CandidateDiagnostic> Diagnostics { get; }

        public IReadOnlyList<CandidateDiagnostic> RejectionReasons { get; }

        public LayoutCandidateMetrics Metrics { get; }

        public LayoutCandidateStructure Structure { get; }

        public IReadOnlyList<TileFootprintAssessment> TileAssessments { get; }

        public IReadOnlyList<WallCornerAssessment> WallCornerAssessments { get; }

        public IReadOnlyList<GridPhaseSource> PhaseSources { get; }

        public BoundaryBandPlan GetAxisPlan(TileLayoutAxis axis)
        {
            BoundaryBandPlan plan;
            if (TryGetAxisPlan(axis, out plan))
            {
                return plan;
            }

            throw new InvalidOperationException(
                "The candidate does not contain a plan for the requested axis.");
        }

        public bool TryGetAxisPlan(
            TileLayoutAxis axis,
            out BoundaryBandPlan plan)
        {
            foreach (BoundaryBandPlan candidatePlan in axisPlans)
            {
                if (candidatePlan.Axis == axis)
                {
                    plan = candidatePlan;
                    return true;
                }
            }

            plan = null;
            return false;
        }
    }
}
