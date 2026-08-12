using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class EngineeringRectangularLayoutCalculatorTests
    {
        [TestMethod]
        public void Calculate_ArtificialSample01_PreservesNaturalDepthAndRedistributesAlongWall()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                3276.02,
                3126.67,
                RoomSide.West,
                2200.0,
                2800.0);

            Assert.IsTrue(result.IsSuccessful);
            Assert.AreEqual(1, result.ViableCandidates.Count);
            BoundaryBandPlan xPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.Y);

            Assert.AreEqual(DoorControlledAxisRole.DoorNormal, xPlan.Role);
            Assert.AreEqual(DoorControlledAxisRole.AlongWall, yPlan.Role);
            AssertBand(xPlan, RoomSide.West, 600.0, BoundaryBandKind.FullTile);
            AssertBand(
                xPlan,
                RoomSide.East,
                276.02,
                BoundaryBandKind.NaturalRemainder);
            Assert.IsFalse(xPlan.UsesRedistribution);

            AssertBand(
                yPlan,
                RoomSide.South,
                426.67,
                BoundaryBandKind.Transition);
            AssertBand(
                yPlan,
                RoomSide.North,
                300.0,
                BoundaryBandKind.HalfTile);
            Assert.AreEqual(RoomSide.North, yPlan.ConstructionStartSide);
            Assert.AreEqual(10, result.DefaultCandidate.DivisionLines.Count);
            AssertVertical(result.DefaultCandidate.DivisionLines[0], 600.0);
            AssertHorizontal(
                result.DefaultCandidate.DivisionLines[5],
                426.67);
        }

        [TestMethod]
        public void Calculate_GroutAddsPitchButKeepsNominalTileAndWallHalfGap()
        {
            var parameters = new EngineeringRectangularLayoutParameters(
                600.0,
                600.0,
                new DoorOpening(RoomSide.West, 700.0, 1300.0),
                1.5);
            EngineeringRectangularLayoutResult result =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(
                        0.0,
                        1804.5,
                        0.0,
                        1804.5),
                    parameters);

            Assert.IsTrue(result.IsSuccessful);
            BoundaryBandPlan xPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.Y);
            Assert.AreEqual(601.5, xPlan.GridTileSize,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(601.5, yPlan.GridTileSize,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(3, xPlan.SegmentWidths.Count);
            Assert.AreEqual(3, yPlan.SegmentWidths.Count);
            Assert.IsTrue(xPlan.SegmentWidths.All(width =>
                Math.Abs(width - 601.5)
                    <= GeometryTolerance.Coordinate));
            Assert.IsTrue(yPlan.SegmentWidths.All(width =>
                Math.Abs(width - 601.5)
                    <= GeometryTolerance.Coordinate));

            TileFootprint firstTile = result.DefaultCandidate.Tiles[0];
            Assert.AreEqual(600.0, firstTile.NominalWidth,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.0, firstTile.NominalHeight,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(0.75, firstTile.Outline.Min(point => point.X),
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.75, firstTile.Outline.Max(point => point.X),
                GeometryTolerance.Coordinate);
            Assert.AreEqual(0.75, firstTile.Outline.Min(point => point.Y),
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.75, firstTile.Outline.Max(point => point.Y),
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void Calculate_GroutOccupancyIsNotCountedAsBoundaryCut()
        {
            var parameters = new EngineeringRectangularLayoutParameters(
                600.0,
                600.0,
                new DoorOpening(RoomSide.West, 700.0, 1300.0),
                1.5);
            EngineeringRectangularLayoutResult result =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(
                        0.0,
                        1504.5,
                        0.0,
                        1804.5),
                    parameters);

            Assert.IsTrue(result.IsSuccessful);
            BoundaryBandPlan xPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            Assert.AreEqual(300.0,
                xPlan.GetBoundary(RoomSide.East).Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(0L,
                result.DefaultCandidate.Metrics
                    .BelowDefaultMinimumBoundaryTileCount);
        }

        [TestMethod]
        public void Calculate_ConfiguredMinimumCutRatioChangesBoundaryTreatment()
        {
            AxisAlignedRectangle room = new AxisAlignedRectangle(
                0.0,
                1460.0,
                0.0,
                1200.0);
            DoorOpening door = new DoorOpening(
                RoomSide.West,
                400.0,
                800.0);
            var legacyParameters = new EngineeringRectangularLayoutParameters(
                600.0,
                600.0,
                door);
            var configuredParameters = new EngineeringRectangularLayoutParameters(
                600.0,
                600.0,
                door,
                0.0,
                0.75);

            EngineeringRectangularLayoutResult legacy =
                EngineeringRectangularLayoutCalculator.Calculate(
                    room,
                    legacyParameters);
            EngineeringRectangularLayoutResult configured =
                EngineeringRectangularLayoutCalculator.Calculate(
                    room,
                    configuredParameters);
            BoundaryBandPlan legacyX =
                legacy.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan configuredX =
                configured.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);

            Assert.AreEqual(0.42, legacyX.RecommendedMinimumCutRatio,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(0.75, configuredX.RecommendedMinimumCutRatio,
                GeometryTolerance.Coordinate);
            Assert.IsFalse(legacyX.UsesRedistribution);
            Assert.IsTrue(configuredX.UsesRedistribution);
            Assert.AreEqual(450.0, configuredX.MinimumCut,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void Calculate_ArtificialSample02_AssignsDoorNormalAndAlongWallBands()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                3126.0,
                3076.0,
                RoomSide.West,
                200.0,
                800.0);

            BoundaryBandPlan xPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.Y);

            AssertBand(
                xPlan,
                RoomSide.West,
                426.0,
                BoundaryBandKind.Transition);
            AssertBand(
                xPlan,
                RoomSide.East,
                300.0,
                BoundaryBandKind.HalfTile);
            Assert.AreEqual(RoomSide.East, xPlan.ConstructionStartSide);

            AssertBand(
                yPlan,
                RoomSide.South,
                300.0,
                BoundaryBandKind.HalfTile);
            AssertBand(
                yPlan,
                RoomSide.North,
                376.0,
                BoundaryBandKind.Transition);
            Assert.AreEqual(RoomSide.South, yPlan.ConstructionStartSide);
            AssertVertical(result.DefaultCandidate.DivisionLines[0], 426.0);
            AssertHorizontal(result.DefaultCandidate.DivisionLines[5], 300.0);
        }

        [TestMethod]
        public void Calculate_FourDoorWalls_ReuseRotatedAndMirroredRules()
        {
            EngineeringRectangularLayoutResult west = Calculate(
                1300.0,
                1300.0,
                RoomSide.West,
                100.0,
                500.0);
            AssertBands(
                west.DefaultCandidate,
                400.0,
                300.0,
                300.0,
                400.0);

            EngineeringRectangularLayoutResult east = Calculate(
                1300.0,
                1300.0,
                RoomSide.East,
                100.0,
                500.0);
            AssertBands(
                east.DefaultCandidate,
                300.0,
                400.0,
                300.0,
                400.0);

            EngineeringRectangularLayoutResult south = Calculate(
                1300.0,
                1300.0,
                RoomSide.South,
                100.0,
                500.0);
            AssertBands(
                south.DefaultCandidate,
                300.0,
                400.0,
                400.0,
                300.0);

            EngineeringRectangularLayoutResult north = Calculate(
                1300.0,
                1300.0,
                RoomSide.North,
                100.0,
                500.0);
            AssertBands(
                north.DefaultCandidate,
                300.0,
                400.0,
                300.0,
                400.0);
        }

        [TestMethod]
        public void Calculate_RemainderExactlyMinimumCut_KeepsNaturalLayout()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1452.0,
                1200.0,
                RoomSide.West,
                300.0,
                900.0);

            BoundaryBandPlan plan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);

            Assert.IsFalse(plan.UsesRedistribution);
            Assert.AreEqual(252.0, plan.NaturalRemainder);
            AssertBand(
                plan,
                RoomSide.West,
                600.0,
                BoundaryBandKind.FullTile);
            AssertBand(
                plan,
                RoomSide.East,
                252.0,
                BoundaryBandKind.NaturalRemainder);
        }

        [TestMethod]
        public void Calculate_RemainderBelowMinimumBeyondTolerance_UsesHalfRedistribution()
        {
            double remainder =
                252.0 - (GeometryTolerance.Coordinate * 2.0);
            EngineeringRectangularLayoutResult result = Calculate(
                1200.0 + remainder,
                1200.0,
                RoomSide.West,
                300.0,
                900.0);

            BoundaryBandPlan plan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);

            Assert.IsTrue(plan.UsesRedistribution);
            AssertBand(
                plan,
                RoomSide.West,
                300.0 + remainder,
                BoundaryBandKind.Transition);
            AssertBand(
                plan,
                RoomSide.East,
                300.0,
                BoundaryBandKind.HalfTile);
            Assert.AreEqual(1, plan.InteriorFullTileCount);
        }

        [TestMethod]
        public void Calculate_NaturalRemainderAboveHalfTile_IsNotShifted()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1700.0,
                1200.0,
                RoomSide.West,
                300.0,
                900.0);

            BoundaryBandPlan plan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);

            Assert.IsFalse(plan.UsesRedistribution);
            AssertBand(
                plan,
                RoomSide.West,
                600.0,
                BoundaryBandKind.FullTile);
            AssertBand(
                plan,
                RoomSide.East,
                500.0,
                BoundaryBandKind.NaturalRemainder);
        }

        [TestMethod]
        public void Calculate_ExactTileMultiple_DoesNotCreateUnnecessaryHalfBands()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1200.0,
                1800.0,
                RoomSide.West,
                600.0,
                1200.0);

            BoundaryBandPlan xPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.Y);

            Assert.IsFalse(xPlan.UsesRedistribution);
            Assert.IsFalse(yPlan.UsesRedistribution);
            Assert.IsNull(xPlan.HalfTileSide);
            Assert.IsNull(yPlan.HalfTileSide);
            Assert.AreEqual(3, result.DefaultCandidate.DivisionLines.Count);
        }

        [TestMethod]
        public void Calculate_CenteredWestDoor_ProvidesDeterministicDefaultAndFlip()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1300.0,
                1300.0,
                RoomSide.West,
                500.0,
                800.0);

            Assert.AreEqual(2, result.ViableCandidates.Count);
            Assert.IsNotNull(result.DefaultCandidate);
            Assert.IsNotNull(result.FlippedCandidate);

            BoundaryBandPlan defaultPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.Y);
            AssertBand(
                defaultPlan,
                RoomSide.South,
                400.0,
                BoundaryBandKind.Transition);
            AssertBand(
                defaultPlan,
                RoomSide.North,
                300.0,
                BoundaryBandKind.HalfTile);

            BoundaryBandPlan flippedPlan =
                result.FlippedCandidate.GetAxisPlan(TileLayoutAxis.Y);
            AssertBand(
                flippedPlan,
                RoomSide.South,
                300.0,
                BoundaryBandKind.HalfTile);
            AssertBand(
                flippedPlan,
                RoomSide.North,
                400.0,
                BoundaryBandKind.Transition);

            Assert.AreEqual(
                CandidateDiagnosticCode.CenteredDoorDefaultApplied,
                result.DefaultCandidate.Diagnostics.Last().Code);
            Assert.AreEqual(
                CandidateDiagnosticCode.CenteredDoorFlipped,
                result.FlippedCandidate.Diagnostics.Last().Code);
        }

        [TestMethod]
        public void Calculate_CenteredSouthDoor_DefaultsWestAndFlipsEast()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1300.0,
                1300.0,
                RoomSide.South,
                500.0,
                800.0);

            BoundaryBandPlan defaultPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan flippedPlan =
                result.FlippedCandidate.GetAxisPlan(TileLayoutAxis.X);

            Assert.AreEqual(RoomSide.West, defaultPlan.HalfTileSide);
            Assert.AreEqual(RoomSide.East, flippedPlan.HalfTileSide);
        }

        [TestMethod]
        public void Calculate_RectangularTile_UsesEachAxisOwnThresholdAndHalfSize()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1700.0,
                1210.0,
                RoomSide.West,
                100.0,
                500.0,
                800.0,
                500.0);

            BoundaryBandPlan xPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                result.DefaultCandidate.GetAxisPlan(TileLayoutAxis.Y);

            AssertBand(
                xPlan,
                RoomSide.West,
                500.0,
                BoundaryBandKind.Transition);
            AssertBand(
                xPlan,
                RoomSide.East,
                400.0,
                BoundaryBandKind.HalfTile);
            Assert.IsFalse(yPlan.UsesRedistribution);
            AssertBand(
                yPlan,
                RoomSide.North,
                210.0,
                BoundaryBandKind.NaturalRemainder);
        }

        [TestMethod]
        public void Calculate_TooSmallAxis_ReturnsRejectedCandidateWithReasons()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                200.0,
                600.0,
                RoomSide.West,
                100.0,
                500.0);

            Assert.IsFalse(result.IsSuccessful);
            Assert.AreEqual(0, result.ViableCandidates.Count);
            Assert.AreEqual(1, result.EliminatedCandidates.Count);
            CollectionAssert.Contains(
                result.EliminatedCandidates[0]
                    .RejectionReasons
                    .Select(diagnostic => diagnostic.Code)
                    .ToArray(),
                CandidateDiagnosticCode.MinimumCutNotMet);
            CollectionAssert.Contains(
                result.EliminatedCandidates[0]
                    .RejectionReasons
                    .Select(diagnostic => diagnostic.Code)
                    .ToArray(),
                CandidateDiagnosticCode.InsufficientFullTileForRedistribution);
        }

        [TestMethod]
        public void Calculate_CandidateExposesActualTilesClassificationAndRawMetrics()
        {
            EngineeringRectangularLayoutResult result = Calculate(
                1300.0,
                1300.0,
                RoomSide.West,
                500.0,
                800.0);
            LayoutCandidate candidate = result.DefaultCandidate;

            Assert.AreEqual(9, candidate.Tiles.Count);
            Assert.AreEqual(TileClassification.Interior, candidate.Tiles[4].Classification);
            Assert.IsTrue(candidate.Tiles[4].IsFullTile);
            Assert.AreEqual(4, candidate.Tiles[4].Outline.Count);
            Assert.AreEqual(TileClassification.Boundary, candidate.Tiles[0].Classification);
            Assert.IsFalse(candidate.Tiles[0].IsContinuousIrregular);
            Assert.AreEqual(8L, candidate.Metrics.BoundaryNonFullTileCount);
            Assert.AreEqual(0L, candidate.Metrics.InteriorNonFullTileCount);
            Assert.AreEqual(300.0, candidate.Metrics.MinimumBoundaryBandWidth);
            Assert.AreEqual(0, candidate.Metrics.PhaseResetCount);
            Assert.AreEqual(0, candidate.Metrics.KeyAlignmentCount);
        }

        [TestMethod]
        public void Calculate_LargeWcsOffset_PreservesLocalBandDimensions()
        {
            double origin = 1e12;
            var room = new AxisAlignedRectangle(
                origin,
                origin + 1300.0,
                origin,
                origin + 1300.0,
                25.0);
            var parameters = new EngineeringRectangularLayoutParameters(
                600.0,
                600.0,
                new DoorOpening(
                    RoomSide.West,
                    origin + 100.0,
                    origin + 500.0));

            EngineeringRectangularLayoutResult result =
                EngineeringRectangularLayoutCalculator.Calculate(room, parameters);

            Assert.IsTrue(result.IsSuccessful);
            AssertBands(
                result.DefaultCandidate,
                400.0,
                300.0,
                300.0,
                400.0);
            Assert.AreEqual(
                origin + 400.0,
                result.DefaultCandidate.DivisionLines[0].Start.X);
            Assert.AreEqual(
                25.0,
                result.DefaultCandidate.DivisionLines[0].Start.Z);
        }

        [TestMethod]
        [Timeout(2000)]
        public void Calculate_OutputAboveExistingLimit_IsRejectedBeforeAllocation()
        {
            var room = new AxisAlignedRectangle(0.0, 10002.0, 0.0, 0.5);
            var parameters = new EngineeringRectangularLayoutParameters(
                1.0,
                1.0,
                new DoorOpening(RoomSide.South, 100.0, 200.0));

            TileLayoutLimitExceededException exception = null;
            try
            {
                EngineeringRectangularLayoutCalculator.Calculate(room, parameters);
            }
            catch (TileLayoutLimitExceededException caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception);
            Assert.AreEqual(10001.0, exception.EstimatedDivisionLineCount);
        }

        [TestMethod]
        public void Calculate_DoorOutsideSelectedWall_IsRejected()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);
            var parameters = new EngineeringRectangularLayoutParameters(
                600.0,
                600.0,
                new DoorOpening(RoomSide.West, 1200.0, 1400.0));

            ArgumentOutOfRangeException exception = null;
            try
            {
                EngineeringRectangularLayoutCalculator.Calculate(room, parameters);
            }
            catch (ArgumentOutOfRangeException caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception);
        }

        [TestMethod]
        public void DoorOpening_ReversedEndpoints_AreNormalizedDeterministically()
        {
            var opening = new DoorOpening(RoomSide.North, 900.0, 300.0);
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1200.0);

            Assert.AreEqual(300.0, opening.AlongWallStart);
            Assert.AreEqual(900.0, opening.AlongWallEnd);
            Assert.AreEqual(600.0, opening.Center);
            Assert.AreEqual(600.0, opening.Width);
            Assert.AreEqual(300.0, opening.GetDistanceToLowWallEnd(room));
            Assert.AreEqual(400.0, opening.GetDistanceToHighWallEnd(room));
        }

        private static EngineeringRectangularLayoutResult Calculate(
            double width,
            double height,
            RoomSide doorWall,
            double doorStart,
            double doorEnd,
            double tileWidth = 600.0,
            double tileHeight = 600.0)
        {
            return EngineeringRectangularLayoutCalculator.Calculate(
                new AxisAlignedRectangle(0.0, width, 0.0, height),
                new EngineeringRectangularLayoutParameters(
                    tileWidth,
                    tileHeight,
                    new DoorOpening(doorWall, doorStart, doorEnd)));
        }

        private static void AssertBands(
            LayoutCandidate candidate,
            double west,
            double east,
            double south,
            double north)
        {
            BoundaryBandPlan xPlan =
                candidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                candidate.GetAxisPlan(TileLayoutAxis.Y);
            Assert.AreEqual(
                west,
                xPlan.GetBoundary(RoomSide.West).Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                east,
                xPlan.GetBoundary(RoomSide.East).Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                south,
                yPlan.GetBoundary(RoomSide.South).Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                north,
                yPlan.GetBoundary(RoomSide.North).Width,
                GeometryTolerance.Coordinate);
        }

        private static void AssertBand(
            BoundaryBandPlan plan,
            RoomSide side,
            double width,
            BoundaryBandKind kind)
        {
            AxisBoundaryBand band = plan.GetBoundary(side);
            Assert.AreEqual(
                width,
                band.Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(kind, band.Kind);
        }

        private static void AssertVertical(LineSegment3D line, double x)
        {
            Assert.AreEqual(x, line.Start.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(x, line.End.X, GeometryTolerance.Coordinate);
        }

        private static void AssertHorizontal(LineSegment3D line, double y)
        {
            Assert.AreEqual(y, line.Start.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(y, line.End.Y, GeometryTolerance.Coordinate);
        }
    }
}
