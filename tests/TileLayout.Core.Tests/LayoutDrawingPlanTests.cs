using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class LayoutDrawingPlanTests
    {
        [TestMethod]
        public void L01PlanPreservesCandidateGeometryAndStableIdentifiers()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);

            LayoutDrawingPlan first = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);
            LayoutDrawingPlan second = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.AreEqual(selected.Id, first.CandidateId);
            Assert.AreEqual(6, first.RoomOutline.Count);
            Assert.AreEqual(selected.Candidate.DivisionLines.Count,
                first.DivisionLines.Count);
            Assert.AreEqual(selected.Candidate.Tiles.Count, first.Tiles.Count);
            Assert.AreEqual("division-0001", first.DivisionLines[0].Id);
            Assert.AreEqual("tile-0001", first.Tiles[0].Id);
            Assert.IsTrue(first.Tiles.Any(tile => tile.IsContinuousIrregular));
            Assert.AreEqual(selected.Candidate.WallCornerAssessments.Count,
                first.WallCorners.Count);
            Assert.AreEqual(Snapshot(first), Snapshot(second));
        }

        [TestMethod]
        public void AutomaticStartPointUsesFarWallAndCandidateConstructionDirections()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.IsNotNull(plan.StartPoint);
            Assert.AreEqual(RoomSide.South, plan.StartPoint.FarWall);
            Assert.AreEqual(RoomSide.North, plan.StartPoint.InwardDirection);
            Assert.AreEqual(TileLayoutAxis.X, plan.StartPoint.AlongWallAxis);
            Assert.AreEqual(
                Opposite(selected.Candidate.GetAxisPlan(TileLayoutAxis.X)
                    .ConstructionStartSide),
                plan.StartPoint.AlongWallDirection);
            Assert.AreEqual("tile-0009", plan.StartPoint.WallTileId);
            Assert.AreEqual(
                LayoutDrawingStartPointTileKind.FullTile,
                plan.StartPoint.WallTileKind);
            Assert.AreEqual(576.0, plan.StartPoint.Position.X,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(300.0, plan.StartPoint.Position.Y,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(plan.StartPoint.Position.X > plan.West);
            Assert.IsTrue(plan.StartPoint.Position.X < plan.East);
            Assert.IsTrue(plan.StartPoint.Position.Y > plan.South);
            Assert.IsTrue(plan.StartPoint.Position.Y < plan.North);
            Assert.IsTrue(
                plan.StartPoint.WallTileId.StartsWith("tile-", StringComparison.Ordinal));
            Assert.IsTrue(
                plan.StartPoint.WallTileKind
                    == LayoutDrawingStartPointTileKind.FullTile
                || plan.StartPoint.WallTileKind
                    == LayoutDrawingStartPointTileKind.HalfTile);
        }

        [TestMethod]
        public void AutomaticStartPointOpposesEveryDoorWall()
        {
            foreach (RoomSide doorWall in new[]
            {
                RoomSide.West,
                RoomSide.East,
                RoomSide.South,
                RoomSide.North
            })
            {
                EngineeringOrthogonalDecisionResult result =
                    L01RightHandDoor(doorWall);
                EvaluatedLayoutCandidate selected = result.Candidates.First(
                    item => item.State == LayoutCandidateState.AutomaticUsable);
                LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                    result,
                    selected.Id);

                Assert.IsNotNull(plan.StartPoint, doorWall.ToString());
                Assert.AreEqual(
                    Opposite(doorWall),
                    plan.StartPoint.FarWall,
                    doorWall.ToString());
                Assert.AreEqual(
                    Opposite(selected.Candidate.GetAxisPlan(
                        plan.StartPoint.AlongWallAxis).ConstructionStartSide),
                    plan.StartPoint.AlongWallDirection,
                    doorWall.ToString());
                if (doorWall == RoomSide.West || doorWall == RoomSide.East)
                {
                    Assert.AreEqual(
                        LayoutDrawingStartPointTileKind.FullTile,
                        plan.StartPoint.WallTileKind,
                        doorWall.ToString());
                }
            }
        }

        [TestMethod]
        public void AutomaticStartPointUsesFourTileGroutIntersectionWithGrout()
        {
            EngineeringOrthogonalDecisionResult result =
                L01FinishedFaceWithGrout();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.IsNotNull(plan.StartPoint);
            Assert.AreEqual(RoomSide.South, plan.StartPoint.FarWall);
            Assert.AreEqual(RoomSide.East, plan.StartPoint.AlongWallDirection);
            Assert.AreEqual(401.5, plan.StartPoint.Position.X,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(401.5, plan.StartPoint.Position.Y,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void AutomaticDimensionPlanRoundsValuesAndSeparatesFeatureKinds()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id,
                true);

            Assert.IsTrue(plan.Dimensions.Count > 0);
            Assert.IsTrue(plan.Dimensions.Any(dimension =>
                dimension.Kind == LayoutDrawingDimensionKind.TileSize));
            Assert.IsTrue(plan.Dimensions.Any(dimension =>
                dimension.Kind == LayoutDrawingDimensionKind.BoundaryFeature));
            Assert.IsTrue(plan.Dimensions.All(dimension =>
                !dimension.DisplayText.Contains(".")
                && !dimension.DisplayText.Contains("mm")));
            Assert.IsTrue(plan.Dimensions.All(dimension =>
                dimension.DisplayMillimetres
                    == (int)Math.Round(
                        dimension.ActualMillimetres,
                        MidpointRounding.AwayFromZero)));
            Assert.IsTrue(plan.Dimensions.Any(dimension =>
                plan.Tiles.Any(tile => TileHasSegment(tile, dimension))));
            var generalDimensions = plan.Dimensions
                .Where(dimension =>
                    dimension.Kind == LayoutDrawingDimensionKind.TileSize
                    && !dimension.SourceId.Contains("-edge-"))
                .ToList();
            Assert.IsTrue(generalDimensions.Count > 2);
            Assert.IsTrue(generalDimensions.Any(dimension =>
                dimension.Axis == TileLayoutAxis.X));
            Assert.IsTrue(generalDimensions.Any(dimension =>
                dimension.Axis == TileLayoutAxis.Y));
            Assert.IsTrue(generalDimensions.Count(dimension =>
                dimension.Axis == TileLayoutAxis.X) > 1);
            Assert.IsTrue(generalDimensions.Count(dimension =>
                dimension.Axis == TileLayoutAxis.Y) > 1);
            Assert.AreEqual(
                1,
                generalDimensions
                    .Where(dimension => dimension.Axis == TileLayoutAxis.X)
                    .Select(dimension => dimension.MeasuredSegment.Start.Y)
                    .Distinct()
                    .Count());
            Assert.AreEqual(
                1,
                generalDimensions
                    .Where(dimension => dimension.Axis == TileLayoutAxis.Y)
                    .Select(dimension => dimension.MeasuredSegment.Start.X)
                    .Distinct()
                    .Count());
            var specialDimensions = plan.Dimensions
                .Where(dimension =>
                    dimension.Kind == LayoutDrawingDimensionKind.TileSize
                    && dimension.SourceId.Contains("-edge-"))
                .ToList();
            Assert.IsTrue(specialDimensions
                .GroupBy(dimension => dimension.Axis + ":"
                    + dimension.DisplayMillimetres)
                .All(group => group.Count() == 1));
            Assert.IsTrue(specialDimensions
                .Where(dimension => dimension.Axis == TileLayoutAxis.X)
                .All(dimension => dimension.DimensionLinePoint.Y
                    < plan.South));
            Assert.IsTrue(specialDimensions
                .Where(dimension => dimension.Axis == TileLayoutAxis.Y)
                .All(dimension => dimension.DimensionLinePoint.X
                    < plan.West));
            Assert.IsTrue(generalDimensions.All(dimension =>
                dimension.DimensionLinePoint.Y < plan.South
                    || dimension.DimensionLinePoint.X < plan.West));
            Assert.IsTrue(result.RawResult.Room.Vertices
                .Select((point, index) => new LineSegment3D(
                    point,
                    result.RawResult.Room.Vertices[
                        (index + 1) % result.RawResult.Room.Vertices.Count]))
                .Where(edge => !IsExtremeRoomEdge(result.RawResult.Room, edge))
                .All(edge => plan.Dimensions.Any(dimension =>
                    dimension.Kind == LayoutDrawingDimensionKind.BoundaryFeature
                    && SameSegment(dimension.MeasuredSegment, edge))));
        }

        [TestMethod]
        public void InsideRoomDimensionPlanUsesTileEdgesAndCanOmitRoomSteps()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id,
                true,
                LayoutDrawingDimensionPlacement.InsideRoom,
                new LayoutDrawingColorSettings(3, 2, 6, 4),
                false);

            Assert.AreEqual(
                LayoutDrawingDimensionPlacement.InsideRoom,
                plan.DimensionPlacement);
            Assert.IsFalse(plan.IncludeRoomFeatureDimensions);
            Assert.IsFalse(plan.Dimensions.Any(dimension =>
                dimension.Kind == LayoutDrawingDimensionKind.BoundaryFeature));
            Assert.IsTrue(plan.Dimensions.All(dimension =>
                dimension.Axis == TileLayoutAxis.X
                    ? Nearly(
                        dimension.DimensionLinePoint.Y,
                        dimension.MeasuredSegment.Start.Y)
                    : Nearly(
                        dimension.DimensionLinePoint.X,
                        dimension.MeasuredSegment.Start.X)));

            for (int first = 0; first < plan.Dimensions.Count; first++)
            {
                for (int second = first + 1;
                    second < plan.Dimensions.Count;
                    second++)
                {
                    Assert.IsFalse(
                        plan.Dimensions[first].Kind
                            == plan.Dimensions[second].Kind
                        && SameSegment(
                            plan.Dimensions[first].MeasuredSegment,
                            plan.Dimensions[second].MeasuredSegment));
                }
            }
        }

        [TestMethod]
        public void GeneralDimensionsUseContinuousFullSpanRowAndColumn()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id,
                true,
                LayoutDrawingDimensionPlacement.InsideRoom,
                new LayoutDrawingColorSettings(3, 2, 6, 4),
                false);
            var generalDimensions = plan.Dimensions
                .Where(dimension =>
                    dimension.Kind == LayoutDrawingDimensionKind.TileSize
                    && !dimension.SourceId.Contains("-edge-"))
                .ToList();
            double rowY = generalDimensions
                .First(dimension => dimension.Axis == TileLayoutAxis.X)
                .MeasuredSegment.Start.Y;
            double columnX = generalDimensions
                .First(dimension => dimension.Axis == TileLayoutAxis.Y)
                .MeasuredSegment.Start.X;

            Assert.IsTrue(rowY > plan.South
                + GeometryTolerance.Coordinate);
            Assert.IsTrue(rowY < plan.North
                - GeometryTolerance.Coordinate);
            Assert.IsTrue(columnX > plan.West
                + GeometryTolerance.Coordinate);
            Assert.IsTrue(columnX < plan.East
                - GeometryTolerance.Coordinate);
            Assert.AreEqual(
                plan.East - plan.West,
                LongestBandCoverage(plan.Tiles, rowY, true),
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                plan.North - plan.South,
                LongestBandCoverage(plan.Tiles, columnX, false),
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void L04DPlanKeepsPolicyUndecidedIndependentBand()
        {
            EngineeringOrthogonalDecisionResult result = L04(1690, 1890, null);
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.RequiresProjectPolicy
                    && item.Candidate.Structure.Connections.Any(connection =>
                        connection.ProtrusionTreatment
                            == ProtrusionBandTreatment.Independent));

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.AreEqual(LayoutCandidateState.RequiresProjectPolicy,
                plan.CandidateState);
            Assert.AreEqual(2, plan.Regions.Count);
            Assert.AreEqual(1, plan.Connections.Count);
            Assert.IsTrue(plan.Tiles.Any(tile => Nearly(tile.NominalWidth, 200)));
            Assert.AreEqual(LayoutDrawingLineSemantic.Connection,
                plan.Connections[0].Semantic);
            Assert.AreEqual(selected.Candidate.TileAssessments.Count,
                plan.Tiles.Count);
            Assert.IsTrue(plan.Tiles.Any(tile =>
                tile.IsBelowRecommended
                && tile.CutMeasurements.Count > 0
                && tile.BoundarySides.Count > 0
                && !string.IsNullOrWhiteSpace(tile.AssessmentReason)));
            Assert.AreEqual(
                result.RawResult.NeutralRegionPartition.Regions.Count,
                plan.NeutralRegions.Count);
            Assert.AreEqual(
                result.RawResult.NeutralRegionPartition.Connections.Count,
                plan.NeutralConnections.Count);
        }

        [TestMethod]
        public void L04EPlanKeepsAbsorbedTopologyAndOriginalOrder()
        {
            EngineeringOrthogonalDecisionResult result = L04(1876, 2076, 200);
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable
                    && item.Candidate.Structure.Connections.Any(connection =>
                        connection.ProtrusionTreatment
                            == ProtrusionBandTreatment.Absorbed)
                    && item.Id.EndsWith(
                        "absorbed-mirrored",
                        StringComparison.Ordinal));

            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.AreEqual(selected.Candidate.DivisionLines.Count,
                plan.DivisionLines.Count);
            Assert.IsTrue(plan.Tiles.Any(tile => Nearly(tile.NominalWidth, 500)));
            for (int index = 0; index < plan.DivisionLines.Count; index++)
            {
                Assert.AreEqual(
                    selected.Candidate.DivisionLines[index].Start.X,
                    plan.DivisionLines[index].Geometry.Start.X,
                    GeometryTolerance.Coordinate);
                Assert.AreEqual(
                    selected.Candidate.DivisionLines[index].Start.Y,
                    plan.DivisionLines[index].Geometry.Start.Y,
                    GeometryTolerance.Coordinate);
            }
        }

        [TestMethod]
        public void DOR8FormalWritebackUsesOnlyDivisionAndConnections()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            IReadOnlyList<LayoutDrawingLine> formalLines;
            string rejectionReason;
            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                true,
                true,
                out formalLines,
                out rejectionReason));
            Assert.AreEqual(
                plan.DivisionLines.Count + plan.Connections.Count,
                formalLines.Count);
            CollectionAssert.AreEqual(
                plan.DivisionLines.Concat(plan.Connections).ToList(),
                formalLines.ToList());
            Assert.IsTrue(formalLines.All(line =>
                !plan.NeutralConnections.Contains(line)));

            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                false,
                true,
                out formalLines,
                out rejectionReason));
            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                true,
                false,
                out formalLines,
                out rejectionReason));

            LayoutDrawingPlan annotatedPlan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id,
                true);
            IReadOnlyList<LayoutDrawingDimension> formalDimensions;
            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.TryGetFormalDimensions(
                annotatedPlan,
                true,
                true,
                false,
                out formalDimensions,
                out rejectionReason));
            Assert.AreEqual(
                annotatedPlan.Dimensions.Count,
                formalDimensions.Count);
        }

        [TestMethod]
        public void DOR8RoomRangeDuplicateProtectionAllowsDifferentRooms()
        {
            EngineeringOrthogonalDecisionResult result = L01();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.IsSameRoomRange(
                plan,
                plan.West,
                plan.East,
                plan.South,
                plan.North,
                plan.Elevation));
            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.IsSameRoomRange(
                plan,
                plan.West + 1.0,
                plan.East + 1.0,
                plan.South,
                plan.North,
                plan.Elevation));
            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.IsSameRoomRange(
                plan,
                plan.West,
                plan.East,
                plan.South,
                plan.North,
                plan.Elevation + 1.0));
        }

        [TestMethod]
        public void DOR9PlanUsesFinishedFaceAndEmitsGroutBoundaries()
        {
            EngineeringOrthogonalDecisionResult result =
                L01FinishedFaceWithGrout();
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.HasRawCandidate);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            Assert.AreEqual(100.0, plan.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(0.0, plan.SourceWest,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, plan.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(0.0, plan.SourceSouth,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1.5, plan.GroutWidthMm,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, plan.PlasterThicknessMm,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(6,
                plan.DivisionLines.Count(line =>
                    line.Semantic
                        == LayoutDrawingLineSemantic.FinishedFaceOutline));
            Assert.IsTrue(plan.DivisionLines.Any(line =>
                line.Semantic == LayoutDrawingLineSemantic.GroutBoundary));
            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.IsSameRoomRange(
                plan,
                0.0,
                2076.0,
                0.0,
                4476.0,
                plan.Elevation));
            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.IsSameRoomRange(
                plan,
                plan.West,
                plan.East,
                plan.South,
                plan.North,
                plan.Elevation));

            IReadOnlyList<LayoutDrawingLine> formalLines;
            string rejectionReason;
            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                true,
                true,
                out formalLines,
                out rejectionReason));
            Assert.IsTrue(formalLines.Any(line =>
                line.Semantic == LayoutDrawingLineSemantic.FinishedFaceOutline));
            Assert.IsTrue(formalLines.Any(line =>
                line.Semantic == LayoutDrawingLineSemantic.GroutBoundary));
        }

        [TestMethod]
        public void DOR8ManualReviewCanWriteAfterWarningWithoutReason()
        {
            EngineeringOrthogonalDecisionResult result = L04(1690, 1890, 200);
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == LayoutCandidateState.RequiresUserDecision);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                result,
                selected.Id);

            IReadOnlyList<LayoutDrawingLine> formalLines;
            string rejectionReason;
            Assert.IsTrue(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                plan,
                true,
                true,
                out formalLines,
                out rejectionReason));

            EngineeringOrthogonalDecisionResult missingPolicy =
                L04(1690, 1890, null);
            EvaluatedLayoutCandidate requiresPolicy =
                missingPolicy.Candidates.First(
                    item => item.State
                        == LayoutCandidateState.RequiresProjectPolicy);
            LayoutDrawingPlan policyPlan = LayoutDrawingPlanBuilder.Build(
                missingPolicy,
                requiresPolicy.Id);
            Assert.IsFalse(OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                policyPlan,
                true,
                true,
                out formalLines,
                out rejectionReason));
        }

        [TestMethod]
        public void UnavailableOrUnknownCandidateCannotProducePlan()
        {
            EngineeringOrthogonalDecisionResult result = L04(1876, 2076, 251);
            EvaluatedLayoutCandidate eliminated = result.Candidates.First(
                item => item.State == LayoutCandidateState.Eliminated);

            AssertThrows<InvalidOperationException>(() =>
                LayoutDrawingPlanBuilder.Build(result, eliminated.Id));
            AssertThrows<ArgumentException>(() =>
                LayoutDrawingPlanBuilder.Build(result, "missing"));
        }

        [TestMethod]
        public void RepresentativeSvgSnapshotsAreDeterministic()
        {
            AssertSnapshot(
                "dor7-l01.svg",
                Plan(L01(), LayoutCandidateState.AutomaticUsable),
                "D73A63BD7DDADC46C6E317206BAF7525452B7E47F77E5DADD86CA71BBE1425FA");
            AssertSnapshot(
                "dor7-l01-east.svg",
                Plan(L01East(), LayoutCandidateState.AutomaticUsable),
                "BF081FFE36B2CC25793D209045747DC1AAA7DCD1791B1AADA75840EFB45B3075");
            AssertSnapshot(
                "dor7-l01-north-right.svg",
                Plan(L01RightHandDoor(RoomSide.North),
                    LayoutCandidateState.AutomaticUsable),
                "975DA9CF4CA0F888F2482F2B77F89CB960F24DBFB8FCAAF9016D8DD9A4812811");
            AssertSnapshot(
                "dor7-l01-south-right.svg",
                Plan(L01RightHandDoor(RoomSide.South),
                    LayoutCandidateState.AutomaticUsable),
                "2C0816484EF3F64BEDEC8FC2694070487FBC5F3AA12C4D725FF5FDF549BB4556");
            EngineeringOrthogonalDecisionResult l04d = L04(1690, 1890, null);
            EvaluatedLayoutCandidate independent = l04d.Candidates.First(item =>
                item.State == LayoutCandidateState.RequiresProjectPolicy
                && item.Candidate.Structure.Connections.Any(connection =>
                    connection.ProtrusionTreatment
                        == ProtrusionBandTreatment.Independent)
                && item.Candidate.Tiles.Any(tile => Nearly(tile.NominalWidth, 200)));
            AssertSnapshot(
                "dor7-l04d.svg",
                LayoutDrawingPlanBuilder.Build(l04d, independent.Id),
                "85C55BD93F5E446A354FCB4CB6059950D2423755DF0001E4EA148B4D04820260");

            EngineeringOrthogonalDecisionResult l04e = L04(1876, 2076, 200);
            EvaluatedLayoutCandidate absorbed = l04e.Candidates.First(
                item => item.State == LayoutCandidateState.AutomaticUsable
                    && item.Candidate.Structure.Connections.Any(connection =>
                        connection.ProtrusionTreatment
                            == ProtrusionBandTreatment.Absorbed)
                    && item.Id.EndsWith(
                        "absorbed-mirrored",
                        StringComparison.Ordinal));
            AssertSnapshot(
                "dor7-l04e.svg",
                LayoutDrawingPlanBuilder.Build(l04e, absorbed.Id),
                "68A994DC841A07F3E21D7F5EA5C6BF7EC9B3ACCBEC4BDDF4B6D3A6BA28F86AC1");
        }

        private static void AssertSnapshot(
            string fileName,
            LayoutDrawingPlan plan,
            string expectedSha256)
        {
            string svg = LayoutDrawingPlanSvgRenderer.Render(plan);
            Assert.AreEqual(svg, LayoutDrawingPlanSvgRenderer.Render(plan));
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
                Assert.AreEqual(svg, LayoutDrawingPlanSvgRenderer.Render(plan));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
            StringAssert.Contains(svg, "id=\"room-boundary\"");
            StringAssert.Contains(svg, "id=\"division-lines\"");
            StringAssert.Contains(svg, "id=\"wall-corners\"");
            StringAssert.Contains(svg, "铺贴图预览");

            string directory = Path.Combine(
                Path.GetDirectoryName(
                    typeof(LayoutDrawingPlanTests).Assembly.Location),
                "visual-snapshots");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, fileName),
                svg,
                new UTF8Encoding(false));

            if (!string.IsNullOrEmpty(expectedSha256))
            {
                Assert.AreEqual(expectedSha256, Sha256(svg));
            }
        }

        private static string Sha256(string value)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(
                    hash.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty);
            }
        }

        private static string Snapshot(LayoutDrawingPlan plan)
        {
            return plan.CandidateId + "|"
                + string.Join(",", plan.DivisionLines.Select(line =>
                    line.Id + ":" + line.Geometry.Start.X + ":"
                        + line.Geometry.Start.Y + ":"
                        + line.Geometry.End.X + ":"
                        + line.Geometry.End.Y)) + "|"
                + string.Join(",", plan.Tiles.Select(tile =>
                    tile.Id + ":" + tile.Outline.Count + ":"
                    + tile.IsContinuousIrregular));
        }

        private static bool SameSegment(
            LineSegment3D first,
            LineSegment3D second)
        {
            return (Nearly(first.Start.X, second.Start.X)
                    && Nearly(first.Start.Y, second.Start.Y)
                    && Nearly(first.End.X, second.End.X)
                    && Nearly(first.End.Y, second.End.Y))
                || (Nearly(first.Start.X, second.End.X)
                    && Nearly(first.Start.Y, second.End.Y)
                    && Nearly(first.End.X, second.Start.X)
                    && Nearly(first.End.Y, second.Start.Y));
        }

        private static double LongestBandCoverage(
            IReadOnlyList<LayoutDrawingTile> tiles,
            double position,
            bool horizontal)
        {
            var intervals = new List<double[]>();
            foreach (LayoutDrawingTile tile in tiles)
            {
                double fixedCoordinate = horizontal
                    ? tile.Outline.Min(point => point.Y)
                    : tile.Outline.Min(point => point.X);
                if (!Nearly(fixedCoordinate, position))
                {
                    continue;
                }

                intervals.Add(new[]
                {
                    horizontal
                        ? tile.Outline.Min(point => point.X)
                        : tile.Outline.Min(point => point.Y),
                    horizontal
                        ? tile.Outline.Max(point => point.X)
                        : tile.Outline.Max(point => point.Y)
                });
            }

            intervals = intervals
                .OrderBy(interval => interval[0])
                .ThenBy(interval => interval[1])
                .ToList();
            if (intervals.Count == 0)
            {
                return 0.0;
            }

            double currentStart = intervals[0][0];
            double currentEnd = intervals[0][1];
            double longest = 0.0;
            foreach (double[] interval in intervals.Skip(1))
            {
                if (interval[0] <= currentEnd
                    + GeometryTolerance.Coordinate)
                {
                    currentEnd = Math.Max(currentEnd, interval[1]);
                    continue;
                }

                longest = Math.Max(longest, currentEnd - currentStart);
                currentStart = interval[0];
                currentEnd = interval[1];
            }

            return Math.Max(longest, currentEnd - currentStart);
        }

        private static bool TileHasSegment(
            LayoutDrawingTile tile,
            LayoutDrawingDimension dimension)
        {
            for (int index = 0; index < tile.Outline.Count; index++)
            {
                if (SameSegment(
                    dimension.MeasuredSegment,
                    new LineSegment3D(
                        tile.Outline[index],
                        tile.Outline[(index + 1) % tile.Outline.Count])))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAtMinimumX(
            LayoutDrawingTile tile,
            LineSegment3D segment)
        {
            double minimumX = tile.Outline.Min(point => point.X);
            return Math.Abs(segment.Start.X - minimumX)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(segment.End.X - minimumX)
                    <= GeometryTolerance.Coordinate;
        }

        private static bool IsAtMinimumY(
            LayoutDrawingTile tile,
            LineSegment3D segment)
        {
            double minimumY = tile.Outline.Min(point => point.Y);
            return Math.Abs(segment.Start.Y - minimumY)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(segment.End.Y - minimumY)
                    <= GeometryTolerance.Coordinate;
        }

        private static bool IsExtremeRoomEdge(
            AxisAlignedOrthogonalPolygon room,
            LineSegment3D edge)
        {
            bool horizontal = Math.Abs(edge.Start.Y - edge.End.Y)
                <= GeometryTolerance.Coordinate;
            return horizontal
                ? Math.Abs(edge.Start.Y - room.South)
                        <= GeometryTolerance.Coordinate
                    || Math.Abs(edge.Start.Y - room.North)
                        <= GeometryTolerance.Coordinate
                : Math.Abs(edge.Start.X - room.West)
                        <= GeometryTolerance.Coordinate
                    || Math.Abs(edge.Start.X - room.East)
                        <= GeometryTolerance.Coordinate;
        }

        private static LayoutDrawingPlan Plan(
            EngineeringOrthogonalDecisionResult result,
            LayoutCandidateState state)
        {
            EvaluatedLayoutCandidate selected = result.Candidates.First(
                item => item.State == state);
            return LayoutDrawingPlanBuilder.Build(result, selected.Id);
        }

        private static EngineeringOrthogonalDecisionResult L01()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    room,
                    600,
                    600,
                    new LayoutPolicyProfile("P-1"),
                    new RoomDecision(
                        new AxisAlignedRectangle(0, 2076, 0, 4476),
                        new DoorOpening(RoomSide.North, 669, 1326),
                        RoomLayoutIntent.WholeRoomSinglePhase),
                    null,
                    LayoutDecisionMode.ControlledProduction));
        }

        private static EngineeringOrthogonalDecisionResult L01East()
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    room,
                    600,
                    600,
                    new LayoutPolicyProfile("P-1"),
                    new RoomDecision(
                        new AxisAlignedRectangle(0, 2076, 0, 4476),
                        new DoorOpening(RoomSide.East, 2800, 3400),
                        RoomLayoutIntent.WholeRoomSinglePhase),
                    null,
                    LayoutDecisionMode.ControlledProduction));
        }

        private static EngineeringOrthogonalDecisionResult
            L01FinishedFaceWithGrout()
        {
            AxisAlignedOrthogonalPolygon source = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    source,
                    600,
                    600,
                    new LayoutPolicyProfile("P-1"),
                    new RoomDecision(
                        new AxisAlignedRectangle(100, 1976, 100, 4376),
                        new DoorOpening(RoomSide.North, 669, 1326),
                        RoomLayoutIntent.WholeRoomSinglePhase),
                    null,
                    LayoutDecisionMode.ControlledProduction,
                    false,
                    1.5,
                    100,
                    source));
        }

        private static EngineeringOrthogonalDecisionResult L01RightHandDoor(
            RoomSide wall)
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(2076, 0), P(2076, 4476),
                P(200, 4476), P(200, 3476), P(0, 3476));
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    room,
                    600,
                    600,
                    new LayoutPolicyProfile("P-1"),
                    new RoomDecision(
                        new AxisAlignedRectangle(0, 2076, 0, 4476),
                        new DoorOpening(wall, 1300, 1900),
                        RoomLayoutIntent.WholeRoomSinglePhase),
                    null,
                    LayoutDecisionMode.ControlledProduction));
        }

        private static EngineeringOrthogonalDecisionResult L04(
            double upperWidth,
            double lowerWidth,
            double? secondMinimum)
        {
            AxisAlignedOrthogonalPolygon room = Room(
                P(0, 0), P(lowerWidth, 0), P(lowerWidth, 1774),
                P(upperWidth, 1774), P(upperWidth, 3526), P(0, 3526));
            var main = new AxisAlignedRectangle(0, upperWidth, 1774, 3526);
            var secondary = new AxisAlignedRectangle(0, lowerWidth, 0, 1774);
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    room,
                    600,
                    600,
                    new LayoutPolicyProfile("P-1", secondMinimum),
                    new RoomDecision(
                        main,
                        new DoorOpening(RoomSide.East, 2800, 3400),
                        RoomLayoutIntent.MainSecondary,
                        new MainSecondaryRegionDefinition(main, secondary),
                        L(0, 1774, upperWidth, 1774)),
                    null,
                    LayoutDecisionMode.Research));
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

            OrthogonalRoomValidationResult result =
                OrthogonalRoomValidator.Validate(lines);
            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            return result.Room;
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

        private static RoomSide Opposite(RoomSide side)
        {
            switch (side)
            {
                case RoomSide.West:
                    return RoomSide.East;
                case RoomSide.East:
                    return RoomSide.West;
                case RoomSide.South:
                    return RoomSide.North;
                default:
                    return RoomSide.South;
            }
        }

        private static bool Nearly(double first, double second)
        {
            return Math.Abs(first - second) <= GeometryTolerance.Coordinate;
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            try
            {
                action();
                Assert.Fail("Expected " + typeof(TException).Name + ".");
            }
            catch (TException)
            {
            }
        }
    }
}
