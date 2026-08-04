using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace TileLayout.Core
{
    public sealed class EngineeringOrthogonalDecisionResult
    {
        internal EngineeringOrthogonalDecisionResult(
            EngineeringOrthogonalLayoutResult rawResult,
            IList<EvaluatedLayoutCandidate> candidates,
            IList<DecisionRequirement> requirements,
            DecisionRecord appliedRecord)
        {
            RawResult = rawResult;
            Candidates = new ReadOnlyCollection<EvaluatedLayoutCandidate>(candidates);
            Requirements = new ReadOnlyCollection<DecisionRequirement>(requirements);
            AppliedRecord = appliedRecord;
        }

        public EngineeringOrthogonalLayoutResult RawResult { get; }
        public IReadOnlyList<EvaluatedLayoutCandidate> Candidates { get; }
        public IReadOnlyList<DecisionRequirement> Requirements { get; }
        public DecisionRecord AppliedRecord { get; }

        public bool AllowsVisualConfirmation =>
            RawResult != null
                && RawResult.Parameters != null
                && RawResult.Parameters.Policy != null
                && RawResult.Parameters.Policy.AllowsVisualConfirmation;

        public bool CanProceedAutomatically
        {
            get
            {
                if (Requirements.Count != 0)
                {
                    return false;
                }

                int automaticCount = 0;
                foreach (EvaluatedLayoutCandidate candidate in Candidates)
                {
                    if (candidate.State == LayoutCandidateState.AutomaticUsable)
                    {
                        automaticCount++;
                    }
                    else if (candidate.State != LayoutCandidateState.Eliminated
                        && candidate.State != LayoutCandidateState.RequiresProjectPolicy)
                    {
                        return false;
                    }
                }

                if (AllowsVisualConfirmation
                    && Candidates.Any(candidate =>
                        candidate.State
                            == LayoutCandidateState.RequiresProjectPolicy))
                {
                    return false;
                }

                return automaticCount == 1;
            }
        }
    }
}
