using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class EngineeringOrthogonalLayoutResult
    {
        internal EngineeringOrthogonalLayoutResult(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            IList<LayoutCandidate> candidates,
            CandidateGenerationReport generationReport = null,
            NeutralOrthogonalRegionPartition neutralRegionPartition = null)
        {
            Room = room;
            SourceRoom = parameters.SourceRoom ?? room;
            Parameters = parameters;
            Candidates = new ReadOnlyCollection<LayoutCandidate>(candidates);
            GenerationReport = generationReport
                ?? CandidateGenerationReport.Empty;
            NeutralRegionPartition = neutralRegionPartition
                ?? NeutralOrthogonalRegionPartitioner.Create(room);

            var resolved = new List<LayoutCandidate>();
            var policyUndecided = new List<LayoutCandidate>();
            var rejected = new List<LayoutCandidate>();
            foreach (LayoutCandidate candidate in candidates)
            {
                if (candidate.IsRejected)
                {
                    rejected.Add(candidate);
                }
                else if (candidate.RequiresPolicyDecision)
                {
                    policyUndecided.Add(candidate);
                }
                else
                {
                    resolved.Add(candidate);
                }
            }

            ResolvedCandidates =
                new ReadOnlyCollection<LayoutCandidate>(resolved);
            PolicyUndecidedCandidates =
                new ReadOnlyCollection<LayoutCandidate>(policyUndecided);
            RejectedCandidates =
                new ReadOnlyCollection<LayoutCandidate>(rejected);
        }

        public AxisAlignedOrthogonalPolygon Room { get; }

        /// <summary>
        /// The original room boundary.  This remains stable when Room is an
        /// inward-offset finished face.
        /// </summary>
        public AxisAlignedOrthogonalPolygon SourceRoom { get; }

        public EngineeringOrthogonalLayoutParameters Parameters { get; }

        public IReadOnlyList<LayoutCandidate> Candidates { get; }

        public IReadOnlyList<LayoutCandidate> ResolvedCandidates { get; }

        public IReadOnlyList<LayoutCandidate> PolicyUndecidedCandidates { get; }

        public IReadOnlyList<LayoutCandidate> RejectedCandidates { get; }

        public CandidateGenerationReport GenerationReport { get; }

        public NeutralOrthogonalRegionPartition NeutralRegionPartition { get; }

        public bool HasTruncatedCandidateSearch => GenerationReport.IsTruncated;

        public bool HasMultipleRetainedCandidates =>
            ResolvedCandidates.Count + PolicyUndecidedCandidates.Count > 1;

        public bool HasUniqueAutomaticSelection =>
            ResolvedCandidates.Count == 1
            && PolicyUndecidedCandidates.Count == 0;
    }
}
