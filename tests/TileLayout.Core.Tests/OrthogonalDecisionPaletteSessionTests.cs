using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalDecisionPaletteSessionTests
    {
        [TestMethod]
        public void AutomaticUniqueCandidate_CanRequestAndCancelZeroWritePreview()
        {
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(CalculateRectangle(), LayoutDecisionMode.ControlledProduction);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.AutomaticPreviewReady,
                session.State);
            Assert.IsTrue(session.IsAutomaticPreview);
            Assert.AreEqual(1, session.Candidates.Count);

            LayoutCandidate preview;
            Assert.IsTrue(session.TryRequestPreview(out preview));
            Assert.AreSame(session.Candidates[0].Candidate, preview);
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.PreviewRequested,
                session.State);
            StringAssert.Contains(
                OrthogonalDecisionPaletteText.FormatCandidate(
                    session.SelectedCandidate),
                "原始指标");

            session.CancelPreview();
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.AutomaticPreviewReady,
                session.State);
            Assert.IsNull(session.PreviewCandidate);
        }

        [TestMethod]
        public void MissingProjectPolicyCandidateDoesNotHideOtherSelectableCandidates()
        {
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(CalculateL04(null, null), LayoutDecisionMode.Research);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.NeedsCandidateSelection,
                session.State);
            Assert.AreEqual(0, session.ProjectPolicyRequirements.Count);
            Assert.IsTrue(session.Candidates.Any(candidate =>
                candidate.State == LayoutCandidateState.RequiresProjectPolicy));
            Assert.IsFalse(session.CanRequestPreview);
        }

        [TestMethod]
        public void MissingRoomSemantics_ShowsRoomQuestionWithoutGuessing()
        {
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(
                Calculate(
                    L01Room(),
                    null,
                    new LayoutPolicyProfile("P-1"),
                    null,
                    LayoutDecisionMode.Research),
                LayoutDecisionMode.Research);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.NeedsRoomSemantics,
                session.State);
            Assert.IsTrue(session.RoomSemanticRequirements.Any(
                item => item.Code == DecisionRequirementCode.RoomControlDoor));
            Assert.IsTrue(session.RoomSemanticRequirements.Any(
                item => item.Code == DecisionRequirementCode.RoomControlRegion));
            Assert.IsFalse(session.CanRequestPreview);
        }

        [TestMethod]
        public void MultipleCandidates_KeepDOR3OrderAndRequireSelection()
        {
            EngineeringOrthogonalDecisionResult result = CalculateL04(
                new LayoutPolicyProfile("P-1", 200), null);
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(result, LayoutDecisionMode.Research);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.ManualReviewPreviewReady,
                session.State);
            Assert.IsTrue(session.CandidateRequirements.Any(
                item => item.Code == DecisionRequirementCode.CandidateSelection));
            CollectionAssert.AreEqual(
                result.Candidates.Select(item => item.Id).ToArray(),
                session.Candidates.Select(item => item.Id).ToArray());
            Assert.IsTrue(session.TrySelectCandidate(session.Candidates[0].Id));
            Assert.IsFalse(session.CanRequestPreview);
        }

        [TestMethod]
        public void SelectedManualCandidate_CanRequestPrimaryPreviewWithoutReasonRecord()
        {
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(
                CalculateL04(new LayoutPolicyProfile("P-1", 200), null),
                LayoutDecisionMode.ControlledProduction);

            EvaluatedLayoutCandidate manual = session.Candidates.Single(
                candidate => candidate.State
                    == LayoutCandidateState.RequiresUserDecision);
            Assert.IsTrue(session.TrySelectCandidate(manual.Id));
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.ManualReviewPreviewReady,
                session.State);
            Assert.IsTrue(session.CanRequestPreview);

            LayoutCandidate preview;
            Assert.IsTrue(session.TryRequestPreview(out preview));
            Assert.AreSame(manual.Candidate, preview);
        }

        [TestMethod]
        public void VisualConfirmationCandidate_CanRequestPreviewButIsNotAutomatic()
        {
            EngineeringOrthogonalDecisionResult result = CalculateL04(
                new LayoutPolicyProfile(
                    "P-1",
                    null,
                    ProjectAbsoluteMinimumMode.VisualConfirmation),
                null);
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(result, LayoutDecisionMode.ControlledProduction);

            EvaluatedLayoutCandidate visual = session.Candidates.Single(
                candidate => candidate.State
                    == LayoutCandidateState.RequiresProjectPolicy);
            Assert.IsTrue(session.TrySelectCandidate(visual.Id));
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady,
                session.State);
            Assert.IsFalse(session.IsAutomaticPreview);
            Assert.IsTrue(session.CanRequestPreview);

            LayoutCandidate preview;
            Assert.IsTrue(session.TryRequestPreview(out preview));
            Assert.AreSame(visual.Candidate, preview);
        }

        [TestMethod]
        public void VisualConfirmationCandidate_IsWriteableOnlyThroughVisualAuthorization()
        {
            EngineeringOrthogonalDecisionResult result = CalculateL04(
                new LayoutPolicyProfile(
                    "P-1",
                    null,
                    ProjectAbsoluteMinimumMode.VisualConfirmation),
                null);
            EvaluatedLayoutCandidate visual = result.Candidates.Single(
                candidate => candidate.State
                    == LayoutCandidateState.RequiresProjectPolicy);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                visual.Id);

            IReadOnlyList<LayoutDrawingLine> lines;
            string rejectionReason;
            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                true,
                true,
                false,
                out lines,
                out rejectionReason));
            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                true,
                true,
                true,
                out lines,
                out rejectionReason));
            Assert.IsTrue(lines.Count > 0);
        }

        [TestMethod]
        public void RecordedException_IsPreviewableButNeverAutomaticInResearchOrProduction()
        {
            var record = new DecisionRecord(
                "whole-confirmed-l05",
                "P-1",
                "Documented review.",
                true);
            EngineeringOrthogonalDecisionResult research = Calculate(
                L05Room(),
                L05Decision(),
                new LayoutPolicyProfile("P-1", 200),
                new CandidateDecision(record),
                LayoutDecisionMode.Research);
            EngineeringOrthogonalDecisionResult production = Calculate(
                L05Room(),
                L05Decision(),
                new LayoutPolicyProfile("P-1", 200),
                new CandidateDecision(record),
                LayoutDecisionMode.ControlledProduction);

            var researchSession = new OrthogonalDecisionPaletteSession();
            researchSession.SetResult(research, LayoutDecisionMode.Research);
            var productionSession = new OrthogonalDecisionPaletteSession();
            productionSession.SetResult(production, LayoutDecisionMode.ControlledProduction);

            Assert.AreEqual(
                OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady,
                researchSession.State);
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady,
                productionSession.State);
            Assert.IsFalse(researchSession.IsAutomaticPreview);
            Assert.IsFalse(productionSession.IsAutomaticPreview);
            StringAssert.Contains(
                OrthogonalDecisionPaletteText.FormatPreviewStatus(productionSession),
                "非自动合规");
            StringAssert.Contains(
                OrthogonalDecisionPaletteText.FormatDecisionRecord(
                    production.AppliedRecord,
                    LayoutDecisionMode.ControlledProduction),
                "Documented review.");
        }

        [TestMethod]
        public void ResetAndRepeatedDOR3Result_PreserveZeroWriteBoundaryAndStableStates()
        {
            EngineeringOrthogonalDecisionResult first = CalculateL04(
                new LayoutPolicyProfile("P-1", 200), null);
            EngineeringOrthogonalDecisionResult second = CalculateL04(
                new LayoutPolicyProfile("P-1", 200), null);
            var session = new OrthogonalDecisionPaletteSession();
            session.SetResult(first, LayoutDecisionMode.Research);
            string[] firstStates = session.Candidates.Select(
                item => item.Id + ":" + item.State).ToArray();

            session.Reset();
            Assert.AreEqual(OrthogonalDecisionPaletteState.Empty, session.State);
            Assert.AreEqual(0, session.Candidates.Count);
            Assert.IsFalse(session.CanRequestPreview);

            session.SetResult(second, LayoutDecisionMode.Research);
            CollectionAssert.AreEqual(
                firstStates,
                session.Candidates.Select(item => item.Id + ":" + item.State)
                    .ToArray());
        }

        private static EngineeringOrthogonalDecisionResult CalculateL01()
        {
            return Calculate(
                L01Room(),
                new RoomDecision(
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new DoorOpening(RoomSide.North, 700, 1300),
                    RoomLayoutIntent.WholeRoomSinglePhase),
                new LayoutPolicyProfile("P-1"),
                null,
                LayoutDecisionMode.ControlledProduction);
        }

        private static EngineeringOrthogonalDecisionResult CalculateRectangle()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 4476), P(0, 4476));
            return Calculate(
                room,
                new RoomDecision(
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new DoorOpening(RoomSide.North, 700, 1300),
                    RoomLayoutIntent.WholeRoomSinglePhase),
                new LayoutPolicyProfile("P-1"),
                null,
                LayoutDecisionMode.ControlledProduction);
        }

        private static EngineeringOrthogonalDecisionResult CalculateL04(
            LayoutPolicyProfile policy,
            CandidateDecision candidateDecision)
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 1774), P(1876, 1774),
                P(1876, 3526), P(0, 3526));
            var main = new AxisAlignedRectangle(0, 1876, 1774, 3526);
            return Calculate(
                room,
                new RoomDecision(
                    main,
                    new DoorOpening(RoomSide.East, 2800, 3400),
                    RoomLayoutIntent.MainSecondary,
                    new MainSecondaryRegionDefinition(
                        main,
                        new AxisAlignedRectangle(0, 2076, 0, 1774)),
                    new LineSegment3D(
                        new Point3D(0, 1774),
                        new Point3D(1876, 1774))),
                policy,
                candidateDecision,
                LayoutDecisionMode.Research);
        }

        private static EngineeringOrthogonalDecisionResult Calculate(
            AxisAlignedOrthogonalPolygon room,
            RoomDecision decision,
            LayoutPolicyProfile policy,
            CandidateDecision candidateDecision,
            LayoutDecisionMode mode)
        {
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    room,
                    600,
                    600,
                    policy,
                    decision,
                    candidateDecision,
                    mode));
        }

        private static RoomDecision L05Decision()
        {
            return new RoomDecision(
                new AxisAlignedRectangle(0, 5676, 0, 5176),
                new DoorOpening(RoomSide.North, 4100, 4900),
                RoomLayoutIntent.WholeRoomSinglePhase,
                null,
                null,
                new List<ConfirmedGridPhase>
                {
                    new ConfirmedGridPhase(
                        "l05",
                        300,
                        376,
                        "Confirmed central field.",
                        true)
                });
        }

        private static AxisAlignedOrthogonalPolygon L01Room()
        {
            return Room(
                P(0, 0), P(2076, 0), P(2076, 4476), P(200, 4476),
                P(200, 3476), P(0, 3476));
        }

        private static AxisAlignedOrthogonalPolygon L05Room()
        {
            return Room(
                P(0, 0), P(5676, 0), P(5676, 5176), P(3650, 5176),
                P(3650, 3726), P(0, 3726));
        }

        private static AxisAlignedOrthogonalPolygon Room(params Point3D[] vertices)
        {
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]));
            }

            return OrthogonalRoomValidator.Validate(lines).Room;
        }

        private static Point3D P(double x, double y)
        {
            return new Point3D(x, y);
        }
    }
}
