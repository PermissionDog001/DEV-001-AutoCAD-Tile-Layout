using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class EngineeringOrthogonalDecisionCalculatorTests
    {
        [TestMethod]
        public void MissingAbsoluteMinimumBlocksOnlyWhenNoIndependentUsableCandidateExists()
        {
            EngineeringOrthogonalDecisionResult l04 = CalculateL04(1690, 1890, null, null);
            EngineeringOrthogonalDecisionResult l05 = CalculateL05(null, null);

            AssertProjectPolicyRequirement(l04);
            Assert.IsFalse(l05.Requirements.Any(requirement =>
                requirement.Code == DecisionRequirementCode.ProjectSecondAbsoluteMinimum));
            Assert.IsTrue(l05.Candidates.Any(candidate =>
                candidate.State == LayoutCandidateState.RequiresProjectPolicy));
            Assert.IsTrue(l05.Candidates.Any(candidate =>
                candidate.State == LayoutCandidateState.AutomaticUsable));
            Assert.IsTrue(l04.Candidates.All(candidate => candidate.State != LayoutCandidateState.AutomaticUsable));
        }

        [TestMethod]
        public void CompletedProfileChangesStateWithoutInventingApplicability()
        {
            Assert.AreEqual(EngineeringLayoutRules.DefaultMinimumCutRatio,
                new LayoutPolicyProfile("P-1").DefaultMinimumCutRatio,
                GeometryTolerance.Coordinate);
            EngineeringOrthogonalDecisionResult result = CalculateL04(
                1690, 1890, new LayoutPolicyProfile("P-1", 200), null);

            Assert.IsFalse(result.Requirements.Any(requirement => requirement.Code == DecisionRequirementCode.ProjectSecondAbsoluteMinimum));
            Assert.IsTrue(result.Candidates.Any(candidate => candidate.State == LayoutCandidateState.RequiresUserDecision));
            Assert.IsTrue(result.Requirements.Any(requirement => requirement.Code == DecisionRequirementCode.CandidateExceptionAcceptance));
        }

        [TestMethod]
        public void VisualConfirmationModeRetainsPolicyCandidatesWithoutMakingThemAutomatic()
        {
            var policy = new LayoutPolicyProfile(
                "P-1",
                null,
                ProjectAbsoluteMinimumMode.VisualConfirmation);
            EngineeringOrthogonalDecisionResult result = CalculateL04(
                1690,
                1890,
                policy,
                null);

            Assert.IsTrue(policy.AllowsVisualConfirmation);
            Assert.IsTrue(result.AllowsVisualConfirmation);
            Assert.IsFalse(result.Requirements.Any(requirement =>
                requirement.Code
                    == DecisionRequirementCode.ProjectSecondAbsoluteMinimum));
            Assert.IsTrue(result.Candidates.Any(candidate =>
                candidate.State == LayoutCandidateState.RequiresProjectPolicy));
            Assert.IsFalse(result.CanProceedAutomatically);
        }

        [TestMethod]
        public void MissingMainSecondarySemanticsReturnsRoomRequirementThenReusesDor2()
        {
            AxisAlignedOrthogonalPolygon room = L04Room(1876, 2076);
            var main = new AxisAlignedRectangle(0, 1876, 1774, 3526);
            var missing = new RoomDecision(main, new DoorOpening(RoomSide.East, 2800, 3400), RoomLayoutIntent.MainSecondary);
            EngineeringOrthogonalDecisionResult pending = Calculate(room, missing, null, null);
            Assert.IsTrue(pending.Requirements.Any(requirement => requirement.Code == DecisionRequirementCode.RoomMainSecondaryDefinition));

            var complete = new RoomDecision(main, new DoorOpening(RoomSide.East, 2800, 3400), RoomLayoutIntent.MainSecondary,
                new MainSecondaryRegionDefinition(main, new AxisAlignedRectangle(0, 2076, 0, 1774)),
                new LineSegment3D(new Point3D(0, 1774), new Point3D(1876, 1774)));
            EngineeringOrthogonalDecisionResult calculated = Calculate(room, complete, new LayoutPolicyProfile("P-1", 200), null);
            Assert.IsNotNull(calculated.RawResult);
            Assert.IsTrue(calculated.Candidates.Any(candidate => candidate.Candidate.Structure.Kind == OrthogonalCandidateKind.MainSecondary));
        }

        [TestMethod]
        public void MultipleCandidatesRequireSelectionWithoutScoreIncludingG3Anchors()
        {
            EngineeringOrthogonalDecisionResult multiple = CalculateL04(1876, 2076, new LayoutPolicyProfile("P-1", 200), null);
            Assert.IsTrue(multiple.Requirements.Any(requirement => requirement.Code == DecisionRequirementCode.CandidateSelection));
            Assert.IsFalse(multiple.Candidates.Any(candidate => candidate.Candidate.Metrics == null));

            AxisAlignedOrthogonalPolygon room = L01Room();
            var whole = new RoomDecision(new AxisAlignedRectangle(0, 2076, 0, 4476), new DoorOpening(RoomSide.North, 700, 1300), RoomLayoutIntent.WholeRoomSinglePhase);
            EngineeringOrthogonalDecisionResult unique = Calculate(
                room,
                whole,
                new LayoutPolicyProfile("P-1"),
                null,
                LayoutDecisionMode.Research,
                600,
                600,
                true);
            Assert.IsFalse(unique.CanProceedAutomatically);
            Assert.IsTrue(unique.Requirements.Any(requirement =>
                requirement.Code == DecisionRequirementCode.CandidateSelection));
            Assert.IsTrue(unique.Candidates.Count(candidate =>
                candidate.State == LayoutCandidateState.AutomaticUsable) > 1);
        }

        [TestMethod]
        public void ResearchPreservesManualExceptionButProductionNeverMarksItAutomatic()
        {
            var record = new DecisionRecord("whole-confirmed-l05", "P-1", "Research review accepted the documented exception.", true);
            EngineeringOrthogonalDecisionResult research = CalculateL05(new CandidateDecision(record), new LayoutPolicyProfile("P-1", 200));
            EngineeringOrthogonalDecisionResult production = Calculate(
                L05Room(), L05Decision(), new LayoutPolicyProfile("P-1", 200), new CandidateDecision(record), LayoutDecisionMode.ControlledProduction);

            Assert.IsNotNull(research.AppliedRecord);
            Assert.IsNotNull(production.AppliedRecord);
            Assert.IsFalse(research.CanProceedAutomatically);
            Assert.IsFalse(production.CanProceedAutomatically);
            Assert.AreEqual(LayoutCandidateState.RequiresUserDecision, research.Candidates.Single(candidate => candidate.Id == "whole-confirmed-l05").State);
        }

        [TestMethod]
        public void CandidateOrderRecordsAndStatesAreDeterministic()
        {
            EngineeringOrthogonalDecisionResult first = CalculateL04(1876, 2076, new LayoutPolicyProfile("P-1", 200), null);
            EngineeringOrthogonalDecisionResult second = CalculateL04(1876, 2076, new LayoutPolicyProfile("P-1", 200), null);
            CollectionAssert.AreEqual(first.Candidates.Select(candidate => candidate.Id).ToArray(), second.Candidates.Select(candidate => candidate.Id).ToArray());
            CollectionAssert.AreEqual(first.Requirements.Select(requirement => requirement.Code).ToArray(), second.Requirements.Select(requirement => requirement.Code).ToArray());
        }

        [TestMethod]
        public void UntrustedEliminatedAndUnsupportedStatesAreDistinct()
        {
            EngineeringOrthogonalDecisionResult untrusted = Calculate(L01Room(), new RoomDecision(new AxisAlignedRectangle(3000, 4000, 0, 1000), new DoorOpening(RoomSide.North, 700, 1300), RoomLayoutIntent.WholeRoomSinglePhase), null, null);
            EngineeringOrthogonalDecisionResult unsupported = Calculate(L01Room(), new RoomDecision(new AxisAlignedRectangle(0, 2076, 0, 4476), new DoorOpening(RoomSide.North, 700, 1300), RoomLayoutIntent.Unsupported), null, null);
            EngineeringOrthogonalDecisionResult eliminated = CalculateL04(
                1876, 2076, new LayoutPolicyProfile("hard-minimum", 251), null);

            Assert.AreEqual(LayoutCandidateState.InputUntrusted, untrusted.Candidates.Single().State);
            Assert.AreEqual(LayoutCandidateState.CapabilityUnsupported, unsupported.Candidates.Single().State);
            Assert.IsTrue(eliminated.Candidates.Any(candidate => candidate.State == LayoutCandidateState.Eliminated));
        }

        [TestMethod]
        public void ResourceLimit_10000And10001ArePreservedThroughDecisionProtocol()
        {
            RoomDecision decision = new RoomDecision(
                new AxisAlignedRectangle(0, 10001, 0, 0.5),
                new DoorOpening(RoomSide.South, 100, 200),
                RoomLayoutIntent.WholeRoomSinglePhase);
            EngineeringOrthogonalDecisionResult allowed = Calculate(
                Room(P(0, 0), P(10001, 0), P(10001, 0.5), P(0, 0.5)),
                decision, null, null, LayoutDecisionMode.Research, 1, 1);
            Assert.AreEqual(10000, allowed.RawResult.Candidates[0].DivisionLines.Count);

            try
            {
                Calculate(Room(P(0, 0), P(10002, 0), P(10002, 0.5), P(0, 0.5)),
                    new RoomDecision(new AxisAlignedRectangle(0, 10002, 0, 0.5),
                        new DoorOpening(RoomSide.South, 100, 200),
                        RoomLayoutIntent.WholeRoomSinglePhase), null, null,
                    LayoutDecisionMode.Research, 1, 1);
                Assert.Fail("Expected the unchanged DOR2 10,001 limit.");
            }
            catch (TileLayoutLimitExceededException error)
            {
                Assert.AreEqual(10001.0, error.EstimatedDivisionLineCount);
            }
        }

        [TestMethod]
        public void PlasterFinishedFaceIsCalculatedBeforeOrthogonalLayout()
        {
            AxisAlignedOrthogonalPolygon source = Room(
                P(0.0, 0.0),
                P(3600.0, 0.0),
                P(3600.0, 3000.0),
                P(0.0, 3000.0));
            var finishedFaceRegion = new AxisAlignedRectangle(
                100.0,
                3500.0,
                100.0,
                2900.0);
            EngineeringOrthogonalDecisionResult result =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        source,
                        600.0,
                        600.0,
                        new LayoutPolicyProfile("P-1"),
                        new RoomDecision(
                            finishedFaceRegion,
                            new DoorOpening(RoomSide.North, 700.0, 1300.0),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.Research,
                        false,
                        1.5,
                        100.0,
                        source));

            Assert.IsNotNull(result.RawResult);
            Assert.AreSame(source, result.RawResult.SourceRoom);
            Assert.AreEqual(100.0, result.RawResult.Room.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(3500.0, result.RawResult.Room.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, result.RawResult.Room.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(2900.0, result.RawResult.Room.North,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1.5,
                result.RawResult.Parameters.GroutWidthMm,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0,
                result.RawResult.Parameters.PlasterThicknessMm,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(result.Candidates.Any(candidate =>
                candidate.HasRawCandidate));
        }

        [TestMethod]
        public void InvalidFinishedFaceStopsCandidateCalculation()
        {
            AxisAlignedOrthogonalPolygon source = Room(
                P(0.0, 0.0),
                P(1000.0, 0.0),
                P(1000.0, 1000.0),
                P(0.0, 1000.0));
            var decision = new RoomDecision(
                new AxisAlignedRectangle(0.0, 1000.0, 0.0, 1000.0),
                new DoorOpening(RoomSide.North, 200.0, 800.0),
                RoomLayoutIntent.WholeRoomSinglePhase);
            EngineeringOrthogonalDecisionResult result =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        source,
                        600.0,
                        600.0,
                        null,
                        decision,
                        null,
                        LayoutDecisionMode.Research,
                        false,
                        0.0,
                        600.0,
                        source));

            Assert.IsNull(result.RawResult);
            Assert.AreEqual(0, result.Candidates.Count);
            Assert.IsTrue(result.Requirements.Any(requirement =>
                requirement.Code == DecisionRequirementCode.InputUntrusted));
        }

        private static void AssertProjectPolicyRequirement(EngineeringOrthogonalDecisionResult result)
        {
            Assert.IsTrue(result.Requirements.Any(requirement => requirement.Code == DecisionRequirementCode.ProjectSecondAbsoluteMinimum && requirement.Level == DecisionRequirementLevel.ProjectPolicy));
        }

        private static EngineeringOrthogonalDecisionResult CalculateL04(double upperWidth, double lowerWidth, LayoutPolicyProfile policy, CandidateDecision decision)
        {
            AxisAlignedOrthogonalPolygon room = L04Room(upperWidth, lowerWidth);
            var main = new AxisAlignedRectangle(0, upperWidth, 1774, 3526);
            var roomDecision = new RoomDecision(main, new DoorOpening(RoomSide.East, 2800, 3400), RoomLayoutIntent.MainSecondary,
                new MainSecondaryRegionDefinition(main, new AxisAlignedRectangle(0, lowerWidth, 0, 1774)),
                new LineSegment3D(new Point3D(0, 1774), new Point3D(upperWidth, 1774)));
            return Calculate(room, roomDecision, policy, decision);
        }

        private static EngineeringOrthogonalDecisionResult CalculateL05(CandidateDecision decision, LayoutPolicyProfile policy)
        {
            return Calculate(L05Room(), L05Decision(), policy, decision);
        }

        private static EngineeringOrthogonalDecisionResult Calculate(AxisAlignedOrthogonalPolygon room, RoomDecision decision, LayoutPolicyProfile policy, CandidateDecision candidateDecision, LayoutDecisionMode mode = LayoutDecisionMode.Research, double tileWidth = 600, double tileHeight = 600, bool preferWallCornerAlignment = false)
        {
            return EngineeringOrthogonalDecisionCalculator.Calculate(new EngineeringOrthogonalDecisionRequest(room, tileWidth, tileHeight, policy, decision, candidateDecision, mode, preferWallCornerAlignment));
        }

        private static RoomDecision L05Decision()
        {
            return new RoomDecision(new AxisAlignedRectangle(0, 5676, 0, 5176), new DoorOpening(RoomSide.North, 4100, 4900), RoomLayoutIntent.WholeRoomSinglePhase,
                null, null, new List<ConfirmedGridPhase> { new ConfirmedGridPhase("l05", 300, 376, "Confirmed central field.", true) });
        }

        private static AxisAlignedOrthogonalPolygon L01Room()
        {
            return Room(P(0, 0), P(2076, 0), P(2076, 4476), P(200, 4476), P(200, 3476), P(0, 3476));
        }

        private static AxisAlignedOrthogonalPolygon L05Room()
        {
            return Room(P(0, 0), P(5676, 0), P(5676, 5176), P(3650, 5176), P(3650, 3726), P(0, 3726));
        }

        private static AxisAlignedOrthogonalPolygon L04Room(double upper, double lower)
        {
            return Room(P(0, 0), P(lower, 0), P(lower, 1774), P(upper, 1774), P(upper, 3526), P(0, 3526));
        }
        private static AxisAlignedOrthogonalPolygon Room(params Point3D[] vertices)
        {
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(new LineSegment3D(vertices[index], vertices[(index + 1) % vertices.Length]));
            }

            return OrthogonalRoomValidator.Validate(lines).Room;
        }
        private static Point3D P(double x, double y)
        {
            return new Point3D(x, y);
        }
    }
}
