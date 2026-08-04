using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class Dor7G1GeneralizationTests
    {
        [TestMethod]
        public void ComplexFixture_PreservesExactDwgBoundaryWithoutOriginalRandomDoorClaim()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom();

            Assert.AreEqual(14, room.Vertices.Count);
            Assert.AreEqual(832.19436286384735, room.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(12339.692550068714, room.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(141.45541868191731, room.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(6390.5900001384452, room.North,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(60370338.4946013, PolygonArea(room), 0.001);
        }

        [TestMethod]
        public void ProjectAbsoluteMinimum_UsesActual121Point606AndEqualityPasses()
        {
            const double actual = 121.606;
            LayoutCandidate equal = ConfirmedRectangleCandidate(
                actual,
                new LayoutPolicyProfile("equal", actual));
            LayoutCandidate oneHundred = ConfirmedRectangleCandidate(
                actual,
                new LayoutPolicyProfile("100-mm", 100));
            LayoutCandidate above = ConfirmedRectangleCandidate(
                actual,
                new LayoutPolicyProfile("above", actual + 0.01));

            Assert.IsFalse(equal.IsRejected);
            Assert.IsTrue(equal.RequiresUserReview);
            Assert.IsFalse(oneHundred.IsRejected);
            Assert.IsTrue(oneHundred.RequiresUserReview);
            Assert.IsTrue(above.IsRejected);
            Assert.IsTrue(above.TileAssessments.Any(assessment =>
                assessment.Status == ProjectCutStatus.BelowProjectAbsoluteMinimum
                && assessment.Measurements.Any(measurement =>
                    Math.Abs(measurement.ActualValue - actual)
                        <= GeometryTolerance.Coordinate)));
        }

        [TestMethod]
        public void RecommendedMinimum_EqualityIsAutomaticAndCornerMeasuresBothAxes()
        {
            AxisAlignedOrthogonalPolygon room = RectangleRoom(852, 852);
            var phase = new ConfirmedGridPhase(
                "recommended-equality", 252, 252, "Threshold equality.");
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, 852, 0, 852),
                new DoorOpening(RoomSide.North, 126, 726),
                new LayoutPolicyProfile("100-mm", 100),
                new List<ConfirmedGridPhase> { phase });
            LayoutCandidate candidate = result.Candidates.Single(value =>
                value.Id == "whole-confirmed-recommended-equality");

            Assert.IsFalse(candidate.IsRejected);
            Assert.IsFalse(candidate.RequiresUserReview);
            TileFootprintAssessment corner = candidate.TileAssessments.First(value =>
                value.Footprint.NominalWidth < 600
                && value.Footprint.NominalHeight < 600);
            Assert.AreEqual(2, corner.Measurements.Count);
            Assert.IsTrue(corner.Measurements.All(measurement =>
                measurement.Status == ProjectCutStatus.MeetsRecommendedMinimum));
        }

        [TestMethod]
        public void EveryFootprintHasLocatedAssessmentAndCoveragePreservesComplexRoomArea()
        {
            EngineeringOrthogonalLayoutResult result = CalculateComplex(
                new LayoutPolicyProfile("100-mm", 100));
            LayoutCandidate candidate = result.Candidates.First(value =>
                !value.IsRejected);

            Assert.AreEqual(candidate.Tiles.Count, candidate.TileAssessments.Count);
            Assert.IsTrue(candidate.TileAssessments.All(value =>
                object.ReferenceEquals(candidate.Tiles[value.TileIndex], value.Footprint)));
            Assert.AreEqual(
                60370338.4946013,
                candidate.Tiles.Sum(tile => tile.Area),
                0.01);
            Assert.IsTrue(candidate.Tiles.Any(tile => tile.IsContinuousIrregular));
            Assert.IsTrue(candidate.TileAssessments
                .Where(value => value.IsBelowRecommended)
                .All(value => value.Footprint.Outline.Count >= 4
                    && !string.IsNullOrWhiteSpace(value.Reason)));
        }

        [TestMethod]
        public void ComplexRoom_CalculationIsDeterministicAndBounded()
        {
            EngineeringOrthogonalLayoutResult first = CalculateComplex(
                new LayoutPolicyProfile("250-mm", 250));
            EngineeringOrthogonalLayoutResult second = CalculateComplex(
                new LayoutPolicyProfile("250-mm", 250));

            CollectionAssert.AreEqual(
                first.Candidates.Select(candidate => candidate.Id).ToArray(),
                second.Candidates.Select(candidate => candidate.Id).ToArray());
            Assert.AreEqual(first.GenerationReport.XPhaseCount,
                second.GenerationReport.XPhaseCount);
            Assert.IsTrue(first.GenerationReport.GeneratedAlternativeCount > 0);
            Assert.IsTrue(first.GenerationReport.CombinationLimitReached);
            Assert.IsTrue(first.HasTruncatedCandidateSearch);
            Assert.IsTrue(first.GenerationReport.XPhaseCount
                <= CandidateSearchLimits.MaximumPhaseCandidatesPerAxis);
            Assert.IsTrue(first.GenerationReport.YPhaseCount
                <= CandidateSearchLimits.MaximumPhaseCandidatesPerAxis);
            Assert.IsTrue(first.GenerationReport.PhaseCombinationCount
                <= CandidateSearchLimits.MaximumWholeRoomPhaseCombinations);
            Assert.IsTrue(first.Candidates.Count(candidate => !candidate.IsRejected)
                <= CandidateSearchLimits.MaximumNonDominatedCandidates
                    + first.Candidates.Count(candidate =>
                        !candidate.Diagnostics.Any(diagnostic => diagnostic.Code ==
                            CandidateDiagnosticCode.AlternativeWholeRoomPhaseGenerated)));
        }

        [TestMethod]
        public void GeneratedWholeRoomPhasesExposeBothAxisPlansForHostConsumers()
        {
            EngineeringOrthogonalLayoutResult result = CalculateComplex(
                new LayoutPolicyProfile("100-mm", 100));
            LayoutCandidate[] generated = result.Candidates
                .Where(candidate => candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code ==
                        CandidateDiagnosticCode.AlternativeWholeRoomPhaseGenerated))
                .ToArray();

            Assert.IsTrue(generated.Length > 0);
            foreach (LayoutCandidate candidate in generated)
            {
                BoundaryBandPlan xPlan;
                BoundaryBandPlan yPlan;
                Assert.IsTrue(candidate.TryGetAxisPlan(
                    TileLayoutAxis.X, out xPlan), candidate.Id);
                Assert.IsTrue(candidate.TryGetAxisPlan(
                    TileLayoutAxis.Y, out yPlan), candidate.Id);
                Assert.AreEqual(DoorControlledAxisRole.WholeRoomPhase,
                    xPlan.Role, candidate.Id);
                Assert.AreEqual(DoorControlledAxisRole.WholeRoomPhase,
                    yPlan.Role, candidate.Id);
                Assert.AreEqual(
                    candidate.Structure.Regions[0].VerticalCuts.Count + 1,
                    xPlan.SegmentWidths.Count,
                    candidate.Id);
                Assert.AreEqual(
                    candidate.Structure.Regions[0].HorizontalCuts.Count + 1,
                    yPlan.SegmentWidths.Count,
                    candidate.Id);
            }
        }

        [TestMethod]
        public void NeutralPartition_IsSeamlessConnectedAndHasNoRegionSemantics()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom();
            NeutralOrthogonalRegionPartition partition =
                NeutralOrthogonalRegionPartitioner.Create(room);

            Assert.IsTrue(partition.Regions.Count > 2);
            Assert.AreEqual(PolygonArea(room), partition.CoveredArea, 0.001);
            Assert.IsTrue(partition.Connections.Count >= partition.Regions.Count - 1);
            Assert.IsTrue(partition.Regions.All(region =>
                region.Id.StartsWith("region-", StringComparison.Ordinal)));
            for (int first = 0; first < partition.Regions.Count - 1; first++)
            {
                for (int second = first + 1; second < partition.Regions.Count; second++)
                {
                    Assert.AreEqual(0.0, OverlapArea(
                        partition.Regions[first].Bounds,
                        partition.Regions[second].Bounds),
                        GeometryTolerance.Coordinate);
                }
            }
        }

        [TestMethod]
        public void NeutralPartition_IsInvariantToTranslationMirrorRotationAndInputOrder()
        {
            IList<Point3D> source = ComplexOrthogonalBoundaryFixture.Vertices();
            NeutralOrthogonalRegionPartition baseline = Partition(source);
            NeutralOrthogonalRegionPartition translated = Partition(source
                .Select(point => new Point3D(point.X + 321.25, point.Y - 98.5))
                .ToList());
            NeutralOrthogonalRegionPartition mirrored = Partition(source
                .Select(point => new Point3D(-point.X, point.Y))
                .Reverse()
                .ToList());
            NeutralOrthogonalRegionPartition rotated = Partition(source
                .Select(point => new Point3D(-point.Y, point.X))
                .ToList());
            var reordered = source.Reverse().ToList();
            reordered.Insert(0, reordered[reordered.Count - 1]);
            reordered.RemoveAt(reordered.Count - 1);
            NeutralOrthogonalRegionPartition inputOrder = Partition(reordered);

            foreach (NeutralOrthogonalRegionPartition transformed in new[]
            {
                translated, mirrored, rotated, inputOrder
            })
            {
                Assert.AreEqual(baseline.Regions.Count, transformed.Regions.Count);
                Assert.AreEqual(baseline.Connections.Count, transformed.Connections.Count);
                Assert.AreEqual(baseline.CoveredArea, transformed.CoveredArea, 0.001);
            }
        }

        [TestMethod]
        public void WholeRoomCandidates_PreserveMetricsUnderTranslationMirrorRotationAndInputOrder()
        {
            IList<Point3D> source = ComplexOrthogonalBoundaryFixture.Vertices();
            AxisAlignedRectangle control =
                ComplexOrthogonalBoundaryFixture.CreateControlRegion();
            DoorOpening door =
                ComplexOrthogonalBoundaryFixture.CreateDeterministicWestDoor();
            LayoutPolicyProfile policy = new LayoutPolicyProfile("100-mm", 100);
            EngineeringOrthogonalLayoutResult baseline = Calculate(
                ComplexOrthogonalBoundaryFixture.CreateRoom(),
                control,
                door,
                policy,
                null);

            const double dx = 321.25;
            const double dy = -98.5;
            EngineeringOrthogonalLayoutResult translated = Calculate(
                ComplexOrthogonalBoundaryFixture.CreateRoom(source.Select(point =>
                    new Point3D(point.X + dx, point.Y + dy)).ToList()),
                new AxisAlignedRectangle(
                    control.West + dx, control.East + dx,
                    control.South + dy, control.North + dy),
                new DoorOpening(RoomSide.West,
                    door.AlongWallStart + dy, door.AlongWallEnd + dy),
                policy,
                null);

            EngineeringOrthogonalLayoutResult mirrored = Calculate(
                ComplexOrthogonalBoundaryFixture.CreateRoom(source.Select(point =>
                    new Point3D(-point.X, point.Y)).Reverse().ToList()),
                new AxisAlignedRectangle(
                    -control.East, -control.West,
                    control.South, control.North),
                new DoorOpening(RoomSide.East,
                    door.AlongWallStart, door.AlongWallEnd),
                policy,
                null);

            EngineeringOrthogonalLayoutResult rotated = Calculate(
                ComplexOrthogonalBoundaryFixture.CreateRoom(source.Select(point =>
                    new Point3D(-point.Y, point.X)).ToList()),
                new AxisAlignedRectangle(
                    -control.North, -control.South,
                    control.West, control.East),
                new DoorOpening(RoomSide.South,
                    -door.AlongWallEnd, -door.AlongWallStart),
                policy,
                null);

            EngineeringOrthogonalLayoutResult inputOrder = Calculate(
                RoomFromShuffledLines(source),
                control,
                door,
                policy,
                null);

            string[] expected = MetricSignatures(baseline);
            CollectionAssert.AreEqual(expected, MetricSignatures(translated));
            CollectionAssert.AreEqual(expected, MetricSignatures(mirrored),
                "Expected:\n" + string.Join("\n", expected)
                    + "\nActual:\n" + string.Join("\n", MetricSignatures(mirrored)));
            CollectionAssert.AreEqual(expected, MetricSignatures(rotated));
            CollectionAssert.AreEqual(expected, MetricSignatures(inputOrder));
        }

        [TestMethod]
        public void ComplexRoom_ReleaseScaleSmokeCompletesWithinFiveSeconds()
        {
            var stopwatch = Stopwatch.StartNew();
            EngineeringOrthogonalLayoutResult result = CalculateComplex(
                new LayoutPolicyProfile("250-mm", 250));
            stopwatch.Stop();

            Assert.IsTrue(result.Candidates.Count > 0);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                "Elapsed=" + stopwatch.Elapsed);
        }

        [TestMethod]
        public void ContradictoryAbsoluteMinimumAboveRecommendedIsRejectedAsInput()
        {
            try
            {
                new EngineeringOrthogonalLayoutParameters(
                    600,
                    600,
                    new AxisAlignedRectangle(0, 1200, 0, 1200),
                    new DoorOpening(RoomSide.North, 300, 900),
                    null,
                    null,
                    new LayoutPolicyProfile("invalid", 252.01));
                Assert.Fail("Expected contradictory policy input to be rejected.");
            }
            catch (ArgumentException)
            {
            }
        }

        [TestMethod]
        public void LegacyConfirmedPhaseFlagCannotChangeGeneralProjectRuleClassification()
        {
            const double actual = 121.606;
            AxisAlignedOrthogonalPolygon room = RectangleRoom(actual + 600, 600);
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, actual + 600, 0, 600),
                new DoorOpening(RoomSide.North, actual, actual + 600),
                new LayoutPolicyProfile("100-mm", 100),
                new List<ConfirmedGridPhase>
                {
                    new ConfirmedGridPhase("plain", actual, 0, "Plain.", false),
                    new ConfirmedGridPhase("legacy-flag", actual, 0, "Flagged.", true)
                });
            LayoutCandidate plain = result.Candidates.Single(candidate =>
                candidate.Id == "whole-confirmed-plain");
            LayoutCandidate flagged = result.Candidates.Single(candidate =>
                candidate.Id == "whole-confirmed-legacy-flag");

            Assert.AreEqual(plain.IsRejected, flagged.IsRejected);
            Assert.AreEqual(plain.RequiresUserReview, flagged.RequiresUserReview);
            Assert.AreEqual(
                plain.Metrics.BelowDefaultMinimumBoundaryTileCount,
                flagged.Metrics.BelowDefaultMinimumBoundaryTileCount);
            CollectionAssert.AreEqual(
                plain.TileAssessments.Select(value => value.Status).ToArray(),
                flagged.TileAssessments.Select(value => value.Status).ToArray());
        }

        private static LayoutCandidate ConfirmedRectangleCandidate(
            double actual,
            LayoutPolicyProfile policy)
        {
            AxisAlignedOrthogonalPolygon room = RectangleRoom(actual + 600, 600);
            var phase = new ConfirmedGridPhase(
                "actual-cut", actual, 0, "Exact cut fixture.");
            EngineeringOrthogonalLayoutResult result = Calculate(
                room,
                new AxisAlignedRectangle(0, actual + 600, 0, 600),
                new DoorOpening(RoomSide.North, actual, actual + 600),
                policy,
                new List<ConfirmedGridPhase> { phase });
            return result.Candidates.Single(candidate =>
                candidate.Id == "whole-confirmed-actual-cut");
        }

        private static EngineeringOrthogonalLayoutResult CalculateComplex(
            LayoutPolicyProfile policy)
        {
            return Calculate(
                ComplexOrthogonalBoundaryFixture.CreateRoom(),
                ComplexOrthogonalBoundaryFixture.CreateControlRegion(),
                ComplexOrthogonalBoundaryFixture.CreateDeterministicWestDoor(),
                policy,
                null);
        }

        private static EngineeringOrthogonalLayoutResult Calculate(
            AxisAlignedOrthogonalPolygon room,
            AxisAlignedRectangle control,
            DoorOpening door,
            LayoutPolicyProfile policy,
            IList<ConfirmedGridPhase> phases)
        {
            return EngineeringOrthogonalLayoutCalculator.Calculate(
                room,
                new EngineeringOrthogonalLayoutParameters(
                    600, 600, control, door, null, phases, policy, true));
        }

        private static AxisAlignedOrthogonalPolygon RectangleRoom(
            double width,
            double height)
        {
            return ComplexOrthogonalBoundaryFixture.CreateRoom(new List<Point3D>
            {
                new Point3D(0, 0),
                new Point3D(width, 0),
                new Point3D(width, height),
                new Point3D(0, height)
            });
        }

        private static NeutralOrthogonalRegionPartition Partition(
            IList<Point3D> vertices)
        {
            return NeutralOrthogonalRegionPartitioner.Create(
                ComplexOrthogonalBoundaryFixture.CreateRoom(vertices));
        }

        private static double PolygonArea(AxisAlignedOrthogonalPolygon room)
        {
            double twice = 0.0;
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D first = room.Vertices[index];
                Point3D second = room.Vertices[(index + 1) % room.Vertices.Count];
                twice += (first.X * second.Y) - (second.X * first.Y);
            }

            return Math.Abs(twice) / 2.0;
        }

        private static double OverlapArea(
            AxisAlignedRectangle first,
            AxisAlignedRectangle second)
        {
            double width = Math.Max(0.0,
                Math.Min(first.East, second.East)
                    - Math.Max(first.West, second.West));
            double height = Math.Max(0.0,
                Math.Min(first.North, second.North)
                    - Math.Max(first.South, second.South));
            return width * height;
        }

        private static AxisAlignedOrthogonalPolygon RoomFromShuffledLines(
            IList<Point3D> vertices)
        {
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Count; index++)
            {
                LineSegment3D line = new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Count]);
                lines.Add((index % 2) == 0
                    ? new LineSegment3D(line.End, line.Start)
                    : line);
            }

            lines = lines.OrderByDescending(line => line.Start.X + line.Start.Y)
                .ToList();
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            return validation.Room;
        }

        private static string[] MetricSignatures(
            EngineeringOrthogonalLayoutResult result)
        {
            return result.Candidates.Select(candidate => string.Join("|",
                    candidate.IsRejected,
                    candidate.Tiles.Count,
                    candidate.Metrics.InteriorNonFullTileCount,
                    Math.Round(candidate.Metrics.InteriorNonFullTileArea, 6),
                    candidate.Metrics.BoundaryNonFullTileCount,
                    candidate.Metrics.BelowDefaultMinimumBoundaryTileCount,
                    candidate.Metrics.BelowProjectAbsoluteMinimumBoundaryTileCount,
                    Math.Round(candidate.Metrics.MinimumBoundaryBandWidth, 6),
                    candidate.Metrics.PhaseResetCount))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
