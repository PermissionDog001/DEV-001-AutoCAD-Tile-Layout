using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalDecisionInputSessionTests
    {
        [TestMethod]
        public void ValidLRoomAndExplicitSemantics_ProduceG3CandidateSelection()
        {
            var input = new OrthogonalDecisionInputSession();
            Assert.IsTrue(input.LoadBoundary(L01Lines(), 600, 600).IsValid);
            input.SetPolicy(
                new LayoutPolicyProfile("P-1"),
                LayoutDecisionMode.ControlledProduction);
            input.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            input.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));
            input.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            input.SetWallCornerAlignmentPreference(true);

            Assert.IsFalse(input.Result.CanProceedAutomatically);
            Assert.IsTrue(input.Result.Requirements.Any(requirement =>
                requirement.Code == DecisionRequirementCode.CandidateSelection));
            var palette = new OrthogonalDecisionPaletteSession();
            palette.SetResult(input.Result, input.Mode);
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.NeedsCandidateSelection,
                palette.State);
            Assert.IsTrue(palette.Candidates.Count(candidate =>
                candidate.State == LayoutCandidateState.AutomaticUsable) > 1);
            Assert.IsFalse(input.HasWriteAuthorization);
        }

        [TestMethod]
        public void MissingProjectPolicy_UsesDor3Requirement()
        {
            OrthogonalDecisionInputSession input = L04Session(1690, 1890);

            Assert.IsTrue(input.Result.Requirements.Any(requirement =>
                requirement.Code
                    == DecisionRequirementCode.ProjectSecondAbsoluteMinimum));
            Assert.IsFalse(input.Result.CanProceedAutomatically);
        }

        [TestMethod]
        public void MissingRoomSemantics_AreReportedIndividuallyWithoutGuessing()
        {
            var input = new OrthogonalDecisionInputSession();
            input.LoadBoundary(L01Lines(), 600, 600);
            input.SetPolicy(
                new LayoutPolicyProfile("P-1"),
                LayoutDecisionMode.Research);

            AssertRequirement(input, DecisionRequirementCode.RoomControlRegion);
            AssertRequirement(input, DecisionRequirementCode.RoomControlDoor);
            AssertRequirement(input, DecisionRequirementCode.RoomLayoutIntent);

            input.SetControlRegion(
                new AxisAlignedRectangle(0, 2076, 0, 4476));
            AssertRequirement(input, DecisionRequirementCode.RoomControlDoor);
            input.SetControlDoor(
                new DoorOpening(RoomSide.North, 700, 1300));
            input.SetLayoutIntent(RoomLayoutIntent.MainSecondary);
            AssertRequirement(
                input,
                DecisionRequirementCode.RoomMainSecondaryDefinition);

            input.SetMainSecondary(
                new MainSecondaryRegionDefinition(
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new AxisAlignedRectangle(0, 2076, 4476, 5076)));
            AssertRequirement(input, DecisionRequirementCode.RoomConnectionEdge);
        }

        [TestMethod]
        public void MultipleCandidates_RequireRecordedSelectionInOriginalOrder()
        {
            OrthogonalDecisionInputSession input = L04Session(1876, 2076);
            input.SetPolicy(
                new LayoutPolicyProfile("P-1", 200),
                LayoutDecisionMode.Research);
            string[] originalOrder = input.Result.Candidates.Select(candidate =>
                candidate.Id).ToArray();
            EvaluatedLayoutCandidate selected = input.Result.Candidates.First(
                candidate => candidate.HasRawCandidate
                    && candidate.State != LayoutCandidateState.Eliminated);

            var palette = new OrthogonalDecisionPaletteSession();
            palette.SetResult(input.Result, input.Mode);
            Assert.IsTrue(palette.TrySelectCandidate(selected.Id));
            Assert.IsTrue(palette.CanRequestPreview);

            input.ApplyDecisionRecord(
                new DecisionRecord(
                    selected.Id,
                    "P-1",
                    "Compared in original candidate order.",
                    selected.State == LayoutCandidateState.RequiresUserDecision));

            CollectionAssert.AreEqual(
                originalOrder,
                input.Result.Candidates.Select(candidate => candidate.Id)
                    .ToArray());
            var recordedPalette = new OrthogonalDecisionPaletteSession();
            recordedPalette.SetResult(input.Result, input.Mode);
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady,
                recordedPalette.State);
            Assert.IsFalse(recordedPalette.IsAutomaticPreview);
        }

        [TestMethod]
        public void ResearchAndProductionRecordsRemainNonAutomaticAndChangesInvalidateRecord()
        {
            OrthogonalDecisionInputSession input = L04Session(1690, 1890);
            input.SetPolicy(
                new LayoutPolicyProfile("P-1", 200),
                LayoutDecisionMode.Research);
            EvaluatedLayoutCandidate candidate = input.Result.Candidates.First(
                item => item.State == LayoutCandidateState.RequiresUserDecision);
            input.ApplyDecisionRecord(
                new DecisionRecord(
                    candidate.Id,
                    "P-1",
                    "Documented exception.",
                    true));

            Assert.IsNotNull(input.Result.AppliedRecord);
            Assert.IsFalse(input.Result.CanProceedAutomatically);
            input.SetPolicy(
                new LayoutPolicyProfile("P-1", 200),
                LayoutDecisionMode.ControlledProduction);
            Assert.IsNull(input.DecisionRecord);
            Assert.IsNull(input.Result.AppliedRecord);

            input.ApplyDecisionRecord(
                new DecisionRecord(
                    candidate.Id,
                    "P-1",
                    "Accountable production exception.",
                    true));
            var palette = new OrthogonalDecisionPaletteSession();
            palette.SetResult(input.Result, input.Mode);
            Assert.AreEqual(
                OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady,
                palette.State);
            StringAssert.Contains(
                OrthogonalDecisionPaletteText.FormatPreviewStatus(palette),
                "非自动合规");

            input.SetConnectionEdge(input.SelectedConnectionEdge);
            Assert.IsNull(input.DecisionRecord);
        }

        [TestMethod]
        public void InvalidBoundaryAndCancel_ClearRoomWithoutWriteAuthorization()
        {
            var input = new OrthogonalDecisionInputSession();
            input.SetPolicy(
                new LayoutPolicyProfile("P-1"),
                LayoutDecisionMode.Research);
            Assert.IsTrue(input.LoadBoundary(L01Lines(), 600, 600).IsValid);

            OrthogonalRoomValidationResult invalid = input.LoadBoundary(
                new List<LineSegment3D>
                {
                    L(0, 0, 1000, 0),
                    L(1000, 0, 1000, 1000),
                    L(1000, 1000, 0, 1000),
                    L(0, 1000, 0, 100)
                },
                600,
                600);

            Assert.IsFalse(invalid.IsValid);
            Assert.IsFalse(input.HasRoom);
            Assert.IsNull(input.Result);
            Assert.IsNotNull(input.Policy);
            input.Cancel();
            Assert.IsNull(input.Policy);
            Assert.IsFalse(input.HasWriteAuthorization);
        }

        [TestMethod]
        public void ReselectAndRepeatedCalculation_ClearRoomStateAndStayStable()
        {
            OrthogonalDecisionInputSession input = L04Session(1876, 2076);
            input.SetPolicy(
                new LayoutPolicyProfile("P-1", 200),
                LayoutDecisionMode.Research);
            string[] first = StableSnapshot(input.Result);
            string[] repeated = StableSnapshot(input.Recalculate());
            CollectionAssert.AreEqual(first, repeated);

            input.LoadBoundary(L01Lines(), 600, 600);
            Assert.IsNotNull(input.Policy);
            Assert.IsNull(input.ControlRegion);
            Assert.IsNull(input.ControlDoor);
            Assert.IsNull(input.LayoutIntent);
            Assert.IsNull(input.MainSecondary);
            Assert.IsFalse(input.SelectedConnectionEdge.HasValue);
            Assert.IsNull(input.DecisionRecord);
            AssertRequirement(input, DecisionRequirementCode.RoomControlRegion);
        }

        [TestMethod]
        public void PlasterInputUsesFinishedFaceAndSynchronizesSelectedRoomData()
        {
            var input = new OrthogonalDecisionInputSession();
            OrthogonalRoomValidationResult loaded = input.LoadBoundary(
                L01Lines(),
                600.0,
                600.0,
                1.5,
                100.0);

            Assert.IsTrue(loaded.IsValid, loaded.ErrorMessage);
            Assert.AreEqual(1.5, input.GroutWidthMm,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, input.PlasterThicknessMm,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, input.Room.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1976.0, input.Room.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, input.Room.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(4376.0, input.Room.North,
                GeometryTolerance.Coordinate);

            input.SetControlRegion(
                new AxisAlignedRectangle(100.0, 1976.0, 100.0, 4376.0));
            input.SetControlDoor(
                new DoorOpening(RoomSide.North, 700.0, 1300.0));
            input.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);

            input.SetPlasterThickness(200.0);

            Assert.AreEqual(200.0, input.Room.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1876.0, input.Room.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(200.0, input.Room.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(4276.0, input.Room.North,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(200.0, input.ControlRegion.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1876.0, input.ControlRegion.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(200.0, input.ControlRegion.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(4276.0, input.ControlRegion.North,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(700.0, input.ControlDoor.AlongWallStart,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1300.0, input.ControlDoor.AlongWallEnd,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void PlasterInputSynchronizesConcaveBoundaryAttachedData()
        {
            var input = new OrthogonalDecisionInputSession();
            OrthogonalRoomValidationResult loaded = input.LoadBoundary(
                L01Lines(),
                600.0,
                600.0);

            Assert.IsTrue(loaded.IsValid, loaded.ErrorMessage);
            input.SetControlRegion(
                new AxisAlignedRectangle(200.0, 2076.0, 3476.0, 4476.0));
            input.SetControlDoor(
                new DoorOpening(RoomSide.North, 200.0, 2076.0));
            input.SetConnectionEdge(
                L(200.0, 4476.0, 2076.0, 4476.0));

            input.SetPlasterThickness(100.0);

            Assert.AreEqual(300.0, input.ControlRegion.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1976.0, input.ControlRegion.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(3476.0, input.ControlRegion.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(4376.0, input.ControlRegion.North,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(300.0, input.ControlDoor.AlongWallStart,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1976.0, input.ControlDoor.AlongWallEnd,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(input.SelectedConnectionEdge.HasValue);
            Assert.AreEqual(300.0, input.SelectedConnectionEdge.Value.Start.X,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(4376.0, input.SelectedConnectionEdge.Value.Start.Y,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1976.0, input.SelectedConnectionEdge.Value.End.X,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(4376.0, input.SelectedConnectionEdge.Value.End.Y,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void InvalidPlasterInputClearsFinishedFaceAndCandidates()
        {
            var input = new OrthogonalDecisionInputSession();
            OrthogonalRoomValidationResult invalid = input.LoadBoundary(
                L01Lines(),
                600.0,
                600.0,
                1.5,
                1100.0);

            Assert.IsFalse(invalid.IsValid);
            Assert.AreEqual(
                OrthogonalRoomValidationError.InvalidFinishedFace,
                invalid.Error);
            Assert.IsFalse(input.HasRoom);
            Assert.IsNull(input.Result);
            StringAssert.Contains(input.FinishedFaceErrorMessage, "finished face");
        }

        private static OrthogonalDecisionInputSession L04Session(
            double upperWidth,
            double lowerWidth)
        {
            var input = new OrthogonalDecisionInputSession();
            input.LoadBoundary(
                PolygonLines(
                    P(0, 0),
                    P(lowerWidth, 0),
                    P(lowerWidth, 1774),
                    P(upperWidth, 1774),
                    P(upperWidth, 3526),
                    P(0, 3526)),
                600,
                600);
            var main = new AxisAlignedRectangle(
                0,
                upperWidth,
                1774,
                3526);
            input.SetMainSecondary(
                new MainSecondaryRegionDefinition(
                    main,
                    new AxisAlignedRectangle(0, lowerWidth, 0, 1774)));
            input.SetControlDoor(
                new DoorOpening(RoomSide.East, 2800, 3400));
            input.SetConnectionEdge(
                L(0, 1774, upperWidth, 1774));
            return input;
        }

        private static void AssertRequirement(
            OrthogonalDecisionInputSession input,
            DecisionRequirementCode code)
        {
            Assert.IsTrue(
                input.Result.Requirements.Any(requirement =>
                    requirement.Code == code),
                "Expected requirement " + code + ".");
        }

        private static string[] StableSnapshot(
            EngineeringOrthogonalDecisionResult result)
        {
            return result.Candidates.Select(candidate =>
                candidate.Id
                    + ":"
                    + candidate.State
                    + ":"
                    + OrthogonalDecisionPaletteText.FormatCandidate(candidate))
                .ToArray();
        }

        private static IReadOnlyCollection<LineSegment3D> L01Lines()
        {
            return PolygonLines(
                P(0, 0),
                P(2076, 0),
                P(2076, 4476),
                P(200, 4476),
                P(200, 3476),
                P(0, 3476));
        }

        private static IReadOnlyCollection<LineSegment3D> PolygonLines(
            params Point3D[] vertices)
        {
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(
                    new LineSegment3D(
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
