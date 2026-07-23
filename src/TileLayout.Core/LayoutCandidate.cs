using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            LayoutCandidateMetrics metrics)
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

        public string SelectionReason { get; }

        public IReadOnlyList<BoundaryBandPlan> AxisPlans => axisPlans;

        public IReadOnlyList<LineSegment3D> DivisionLines { get; }

        public IReadOnlyList<TileFootprint> Tiles { get; }

        public IReadOnlyList<CandidateDiagnostic> Diagnostics { get; }

        public IReadOnlyList<CandidateDiagnostic> RejectionReasons { get; }

        public LayoutCandidateMetrics Metrics { get; }

        public BoundaryBandPlan GetAxisPlan(TileLayoutAxis axis)
        {
            foreach (BoundaryBandPlan plan in axisPlans)
            {
                if (plan.Axis == axis)
                {
                    return plan;
                }
            }

            throw new InvalidOperationException(
                "The candidate does not contain a plan for the requested axis.");
        }
    }
}
