using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalDecisionGuidedWorkflowTests
    {
        [TestMethod]
        public void PresentationProjectionsAreReusedUntilTheDecisionChanges()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);

            IReadOnlyList<GuidedRequirementPresentation> requirements =
                workflow.Requirements;
            IReadOnlyList<GuidedCandidatePresentation> automatic =
                workflow.RuleSatisfiedCandidates;
            IReadOnlyList<GuidedCandidatePresentation> review =
                workflow.ReviewCandidates;
            IReadOnlyList<GuidedCandidatePresentation> missing =
                workflow.MissingRuleCandidates;
            IReadOnlyList<GuidedCandidatePresentation> eliminated =
                workflow.EliminatedCandidatePage;
            string summary = workflow.BuildOrdinarySummary();

            Assert.AreSame(requirements, workflow.Requirements);
            Assert.AreSame(automatic, workflow.RuleSatisfiedCandidates);
            Assert.AreSame(review, workflow.ReviewCandidates);
            Assert.AreSame(missing, workflow.MissingRuleCandidates);
            Assert.AreSame(eliminated, workflow.EliminatedCandidatePage);
            Assert.AreSame(summary, workflow.BuildOrdinarySummary());

            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);

            Assert.AreNotSame(requirements, workflow.Requirements);
            Assert.AreNotSame(automatic, workflow.RuleSatisfiedCandidates);
            Assert.AreNotSame(review, workflow.ReviewCandidates);
            Assert.AreNotSame(missing, workflow.MissingRuleCandidates);
            Assert.AreNotSame(eliminated, workflow.EliminatedCandidatePage);
            Assert.AreNotSame(summary, workflow.BuildOrdinarySummary());
        }

        [TestMethod]
        public void L01_G3AnchorsReachComparisonPreviewWithoutLetterMenuOrPhaseInput()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            string reason;
            Assert.IsTrue(workflow.BeginHostAction(
                OrthogonalDecisionGuideAction.SelectRoom,
                out reason));
            Assert.IsTrue(workflow.LoadBoundary(L01Lines(), 600, 600).IsValid);
            workflow.EndHostAction("房间已选择。");
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));
            workflow.SetWallCornerAlignmentPreference(true);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.NeedsCandidateSelection,
                workflow.Palette.State);
            GuidedCandidatePresentation selected = workflow.Candidates
                .Where(item => item.Group
                    == GuidedCandidateGroup.AutomaticRecommendation)
                .OrderByDescending(item => item.Candidate.Candidate.Metrics
                    .ExactGridIntersectionCornerCount)
                .ThenByDescending(item => item.Candidate.Candidate.Metrics
                    .ExactSeamAlignedCornerCount)
                .First();
            Assert.IsTrue(workflow.TrySelectCandidate(
                selected.Candidate.Id));
            StringAssert.Contains(workflow.Notice, "方案");
            Assert.IsFalse(workflow.Notice.Contains("填写原因"));
            string overview = OrthogonalDecisionGuidedText.FormatCandidateOverview(
                selected.Candidate);
            StringAssert.Contains(overview, "地砖");
            StringAssert.Contains(overview, "最窄边砖");
            StringAssert.Contains(overview, "墙角对缝");
            Assert.IsFalse(overview.Contains("LayoutCandidate"));
            LayoutCandidate preview;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out preview));
            Assert.IsNotNull(preview);
            Assert.IsFalse(workflow.HasWriteAuthorization);
            Assert.IsFalse(Enum.GetNames(typeof(OrthogonalDecisionGuideAction))
                .Any(name => name.Length == 1));
            Assert.IsFalse(workflow.Requirements.Any(item =>
                item.Message.Contains("WCS") || item.NextAction.Contains("WCS")));
        }

        [TestMethod]
        public void SelectedAutomaticCandidate_CanRequestPrimaryPreviewAfterCandidateSelection()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));

            GuidedCandidatePresentation automatic = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.AutomaticRecommendation
                    && item.Candidate.State
                        == LayoutCandidateState.AutomaticUsable);
            Assert.IsTrue(workflow.TrySelectCandidate(automatic.Candidate.Id));
            Assert.IsTrue(workflow.Palette.CanRequestPreview);

            LayoutCandidate preview;
            Assert.IsTrue(workflow.TryRequestPreview(out preview));
            Assert.AreEqual(automatic.Candidate.Id, preview.Id);
            LayoutDrawingPlan plan = workflow.PreviewPlan;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out preview));
            Assert.AreSame(plan, workflow.PreviewPlan);
        }

        [TestMethod]
        public void SelectedManualCandidate_CanRequestPrimaryPreviewWithoutReasonInput()
        {
            OrthogonalDecisionGuidedWorkflow workflow = CompletedL04E(
                LayoutDecisionMode.ControlledProduction);
            GuidedCandidatePresentation manual = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.ManualConfirmation
                    && item.Candidate.State
                        == LayoutCandidateState.RequiresUserDecision);
            Assert.IsTrue(workflow.TrySelectCandidate(manual.Candidate.Id));
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.ManualReviewPreviewReady,
                workflow.Palette.State);

            LayoutCandidate preview;
            Assert.IsTrue(workflow.TryRequestPreview(out preview));
            Assert.AreEqual(manual.Candidate.Id, preview.Id);
        }

        [TestMethod]
        public void VisualConfirmationCandidate_ShowsFactsBeforeFormalWriteback()
        {
            OrthogonalDecisionGuidedWorkflow workflow =
                VisualConfirmationL04();
            GuidedCandidatePresentation visual = workflow.Candidates.First(
                item => item.Candidate.State
                    == LayoutCandidateState.RequiresProjectPolicy);

            Assert.IsTrue(workflow.TrySelectCandidate(visual.Candidate.Id));
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady,
                workflow.Palette.State);

            LayoutCandidate preview;
            Assert.IsTrue(workflow.TryRequestPreview(out preview));
            workflow.MarkPreviewVisible();
            StringAssert.Contains(
                workflow.GetVisualConfirmationWarning(),
                "实际最小边界砖尺寸");
            Assert.IsTrue(workflow.CanRequestFormalWriteback);
            StringAssert.Contains(
                workflow.GetFormalWritebackConfirmationMessage(),
                "按图面确认提醒");

            string error;
            Assert.IsTrue(workflow.TryAcknowledgeFormalWriteback(out error));
            Assert.AreEqual(string.Empty, error);
            Assert.IsTrue(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void WallCornerPreferenceIsOptionalAndMarksTheSortedRecommendation()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));

            Assert.IsFalse(workflow.PreferWallCornerAlignment);
            Assert.IsFalse(workflow.Candidates.Any(item => item.IsRecommended));
            string[] originalOrder = workflow.Candidates
                .Select(item => item.Candidate.Id).ToArray();

            workflow.SetWallCornerAlignmentPreference(true);

            Assert.IsTrue(workflow.PreferWallCornerAlignment);
            GuidedCandidatePresentation recommended = workflow.Candidates
                .Single(item => item.IsRecommended);
            Assert.AreEqual(1, recommended.RecommendationRank);
            Assert.IsTrue(recommended.Status.Contains("推荐首选"));
            Assert.AreEqual(0, recommended.Candidate.Candidate.Metrics
                .EntranceVisualBelowRecommendedBoundaryTileCount);
            Assert.IsFalse(originalOrder.SequenceEqual(
                workflow.Candidates.Select(item => item.Candidate.Id)));
            Assert.IsFalse(workflow.HasWriteAuthorization);

            workflow.SetWallCornerAlignmentPreference(false);
            Assert.IsFalse(workflow.PreferWallCornerAlignment);
            Assert.IsFalse(workflow.Candidates.Any(item => item.IsRecommended));
        }

        [TestMethod]
        public void DisabledOptionalQualityKeepsCornerFactsOutOfOrdinaryCandidateText()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));
            GuidedCandidatePresentation candidate = workflow.Candidates.First(
                item => item.Candidate.HasRawCandidate);

            Assert.IsFalse(candidate.Status.Contains("墙角对缝"));
            Assert.IsFalse(
                OrthogonalDecisionGuidedText.FormatCandidateOverview(
                    candidate.Candidate,
                    false).Contains("墙角对缝"));
            Assert.IsFalse(
                OrthogonalDecisionGuidedText.FormatCandidateGenerationReport(
                    workflow.Input.Result.RawResult.GenerationReport,
                    false).Contains("墙角对缝"));
            Assert.IsTrue(
                OrthogonalDecisionPaletteText.FormatCandidate(
                    candidate.Candidate).Contains("墙角目标"));
        }

        [TestMethod]
        public void L01_EastDoor_PresentsRecoveredPhaseAsAutomaticPreview()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            workflow.SetWallCornerAlignmentPreference(true);

            GuidedCandidatePresentation automatic = workflow.Candidates.Single(
                item => item.OriginalIndex == 2);
            GuidedCandidatePresentation eliminated = workflow.Candidates.Single(
                item => item.OriginalIndex == 1);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.NeedsCandidateSelection,
                workflow.Palette.State);
            Assert.AreEqual(1, eliminated.OriginalIndex);
            Assert.AreEqual(2, automatic.OriginalIndex);
            Assert.IsTrue(workflow.TrySelectCandidate(automatic.Candidate.Id));
            LayoutCandidate preview;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out preview));
            CollectionAssert.AreEqual(
                new[] { 576.0, 600.0, 600.0, 300.0 },
                preview.GetAxisPlan(TileLayoutAxis.X).SegmentWidths.ToArray());
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void L01_NorthAndSouthRightHandDoors_PresentFlippedPhaseForPreview()
        {
            foreach (RoomSide wall in new[]
            {
                RoomSide.North,
                RoomSide.South
            })
            {
                var workflow = new OrthogonalDecisionGuidedWorkflow();
                workflow.LoadBoundary(L01Lines(), 600, 600);
                workflow.ApplyProjectSettings(
                    LayoutDecisionMode.ControlledProduction,
                    "P-1",
                    null);
                workflow.SetLayoutIntent(
                    RoomLayoutIntent.WholeRoomSinglePhase);
                workflow.SetControlRegion(
                    new AxisAlignedRectangle(0, 2076, 0, 4476));
                workflow.SetControlDoor(
                    new DoorOpening(wall, 1300, 1900));
                workflow.SetWallCornerAlignmentPreference(true);

                GuidedCandidatePresentation automatic =
                    workflow.Candidates.Single(item => item.OriginalIndex == 2);
                GuidedCandidatePresentation eliminated =
                    workflow.Candidates.Single(item => item.OriginalIndex == 1);

                Assert.AreEqual(
                    OrthogonalDecisionPaletteState.NeedsCandidateSelection,
                    workflow.Palette.State,
                    wall.ToString());
                Assert.AreEqual(1, eliminated.OriginalIndex, wall.ToString());
                Assert.AreEqual(2, automatic.OriginalIndex, wall.ToString());
                Assert.IsTrue(workflow.TrySelectCandidate(
                    automatic.Candidate.Id), wall.ToString());
                LayoutCandidate preview;
                Assert.IsTrue(workflow.TryRequestComparisonPreview(out preview),
                    wall.ToString());
                CollectionAssert.AreEqual(
                    new[] { 600.0, 600.0, 600.0, 276.0 },
                    preview.GetAxisPlan(TileLayoutAxis.X)
                        .SegmentWidths.ToArray(),
                    wall.ToString());
                Assert.IsFalse(workflow.HasWriteAuthorization,
                    wall.ToString());
            }
        }

        [TestMethod]
        public void RequirementMapping_UsesFriendlyNextActionsAndKeepsCodesInEngineeringDetails()
        {
            foreach (DecisionRequirementCode code in Enum.GetValues(
                typeof(DecisionRequirementCode)))
            {
                GuidedRequirementPresentation presentation =
                    OrthogonalDecisionGuidedText.PresentRequirement(
                        new DecisionRequirement(
                            code,
                            code == DecisionRequirementCode.ProjectSecondAbsoluteMinimum
                                ? DecisionRequirementLevel.ProjectPolicy
                                : code == DecisionRequirementCode.CandidateSelection
                                    || code
                                        == DecisionRequirementCode.CandidateExceptionAcceptance
                                    ? DecisionRequirementLevel.CandidateSelection
                                    : DecisionRequirementLevel.RoomSemantics,
                            "raw reason",
                            "raw input"));

                Assert.IsFalse(string.IsNullOrWhiteSpace(presentation.Message));
                Assert.IsFalse(string.IsNullOrWhiteSpace(presentation.NextAction));
                Assert.IsFalse(presentation.Message.Contains(code.ToString()));
                StringAssert.Contains(
                    presentation.EngineeringDetails,
                    code.ToString());
            }
        }

        [TestMethod]
        public void DefaultGuidanceUsesProductTermsWhileEngineeringDetailsKeepInternalCodes()
        {
            GuidedRequirementPresentation presentation =
                OrthogonalDecisionGuidedText.PresentRequirement(
                    new DecisionRequirement(
                        DecisionRequirementCode.RoomControlRegion,
                        DecisionRequirementLevel.RoomSemantics,
                        "raw reason",
                        "RoomControlRegion"));

            StringAssert.Contains(presentation.NextAction, "门洞影响范围");
            Assert.IsFalse(presentation.Message.Contains("RoomControlRegion"));
            Assert.IsFalse(presentation.NextAction.Contains("控制区"));
            StringAssert.Contains(
                presentation.EngineeringDetails,
                "RoomControlRegion");
            Assert.AreEqual(
                "分区铺贴（主要区 + 相邻区）",
                OrthogonalDecisionGuidedText.FormatIntent(
                    RoomLayoutIntent.MainSecondary));
            Assert.AreEqual(
                "项目执行",
                OrthogonalDecisionGuidedText.FormatMode(
                    LayoutDecisionMode.ControlledProduction));
        }

        [TestMethod]
        public void PreviewLifecycleUsesOnePlanAndNeverGrantsWriteAuthorization()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));

            GuidedCandidatePresentation selected = workflow.Candidates
                .First(item => item.Group
                    == GuidedCandidateGroup.AutomaticRecommendation);
            Assert.IsTrue(workflow.TrySelectCandidate(selected.Candidate.Id));
            LayoutCandidate candidate;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out candidate));
            LayoutDrawingPlan plan = workflow.PreviewPlan;
            Assert.IsNotNull(plan);
            Assert.AreEqual(candidate.Id, plan.CandidateId);
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.DisplayRequested,
                workflow.PreviewState);

            workflow.MarkPreviewVisible();
            Assert.IsTrue(workflow.CanRefreshPreview);
            string reason;
            Assert.IsTrue(workflow.TryRequestPreviewRefresh(out reason));
            Assert.AreSame(plan, workflow.PreviewPlan);
            workflow.MarkPreviewVisible();
            workflow.MarkPreviewRefreshRequired("切回原图后请刷新。");
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.RefreshRequired,
                workflow.PreviewState);
            Assert.IsTrue(workflow.BeginClearPreview());
            workflow.MarkPreviewCleared(null);
            Assert.IsNull(workflow.PreviewPlan);
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.None,
                workflow.PreviewState);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void InputChangeInvalidatesPreviewButCancelledSelectionKeepsIt()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.Research,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));
            LayoutCandidate candidate;
            workflow.TryRequestPreview(out candidate);
            LayoutDrawingPlan original = workflow.PreviewPlan;

            string reason;
            Assert.IsTrue(workflow.BeginHostAction(
                OrthogonalDecisionGuideAction.SelectControlDoor,
                out reason));
            workflow.EndHostAction("已取消本次门洞选择。");
            Assert.AreSame(original, workflow.PreviewPlan);

            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 669, 1326));
            Assert.IsNull(workflow.PreviewPlan);
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.None,
                workflow.PreviewState);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void L04D_MissingProjectMinimumAndMainSecondaryInputsShowFriendlyActions()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1690, 1890), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);

            StringAssert.Contains(
                workflow.GetNextDisabledReason(),
                "门洞影响范围");
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 1690, 1774, 3526));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            Assert.IsTrue(workflow.Requirements.Any(item =>
                item.Requirement.Code
                    == DecisionRequirementCode.RoomMainSecondaryDefinition
                && item.NextAction.Contains("主要铺贴区")));

            workflow.SetMainRegionDraft(
                new AxisAlignedRectangle(0, 1690, 1774, 3526));
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 1890, 0, 1774));
            Assert.IsTrue(workflow.Requirements.Any(item =>
                item.Requirement.Code == DecisionRequirementCode.RoomConnectionEdge
                && item.NextAction.Contains("接合边")));
            workflow.SetConnectionEdge(L(0, 1774, 1690, 1774));

            GuidedRequirementPresentation policy = workflow.Requirements.Single(
                item => item.Requirement.Code
                    == DecisionRequirementCode.ProjectSecondAbsoluteMinimum);
            StringAssert.Contains(policy.Message, "最小边砖宽度");
            StringAssert.Contains(policy.NextAction, "确定铺贴要求");
            Assert.IsFalse(workflow.Palette.CanRequestPreview);
        }

        [TestMethod]
        public void L04D_UniqueManualCandidateIsFocusedAndCanBeInspectedBeforeRecord()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1690, 1890), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                200);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var main = new AxisAlignedRectangle(0, 1690, 1774, 3526);
            workflow.SetControlRegion(main);
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            workflow.SetMainRegionDraft(main);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 1890, 0, 1774));
            workflow.SetConnectionEdge(L(0, 1774, 1690, 1774));

            GuidedCandidatePresentation manual = workflow.Candidates.Single(
                item => item.Group == GuidedCandidateGroup.ManualConfirmation
                    && item.Candidate.State
                        == LayoutCandidateState.RequiresUserDecision);
            Assert.AreEqual(
                manual.Candidate.Id,
                workflow.Palette.SelectedCandidate.Id);
            Assert.IsTrue(workflow.Palette.CanInspectSelectedCandidate);
            Assert.IsTrue(workflow.Palette.CanRequestPreview);
            Assert.IsNull(workflow.Input.Result.AppliedRecord);
            Assert.AreEqual(string.Empty, workflow.GetNextDisabledReason());

            LayoutCandidate candidate;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out candidate));
            Assert.AreEqual(manual.Candidate.Id, candidate.Id);
            Assert.AreEqual(candidate.Id, workflow.PreviewPlan.CandidateId);
            Assert.IsNull(workflow.Input.Result.AppliedRecord);
            Assert.IsFalse(workflow.HasWriteAuthorization);
            StringAssert.Contains(workflow.Notice, "不代表确认采用");

            GuidedCandidatePresentation unavailable = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.Unavailable);
            Assert.IsTrue(workflow.TrySelectCandidate(
                unavailable.Candidate.Id));
            Assert.IsFalse(workflow.Palette.CanInspectSelectedCandidate);
            Assert.IsFalse(workflow.TryRequestComparisonPreview(out candidate));
            Assert.IsNull(workflow.PreviewPlan);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void DOR8WritebackConfirmationIsIndependentFromPreviewAndReasonInput()
        {
            OrthogonalDecisionGuidedWorkflow workflow = CompletedL04E(
                LayoutDecisionMode.ControlledProduction);
            GuidedCandidatePresentation manual = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.ManualConfirmation
                    && item.Candidate.State
                        == LayoutCandidateState.RequiresUserDecision);
            Assert.IsTrue(workflow.TrySelectCandidate(manual.Candidate.Id));

            LayoutCandidate candidate;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out candidate));
            LayoutDrawingPlan plan = workflow.PreviewPlan;
            workflow.MarkPreviewVisible();
            Assert.IsTrue(workflow.CanRequestFormalWriteback);
            Assert.IsFalse(workflow.HasWriteAuthorization);
            Assert.IsNull(workflow.Input.Result.AppliedRecord);

            string error;
            Assert.IsTrue(workflow.TryAcknowledgeFormalWriteback(out error));
            Assert.AreEqual(string.Empty, error);
            Assert.IsTrue(workflow.HasWriteAuthorization);

            workflow.MarkFormalWritebackFailed("测试失败");
            Assert.IsFalse(workflow.HasWriteAuthorization);
            Assert.AreSame(plan, workflow.PreviewPlan);
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.Visible,
                workflow.PreviewState);
            Assert.IsTrue(workflow.CanRequestFormalWriteback);

            Assert.IsTrue(workflow.TryAcknowledgeFormalWriteback(out error));
            Assert.IsTrue(workflow.HasWriteAuthorization);
            int lineCount = plan.DivisionLines.Count + plan.Connections.Count;
            workflow.MarkFormalWritebackSucceeded(lineCount);
            Assert.IsTrue(workflow.FormalWritebackCompleted);
            Assert.AreEqual(lineCount, workflow.FormalWritebackLineCount);
            Assert.IsNull(workflow.PreviewPlan);
            Assert.IsFalse(workflow.HasWriteAuthorization);

            Assert.IsTrue(workflow.TryRequestComparisonPreview(out candidate));
            workflow.MarkPreviewVisible();
            Assert.IsTrue(workflow.CanRequestFormalWriteback);
        }

        [TestMethod]
        public void CancelAllAfterFormalWritebackKeepsFormalObjectsOutOfSessionReset()
        {
            OrthogonalDecisionGuidedWorkflow workflow = CompletedL04E(
                LayoutDecisionMode.ControlledProduction);
            GuidedCandidatePresentation candidate = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.ManualConfirmation
                    && item.Candidate.State
                        == LayoutCandidateState.RequiresUserDecision);
            Assert.IsTrue(workflow.TrySelectCandidate(candidate.Candidate.Id));

            LayoutCandidate selected;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out selected));
            workflow.MarkPreviewVisible();
            string error;
            Assert.IsTrue(workflow.TryAcknowledgeFormalWriteback(out error));
            Assert.IsTrue(workflow.FormalWritebackAwaitingCompletion);
            int lineCount = workflow.PreviewPlan.DivisionLines.Count
                + workflow.PreviewPlan.Connections.Count;
            workflow.MarkFormalWritebackSucceeded(lineCount);
            Assert.IsTrue(workflow.FormalWritebackCompleted);
            Assert.IsFalse(workflow.FormalWritebackAwaitingCompletion);
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.None,
                workflow.PreviewState);
            Assert.IsNull(workflow.PreviewPlan);

            workflow.CancelAll();

            Assert.IsFalse(workflow.FormalWritebackCompleted);
            Assert.AreEqual(0, workflow.FormalWritebackLineCount);
            Assert.IsFalse(workflow.Input.HasRoom);
            Assert.AreEqual(
                OrthogonalDecisionPreviewState.None,
                workflow.PreviewState);
            Assert.IsNull(workflow.PreviewPlan);
            StringAssert.Contains(workflow.Notice, "正式写回");
            StringAssert.Contains(workflow.Notice, "保留");
        }

        [TestMethod]
        public void CandidateOverviewNamesFourWallsAndClippedMinimumLocation()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));

            GuidedCandidatePresentation unavailable = workflow.Candidates.Single(
                item => item.OriginalIndex == 1);
            string overview = OrthogonalDecisionGuidedText.FormatCandidateOverview(
                unavailable.Candidate);
            StringAssert.Contains(overview, "西墙");
            StringAssert.Contains(overview, "东墙");
            StringAssert.Contains(overview, "南墙");
            StringAssert.Contains(overview, "北墙");
            StringAssert.Contains(overview, "异形转角或边界裁切处 76 mm");
            string reason = OrthogonalDecisionGuidedText.FormatCandidateReason(
                unavailable.Candidate,
                600,
                600);
            StringAssert.Contains(reason, "不能列入满足规则");
            StringAssert.Contains(reason, "对面东墙");
        }

        [TestMethod]
        public void L04E_ManualReasonCreatesAuditableDecisionRecordInOriginalOrder()
        {
            OrthogonalDecisionGuidedWorkflow workflow = CompletedL04E(
                LayoutDecisionMode.Research);
            string[] dor3Order = workflow.Input.Result.Candidates
                .Select(item => item.Id)
                .ToArray();
            CollectionAssert.AreEqual(
                dor3Order,
                workflow.Candidates.OrderBy(item => item.OriginalIndex)
                    .Select(item => item.Candidate.Id)
                    .ToArray());

            GuidedCandidatePresentation selected = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.ManualConfirmation
                    && item.Candidate.HasRawCandidate);
            Assert.IsTrue(workflow.TrySelectCandidate(selected.Candidate.Id));
            string error;
            Assert.IsFalse(workflow.TryApplyDecisionRecord("", out error));
            StringAssert.Contains(error, "原因");
            Assert.IsTrue(workflow.TryApplyDecisionRecord(
                "对比门口观感和边界砖后由项目负责人确认。",
                out error));

            DecisionRecord record = workflow.Input.Result.AppliedRecord;
            Assert.IsNotNull(record);
            Assert.AreEqual(selected.Candidate.Id, record.CandidateId);
            Assert.AreEqual("P-1", record.PolicyVersion);
            StringAssert.Contains(record.Reason, "项目负责人");
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady,
                workflow.Palette.State);
            StringAssert.Contains(workflow.Notice, "不代表自动合规");
            string userSummary = workflow.BuildSummary();
            StringAssert.Contains(userSummary, "人工确认记录：已保存");
            Assert.IsFalse(userSummary.Contains(record.CandidateId));
            Assert.IsFalse(userSummary.Contains("DecisionRecord"));

            GuidedCandidatePresentation other = workflow.Candidates.FirstOrDefault(
                item => item.Candidate.Id != selected.Candidate.Id);
            if (other != null)
            {
                workflow.TrySelectCandidate(other.Candidate.Id);
                Assert.IsFalse(workflow.Palette.CanRequestPreview);
                StringAssert.Contains(
                    OrthogonalDecisionGuidedText.PreviewDisabledReason(
                        workflow.Palette),
                    "已记录方案");
                workflow.TrySelectCandidate(selected.Candidate.Id);
                Assert.IsTrue(workflow.Palette.CanRequestPreview);
            }
        }

        [TestMethod]
        public void ModeAndSemanticChangesInvalidateRecordWithVisibleReason()
        {
            OrthogonalDecisionGuidedWorkflow workflow = CompletedL04E(
                LayoutDecisionMode.Research);
            GuidedCandidatePresentation selected = workflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.ManualConfirmation
                    && item.Candidate.HasRawCandidate);
            workflow.TrySelectCandidate(selected.Candidate.Id);
            string error;
            workflow.TryApplyDecisionRecord("研究记录。", out error);
            LayoutCandidate preview;
            Assert.IsTrue(workflow.TryRequestPreview(out preview));

            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                200);

            Assert.IsNull(workflow.Input.DecisionRecord);
            Assert.IsNull(workflow.Input.Result.AppliedRecord);
            Assert.IsNull(workflow.Palette.PreviewCandidate);
            StringAssert.Contains(workflow.InvalidationNotice, "项目使用方式或规则");

            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            Assert.IsNull(workflow.Input.DecisionRecord);
            Assert.IsFalse(workflow.ShowsMainSecondaryControls);
        }

        [TestMethod]
        public void NavigationReselectInvalidBoundaryAndCancelRespectFrozenBoundaries()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.Research,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));

            workflow.ViewStep(OrthogonalDecisionGuideStep.Summary);
            Assert.IsTrue(workflow.MovePrevious());
            workflow.BeginModify(OrthogonalDecisionGuideStep.Project);
            Assert.IsFalse(workflow.IsCompleted);

            workflow.BeginRoomReselection();
            Assert.IsFalse(workflow.Input.HasRoom);
            Assert.IsNotNull(workflow.Input.Policy);
            Assert.IsNull(workflow.Input.ControlDoor);
            OrthogonalRoomValidationResult invalid = workflow.LoadBoundary(
                InvalidBoundary(),
                600,
                600);
            Assert.IsFalse(invalid.IsValid);
            Assert.IsFalse(workflow.Input.HasRoom);
            Assert.IsNotNull(workflow.Input.Policy);

            workflow.CancelAll();
            Assert.IsNull(workflow.Input.Policy);
            Assert.IsFalse(workflow.Input.HasRoom);
            Assert.AreEqual(OrthogonalDecisionGuideStep.Room, workflow.ActiveStep);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void SelectionCancellationPreservesValuesAndMainSecondaryCommitsAtomically()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.Research,
                "P-1",
                200);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var original = new AxisAlignedRectangle(0, 1876, 1774, 3526);
            workflow.SetControlRegion(original);

            string disabledReason;
            Assert.IsTrue(workflow.BeginHostAction(
                OrthogonalDecisionGuideAction.SelectControlRegion,
                out disabledReason));
            workflow.EndHostAction("取消本次选择。");
            Assert.AreSame(original, workflow.Input.ControlRegion);

            workflow.SetMainRegionDraft(original);
            Assert.IsNull(workflow.Input.MainSecondary);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 2076, 0, 1774));
            Assert.IsNotNull(workflow.Input.MainSecondary);
            Assert.AreSame(original, workflow.Input.MainSecondary.MainRegion);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void DisabledActionsExplainWhatTheUserCanDoNext()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            StringAssert.Contains(
                workflow.GetActionDisabledReason(
                    OrthogonalDecisionGuideAction.SelectControlDoor),
                "选择房间");
            Assert.IsFalse(workflow.ShowsMainSecondaryControls);

            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.Research,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            Assert.IsTrue(workflow.ShowsMainSecondaryControls);
            StringAssert.Contains(
                workflow.GetActionDisabledReason(
                    OrthogonalDecisionGuideAction.SelectConnectionEdge),
                "主要铺贴区和相邻铺贴区");
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                OrthogonalDecisionGuidedText.PreviewDisabledReason(
                    workflow.Palette)));
        }

        [TestMethod]
        public void CandidateGroupingCoversEveryDor3StateDeterministically()
        {
            Assert.AreEqual(
                GuidedCandidateGroup.AutomaticRecommendation,
                OrthogonalDecisionGuidedText.MapCandidateGroup(
                    LayoutCandidateState.AutomaticUsable));
            Assert.AreEqual(
                GuidedCandidateGroup.ManualConfirmation,
                OrthogonalDecisionGuidedText.MapCandidateGroup(
                    LayoutCandidateState.RequiresUserDecision));
            Assert.AreEqual(
                GuidedCandidateGroup.ProjectRuleMissing,
                OrthogonalDecisionGuidedText.MapCandidateGroup(
                    LayoutCandidateState.RequiresProjectPolicy));
            foreach (LayoutCandidateState state in new[]
            {
                LayoutCandidateState.InputUntrusted,
                LayoutCandidateState.Eliminated,
                LayoutCandidateState.CapabilityUnsupported
            })
            {
                Assert.AreEqual(
                    GuidedCandidateGroup.Unavailable,
                    OrthogonalDecisionGuidedText.MapCandidateGroup(state));
            }
        }

        [TestMethod]
        public void FailedDefaultsExposeBoundedAlternativePhasesForSelection()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1876, 2076), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                250);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.ManualReviewPreviewReady,
                workflow.Palette.State);
            Assert.IsFalse(workflow.AllCandidatesUnavailable);
            workflow.ViewStep(OrthogonalDecisionGuideStep.Candidates);
            string nextReason = workflow.GetNextDisabledReason();
            Assert.AreEqual(string.Empty, nextReason);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void ComplexGeneratedCandidatesCanAllRenderFriendlyOverview()
        {
            IList<Point3D> vertices =
                ComplexOrthogonalBoundaryFixture.Vertices();
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Count; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Count]));
            }

            var workflow = new OrthogonalDecisionGuidedWorkflow();
            Assert.IsTrue(workflow.LoadBoundary(lines, 600, 600).IsValid);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "100-mm",
                100);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                ComplexOrthogonalBoundaryFixture.CreateControlRegion());
            workflow.SetControlDoor(
                ComplexOrthogonalBoundaryFixture.CreateDeterministicWestDoor());
            workflow.SetWallCornerAlignmentPreference(true);

            Assert.IsTrue(workflow.Candidates.Count > 1);
            foreach (GuidedCandidatePresentation candidate in workflow.Candidates)
            {
                string overview =
                    OrthogonalDecisionGuidedText.FormatCandidateOverview(
                        candidate.Candidate);
                StringAssert.Contains(overview, "铺贴概况", candidate.Title);
                Assert.IsFalse(string.IsNullOrWhiteSpace(overview),
                    candidate.Title);
            }

            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void WholeWallEndpointsAreExplainedAndActualDoorPointsRecoverL01()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                250);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 200, 2076));
            workflow.SetWallCornerAlignmentPreference(true);

            Assert.IsTrue(workflow.DoorSelectionCoversEntireBoundarySegment());
            StringAssert.Contains(workflow.GetCurrentGuidance(), "整段墙线");
            StringAssert.Contains(workflow.GetCurrentGuidance(), "实际门洞两侧边缘");
            workflow.ViewStep(OrthogonalDecisionGuideStep.Candidates);
            StringAssert.Contains(workflow.GetNextDisabledReason(), "不要使用墙线两端");

            workflow.SetControlDoor(
                new DoorOpening(RoomSide.North, 669, 1326));
            Assert.IsFalse(workflow.DoorSelectionCoversEntireBoundarySegment());
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.NeedsCandidateSelection,
                workflow.Palette.State);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void VisibleOuterStepIsExplainedAndSharedConnectionRecoversL04D()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1876, 2076), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var main = new AxisAlignedRectangle(0, 1876, 1774, 3526);
            workflow.SetControlRegion(main);
            workflow.SetMainRegionDraft(main);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 2076, 0, 1774));
            workflow.SetConnectionEdge(L(1876, 1774, 2076, 1774));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));

            Assert.IsTrue(
                workflow.ConnectionSelectionDoesNotMatchValidatedBoundary());
            Assert.AreEqual(
                "主要铺贴区下边与相邻铺贴区上边重合的整段水平边",
                workflow.DescribeExpectedConnectionEdge());
            StringAssert.Contains(workflow.GetCurrentGuidance(), "共同边界");
            StringAssert.Contains(workflow.GetCurrentGuidance(), "主要铺贴区下边");
            StringAssert.Contains(workflow.GetCurrentGuidance(), "短折边");
            StringAssert.Contains(
                OrthogonalDecisionGuidedText.FormatCandidateReason(
                    workflow.Candidates.Single().Candidate),
                "实际相接的整段边");
            workflow.ViewStep(OrthogonalDecisionGuideStep.Candidates);
            StringAssert.Contains(workflow.GetNextDisabledReason(), "短折边");

            workflow.SetConnectionEdge(L(0, 1774, 1876, 1774));
            Assert.IsFalse(
                workflow.ConnectionSelectionDoesNotMatchValidatedBoundary());
            Assert.AreNotEqual(
                OrthogonalDecisionPaletteState.Blocked,
                workflow.Palette.State);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void RecalculateKeepsCandidateOrderDiagnosticsAndMetricsStableWithZeroWrite()
        {
            OrthogonalDecisionGuidedWorkflow workflow = CompletedL04E(
                LayoutDecisionMode.Research);
            string[] first = StableSnapshot(workflow);
            workflow.Recalculate();
            CollectionAssert.AreEqual(first, StableSnapshot(workflow));
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void G2ProjectRulesRequireRecommendedConfirmationAndExplicitAbsoluteState()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 300);

            AssertThrows<ArgumentException>(() => workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                null,
                false));
            Assert.IsNull(workflow.Input.Policy);
            Assert.IsFalse(workflow.RecommendedMinimumConfirmed);
            AssertThrows<ArgumentException>(() => workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                " ",
                null,
                true));

            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                126,
                true);
            Assert.IsTrue(workflow.RecommendedMinimumConfirmed);
            Assert.AreEqual(126, workflow.Input.Policy.ProjectAbsoluteMinimumCut);
            AssertThrows<ArgumentException>(() => workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                126.001,
                true));
        }

        [TestMethod]
        public void G2MissingRuleCanDiagnoseButHardEliminationCannotEnterPlan()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1690, 1890), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var main = new AxisAlignedRectangle(0, 1690, 1774, 3526);
            workflow.SetControlRegion(main);
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            workflow.SetMainRegionDraft(main);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 1890, 0, 1774));
            workflow.SetConnectionEdge(L(0, 1774, 1690, 1774));

            GuidedCandidatePresentation missing = workflow.MissingRuleCandidates
                .First(item => item.Candidate.HasRawCandidate);
            Assert.IsTrue(workflow.TrySelectCandidate(missing.Candidate.Id));
            Assert.IsTrue(workflow.Palette.CanInspectSelectedCandidate);
            LayoutCandidate diagnosticCandidate;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(
                out diagnosticCandidate));
            Assert.AreEqual(missing.Candidate.Id, workflow.PreviewPlan.CandidateId);
            Assert.IsTrue(workflow.PreviewPlan.Tiles.Any(tile =>
                tile.IsBelowRecommended));

            OrthogonalDecisionGuidedWorkflow eliminatedWorkflow =
                CompletedL04E(LayoutDecisionMode.ControlledProduction, 251);
            GuidedCandidatePresentation eliminated =
                eliminatedWorkflow.Candidates.First(
                item => item.Group == GuidedCandidateGroup.Unavailable
                    && item.Candidate.HasRawCandidate);
            Assert.IsTrue(eliminatedWorkflow.TrySelectCandidate(
                eliminated.Candidate.Id));
            Assert.IsFalse(
                eliminatedWorkflow.Palette.CanInspectSelectedCandidate);
            Assert.IsFalse(eliminatedWorkflow.TryRequestComparisonPreview(
                out diagnosticCandidate));
            Assert.IsNull(eliminatedWorkflow.PreviewPlan);
            Assert.IsFalse(eliminatedWorkflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void G2DiagnosticOptionsAndTileSelectionKeepSameReadOnlyPlan()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1690, 1890), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                null);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var main = new AxisAlignedRectangle(0, 1690, 1774, 3526);
            workflow.SetControlRegion(main);
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            workflow.SetMainRegionDraft(main);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 1890, 0, 1774));
            workflow.SetConnectionEdge(L(0, 1774, 1690, 1774));
            GuidedCandidatePresentation missing = workflow.MissingRuleCandidates
                .First(item => item.Candidate.HasRawCandidate);
            Assert.IsTrue(workflow.TrySelectCandidate(missing.Candidate.Id));
            LayoutCandidate candidate;
            Assert.IsTrue(workflow.TryRequestComparisonPreview(out candidate));
            LayoutDrawingPlan plan = workflow.PreviewPlan;
            LayoutDrawingTile narrow = plan.Tiles.First(tile =>
                tile.IsBelowRecommended);

            workflow.SetDiagnosticDisplayOptions(true, true, true);
            Assert.IsTrue(workflow.SelectDiagnosticTile(narrow.Id));
            Assert.AreSame(plan, workflow.PreviewPlan);
            Assert.AreEqual(narrow.Id, workflow.SelectedDiagnosticTileId);
            Assert.IsTrue(workflow.ShowAllAssessedBoundaryTiles);
            Assert.IsTrue(workflow.ShowNeutralRegions);
            Assert.IsTrue(workflow.ShowWallCornerDiagnostics);
            Assert.IsTrue(plan.NeutralRegions.Count > 0);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void G2ComplexCandidateViewIsGroupedPagedAuditableAndFast()
        {
            IList<Point3D> vertices = ComplexOrthogonalBoundaryFixture.Vertices();
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Count; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Count]));
            }

            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(lines, 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                100);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                ComplexOrthogonalBoundaryFixture.CreateControlRegion());
            workflow.SetControlDoor(
                ComplexOrthogonalBoundaryFixture.CreateDeterministicWestDoor());
            workflow.SetWallCornerAlignmentPreference(true);

            Assert.IsTrue(workflow.Candidates.Count > 50);
            Assert.IsTrue(workflow.EliminatedCandidateCount > 50);
            Assert.IsTrue(workflow.EliminatedCandidatePage.Count <= 50);
            Assert.IsTrue(workflow.LastCandidatePresentationBuildDuration
                < TimeSpan.FromSeconds(2));
            Assert.AreEqual(
                workflow.Candidates.Count,
                workflow.Candidates.Select(item => item.Candidate.Id)
                    .Distinct().Count());
            Assert.IsTrue(workflow.Candidates.Any(item => item.IsRecommended));

            var timer = Stopwatch.StartNew();
            foreach (GuidedEliminatedGroup group in Enum.GetValues(
                typeof(GuidedEliminatedGroup)))
            {
                workflow.SetEliminatedFilter(group);
                Assert.IsTrue(workflow.EliminatedCandidatePage.Count <= 50);
                Assert.IsTrue(workflow.EliminatedCandidatePage.All(item =>
                    item.EliminatedGroup == group));
            }
            timer.Stop();
            Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(1));
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }


        [TestMethod]
        public void WallCornerPreferenceSwitchChangesComplexCandidateSearchAndRestoresLegacyPath()
        {
            IList<Point3D> vertices = ComplexOrthogonalBoundaryFixture.Vertices();
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Count; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Count]));
            }

            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(lines, 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G2",
                100);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                ComplexOrthogonalBoundaryFixture.CreateControlRegion());
            workflow.SetControlDoor(
                ComplexOrthogonalBoundaryFixture.CreateDeterministicWestDoor());

            string[] offIds = workflow.Candidates
                .Select(item => item.Candidate.Id)
                .ToArray();
            Assert.IsFalse(workflow.PreferWallCornerAlignment);
            Assert.IsTrue(workflow.Input.Result.RawResult.GenerationReport
                .PhaseSearchEnabled);
            Assert.IsFalse(workflow.Input.Result.RawResult.GenerationReport
                .WallCornerSearchEnabled);
            StringAssert.Contains(
                OrthogonalDecisionGuidedText.FormatCandidateGenerationReport(
                    workflow.Input.Result.RawResult.GenerationReport),
                "墙角锚定优先未启用");
            Assert.IsTrue(workflow.Candidates.Any(item =>
                item.Candidate.HasRawCandidate
                && item.Candidate.Candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == CandidateDiagnosticCode
                        .AlternativeWholeRoomPhaseGenerated)));
            Assert.IsTrue(workflow.Candidates.Any(item =>
                item.Candidate.HasRawCandidate
                && item.Candidate.Candidate.PhaseSources.Any(source =>
                    source.Kind == GridPhaseSourceKind
                        .DoorControlledBoundaryRedistribution)));

            workflow.SetWallCornerAlignmentPreference(true);
            Assert.IsTrue(workflow.PreferWallCornerAlignment);
            Assert.IsTrue(workflow.Input.Result.RawResult.GenerationReport
                .WallCornerSearchEnabled);
            Assert.IsTrue(workflow.Input.Result.RawResult.GenerationReport
                .GeneratedAlternativeCount > 0);
            Assert.IsTrue(workflow.Candidates.Any(item =>
                item.Group != GuidedCandidateGroup.Unavailable
                && item.Candidate.HasRawCandidate
                && item.Candidate.Candidate.Metrics
                    .SafeDoubleWallCornerAlignmentCount > 0));
            CollectionAssert.AreNotEqual(
                offIds,
                workflow.Candidates.Select(item => item.Candidate.Id).ToArray());

            workflow.SetWallCornerAlignmentPreference(false);
            Assert.IsFalse(workflow.PreferWallCornerAlignment);
            CollectionAssert.AreEqual(
                offIds,
                workflow.Candidates.Select(item => item.Candidate.Id).ToArray());
            Assert.IsTrue(workflow.Candidates.Any(item =>
                item.Candidate.HasRawCandidate
                && item.Candidate.Candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == CandidateDiagnosticCode
                        .AlternativeWholeRoomPhaseGenerated)));
        }

        [TestMethod]
        public void G2SearchAndNeutralReferenceExposeFactsWithoutSemanticInference()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom();
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        ComplexOrthogonalBoundaryFixture.CreateControlRegion(),
                        ComplexOrthogonalBoundaryFixture
                            .CreateDeterministicWestDoor(),
                        null,
                        null,
                        new LayoutPolicyProfile("P-G2", 100),
                        true));

            string search = OrthogonalDecisionGuidedText
                .FormatCandidateGenerationReport(result.GenerationReport);
            StringAssert.Contains(search, "X 相位");
            StringAssert.Contains(search, "组合");
            StringAssert.Contains(search, "相位去重");
            StringAssert.Contains(search, "支配淘汰");
            StringAssert.Contains(search, "保留");
            StringAssert.Contains(search, "截断");

            string neutral = OrthogonalDecisionGuidedText
                .FormatNeutralRegionReference(result.NeutralRegionPartition);
            StringAssert.Contains(neutral, "中性矩形区域");
            StringAssert.Contains(neutral, "共享边");
            StringAssert.Contains(neutral, "不代表主区、次区、重要区或相位重置");
        }

        [TestMethod]
        public void G2OrdinaryFlowAutomaticallyUsesDoorAdjacentRegionAndWholeRoomPhase()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L01Lines(), 600, 600);
            workflow.ApplyOrdinaryProjectRules(100);

            OrthogonalDoorOpeningProjectionResult projection =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    workflow.Input.Room,
                    P(0, 2800),
                    P(0, 3400));
            Assert.IsTrue(projection.IsValid);
            workflow.SetAutomaticallyLocatedDoor(
                projection.ControlRegion,
                projection.Opening);

            Assert.AreEqual(
                LayoutDecisionMode.ControlledProduction,
                workflow.Input.Mode);
            Assert.AreEqual(
                OrthogonalDecisionGuidedWorkflow.OrdinaryPolicyVersion,
                workflow.Input.Policy.Version);
            Assert.AreEqual(
                RoomLayoutIntent.WholeRoomSinglePhase,
                workflow.Input.LayoutIntent);
            Assert.IsNull(workflow.Input.MainSecondary);
            Assert.IsFalse(workflow.Input.SelectedConnectionEdge.HasValue);
            Assert.AreSame(projection.ControlRegion, workflow.Input.ControlRegion);
            Assert.AreSame(projection.Opening, workflow.Input.ControlDoor);
            Assert.AreEqual(
                OrthogonalDecisionGuideStep.Candidates,
                workflow.ActiveStep);
            string ordinarySummary = workflow.BuildOrdinarySummary();
            StringAssert.Contains(ordinarySummary, "全房保持连续相位");
            Assert.IsFalse(ordinarySummary.Contains("G2-AUTO-RULE-1"));
            Assert.IsFalse(ordinarySummary.Contains("使用方式"));
            Assert.IsFalse(ordinarySummary.Contains("门洞影响范围"));
            Assert.IsFalse(ordinarySummary.Contains("主要区"));
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void G2MultipleFullyCompliantPlansCanBeChosenWithoutInventedReason()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(
                PolygonLines(
                    P(0, 0), P(2476, 0), P(2476, 2476), P(0, 2476)),
                600,
                600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                OrthogonalDecisionGuidedWorkflow.OrdinaryPolicyVersion,
                100);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(0, 2476, 0, 2476));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.West, 938, 1538));

            Assert.IsTrue(
                workflow.RuleSatisfiedCandidates.Count > 1,
                "fully compliant=" + workflow.RuleSatisfiedCandidates.Count);
            GuidedCandidatePresentation selected =
                workflow.RuleSatisfiedCandidates[0];
            Assert.IsTrue(workflow.TrySelectCandidate(selected.Candidate.Id));
            string error;
            Assert.IsTrue(workflow.TryApplyDecisionRecord(string.Empty, out error));
            Assert.AreEqual(string.Empty, error);
            Assert.IsNotNull(workflow.Input.Result.AppliedRecord);
            Assert.IsFalse(workflow.Input.Result.AppliedRecord.AcceptsException);
            Assert.AreEqual(string.Empty, workflow.Input.Result.AppliedRecord.Reason);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        private static TException AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException error)
            {
                return error;
            }

            Assert.Fail("Expected " + typeof(TException).Name + ".");
            return null;
        }

        private static OrthogonalDecisionGuidedWorkflow CompletedL04E(
            LayoutDecisionMode mode,
            double absoluteMinimum = 200)
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1876, 2076), 600, 600);
            workflow.ApplyProjectSettings(mode, "P-1", absoluteMinimum);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var main = new AxisAlignedRectangle(0, 1876, 1774, 3526);
            workflow.SetControlRegion(main);
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            workflow.SetMainRegionDraft(main);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 2076, 0, 1774));
            workflow.SetConnectionEdge(L(0, 1774, 1876, 1774));
            return workflow;
        }

        private static OrthogonalDecisionGuidedWorkflow VisualConfirmationL04()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            workflow.LoadBoundary(L04Lines(1876, 2076), 600, 600);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-1",
                null,
                true,
                ProjectAbsoluteMinimumMode.VisualConfirmation);
            workflow.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            var main = new AxisAlignedRectangle(0, 1876, 1774, 3526);
            workflow.SetControlRegion(main);
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            workflow.SetMainRegionDraft(main);
            workflow.SetSecondaryRegionDraft(
                new AxisAlignedRectangle(0, 2076, 0, 1774));
            workflow.SetConnectionEdge(L(0, 1774, 1876, 1774));
            return workflow;
        }

        private static string[] StableSnapshot(
            OrthogonalDecisionGuidedWorkflow workflow)
        {
            return workflow.Candidates.OrderBy(item => item.OriginalIndex)
                .Select(item => item.OriginalIndex
                    + ":"
                    + item.Group
                    + ":"
                    + OrthogonalDecisionPaletteText.FormatCandidate(
                        item.Candidate))
                .ToArray();
        }

        private static IReadOnlyCollection<LineSegment3D> L01Lines()
        {
            return PolygonLines(
                P(0, 0), P(2076, 0), P(2076, 4476), P(200, 4476),
                P(200, 3476), P(0, 3476));
        }

        private static IReadOnlyCollection<LineSegment3D> L04Lines(
            double upperWidth,
            double lowerWidth)
        {
            return PolygonLines(
                P(0, 0), P(lowerWidth, 0), P(lowerWidth, 1774),
                P(upperWidth, 1774), P(upperWidth, 3526), P(0, 3526));
        }

        private static IReadOnlyCollection<LineSegment3D> InvalidBoundary()
        {
            return new List<LineSegment3D>
            {
                L(0, 0, 1000, 0),
                L(1000, 0, 1000, 1000),
                L(1000, 1000, 0, 1000),
                L(0, 1000, 0, 100)
            };
        }

        private static IReadOnlyCollection<LineSegment3D> PolygonLines(
            params Point3D[] vertices)
        {
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]));
            }

            return lines;
        }

        private static LineSegment3D L(
            double x1,
            double y1,
            double x2,
            double y2)
        {
            return new LineSegment3D(P(x1, y1), P(x2, y2));
        }

        private static Point3D P(double x, double y)
        {
            return new Point3D(x, y);
        }
    }
}
