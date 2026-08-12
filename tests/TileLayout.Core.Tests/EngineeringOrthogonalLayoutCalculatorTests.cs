using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class EngineeringOrthogonalLayoutCalculatorTests
    {
        [TestMethod]
        public void L01_WholeRoomPhase_ProducesContinuousLShapedFootprint()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 2076, 0, 4476),
                new DoorOpening(RoomSide.North, 700, 1300));

            LayoutCandidate candidate = Find(result, "whole-door-default");
            TileFootprint irregular = candidate.Tiles.Single(
                tile => tile.IsContinuousIrregular
                    && Nearly(tile.NominalWidth, 600)
                    && Nearly(tile.NominalHeight, 600));

            Assert.AreEqual(6, irregular.Outline.Count);
            Assert.IsFalse(irregular.IsFullTile);
            Assert.AreEqual(1L, candidate.Metrics.ContinuousIrregularTileCount);
            Assert.AreEqual(0L, candidate.Metrics.BelowDefaultMinimumBoundaryTileCount);
            Assert.IsTrue(candidate.Structure.UsesWholeRoomSinglePhase);
        }

        [TestMethod]
        public void NarrowRemaindersKeepHalfTransitionRuleOnBothAxesWhenPreferenceIsOff()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(1976, 0), P(1976, 1976), P(0, 1976));
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 1976, 0, 1976),
                new DoorOpening(RoomSide.West, 700, 1300));

            LayoutCandidate candidate = Find(result, "whole-door-default");
            BoundaryBandPlan xPlan = candidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan = candidate.GetAxisPlan(TileLayoutAxis.Y);

            Assert.IsFalse(candidate.IsRejected);
            Assert.IsFalse(result.Parameters.PreferWallCornerAlignment);
            Assert.IsTrue(xPlan.UsesRedistribution);
            Assert.IsTrue(yPlan.UsesRedistribution);
            CollectionAssert.AreEqual(
                new[] { 476.0, 600.0, 600.0, 300.0 },
                xPlan.SegmentWidths.ToArray());
            CollectionAssert.AreEqual(
                new[] { 476.0, 600.0, 600.0, 300.0 },
                yPlan.SegmentWidths.ToArray());
            Assert.AreEqual(RoomSide.East, xPlan.HalfTileSide);
            Assert.AreEqual(RoomSide.West, xPlan.TransitionTileSide);
            Assert.AreEqual(RoomSide.North, yPlan.HalfTileSide);
            Assert.AreEqual(RoomSide.South, yPlan.TransitionTileSide);
        }

        [TestMethod]
        public void ComplexRoomWithGroutKeepsSearchingAfterAnUnusableTileBody()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom();
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                ComplexOrthogonalBoundaryFixture.CreateControlRegion(),
                ComplexOrthogonalBoundaryFixture.CreateDeterministicWestDoor(),
                groutWidthMm: 1.5);

            Assert.IsTrue(result.Candidates.Count > 0);
            Assert.IsTrue(result.Candidates.Any(candidate =>
                !candidate.IsRejected));
            Assert.IsTrue(result.Candidates.Any(candidate =>
                candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code ==
                        CandidateDiagnosticCode.GroutTileBodyUnavailable)));
        }

        [TestMethod]
        public void CompleteRoomNarrowAlongWallRemainderUsesHalfTransitionPattern()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(1976, 0), P(1976, 1025.555),
                P(0, 1025.555));
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 1800, 0, 1200),
                new DoorOpening(RoomSide.South, 700, 1300));

            Assert.AreEqual(1976.0, room.Width, GeometryTolerance.Coordinate);
            Assert.AreEqual(1025.555, room.Height, GeometryTolerance.Coordinate);
            LayoutCandidate candidate = Find(
                result,
                "whole-door-default-room-boundary-redistributed");
            BoundaryBandPlan xPlan = candidate.GetAxisPlan(TileLayoutAxis.X);

            Assert.IsFalse(result.Parameters.PreferWallCornerAlignment);
            Assert.IsTrue(xPlan.UsesRedistribution);
            Assert.AreEqual(176.0,
                xPlan.NaturalRemainder,
                GeometryTolerance.Coordinate);
            CollectionAssert.AreEqual(
                new[] { 476.0, 600.0, 600.0, 300.0 },
                xPlan.SegmentWidths.ToArray());
            Assert.AreEqual(RoomSide.East, xPlan.HalfTileSide);
            Assert.AreEqual(RoomSide.West, xPlan.TransitionTileSide);
        }

        [TestMethod]
        public void OrthogonalClipRecoveryCanRedistributeRoomEnvelopeOnBothAxes()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(1976, 0), P(1976, 1976), P(200, 1976),
                P(200, 1776), P(0, 1776));
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 1976, 0, 1776),
                new DoorOpening(RoomSide.West, 700, 1300));

            LayoutCandidate recovered = Find(
                result,
                "whole-door-default-room-boundary-redistributed");
            Assert.IsFalse(recovered.IsRejected);
            Assert.AreEqual(0L,
                recovered.Metrics.BelowProjectAbsoluteMinimumBoundaryTileCount);
            Assert.IsTrue(recovered.GetAxisPlan(TileLayoutAxis.X)
                .UsesRedistribution);
            Assert.IsTrue(recovered.GetAxisPlan(TileLayoutAxis.Y)
                .UsesRedistribution);
            CollectionAssert.AreEqual(
                new[] { 476.0, 600.0, 600.0, 300.0 },
                recovered.GetAxisPlan(TileLayoutAxis.X).SegmentWidths.ToArray());
            CollectionAssert.AreEqual(
                new[] { 476.0, 600.0, 600.0, 300.0 },
                recovered.GetAxisPlan(TileLayoutAxis.Y).SegmentWidths.ToArray());
        }

        [TestMethod]
        public void L01_EastDoor_RecoversClippedNarrowBandWithFrozenRedistribution()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 2076, 0, 4476),
                new DoorOpening(RoomSide.East, 2800, 3400));

            LayoutCandidate rejected = Find(result, "whole-door-default");
            LayoutCandidate recovered = Find(
                result,
                "whole-door-default-orthogonal-redistributed");
            BoundaryBandPlan xPlan = recovered.GetAxisPlan(TileLayoutAxis.X);

            Assert.IsFalse(rejected.IsRejected);
            Assert.IsTrue(rejected.RequiresProjectPolicy);
            Assert.AreEqual(76.0,
                rejected.Metrics.MinimumBoundaryBandWidth,
                GeometryTolerance.Coordinate);
            Assert.IsFalse(recovered.IsRejected);
            Assert.IsFalse(result.HasUniqueAutomaticSelection);
            CollectionAssert.AreEqual(
                new[] { 576.0, 600.0, 600.0, 300.0 },
                xPlan.SegmentWidths.ToArray());
            Assert.AreEqual(RoomSide.East, xPlan.HalfTileSide);
            Assert.AreEqual(RoomSide.West, xPlan.TransitionTileSide);
            Assert.AreEqual(276.0,
                recovered.Metrics.MinimumBoundaryBandWidth,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(recovered.Tiles.Any(tile =>
                Nearly(tile.NominalWidth, 376.0)));
            Assert.AreEqual(0L,
                recovered.Metrics.BelowDefaultMinimumBoundaryTileCount);
            Assert.IsTrue(recovered.Diagnostics.Any(diagnostic =>
                diagnostic.Code ==
                    CandidateDiagnosticCode.OrthogonalClipRedistributed));
            CollectionAssert.AreEqual(
                new[] { 576.0, 1176.0, 1776.0 },
                recovered.Structure.Regions[0].VerticalCuts.ToArray());
        }

        [TestMethod]
        public void L01_NorthAndSouthRightHandDoors_KeepWestFullTileAfterClipping()
        {
            foreach (RoomSide wall in new[]
            {
                RoomSide.North,
                RoomSide.South
            })
            {
                AxisAlignedOrthogonalPolygon room = Room(
                    P(0, 0), P(2076, 0), P(2076, 4476),
                    P(200, 4476), P(200, 3476), P(0, 3476));
                EngineeringOrthogonalLayoutResult result = Calculate(
                    room,
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new DoorOpening(wall, 1300, 1900));

                LayoutCandidate rejected = Find(result, "whole-door-default");
                LayoutCandidate recovered = Find(
                    result,
                    "whole-door-default-orthogonal-along-wall-flipped");
                BoundaryBandPlan xPlan =
                    recovered.GetAxisPlan(TileLayoutAxis.X);

                Assert.IsFalse(rejected.IsRejected, wall.ToString());
                Assert.IsTrue(rejected.RequiresProjectPolicy, wall.ToString());
                Assert.AreEqual(76.0,
                    rejected.Metrics.MinimumBoundaryBandWidth,
                    GeometryTolerance.Coordinate,
                    wall.ToString());
                Assert.IsFalse(recovered.IsRejected, wall.ToString());
                Assert.IsFalse(result.HasUniqueAutomaticSelection,
                    wall.ToString());
                CollectionAssert.AreEqual(
                    new[] { 600.0, 600.0, 600.0, 276.0 },
                    xPlan.SegmentWidths.ToArray(),
                    wall.ToString());
                Assert.AreEqual(RoomSide.West, xPlan.ControlSide,
                    wall.ToString());
                Assert.AreEqual(276.0,
                    recovered.Metrics.MinimumBoundaryBandWidth,
                    GeometryTolerance.Coordinate,
                    wall.ToString());
                Assert.AreEqual(0L,
                    recovered.Metrics.BelowDefaultMinimumBoundaryTileCount,
                    wall.ToString());
                Assert.IsTrue(recovered.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == CandidateDiagnosticCode
                        .OrthogonalClipAlongWallFlipped),
                    wall.ToString());
                CollectionAssert.AreEqual(
                    new[] { 600.0, 1200.0, 1800.0 },
                    recovered.Structure.Regions[0].VerticalCuts.ToArray(),
                    wall.ToString());
            }
        }

        [TestMethod]
        public void L03_WholeRoomPhase_ChecksBothOverallAndRecessedEastBands()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2827, 0), P(2827, 2026),
                P(2676, 2026), P(2676, 2776), P(0, 2776));
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 2827, 0, 2776),
                new DoorOpening(RoomSide.West, 100, 700));

            LayoutCandidate candidate = Find(result, "whole-door-default");
            Assert.IsTrue(candidate.Tiles.Any(
                tile => !tile.IsContinuousIrregular
                    && Nearly(tile.NominalWidth, 427)));
            Assert.IsTrue(candidate.Tiles.Any(
                tile => !tile.IsContinuousIrregular
                    && Nearly(tile.NominalWidth, 276)));
            Assert.AreEqual(0L, candidate.Metrics.BelowDefaultMinimumBoundaryTileCount);
        }

        [TestMethod]
        [DataRow(2086.0, 2486.0, 286.0, 400.0)]
        [DataRow(1976.0, 2276.0, 300.0, 300.0)]
        [DataRow(1976.0, 2376.0, 300.0, 400.0)]
        public void L04ABC_MainSecondary_InheritsParallelAndResetsPerpendicular(
            double upperWidth,
            double lowerWidth,
            double firstCut,
            double protrusion)
        {
            EngineeringOrthogonalLayoutResult result = L04(
                upperWidth,
                lowerWidth);
            LayoutCandidate candidate = result.Candidates.First(
                item => item.Structure.Kind == OrthogonalCandidateKind.MainSecondary
                    && item.Structure.Connections[0].ProtrusionTreatment
                        == ProtrusionBandTreatment.Independent);
            LayoutRegionPhase main = candidate.Structure.Regions.Single(
                region => region.Role == LayoutRegionRole.Main);
            LayoutRegionPhase secondary = candidate.Structure.Regions.Single(
                region => region.Role == LayoutRegionRole.Secondary);

            Assert.IsTrue(main.VerticalCuts.Any(cut => Nearly(cut, firstCut)));
            Assert.IsTrue(secondary.VerticalCuts.Any(cut => Nearly(cut, firstCut)));
            Assert.IsTrue(secondary.VerticalCuts.Any(
                cut => Nearly(cut, upperWidth)));
            Assert.IsTrue(main.HorizontalCuts.SequenceEqual(
                new[] { 2326.0, 2926.0 }));
            Assert.IsTrue(secondary.HorizontalCuts.SequenceEqual(
                new[] { 574.0, 1174.0 }));
            Assert.AreEqual(1, candidate.Metrics.PhaseResetCount);
            Assert.AreEqual(protrusion,
                candidate.Structure.Connections[0].ProtrusionWidth,
                GeometryTolerance.Coordinate);
            Assert.IsFalse(candidate.IsRejected);
        }

        [TestMethod]
        public void L04D_Unabsorbable200Band_IsRetainedAsPolicyUndecided()
        {
            EngineeringOrthogonalLayoutResult result = L04(1690, 1890);
            LayoutCandidate candidate = result.Candidates.Single(
                item => item.Structure.Kind == OrthogonalCandidateKind.MainSecondary);

            Assert.AreEqual(
                ProtrusionBandTreatment.Independent,
                candidate.Structure.Connections[0].ProtrusionTreatment);
            Assert.IsFalse(candidate.IsRejected);
            Assert.IsTrue(candidate.RequiresPolicyDecision);
            Assert.IsTrue(candidate.Diagnostics.Any(
                diagnostic => diagnostic.Code ==
                    CandidateDiagnosticCode.ProtrusionBandCannotBeAbsorbed
                    && Nearly(diagnostic.ActualValue.Value, 800)));
            CandidateDiagnostic unresolved = candidate.Diagnostics.Single(
                diagnostic => diagnostic.Code ==
                    CandidateDiagnosticCode.BelowDefaultMinimumRequiresPolicy);
            Assert.IsFalse(unresolved.Threshold.HasValue);
            Assert.IsTrue(candidate.Tiles.Any(
                tile => Nearly(tile.NominalWidth, 200)));
        }

        [TestMethod]
        public void L04E_Enumerates300Plus200Absorption_AndRejectsIndependentBand()
        {
            EngineeringOrthogonalLayoutResult result = L04(1876, 2076);
            LayoutCandidate absorbed = result.Candidates.Single(
                item => item.Structure.Kind == OrthogonalCandidateKind.MainSecondary
                    && item.Id.EndsWith("absorbed-mirrored", StringComparison.Ordinal));
            LayoutCandidate independent = result.Candidates.Single(
                item => item.Structure.Kind == OrthogonalCandidateKind.MainSecondary
                    && item.Structure.Connections[0].ProtrusionTreatment
                        == ProtrusionBandTreatment.Independent);

            Assert.AreEqual(500.0,
                absorbed.Structure.Connections[0].AbsorbedWidth,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(absorbed.Tiles.Any(
                tile => Nearly(tile.NominalWidth, 500)));
            Assert.IsFalse(absorbed.IsRejected);
            Assert.IsFalse(independent.IsRejected);
            Assert.IsTrue(independent.RequiresProjectPolicy);
            Assert.IsTrue(independent.Tiles.Any(
                tile => Nearly(tile.NominalWidth, 200)));
        }

        [TestMethod]
        public void L04E_SixHundredPlusTwoHundred_IsNotAbsorbable()
        {
            EngineeringOrthogonalLayoutResult result = L04(1690, 1890);

            Assert.IsFalse(result.Candidates.Any(
                item => item.Structure.Kind == OrthogonalCandidateKind.MainSecondary
                    && item.Structure.Connections[0].ProtrusionTreatment
                        == ProtrusionBandTreatment.Absorbed));
            Assert.IsTrue(result.Candidates[1].Diagnostics.Any(
                diagnostic => diagnostic.Code ==
                    CandidateDiagnosticCode.ProtrusionBandCannotBeAbsorbed
                    && Nearly(diagnostic.ActualValue.Value, 800)));
        }

        [TestMethod]
        public void L05_ConfirmedCentralField_Records250ExceptionWithoutUniqueScore()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(5676, 0), P(5676, 5176),
                P(3650, 5176), P(3650, 3726), P(0, 3726));
            var phase = new ConfirmedGridPhase(
                "l05",
                300,
                376,
                "Confirmed central full-tile field.",
                true);
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 5676, 0, 5176),
                new DoorOpening(RoomSide.North, 4100, 4900),
                null,
                new List<ConfirmedGridPhase> { phase });
            LayoutCandidate candidate = Find(result, "whole-confirmed-l05");

            Assert.AreEqual(0L, candidate.Metrics.InteriorNonFullTileCount);
            Assert.IsTrue(candidate.Metrics.BelowDefaultMinimumBoundaryTileCount > 0);
            Assert.AreEqual(250.0,
                candidate.Metrics.MinimumBoundaryBandWidth,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(candidate.RequiresPolicyDecision);
            Assert.IsFalse(result.HasUniqueAutomaticSelection);
            Assert.IsFalse(candidate.Diagnostics.Single(
                diagnostic => diagnostic.Code ==
                    CandidateDiagnosticCode.BelowDefaultMinimumRequiresPolicy)
                .Threshold.HasValue);
        }

        [TestMethod]
        public void P01_ConfirmedPhase_PreservesNaturalAlignmentsAndIrregularCornerTile()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2176, 0), P(2176, 4226),
                P(150, 4226), P(150, 4126), P(0, 4126),
                P(0, 3550), P(450, 3550), P(450, 2126), P(0, 2126));
            var phase = new ConfirmedGridPhase(
                "p01",
                450,
                326,
                "Confirmed natural alignments without wall-joint recognition.");
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 2176, 0, 4226),
                new DoorOpening(RoomSide.South, 1200, 1900),
                null,
                new List<ConfirmedGridPhase> { phase });
            LayoutCandidate candidate = Find(result, "whole-confirmed-p01");

            Assert.IsTrue(candidate.Structure.Regions[0].VerticalCuts.Contains(450));
            Assert.IsTrue(candidate.Structure.Regions[0].HorizontalCuts.Contains(2126));
            Assert.IsFalse(candidate.Structure.Regions[0].HorizontalCuts.Contains(3550));
            Assert.IsTrue(candidate.Tiles.Any(
                tile => tile.IsContinuousIrregular
                    && Nearly(tile.NominalWidth, 450)
                    && Nearly(tile.NominalHeight, 300)));
            Assert.AreEqual(0L, candidate.Metrics.BelowDefaultMinimumBoundaryTileCount);
        }

        [TestMethod]
        public void MirroredL01_ProducesEquivalentMetricsAndTileCounts()
        {
            AxisAlignedOrthogonalPolygon original = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            AxisAlignedOrthogonalPolygon mirrored = Room(
                P(0, 0), P(2076, 0), P(2076, 3476),
                P(1876, 3476), P(1876, 4476), P(0, 4476));
            LayoutCandidate first = Find(
                Calculate(
                    original,
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new DoorOpening(RoomSide.North, 700, 1300)),
                "whole-door-default");
            LayoutCandidate second = Find(
                Calculate(
                    mirrored,
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new DoorOpening(RoomSide.North, 776, 1376)),
                "whole-door-default");

            Assert.AreEqual(first.Tiles.Count, second.Tiles.Count);
            Assert.AreEqual(
                first.Metrics.ContinuousIrregularTileCount,
                second.Metrics.ContinuousIrregularTileCount);
            Assert.AreEqual(
                first.Metrics.BelowDefaultMinimumBoundaryTileCount,
                second.Metrics.BelowDefaultMinimumBoundaryTileCount);
        }

        [TestMethod]
        public void RotatedL04E_PreservesAbsorptionAndPhaseResetSemantics()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(0, 2076), P(1774, 2076),
                P(1774, 1876), P(3526, 1876), P(3526, 0));
            var main = new AxisAlignedRectangle(1774, 3526, 0, 1876);
            var secondary = new AxisAlignedRectangle(0, 1774, 0, 2076);
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                main,
                new DoorOpening(RoomSide.North, 2800, 3400),
                new MainSecondaryRegionDefinition(main, secondary));
            LayoutCandidate absorbed = result.Candidates.Single(
                item => item.Id.EndsWith(
                    "absorbed-mirrored",
                    StringComparison.Ordinal));

            Assert.AreEqual(TileLayoutAxis.Y,
                absorbed.Structure.Connections[0].ParallelAxis);
            Assert.AreEqual(RoomSide.North,
                absorbed.Structure.Connections[0].ProtrusionSide);
            Assert.AreEqual(500.0,
                absorbed.Structure.Connections[0].AbsorbedWidth,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1, absorbed.Metrics.PhaseResetCount);
        }

        [TestMethod]
        public void ReorderedBoundaryAndLargeWcs_AreDeterministic()
        {
            const double offset = 1e12;
            Point3D[] vertices =
            {
                P(offset, offset), P(offset + 2076, offset),
                P(offset + 2076, offset + 4476),
                P(offset + 200, offset + 4476),
                P(offset + 200, offset + 3476),
                P(offset, offset + 3476)
            };
            LineSegment3D[] ordered =
                OrthogonalRoomValidatorTests.LinesFromVertices(vertices);
            var reordered = new[]
            {
                Reverse(ordered[4]), Reverse(ordered[1]),
                Reverse(ordered[5]), Reverse(ordered[2]),
                Reverse(ordered[0]), Reverse(ordered[3])
            };
            AxisAlignedOrthogonalPolygon firstRoom =
                OrthogonalRoomValidator.Validate(ordered).Room;
            AxisAlignedOrthogonalPolygon secondRoom =
                OrthogonalRoomValidator.Validate(reordered).Room;
            var control = new AxisAlignedRectangle(
                offset, offset + 2076, offset, offset + 4476);
            var door = new DoorOpening(
                RoomSide.North,
                offset + 700,
                offset + 1300);
            LayoutCandidate first = Find(Calculate(firstRoom, control, door),
                "whole-door-default");
            LayoutCandidate second = Find(Calculate(secondRoom, control, door),
                "whole-door-default");

            AssertCandidateGeometryEqual(first, second);
        }

        [TestMethod]
        public void MultipleCandidateModes_AreRetainedWithoutTotalScore()
        {
            EngineeringOrthogonalLayoutResult result = L04(1876, 2076);

            Assert.IsTrue(result.HasMultipleRetainedCandidates);
            Assert.IsFalse(result.HasUniqueAutomaticSelection);
            Assert.IsTrue(result.Candidates.Any(
                candidate => candidate.Structure.Kind ==
                    OrthogonalCandidateKind.WholeRoomSinglePhase));
            Assert.IsTrue(result.Candidates.Any(
                candidate => candidate.Structure.Kind ==
                    OrthogonalCandidateKind.MainSecondary));
            Assert.IsTrue(result.Candidates.Where(candidate => !candidate.IsRejected)
                .All(candidate => candidate.RequiresPolicyDecision));
        }

        [TestMethod]
        public void RepeatedCalculation_PreservesCandidateAndFootprintOrder()
        {
            EngineeringOrthogonalLayoutResult first = L04(1976, 2376);
            EngineeringOrthogonalLayoutResult second = L04(1976, 2376);

            CollectionAssert.AreEqual(
                first.Candidates.Select(candidate => candidate.Id).ToArray(),
                second.Candidates.Select(candidate => candidate.Id).ToArray());
            for (int candidateIndex = 0;
                candidateIndex < first.Candidates.Count;
                candidateIndex++)
            {
                LayoutCandidate firstCandidate = first.Candidates[candidateIndex];
                LayoutCandidate secondCandidate = second.Candidates[candidateIndex];
                AssertCandidateGeometryEqual(firstCandidate, secondCandidate);
                for (int tileIndex = 0;
                    tileIndex < firstCandidate.Tiles.Count;
                    tileIndex++)
                {
                    TileFootprint firstTile = firstCandidate.Tiles[tileIndex];
                    TileFootprint secondTile = secondCandidate.Tiles[tileIndex];
                    Assert.AreEqual(firstTile.Outline.Count, secondTile.Outline.Count);
                    for (int pointIndex = 0;
                        pointIndex < firstTile.Outline.Count;
                        pointIndex++)
                    {
                        Assert.AreEqual(
                            firstTile.Outline[pointIndex].X,
                            secondTile.Outline[pointIndex].X,
                            GeometryTolerance.Coordinate);
                        Assert.AreEqual(
                            firstTile.Outline[pointIndex].Y,
                            secondTile.Outline[pointIndex].Y,
                            GeometryTolerance.Coordinate);
                    }
                }
            }
        }

        [TestMethod]
        public void ResourceLimit_Exactly10000Fragments_IsAllowed()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(10001, 0), P(10001, 0.5), P(0, 0.5));

            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 10001, 0, 0.5),
                new DoorOpening(RoomSide.South, 100, 200),
                null,
                null,
                1,
                1);

            Assert.AreEqual(10000, Find(result, "whole-door-default").DivisionLines.Count);
        }

        [TestMethod]
        public void ResourceLimit_10001Fragments_IsRejectedBeforeFootprints()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(10002, 0), P(10002, 0.5), P(0, 0.5));

            try
            {
                Calculate(
                    room,
                    new AxisAlignedRectangle(0, 10002, 0, 0.5),
                    new DoorOpening(RoomSide.South, 100, 200),
                    null,
                    null,
                    1,
                    1);
                Assert.Fail("Expected the 10,001-fragment candidate to be rejected.");
            }
            catch (TileLayoutLimitExceededException error)
            {
                Assert.AreEqual(10001.0, error.EstimatedDivisionLineCount);
            }
        }

        private static EngineeringOrthogonalLayoutResult L04(
            double upperWidth,
            double lowerWidth)
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(lowerWidth, 0), P(lowerWidth, 1774),
                P(upperWidth, 1774), P(upperWidth, 3526), P(0, 3526));
            var main = new AxisAlignedRectangle(0, upperWidth, 1774, 3526);
            var secondary = new AxisAlignedRectangle(0, lowerWidth, 0, 1774);
            return Calculate(
                room,
                main,
                new DoorOpening(RoomSide.East, 2800, 3400),
                new MainSecondaryRegionDefinition(main, secondary));
        }

        private static EngineeringOrthogonalLayoutResult Calculate(
            AxisAlignedOrthogonalPolygon room,
            AxisAlignedRectangle control,
            DoorOpening door,
            MainSecondaryRegionDefinition regions = null,
            IList<ConfirmedGridPhase> phases = null,
            double tileWidth = 600,
            double tileHeight = 600,
            double groutWidthMm = 0.0)
        {
            return EngineeringOrthogonalLayoutCalculator.Calculate(
                room,
                new EngineeringOrthogonalLayoutParameters(
                    tileWidth,
                    tileHeight,
                    control,
                    door,
                    regions,
                    phases,
                    null,
                    false,
                    groutWidthMm));
        }

        private static AxisAlignedOrthogonalPolygon Room(params Point3D[] vertices)
        {
            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(
                OrthogonalRoomValidatorTests.LinesFromVertices(vertices));
            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            return result.Room;
        }

        private static LayoutCandidate Find(
            EngineeringOrthogonalLayoutResult result,
            string id)
        {
            return result.Candidates.Single(candidate => candidate.Id == id);
        }

        private static Point3D P(double x, double y)
        {
            return new Point3D(x, y);
        }

        private static LineSegment3D Reverse(LineSegment3D line)
        {
            return new LineSegment3D(line.End, line.Start);
        }

        private static bool Nearly(double first, double second)
        {
            return Math.Abs(first - second) <= GeometryTolerance.Coordinate;
        }

        private static void AssertCandidateGeometryEqual(
            LayoutCandidate first,
            LayoutCandidate second)
        {
            Assert.AreEqual(first.DivisionLines.Count, second.DivisionLines.Count);
            Assert.AreEqual(first.Tiles.Count, second.Tiles.Count);
            for (int index = 0; index < first.DivisionLines.Count; index++)
            {
                Assert.AreEqual(
                    first.DivisionLines[index].Start.X,
                    second.DivisionLines[index].Start.X,
                    GeometryTolerance.Coordinate);
                Assert.AreEqual(
                    first.DivisionLines[index].Start.Y,
                    second.DivisionLines[index].Start.Y,
                    GeometryTolerance.Coordinate);
                Assert.AreEqual(
                    first.DivisionLines[index].End.X,
                    second.DivisionLines[index].End.X,
                    GeometryTolerance.Coordinate);
                Assert.AreEqual(
                    first.DivisionLines[index].End.Y,
                    second.DivisionLines[index].End.Y,
                    GeometryTolerance.Coordinate);
            }
        }
    }
}
