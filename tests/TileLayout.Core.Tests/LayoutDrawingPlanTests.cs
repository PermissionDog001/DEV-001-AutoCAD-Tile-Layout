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
