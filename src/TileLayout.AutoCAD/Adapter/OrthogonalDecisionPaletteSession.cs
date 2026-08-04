using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    public enum OrthogonalDecisionPaletteState
    {
        Empty,
        NeedsProjectPolicy,
        NeedsRoomSemantics,
        NeedsCandidateSelection,
        AutomaticPreviewReady,
        ManualReviewPreviewReady,
        VisualConfirmationPreviewReady,
        RecordedDecisionPreviewReady,
        PreviewRequested,
        Blocked
    }

    public sealed class OrthogonalDecisionPaletteSession
    {
        private readonly List<DecisionRequirement> projectPolicyRequirements =
            new List<DecisionRequirement>();
        private readonly List<DecisionRequirement> roomSemanticRequirements =
            new List<DecisionRequirement>();
        private readonly List<DecisionRequirement> candidateRequirements =
            new List<DecisionRequirement>();

        public OrthogonalDecisionPaletteSession()
        {
            Candidates = new ReadOnlyCollection<EvaluatedLayoutCandidate>(
                new List<EvaluatedLayoutCandidate>());
            ProjectPolicyRequirements =
                new ReadOnlyCollection<DecisionRequirement>(
                    projectPolicyRequirements);
            RoomSemanticRequirements =
                new ReadOnlyCollection<DecisionRequirement>(
                    roomSemanticRequirements);
            CandidateRequirements =
                new ReadOnlyCollection<DecisionRequirement>(
                    candidateRequirements);
            State = OrthogonalDecisionPaletteState.Empty;
        }

        public EngineeringOrthogonalDecisionResult Result { get; private set; }

        public LayoutDecisionMode Mode { get; private set; }

        public IReadOnlyList<EvaluatedLayoutCandidate> Candidates { get; private set; }

        public IReadOnlyList<DecisionRequirement> ProjectPolicyRequirements { get; }

        public IReadOnlyList<DecisionRequirement> RoomSemanticRequirements { get; }

        public IReadOnlyList<DecisionRequirement> CandidateRequirements { get; }

        public EvaluatedLayoutCandidate SelectedCandidate { get; private set; }

        public LayoutCandidate PreviewCandidate { get; private set; }

        public OrthogonalDecisionPaletteState State { get; private set; }

        public bool CanRequestPreview
        {
            get
            {
                if (Result == null
                    || SelectedCandidate == null
                    || !SelectedCandidate.HasRawCandidate)
                {
                    return false;
                }

                if (State == OrthogonalDecisionPaletteState.AutomaticPreviewReady)
                {
                    return SelectedCandidate.State
                        == LayoutCandidateState.AutomaticUsable;
                }

                if (State
                    == OrthogonalDecisionPaletteState.ManualReviewPreviewReady)
                {
                    return SelectedCandidate.State
                        == LayoutCandidateState.RequiresUserDecision;
                }

                if (State
                    == OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady)
                {
                    return Result.AllowsVisualConfirmation
                        && SelectedCandidate.State
                            == LayoutCandidateState.RequiresProjectPolicy;
                }

                return State
                    == OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady
                    && Result.AppliedRecord != null
                    && SelectedCandidate.Id == Result.AppliedRecord.CandidateId;
            }
        }

        public bool IsAutomaticPreview =>
            State == OrthogonalDecisionPaletteState.AutomaticPreviewReady;

        public bool CanInspectSelectedCandidate =>
            Result != null
                && RoomSemanticRequirements.Count == 0
                && SelectedCandidate != null
                && SelectedCandidate.HasRawCandidate
                && (SelectedCandidate.State
                        == LayoutCandidateState.AutomaticUsable
                    || SelectedCandidate.State
                        == LayoutCandidateState.RequiresUserDecision
                    || SelectedCandidate.State
                        == LayoutCandidateState.RequiresProjectPolicy);

        public void SetResult(
            EngineeringOrthogonalDecisionResult result,
            LayoutDecisionMode mode)
        {
            Result = result;
            Mode = mode;
            PreviewCandidate = null;
            SelectedCandidate = null;
            projectPolicyRequirements.Clear();
            roomSemanticRequirements.Clear();
            candidateRequirements.Clear();

            if (result == null)
            {
                State = OrthogonalDecisionPaletteState.Empty;
                ReplaceCandidates(new List<EvaluatedLayoutCandidate>());
                return;
            }

            ReplaceCandidates(result.Candidates);
            foreach (DecisionRequirement requirement in result.Requirements)
            {
                switch (requirement.Level)
                {
                    case DecisionRequirementLevel.ProjectPolicy:
                        projectPolicyRequirements.Add(requirement);
                        break;
                    case DecisionRequirementLevel.RoomSemantics:
                        roomSemanticRequirements.Add(requirement);
                        break;
                    case DecisionRequirementLevel.CandidateSelection:
                        candidateRequirements.Add(requirement);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(requirement));
                }
            }

            if (result.CanProceedAutomatically)
            {
                SelectedCandidate = result.Candidates.Single(candidate =>
                    candidate.State == LayoutCandidateState.AutomaticUsable);
            }
            else if (HasAppliedRecord(result))
            {
                SelectedCandidate = result.Candidates.Single(candidate =>
                    candidate.Id == result.AppliedRecord.CandidateId);
            }
            else if (projectPolicyRequirements.Count == 0
                && roomSemanticRequirements.Count == 0)
            {
                EvaluatedLayoutCandidate[] previewCandidates = result.Candidates
                    .Where(candidate => candidate.HasRawCandidate
                        && (candidate.State
                                == LayoutCandidateState.RequiresUserDecision
                            || result.AllowsVisualConfirmation
                                && candidate.State
                                    == LayoutCandidateState.RequiresProjectPolicy))
                    .ToArray();
                if (previewCandidates.Length == 1)
                {
                    SelectedCandidate = previewCandidates[0];
                }
            }

            State = ResolveState();
        }

        public bool TrySelectCandidate(string candidateId)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                return false;
            }

            EvaluatedLayoutCandidate candidate = Candidates.FirstOrDefault(
                item => item.Id == candidateId);
            if (candidate == null)
            {
                return false;
            }

            bool selectionChanged = SelectedCandidate == null
                || SelectedCandidate.Id != candidate.Id;
            SelectedCandidate = candidate;
            if (selectionChanged)
            {
                PreviewCandidate = null;
            }

            if (selectionChanged
                || State != OrthogonalDecisionPaletteState.PreviewRequested)
            {
                State = ResolveState();
            }

            return true;
        }

        public bool TryRequestPreview(out LayoutCandidate candidate)
        {
            candidate = null;
            if (!CanRequestPreview
                || SelectedCandidate == null
                || !SelectedCandidate.HasRawCandidate)
            {
                return false;
            }

            PreviewCandidate = SelectedCandidate.Candidate;
            candidate = PreviewCandidate;
            State = OrthogonalDecisionPaletteState.PreviewRequested;
            return true;
        }

        public bool TryRequestComparisonPreview(out LayoutCandidate candidate)
        {
            candidate = null;
            if (!CanInspectSelectedCandidate)
            {
                return false;
            }

            PreviewCandidate = SelectedCandidate.Candidate;
            candidate = PreviewCandidate;
            State = OrthogonalDecisionPaletteState.PreviewRequested;
            return true;
        }

        public void CancelPreview()
        {
            PreviewCandidate = null;
            State = ResolveState();
        }

        public void Reset()
        {
            SetResult(null, LayoutDecisionMode.Research);
        }

        private void ReplaceCandidates(
            IEnumerable<EvaluatedLayoutCandidate> candidates)
        {
            Candidates = new ReadOnlyCollection<EvaluatedLayoutCandidate>(
                candidates.ToList());
        }

        private OrthogonalDecisionPaletteState ResolveState()
        {
            if (Result == null)
            {
                return OrthogonalDecisionPaletteState.Empty;
            }

            if (ProjectPolicyRequirements.Count > 0)
            {
                return OrthogonalDecisionPaletteState.NeedsProjectPolicy;
            }

            if (RoomSemanticRequirements.Count > 0)
            {
                return OrthogonalDecisionPaletteState.NeedsRoomSemantics;
            }

            if (HasAppliedRecord(Result))
            {
                return OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady;
            }

            if (SelectedCandidate != null && SelectedCandidate.HasRawCandidate)
            {
                if (SelectedCandidate.State == LayoutCandidateState.AutomaticUsable)
                {
                    return OrthogonalDecisionPaletteState.AutomaticPreviewReady;
                }

                if (SelectedCandidate.State
                    == LayoutCandidateState.RequiresUserDecision)
                {
                    return OrthogonalDecisionPaletteState.ManualReviewPreviewReady;
                }

                if (Result.AllowsVisualConfirmation
                    && SelectedCandidate.State
                        == LayoutCandidateState.RequiresProjectPolicy)
                {
                    return OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady;
                }
            }

            if (CandidateRequirements.Count > 0)
            {
                return OrthogonalDecisionPaletteState.NeedsCandidateSelection;
            }

            return Result.CanProceedAutomatically
                ? OrthogonalDecisionPaletteState.AutomaticPreviewReady
                : OrthogonalDecisionPaletteState.Blocked;
        }

        private static bool HasAppliedRecord(
            EngineeringOrthogonalDecisionResult result)
        {
            return result.AppliedRecord != null
                && result.Candidates.Any(candidate =>
                    candidate.Id == result.AppliedRecord.CandidateId
                    && candidate.HasRawCandidate
                    && candidate.State != LayoutCandidateState.Eliminated
                    && candidate.State != LayoutCandidateState.InputUntrusted
                    && candidate.State != LayoutCandidateState.CapabilityUnsupported);
        }
    }
}
