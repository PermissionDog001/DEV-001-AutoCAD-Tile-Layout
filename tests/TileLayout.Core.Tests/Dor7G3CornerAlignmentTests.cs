using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class Dor7G3CornerAlignmentTests
    {
        [TestMethod]
        public void CandidateOverviewUsesCompleteRoomBoundaryInsteadOfControlRegion()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            Assert.IsTrue(workflow.LoadBoundary(
                SimpleLRoomLines(),
                600,
                600).IsValid);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G3-BOUNDARY",
                100);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(400, 1200, 0, 600));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 100, 500));

            GuidedCandidatePresentation candidate = workflow.Candidates
                .First(item => item.Candidate.HasRawCandidate);
            string overview = OrthogonalDecisionGuidedText
                .FormatCandidateOverview(candidate.Candidate);

            StringAssert.Contains(
                overview,
                "完整房间最外包络实际边砖：西墙 100 mm、东墙 500 mm");
            StringAssert.Contains(
                overview,
                "排版相位参考带（仅用于生成相位，不代表完整房间墙段）："
                    + "西侧 300 mm、东侧 500 mm");
        }

        [TestMethod]
        public void GuidedSatisfiedGroupExcludesOppositeBoundaryExceptions()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();
            Assert.IsTrue(workflow.LoadBoundary(
                SimpleLRoomLines(),
                600,
                600).IsValid);
            workflow.ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                "P-G3-GROUP",
                100);
            workflow.SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            workflow.SetControlRegion(
                new AxisAlignedRectangle(400, 1200, 0, 600));
            workflow.SetControlDoor(
                new DoorOpening(RoomSide.East, 100, 500));

            Assert.IsTrue(workflow.RuleSatisfiedCandidates.All(candidate =>
                !candidate.Candidate.Candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == CandidateDiagnosticCode
                        .SmallBoundaryCutWithoutOppositeFullOrSeam)));
        }

        [TestMethod]
        public void SimpleL_ClassifiesOneReflexTargetAndFiveConvexDiagnostics()
        {
            EngineeringOrthogonalLayoutResult result = CalculateSimpleL();
            LayoutCandidate candidate = result.Candidates.First();

            Assert.AreEqual(6, candidate.WallCornerAssessments.Count);
            Assert.AreEqual(1, candidate.WallCornerAssessments.Count(corner =>
                corner.IsOptimizationTarget
                && corner.GeometryType == WallCornerGeometryType.Reflex270));
            Assert.AreEqual(5, candidate.WallCornerAssessments.Count(corner =>
                !corner.IsOptimizationTarget
                && corner.GeometryType == WallCornerGeometryType.Convex90));
            CollectionAssert.AreEqual(
                new[]
                {
                    "corner-0001", "corner-0002", "corner-0003",
                    "corner-0004", "corner-0005", "corner-0006"
                },
                candidate.WallCornerAssessments.Select(corner =>
                    corner.Id).ToArray());
        }

        [TestMethod]
        public void ConfirmedCornerPhase_UsesActualClippedLinesForDoubleAlignment()
        {
            var confirmed = new ConfirmedGridPhase(
                "corner",
                600,
                600,
                "Exact reflex-corner phase.");
            EngineeringOrthogonalLayoutResult result = CalculateSimpleL(
                new List<ConfirmedGridPhase> { confirmed });
            LayoutCandidate candidate = result.Candidates.Single(value =>
                value.Id == "whole-confirmed-corner");
            WallCornerAssessment target = candidate.WallCornerAssessments.Single(
                corner => corner.IsOptimizationTarget);

            Assert.IsTrue(target.HasVerticalSeam);
            Assert.IsTrue(target.HasHorizontalSeam);
            Assert.IsTrue(target.IsExactGridIntersection);
            Assert.AreEqual(1,
                candidate.Metrics.OptimizationTargetCornerCount);
            Assert.AreEqual(1,
                candidate.Metrics.ExactGridIntersectionCornerCount);
            Assert.AreEqual(1,
                candidate.Metrics.ExactSeamAlignedCornerCount);
            Assert.AreEqual(1, candidate.Metrics.KeyAlignmentCount);
        }

        [TestMethod]
        public void WallCornerSafeThreshold_IncludesTwoThirdsEquality()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(1600, 0),
                        new Point3D(1600, 1000),
                        new Point3D(400, 1000),
                        new Point3D(400, 600),
                        new Point3D(0, 600)
                    });
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 1600, 0, 1000),
                        new DoorOpening(RoomSide.South, 100, 500),
                        null,
                        null,
                        null,
                        true));
            LayoutCandidate candidate = result.Candidates.Single(value =>
                value.Id == "whole-door-default-orthogonal-along-wall-flipped");
            WallCornerAssessment target = candidate.WallCornerAssessments.Single(
                corner => corner.IsOptimizationTarget);

            Assert.AreEqual(400.0, target.VerticalAdjacentSpanA.Value,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.0, target.VerticalAdjacentSpanB.Value,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.0, target.HorizontalAdjacentSpanA.Value,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(400.0, target.HorizontalAdjacentSpanB.Value,
                GeometryTolerance.Coordinate);
            Assert.IsTrue(target.IsSafeDoubleAlignment);
            Assert.AreEqual(1,
                candidate.Metrics.SafeDoubleWallCornerAlignmentCount);
        }

        [TestMethod]
        public void OptionalWallCornerPreferenceSortsOnlyWhenEnabled()
        {
            EngineeringOrthogonalDecisionResult disabled = DecisionSimpleL(false);
            EngineeringOrthogonalDecisionResult enabled = DecisionSimpleL(true);

            CollectionAssert.AreEqual(
                disabled.RawResult.Candidates.Select(candidate => candidate.Id)
                    .ToArray(),
                disabled.Candidates.Select(candidate => candidate.Id).ToArray());
            CollectionAssert.AreNotEqual(
                disabled.Candidates.Select(candidate => candidate.Id).ToArray(),
                enabled.Candidates.Select(candidate => candidate.Id).ToArray());
            Assert.IsTrue(enabled.Candidates[0].HasRawCandidate);
            Assert.AreEqual(LayoutCandidateState.AutomaticUsable,
                enabled.Candidates[0].State);
            Assert.AreEqual(0,
                enabled.Candidates[0].Candidate.Metrics
                    .EntranceVisualBelowRecommendedBoundaryTileCount);
            Assert.IsTrue(enabled.Candidates[0].Candidate.Metrics
                .SafeDoubleWallCornerAlignmentCount > 0);
            Assert.AreEqual(1, enabled.Candidates[0].OriginalIndex);
        }

        [TestMethod]
        public void EnabledWallCornerSearchKeepsG1RecoveryAheadOfCornerOnlyPhases()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(1976, 0),
                        new Point3D(1976, 1976),
                        new Point3D(600, 1976),
                        new Point3D(600, 1376),
                        new Point3D(0, 1376)
                    });
            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        room,
                        600,
                        600,
                        new LayoutPolicyProfile("P-G3-REDISTRIBUTION", 100),
                        new RoomDecision(
                            new AxisAlignedRectangle(0, 1800, 0, 1800),
                            new DoorOpening(RoomSide.West, 700, 1300),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        true));

            EvaluatedLayoutCandidate recovery = decision.Candidates.Single(
                candidate => candidate.Id
                    == "whole-door-default-room-boundary-redistributed");
            Assert.AreEqual(LayoutCandidateState.AutomaticUsable,
                recovery.State);
            Assert.IsFalse(recovery.Candidate.IsRejected);
            Assert.IsTrue(recovery.Candidate.GetAxisPlan(TileLayoutAxis.X)
                .UsesRedistribution);
            Assert.IsTrue(recovery.Candidate.GetAxisPlan(TileLayoutAxis.Y)
                .UsesRedistribution);
            Assert.AreEqual(recovery.Id, decision.Candidates.First().Id);

            string overview = OrthogonalDecisionGuidedText
                .FormatCandidateOverview(decision.Candidates.First());
            StringAssert.Contains(overview, "门洞边界调整");
            StringAssert.Contains(overview, "南北轴");
        }

        [TestMethod]
        public void DisabledWallCornerPreferenceKeepsCornerFactsReadOnly()
        {
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    SimpleLRoom(),
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 1200, 0, 1200),
                        new DoorOpening(RoomSide.North, 100, 500),
                        null,
                        null,
                        null,
                        false));

            Assert.IsFalse(result.GenerationReport.WallCornerSearchEnabled);
            Assert.IsFalse(result.GenerationReport.PhaseSearchEnabled);
            Assert.IsFalse(result.Candidates.Any(candidate =>
                candidate.PhaseSources.Any(source =>
                    source.IsTargetCornerAnchor)));

            string text = OrthogonalDecisionGuidedText
                .FormatCandidateGenerationReport(result.GenerationReport);
            StringAssert.Contains(
                text,
                "墙角命中仅作只读诊断，不参与相位生成或候选排序");
        }

        [TestMethod]
        public void CornerAlternativeOverviewExplainsG1ThresholdDecision()
        {
            EngineeringOrthogonalDecisionResult decision = DecisionSimpleL(true);
            EvaluatedLayoutCandidate evaluated = decision.Candidates.First(value =>
                value.HasRawCandidate
                && value.Candidate.PhaseSources.Any(source =>
                    source.IsTargetCornerAnchor)
                && !value.Candidate.PhaseSources.Any(source =>
                    source.Kind == GridPhaseSourceKind
                        .DoorControlledBoundaryRedistribution));

            string overview = OrthogonalDecisionGuidedText
                .FormatCandidateOverview(evaluated);

            StringAssert.Contains(overview, "墙角锚定替代相位");
            StringAssert.Contains(overview, "自然余量达到推荐下限");
            StringAssert.Contains(overview, "未触发半砖—过渡砖重分配");
        }

        [TestMethod]
        public void DoorControlledPatternProvidesFullOrHalfBoundaryAlternative()
        {
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    PatternLRoom(),
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 2148, 0, 2148),
                        new DoorOpening(RoomSide.South, 700, 1300),
                        null,
                        null,
                        new LayoutPolicyProfile("100-mm", 100),
                        true));

            LayoutCandidate candidate = result.Candidates.FirstOrDefault(
                value => !value.IsRejected
                    && value.PhaseSources.Any(source =>
                        source.Axis == TileLayoutAxis.Y
                        && source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern)
                    && value.GetAxisPlan(TileLayoutAxis.Y)
                        .LowBoundary.Width == 348.0
                    && value.GetAxisPlan(TileLayoutAxis.Y)
                        .HighBoundary.Width == 600.0);
            Assert.IsNotNull(candidate);
            BoundaryBandPlan yPlan = candidate.GetAxisPlan(
                TileLayoutAxis.Y);
            Assert.AreEqual(348.0, yPlan.LowBoundary.Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.0, yPlan.HighBoundary.Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(BoundaryBandKind.FullTile,
                yPlan.HighBoundary.Kind);
            Assert.IsTrue(candidate.TileAssessments
                .SelectMany(assessment => assessment.Measurements)
                .Where(measurement => measurement.Axis == TileLayoutAxis.Y)
                .All(measurement => measurement.Status
                    != ProjectCutStatus.BelowProjectAbsoluteMinimum));
        }

        [TestMethod]
        public void G1BoundaryPatternSearchRunsWithoutWallCornerPreference()
        {
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    PatternLRoom(),
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 2148, 0, 2148),
                        new DoorOpening(RoomSide.South, 700, 1300),
                        null,
                        null,
                        new LayoutPolicyProfile("100-mm", 100),
                        false));

            Assert.IsTrue(result.GenerationReport.PhaseSearchEnabled);
            Assert.IsFalse(result.GenerationReport.WallCornerSearchEnabled);
            Assert.IsFalse(result.Candidates.Any(candidate =>
                candidate.PhaseSources.Any(source =>
                    source.IsTargetCornerAnchor)));
            LayoutCandidate pattern = result.Candidates.FirstOrDefault(
                candidate => !candidate.IsRejected
                    && candidate.PhaseSources.Any(source =>
                        source.Axis == TileLayoutAxis.Y
                        && source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern)
                    && candidate.GetAxisPlan(TileLayoutAxis.Y)
                        .LowBoundary.Width == 348.0
                    && candidate.GetAxisPlan(TileLayoutAxis.Y)
                        .HighBoundary.Width == 600.0);
            Assert.IsNotNull(pattern);
            Assert.AreEqual(348.0,
                pattern.GetAxisPlan(TileLayoutAxis.Y)
                    .LowBoundary.Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(600.0,
                pattern.GetAxisPlan(TileLayoutAxis.Y)
                    .HighBoundary.Width,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void DoorControlledPatternIsExplainedInCandidateOverview()
        {
            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        PatternLRoom(),
                        600,
                        600,
                        new LayoutPolicyProfile("100-mm", 100),
                        new RoomDecision(
                            new AxisAlignedRectangle(0, 2148, 0, 2148),
                            new DoorOpening(RoomSide.South, 700, 1300),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        true));

            EvaluatedLayoutCandidate evaluated = decision.Candidates
                .First(value => value.HasRawCandidate
                    && value.Candidate.PhaseSources.Any(source =>
                        source.Axis == TileLayoutAxis.Y
                        && source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern)
                    && value.Candidate.GetAxisPlan(TileLayoutAxis.Y)
                        .HighBoundary.Width == 600.0);
            string overview = OrthogonalDecisionGuidedText
                .FormatCandidateOverview(evaluated);
            StringAssert.Contains(overview, "门洞边界调整模式");
            StringAssert.Contains(overview, "北墙 600 mm（整砖）");
        }

        [TestMethod]
        public void EnabledCornerPriorityRanksAnAccurateSeamAheadOfNoHit()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(2076, 0),
                        new Point3D(2076, 4476),
                        new Point3D(200, 4476),
                        new Point3D(200, 3476),
                        new Point3D(0, 3476)
                    });
            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        room,
                        600,
                        600,
                        new LayoutPolicyProfile("corner-priority", 100),
                        new RoomDecision(
                            new AxisAlignedRectangle(0, 2076, 0, 4476),
                            new DoorOpening(RoomSide.North, 700, 1300),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        true));
            EvaluatedLayoutCandidate first = decision.Candidates.First(
                candidate => candidate.State == LayoutCandidateState.AutomaticUsable
                    || candidate.State == LayoutCandidateState.RequiresUserDecision);

            Assert.IsTrue(first.Candidate.Metrics.ExactSeamAlignedCornerCount > 0);
            Assert.IsTrue(decision.Candidates.Any(candidate =>
                (candidate.State == LayoutCandidateState.AutomaticUsable
                    || candidate.State == LayoutCandidateState.RequiresUserDecision)
                && candidate.Candidate.Metrics.ExactSeamAlignedCornerCount == 0));
        }

        [TestMethod]
        public void ComplexRoomLargeBoundaryCutsWithoutPurposeAreEliminated()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(2300, 0),
                        new Point3D(2300, 2300),
                        new Point3D(600, 2300),
                        new Point3D(600, 600),
                        new Point3D(0, 600)
                    });
            var policy = new LayoutPolicyProfile("large-cut", 100);
            var roomDecision = new RoomDecision(
                new AxisAlignedRectangle(0, 2300, 0, 2300),
                new DoorOpening(RoomSide.South, 700, 1300),
                RoomLayoutIntent.WholeRoomSinglePhase);
            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        room,
                        600,
                        600,
                        policy,
                        roomDecision,
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        false));

            EvaluatedLayoutCandidate eliminated = decision.Candidates
                .FirstOrDefault(candidate => candidate.HasRawCandidate
                    && candidate.Candidate.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == CandidateDiagnosticCode
                            .LargeBoundaryCutWithoutCornerOrSavingBand));
            Assert.IsNotNull(eliminated);
            Assert.AreEqual(LayoutCandidateState.Eliminated, eliminated.State);
            Assert.AreEqual(
                GuidedEliminatedGroup.UnjustifiedLargeBoundaryCut,
                OrthogonalDecisionGuidedText.MapEliminatedGroup(eliminated));
            Assert.AreEqual(
                "大于半砖且无对缝/节材用途",
                OrthogonalDecisionGuidedText.FormatEliminatedGroup(
                    GuidedEliminatedGroup.UnjustifiedLargeBoundaryCut));

            string reason = OrthogonalDecisionGuidedText.FormatCandidateReason(
                eliminated);
            StringAssert.Contains(reason, "不列入满足规则");
        }

        [TestMethod]
        public void RecommendedToHalfBoundaryCutRequiresOppositeAxisFullOrSeam()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(2076, 0),
                        new Point3D(2076, 4476),
                        new Point3D(200, 4476),
                        new Point3D(200, 3476),
                        new Point3D(0, 3476)
                    });
            var policy = new LayoutPolicyProfile("opposite-boundary", 100);
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 2076, 0, 4476),
                        new DoorOpening(RoomSide.East, 2800, 3400),
                        null,
                        null,
                        policy,
                        false));

            LayoutCandidate reviewedDefault = result.Candidates.Single(candidate =>
                candidate.Id == "whole-door-default");
            BoundaryCutMeasurement reviewedCut = reviewedDefault.TileAssessments
                .SelectMany(assessment => assessment.Measurements)
                .FirstOrDefault(measurement => measurement.Axis == TileLayoutAxis.X
                    && measurement.ActualValue + GeometryTolerance.Coordinate
                        >= 252.0
                    && measurement.ActualValue < 300.0);
            Assert.IsNotNull(reviewedCut);
            Assert.IsTrue(reviewedDefault.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode
                    .SmallBoundaryCutWithoutOppositeFullOrSeam));
            Assert.IsTrue(reviewedDefault.RequiresUserReview);

            LayoutCandidate review = result.Candidates.First(candidate =>
                !candidate.IsRejected
                && candidate.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == CandidateDiagnosticCode
                        .SmallBoundaryCutWithoutOppositeFullOrSeam));
            CandidateDiagnostic reviewDiagnostic = review.Diagnostics.First(
                diagnostic => diagnostic.Code == CandidateDiagnosticCode
                    .SmallBoundaryCutWithoutOppositeFullOrSeam);
            Assert.AreEqual(CandidateDiagnosticSeverity.Warning,
                reviewDiagnostic.Severity);
            Assert.IsFalse(review.IsRejected);
            Assert.IsTrue(review.RequiresUserReview);

            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        room,
                        600,
                        600,
                        policy,
                        new RoomDecision(
                            new AxisAlignedRectangle(0, 2076, 0, 4476),
                            new DoorOpening(RoomSide.East, 2800, 3400),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        false));
            EvaluatedLayoutCandidate evaluated = decision.Candidates.Single(
                candidate => candidate.Id == review.Id);
            Assert.AreNotEqual(LayoutCandidateState.AutomaticUsable,
                evaluated.State);
            Assert.IsFalse(decision.Candidates.Any(candidate =>
                candidate.Id == review.Id
                && candidate.State == LayoutCandidateState.AutomaticUsable));
            StringAssert.Contains(
                OrthogonalDecisionGuidedText.FormatCandidateReason(evaluated),
                "不能列入满足规则");
        }

        [TestMethod]
        public void EveryRetainedCandidateWithSmallCutHasOppositeFullOrExactSeam()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(2076, 0),
                        new Point3D(2076, 4476),
                        new Point3D(200, 4476),
                        new Point3D(200, 3476),
                        new Point3D(0, 3476)
                    });
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 2076, 0, 4476),
                        new DoorOpening(RoomSide.East, 2800, 3400),
                        null,
                        null,
                        new LayoutPolicyProfile("invariant", 100),
                        false));

            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        room,
                        600,
                        600,
                        new LayoutPolicyProfile("invariant", 100),
                        new RoomDecision(
                            new AxisAlignedRectangle(0, 2076, 0, 4476),
                            new DoorOpening(RoomSide.East, 2800, 3400),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        false));

            foreach (EvaluatedLayoutCandidate evaluated in decision.Candidates
                .Where(value => value.State
                    == LayoutCandidateState.AutomaticUsable))
            {
                LayoutCandidate candidate = evaluated.Candidate;
                foreach (TileLayoutAxis axis in new[]
                {
                    TileLayoutAxis.X,
                    TileLayoutAxis.Y
                })
                {
                    RoomSide lowSide = axis == TileLayoutAxis.X
                        ? RoomSide.West
                        : RoomSide.South;
                    RoomSide highSide = axis == TileLayoutAxis.X
                        ? RoomSide.East
                        : RoomSide.North;
                    double tileSize = axis == TileLayoutAxis.X
                        ? 600
                        : 600;
                    double half = tileSize * 0.5;
                    foreach (RoomSide side in new[] { lowSide, highSide })
                    {
                        bool hasSmallCut = candidate.TileAssessments.Any(
                            assessment => assessment.Footprint.BoundarySides
                                .Contains(side)
                                && assessment.Measurements.Any(measurement =>
                                    measurement.Axis == axis
                                    && measurement.ActualValue
                                        + GeometryTolerance.Coordinate
                                            >= measurement.RecommendedMinimum
                                    && measurement.ActualValue < half));
                        if (!hasSmallCut)
                        {
                            continue;
                        }

                        RoomSide opposite = side == RoomSide.West
                            ? RoomSide.East
                            : side == RoomSide.East
                                ? RoomSide.West
                                : side == RoomSide.South
                                    ? RoomSide.North
                                    : RoomSide.South;
                        bool oppositeFull = candidate.TileAssessments
                            .Where(assessment => assessment.Footprint
                                .BoundarySides.Contains(opposite))
                            .All(assessment => assessment.Footprint.IsFullTile);
                        double expected = opposite == RoomSide.West
                            ? room.West
                            : opposite == RoomSide.East
                                ? room.East
                                : opposite == RoomSide.South
                                    ? room.South
                                    : room.North;
                        bool exactSeam = candidate.WallCornerAssessments.Any(
                            corner => corner.IsOptimizationTarget
                                && (axis == TileLayoutAxis.X
                                    ? corner.HasVerticalSeam
                                    : corner.HasHorizontalSeam)
                                && System.Math.Abs(
                                    (axis == TileLayoutAxis.X
                                        ? corner.Position.X
                                        : corner.Position.Y) - expected)
                                    <= GeometryTolerance.Coordinate);
                        Assert.IsTrue(
                            oppositeFull || exactSeam,
                            candidate.Id + " has an unjustified " + axis
                                + " boundary cut on " + side + ".");
                    }
                }
            }
        }

        [TestMethod]
        public void ConfirmedPhaseCannotBypassOppositeBoundaryEligibility()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(2076, 0),
                        new Point3D(2076, 4476),
                        new Point3D(200, 4476),
                        new Point3D(200, 3476),
                        new Point3D(0, 3476)
                    });
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 2076, 0, 4476),
                        new DoorOpening(RoomSide.East, 2800, 3400),
                        null,
                        new List<ConfirmedGridPhase>
                        {
                            new ConfirmedGridPhase(
                                "confirmed-small-cut",
                                600,
                                288,
                                "Confirmed phase regression fixture.")
                        },
                        new LayoutPolicyProfile("confirmed", 100),
                        false));
            LayoutCandidate candidate = result.Candidates.Single(value =>
                value.Id == "whole-confirmed-confirmed-small-cut");

            Assert.IsTrue(candidate.TileAssessments.SelectMany(assessment =>
                    assessment.Measurements).Any(measurement =>
                    measurement.Axis == TileLayoutAxis.Y
                    && measurement.ActualValue
                        + GeometryTolerance.Coordinate
                            >= measurement.RecommendedMinimum
                    && measurement.ActualValue < 300));
            Assert.IsTrue(candidate.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode
                    .SmallBoundaryCutWithoutOppositeFullOrSeam));
            Assert.IsTrue(candidate.RequiresUserReview);

        }

        [TestMethod]
        public void CompleteOppositeFullTilesAllowSavingCut()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(1476, 0),
                        new Point3D(1476, 600),
                        new Point3D(600, 600),
                        new Point3D(600, 1200),
                        new Point3D(0, 1200)
                    });
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 600, 0, 600),
                        new DoorOpening(RoomSide.East, 100, 500),
                        null,
                        new List<ConfirmedGridPhase>
                        {
                            new ConfirmedGridPhase(
                                "full-opposite",
                                276,
                                0,
                                "Complete opposite full-tile regression fixture.")
                        },
                        new LayoutPolicyProfile("full-opposite", 100),
                        false));
            LayoutCandidate candidate = result.Candidates.Single(value =>
                value.Id == "whole-confirmed-full-opposite");

            Assert.IsTrue(candidate.TileAssessments.SelectMany(assessment =>
                    assessment.Measurements).Any(measurement =>
                    measurement.Axis == TileLayoutAxis.X
                    && measurement.ActualValue
                        + GeometryTolerance.Coordinate
                            >= measurement.RecommendedMinimum
                    && measurement.ActualValue < 300));
            Assert.IsFalse(candidate.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode
                    .SmallBoundaryCutWithoutOppositeFullOrSeam));
        }

        [TestMethod]
        public void RecommendedEqualityPairsWithOppositeLargeCutRequireReview()
        {
            const double east = 1426.935;
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(east, 0),
                        new Point3D(east, 600),
                        new Point3D(600, 600),
                        new Point3D(600, 1200),
                        new Point3D(0, 1200)
                    });
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    room,
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 600, 0, 600),
                        new DoorOpening(RoomSide.East, 100, 500),
                        null,
                        new List<ConfirmedGridPhase>
                        {
                            new ConfirmedGridPhase(
                                "recommended-equality",
                                252,
                                0,
                                "Recommended-equality pair regression fixture.")
                        },
                        new LayoutPolicyProfile("recommended-equality", 100),
                        false));
            LayoutCandidate candidate = result.Candidates.Single(value =>
                value.Id == "whole-confirmed-recommended-equality");

            Assert.IsTrue(candidate.TileAssessments.SelectMany(assessment =>
                    assessment.Measurements).Any(measurement =>
                    measurement.Axis == TileLayoutAxis.X
                    && System.Math.Abs(measurement.ActualValue - 252.0)
                        <= GeometryTolerance.Coordinate));
            Assert.IsTrue(candidate.TileAssessments.SelectMany(assessment =>
                    assessment.Measurements).Any(measurement =>
                    measurement.Axis == TileLayoutAxis.X
                    && System.Math.Abs(measurement.ActualValue - 574.935)
                        <= GeometryTolerance.Coordinate));
            Assert.IsTrue(candidate.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode
                    .SmallBoundaryCutWithoutOppositeFullOrSeam));
            Assert.IsTrue(candidate.RequiresUserReview);

            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        room,
                        600,
                        600,
                        new LayoutPolicyProfile(
                            "recommended-equality",
                            100),
                        new RoomDecision(
                            new AxisAlignedRectangle(0, 600, 0, 600),
                            new DoorOpening(RoomSide.East, 100, 500),
                            RoomLayoutIntent.WholeRoomSinglePhase,
                            null,
                            null,
                            new List<ConfirmedGridPhase>
                            {
                                new ConfirmedGridPhase(
                                    "recommended-equality",
                                    252,
                                    0,
                                    "Recommended-equality pair regression fixture.")
                            }),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        false));
            EvaluatedLayoutCandidate evaluated = decision.Candidates.Single(
                value => value.Id == "whole-confirmed-recommended-equality");
            Assert.AreEqual(
                LayoutCandidateState.RequiresUserDecision,
                evaluated.State);
        }

        [TestMethod]
        public void ComplexRoomPatternThatClipsBelowAbsoluteIsExplicitlyEliminated()
        {
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    ComplexOrthogonalBoundaryFixture.CreateRoom(),
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        ComplexOrthogonalBoundaryFixture.CreateControlRegion(),
                        ComplexOrthogonalBoundaryFixture
                            .CreateDeterministicWestDoor(),
                        null,
                        null,
                        new LayoutPolicyProfile("100-mm", 100),
                        false));

            LayoutCandidate eliminatedPattern = result.Candidates
                .FirstOrDefault(candidate => candidate.IsRejected
                    && candidate.PhaseSources.Any(source =>
                        source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern)
                    && candidate.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == CandidateDiagnosticCode
                            .DoorControlledBoundaryPatternClippedBelowAbsoluteMinimum));

            Assert.IsNotNull(eliminatedPattern);
            Assert.IsTrue(eliminatedPattern.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode.MinimumCutNotMet));
            Assert.IsTrue(eliminatedPattern.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode
                    .DoorControlledBoundaryPatternClippedBelowAbsoluteMinimum
                && diagnostic.Axis.HasValue
                && diagnostic.ActualValue.HasValue));
        }

        [TestMethod]
        public void DoorControlledPatternRetainsMirroredOrientationForComparison()
        {
            EngineeringOrthogonalLayoutResult result =
                EngineeringOrthogonalLayoutCalculator.Calculate(
                    PatternLRoom(),
                    new EngineeringOrthogonalLayoutParameters(
                        600,
                        600,
                        new AxisAlignedRectangle(0, 2148, 0, 2148),
                        new DoorOpening(RoomSide.South, 700, 1300),
                        null,
                        null,
                        new LayoutPolicyProfile("100-mm", 100),
                        false));

            LayoutCandidate mirrored = result.Candidates.FirstOrDefault(
                candidate => !candidate.IsRejected
                    && candidate.PhaseSources.Any(source =>
                        source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern
                        && source.Reason.IndexOf(
                            "mirrored",
                            System.StringComparison.OrdinalIgnoreCase) >= 0));
            Assert.IsNotNull(mirrored);
        }

        [TestMethod]
        public void PatternClippingReasonIsShownBeforeGenericMinimumReason()
        {
            EngineeringOrthogonalDecisionResult decision =
                EngineeringOrthogonalDecisionCalculator.Calculate(
                    new EngineeringOrthogonalDecisionRequest(
                        ComplexOrthogonalBoundaryFixture.CreateRoom(),
                        600,
                        600,
                        new LayoutPolicyProfile("100-mm", 100),
                        new RoomDecision(
                            ComplexOrthogonalBoundaryFixture.CreateControlRegion(),
                            ComplexOrthogonalBoundaryFixture
                                .CreateDeterministicWestDoor(),
                            RoomLayoutIntent.WholeRoomSinglePhase),
                        null,
                        LayoutDecisionMode.ControlledProduction,
                        false));
            EvaluatedLayoutCandidate evaluated = decision.Candidates
                .FirstOrDefault(candidate => candidate.HasRawCandidate
                    && candidate.Candidate.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == CandidateDiagnosticCode
                            .DoorControlledBoundaryPatternClippedBelowAbsoluteMinimum));
            Assert.IsNotNull(evaluated);
            string reason = OrthogonalDecisionGuidedText.FormatCandidateReason(
                evaluated);
            StringAssert.Contains(reason, "门洞控制的半砖/整砖");
            StringAssert.Contains(reason, "硬淘汰");
        }

        [TestMethod]
        public void GeneratedPhaseCarriesMergedTargetAndBoundarySources()
        {
            EngineeringOrthogonalLayoutResult result = CalculateSimpleL();
            LayoutCandidate candidate = result.Candidates.First(value =>
                value.PhaseSources.Any(source =>
                    source.Axis == TileLayoutAxis.X
                    && source.IsTargetCornerAnchor
                    && source.CornerId == "corner-0004")
                && value.PhaseSources.Any(source =>
                    source.Axis == TileLayoutAxis.Y
                    && source.IsTargetCornerAnchor
                    && source.CornerId == "corner-0004"));

            Assert.IsTrue(candidate.PhaseSources.Any(source =>
                source.Axis == TileLayoutAxis.X
                && source.IsTargetCornerAnchor
                && source.CornerId == "corner-0004"));
            Assert.IsTrue(candidate.PhaseSources.Any(source =>
                source.Axis == TileLayoutAxis.Y
                && source.IsTargetCornerAnchor
                && source.CornerId == "corner-0004"));
            Assert.IsTrue(candidate.PhaseSources.Any(source =>
                source.Kind == GridPhaseSourceKind.BoundaryVertex));
            Assert.IsTrue(result.GenerationReport.MergedPhaseSourceCount > 0);
        }

        [TestMethod]
        public void GenerationReport_ExposesBoundedAnchorStatistics()
        {
            EngineeringOrthogonalLayoutResult result = CalculateSimpleL();
            CandidateGenerationReport report = result.GenerationReport;

            Assert.AreEqual(1, report.OptimizationTargetCornerCount);
            Assert.IsTrue(report.XTargetAnchorPhaseCount > 0);
            Assert.IsTrue(report.YTargetAnchorPhaseCount > 0);
            Assert.AreEqual(1, report.DoubleAnchorCombinationCount);
            Assert.IsTrue(report.SingleAnchorCombinationCount > 0);
            Assert.IsFalse(report.AnchorCombinationLimitReached);
            Assert.IsTrue(report.XPhaseCount
                <= CandidateSearchLimits.MaximumPhaseCandidatesPerAxis);
            Assert.IsTrue(report.YPhaseCount
                <= CandidateSearchLimits.MaximumPhaseCandidatesPerAxis);
            Assert.IsTrue(report.PhaseCombinationCount
                <= CandidateSearchLimits.MaximumWholeRoomPhaseCombinations);
        }

        [TestMethod]
        public void DrawingPlan_ProjectsSameCornerAssessmentsWithoutRecalculation()
        {
            EngineeringOrthogonalDecisionResult decision = DecisionSimpleL();
            EvaluatedLayoutCandidate selected = decision.Candidates.First(value =>
                value.HasRawCandidate
                && value.State != LayoutCandidateState.Eliminated
                && value.Candidate.Metrics.ExactSeamAlignedCornerCount > 0);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                decision,
                selected.Id);

            Assert.AreEqual(
                selected.Candidate.WallCornerAssessments.Count,
                plan.WallCorners.Count);
            for (int index = 0; index < plan.WallCorners.Count; index++)
            {
                WallCornerAssessment source =
                    selected.Candidate.WallCornerAssessments[index];
                LayoutDrawingWallCorner projected = plan.WallCorners[index];
                Assert.AreEqual(source.Id, projected.Id);
                Assert.AreEqual(source.IsOptimizationTarget,
                    projected.IsOptimizationTarget);
                Assert.AreEqual(source.HasVerticalSeam,
                    projected.HasVerticalSeam);
                Assert.AreEqual(source.HasHorizontalSeam,
                    projected.HasHorizontalSeam);
                Assert.AreEqual(source.HasSafeVerticalSeam,
                    projected.HasSafeVerticalSeam);
                Assert.AreEqual(source.HasSafeHorizontalSeam,
                    projected.HasSafeHorizontalSeam);
                Assert.AreEqual(source.VerticalAdjacentSpanA,
                    projected.VerticalAdjacentSpanA);
                Assert.AreEqual(source.HorizontalAdjacentSpanA,
                    projected.HorizontalAdjacentSpanA);
            }
        }

        [TestMethod]
        public void GuidedText_ReportsTargetHitsDiagnosticsAndRawDistances()
        {
            EngineeringOrthogonalDecisionResult decision = DecisionSimpleL();
            EvaluatedLayoutCandidate selected = decision.Candidates.First(value =>
                value.HasRawCandidate
                && value.State != LayoutCandidateState.Eliminated
                && value.Candidate.Metrics.ExactSeamAlignedCornerCount > 0);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                decision,
                selected.Id);

            string text = OrthogonalDecisionGuidedText
                .FormatWallCornerDiagnostics(plan);

            StringAssert.Contains(text, "目标反射角 1 个");
            StringAssert.Contains(text, "270°目标转角");
            StringAssert.Contains(text, "90°只读转角");
            StringAssert.Contains(text, "最近竖缝");
            StringAssert.Contains(text, "最近横缝");
            StringAssert.Contains(text, "Core 1E-06 mm");
            StringAssert.Contains(text, "2/3T");
        }

        [TestMethod]
        public void SvgRenderer_UsesProjectedCornerFactsForReadOnlyMarkers()
        {
            EngineeringOrthogonalDecisionResult decision = DecisionSimpleL();
            EvaluatedLayoutCandidate selected = decision.Candidates.First(value =>
                value.HasRawCandidate
                && value.State != LayoutCandidateState.Eliminated
                && value.Candidate.Metrics.ExactGridIntersectionCornerCount > 0);
            LayoutDrawingPlan plan = LayoutDrawingPlanBuilder.Build(
                decision,
                selected.Id);

            string svg = LayoutDrawingPlanSvgRenderer.Render(plan);

            StringAssert.Contains(svg, "id=\"wall-corners\"");
            StringAssert.Contains(svg, "id=\"corner-0004\"");
            StringAssert.Contains(svg, "data-geometry=\"Reflex270\"");
            StringAssert.Contains(svg, "data-target=\"true\"");
            StringAssert.Contains(svg, "data-vertical-hit=\"true\"");
            StringAssert.Contains(svg, "data-horizontal-hit=\"true\"");
        }

        [TestMethod]
        public void SameStateParetoRetainsAlignmentTradeoffsWithoutTotalScore()
        {
            EngineeringOrthogonalLayoutResult result = CalculateL01();
            LayoutCandidate[] retained = result.Candidates.Where(candidate =>
                !candidate.IsRejected).ToArray();
            IGrouping<string, LayoutCandidate> tradeoffGroup = retained
                .GroupBy(candidate => string.Join("|",
                    candidate.RequiresProjectPolicy,
                    candidate.RequiresUserReview))
                .FirstOrDefault(group => group.Any(candidate =>
                        candidate.Metrics.ExactSeamAlignedCornerCount > 0)
                    && group.Any(candidate =>
                        candidate.Metrics.ExactSeamAlignedCornerCount == 0));

            Assert.IsNotNull(tradeoffGroup);
            Assert.IsTrue(tradeoffGroup.Select(candidate =>
                candidate.Metrics.ExactSeamAlignedCornerCount).Distinct()
                .Count() > 1);
        }

        [TestMethod]
        public void ComplexFixture_AnchorsStayBoundedAuditableAndResponsive()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom();
            var stopwatch = Stopwatch.StartNew();
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
                        null,
                        true));
            stopwatch.Stop();

            CandidateGenerationReport report = result.GenerationReport;
            Assert.AreEqual(5, report.OptimizationTargetCornerCount);
            Assert.IsTrue(report.XTargetAnchorPhaseCount > 0);
            Assert.IsTrue(report.YTargetAnchorPhaseCount > 0);
            Assert.IsTrue(report.DoubleAnchorCombinationCount > 0);
            Assert.IsTrue(report.SingleAnchorCombinationCount > 0);
            Assert.IsTrue(report.PhaseCombinationCount
                <= CandidateSearchLimits.MaximumWholeRoomPhaseCombinations);
            Assert.IsTrue(result.Candidates.Count(candidate =>
                !candidate.IsRejected)
                <= CandidateSearchLimits.MaximumNonDominatedCandidates);
            Assert.IsTrue(result.Candidates.Any(candidate =>
                candidate.Metrics.ExactGridIntersectionCornerCount > 0));
            Assert.IsTrue(stopwatch.Elapsed < System.TimeSpan.FromSeconds(10));
        }

        private static EngineeringOrthogonalLayoutResult CalculateSimpleL(
            IList<ConfirmedGridPhase> confirmed = null)
        {
            AxisAlignedOrthogonalPolygon room = SimpleLRoom();
            return EngineeringOrthogonalLayoutCalculator.Calculate(
                room,
                new EngineeringOrthogonalLayoutParameters(
                    600,
                    600,
                    new AxisAlignedRectangle(0, 1200, 0, 1200),
                    new DoorOpening(RoomSide.North, 100, 500),
                    null,
                    confirmed,
                    null,
                    true));
        }

        private static EngineeringOrthogonalDecisionResult DecisionSimpleL(
            bool preferWallCornerAlignment = false)
        {
            AxisAlignedOrthogonalPolygon room = SimpleLRoom();
            return EngineeringOrthogonalDecisionCalculator.Calculate(
                new EngineeringOrthogonalDecisionRequest(
                    room,
                    600,
                    600,
                    new LayoutPolicyProfile("100-mm", 100),
                    new RoomDecision(
                        new AxisAlignedRectangle(0, 1200, 0, 1200),
                        new DoorOpening(RoomSide.North, 100, 500),
                        RoomLayoutIntent.WholeRoomSinglePhase),
                    null,
                    LayoutDecisionMode.ControlledProduction,
                    preferWallCornerAlignment));
        }

        private static EngineeringOrthogonalLayoutResult CalculateL01()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom(
                    new List<Point3D>
                    {
                        new Point3D(0, 0),
                        new Point3D(2076, 0),
                        new Point3D(2076, 4476),
                        new Point3D(200, 4476),
                        new Point3D(200, 3476),
                        new Point3D(0, 3476)
                    });
            return EngineeringOrthogonalLayoutCalculator.Calculate(
                room,
                new EngineeringOrthogonalLayoutParameters(
                    600,
                    600,
                    new AxisAlignedRectangle(0, 2076, 0, 4476),
                    new DoorOpening(RoomSide.North, 700, 1300),
                    null,
                    null,
                    null,
                    true));
        }

        private static AxisAlignedOrthogonalPolygon SimpleLRoom()
        {
            return ComplexOrthogonalBoundaryFixture.CreateRoom(
                new List<Point3D>
                {
                    new Point3D(0, 0),
                    new Point3D(1200, 0),
                    new Point3D(1200, 600),
                    new Point3D(600, 600),
                    new Point3D(600, 1200),
                    new Point3D(0, 1200)
                });
        }

        private static AxisAlignedOrthogonalPolygon PatternLRoom()
        {
            return ComplexOrthogonalBoundaryFixture.CreateRoom(
                new List<Point3D>
                {
                    new Point3D(0, 0),
                    new Point3D(2148, 0),
                    new Point3D(2148, 600),
                    new Point3D(600, 600),
                    new Point3D(600, 2148),
                    new Point3D(0, 2148)
                });
        }

        private static List<LineSegment3D> SimpleLRoomLines()
        {
            var vertices = new List<Point3D>
            {
                new Point3D(0, 0),
                new Point3D(1200, 0),
                new Point3D(1200, 600),
                new Point3D(600, 600),
                new Point3D(600, 1200),
                new Point3D(0, 1200)
            };
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Count; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Count]));
            }

            return lines;
        }
    }
}
