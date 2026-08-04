using System;

namespace TileLayout.Core
{
    public sealed class EvaluatedLayoutCandidate
    {
        internal EvaluatedLayoutCandidate(
            LayoutCandidate candidate,
            LayoutCandidateState state,
            string stateReason,
            int originalIndex = 0)
        {
            Candidate = candidate;
            State = state;
            StateReason = stateReason ?? string.Empty;
            OriginalIndex = originalIndex;
        }

        public LayoutCandidate Candidate { get; }

        public string Id => Candidate == null ? string.Empty : Candidate.Id;

        public LayoutCandidateState State { get; }

        public string StateReason { get; }

        /// <summary>
        /// One-based position in the raw candidate generation order.  A
        /// recommendation sort may change presentation order, but this value
        /// keeps the original audit position visible.
        /// </summary>
        public int OriginalIndex { get; }

        public bool HasRawCandidate => Candidate != null;
    }
}
