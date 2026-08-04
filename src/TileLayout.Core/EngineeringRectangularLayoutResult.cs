using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class EngineeringRectangularLayoutResult
    {
        internal EngineeringRectangularLayoutResult(
            AxisAlignedRectangle room,
            EngineeringRectangularLayoutParameters parameters,
            IList<LayoutCandidate> candidates)
        {
            Room = room;
            Parameters = parameters;
            Candidates = new ReadOnlyCollection<LayoutCandidate>(candidates);

            var viable = new List<LayoutCandidate>();
            var eliminated = new List<LayoutCandidate>();
            foreach (LayoutCandidate candidate in candidates)
            {
                if (candidate.IsRejected)
                {
                    eliminated.Add(candidate);
                }
                else
                {
                    viable.Add(candidate);
                    if (candidate.IsDefault)
                    {
                        DefaultCandidate = candidate;
                    }

                    if (candidate.IsFlippedAlternative)
                    {
                        FlippedCandidate = candidate;
                    }
                }
            }

            ViableCandidates = new ReadOnlyCollection<LayoutCandidate>(viable);
            EliminatedCandidates =
                new ReadOnlyCollection<LayoutCandidate>(eliminated);
        }

        public AxisAlignedRectangle Room { get; }

        public EngineeringRectangularLayoutParameters Parameters { get; }

        public IReadOnlyList<LayoutCandidate> Candidates { get; }

        public IReadOnlyList<LayoutCandidate> ViableCandidates { get; }

        public IReadOnlyList<LayoutCandidate> EliminatedCandidates { get; }

        public bool IsSuccessful => DefaultCandidate != null;

        public LayoutCandidate DefaultCandidate { get; }

        public LayoutCandidate FlippedCandidate { get; }
    }
}
