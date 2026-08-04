using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class EngineeringOrthogonalLayoutCalculator
    {
        public static EngineeringOrthogonalLayoutResult Calculate(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (!GeometryTolerance.NearlyEqual(
                room.Elevation,
                parameters.ControlRegion.Elevation)
                || OrthogonalFootprintBuilder.CalculateIntersectionArea(
                    room,
                    parameters.ControlRegion)
                    <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentException(
                    "The explicit control region must overlap the room at the room elevation.",
                    nameof(parameters));
            }

            if (parameters.MainSecondary != null)
            {
                ValidateRectangleInsideRoom(room, parameters.ControlRegion);
                ValidateSameRectangle(
                    parameters.ControlRegion,
                    parameters.MainSecondary.MainRegion,
                    "The control region must be the declared main region.");
            }

            var rectangularParameters =
                new EngineeringRectangularLayoutParameters(
                    parameters.TileWidth,
                    parameters.TileHeight,
                    parameters.DoorOpening,
                    parameters.GroutWidthMm);
            EngineeringRectangularLayoutResult rectangular =
                EngineeringRectangularLayoutCalculator.Calculate(
                    parameters.ControlRegion,
                    rectangularParameters);

            var candidates = new List<LayoutCandidate>();
            CandidateGenerationReport generationReport =
                CandidateGenerationReport.Empty;
            var primaryWholeRoomCandidates = new List<LayoutCandidate>();
            foreach (LayoutCandidate source in rectangular.ViableCandidates)
            {
                LayoutCandidate primary = BuildWholeRoomCandidate(
                    room,
                    parameters,
                    source,
                    "whole-" + source.Id,
                    false,
                    false,
                    null);
                candidates.Add(primary);
                primaryWholeRoomCandidates.Add(primary);
            }

            bool shouldTryClipRecovery =
                !primaryWholeRoomCandidates.All(candidate =>
                    !candidate.IsRejected
                    && candidate.Metrics.BelowDefaultMinimumBoundaryTileCount == 0);
            if (!shouldTryClipRecovery)
            {
                for (int index = 0;
                    index < rectangular.ViableCandidates.Count;
                    index++)
                {
                    if (HasCompleteRoomNarrowRedistribution(
                        room,
                        parameters,
                        rectangular.ViableCandidates[index]))
                    {
                        shouldTryClipRecovery = true;
                        break;
                    }
                }
            }

            if (shouldTryClipRecovery)
            {
                for (int index = 0;
                    index < rectangular.ViableCandidates.Count;
                    index++)
                {
                    LayoutCandidate recovered =
                        TryBuildOrthogonalClipRecovery(
                            room,
                            parameters,
                            rectangular.ViableCandidates[index],
                            primaryWholeRoomCandidates[index]);
                    if (recovered != null)
                    {
                        candidates.Add(recovered);
                        break;
                    }
                }
            }

            foreach (ConfirmedGridPhase phase in
                parameters.ConfirmedWholeRoomPhases)
            {
                candidates.Add(
                    BuildWholeRoomCandidate(
                        room,
                        parameters,
                        null,
                        "whole-confirmed-" + phase.Id,
                        true,
                        phase.RetainBelowDefaultMinimumAsPolicyUndecided,
                        phase));
            }

            bool hasRetainedCandidate = candidates.Any(candidate =>
                !candidate.IsRejected);
            // The door-controlled boundary pattern is a G1 layout rule, not
            // a wall-corner aesthetic option.  A complex room must therefore
            // search for that bounded pattern even when the optional corner
            // preference is off.  The corner preference only decides whether
            // target-corner anchors are added to the same search.
            bool hasDoorControlledPatternOpportunity =
                HasDoorControlledPatternOpportunity(
                    room,
                    parameters);
            bool shouldRunWholeRoomPhaseSearch =
                hasDoorControlledPatternOpportunity
                || (parameters.PreferWallCornerAlignment
                    && (WallCornerEvaluator.HasOptimizationTarget(room)
                        || !hasRetainedCandidate))
                || !hasRetainedCandidate;
            if (parameters.MainSecondary == null
                && shouldRunWholeRoomPhaseSearch
                && IsPartialDoorOnValidatedRoomBoundary(
                    room,
                    parameters.ControlRegion,
                    parameters.DoorOpening))
            {
                generationReport = AddGenericWholeRoomPhaseCandidates(
                    candidates,
                    room,
                    parameters,
                    parameters.PreferWallCornerAlignment);
            }

            if (parameters.MainSecondary != null)
            {
                ConnectionInfo connection = ValidateAndGetConnection(
                    room,
                    parameters.MainSecondary);
                foreach (LayoutCandidate source in rectangular.ViableCandidates)
                {
                    AddMainSecondaryCandidates(
                        candidates,
                        room,
                        parameters,
                        source,
                        connection);
                }
            }

            generationReport = ApplyDominanceAndRetentionLimit(
                candidates,
                generationReport);
            AddMultipleCandidateDiagnostics(candidates);
            return new EngineeringOrthogonalLayoutResult(
                room,
                parameters,
                candidates,
                generationReport);
        }

        private static bool HasDoorControlledPatternOpportunity(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters)
        {
            if (room == null || parameters == null
                || parameters.DoorOpening == null
                // The existing rectangular door calculator remains the
                // compatibility path for TILEDOORRECT.  This G1 phase search
                // is only for the complex orthogonal-room extension.
                || room.Vertices.Count <= 4)
            {
                return false;
            }

            return HasDoorControlledPatternOpportunity(
                    room.Width,
                    parameters.TileWidth,
                    parameters.GridTileWidth)
                || HasDoorControlledPatternOpportunity(
                    room.Height,
                    parameters.TileHeight,
                    parameters.GridTileHeight);
        }

        private static bool HasDoorControlledPatternOpportunity(
            double length,
            double tileSize,
            double gridTileSize)
        {
            double grout = gridTileSize - tileSize;
            double remainder = length % gridTileSize;
            double physicalLength = length - grout;
            if (remainder <= GeometryTolerance.Coordinate
                || gridTileSize - remainder
                    <= GeometryTolerance.Coordinate)
            {
                return false;
            }

            double minimumCut = tileSize
                * EngineeringLayoutRules.DefaultMinimumCutRatio;
            double oppositeWidth;
            double selectedWidth;
            return TryFindBoundaryPattern(
                    physicalLength,
                    tileSize,
                    minimumCut,
                    tileSize * EngineeringLayoutRules.HalfTileRatio,
                    out oppositeWidth,
                    out selectedWidth)
                || TryFindBoundaryPattern(
                    physicalLength,
                    tileSize,
                    minimumCut,
                    tileSize,
                    out oppositeWidth,
                    out selectedWidth);
        }

        private static BoundaryBandPlan BuildCompleteRoomNarrowRedistribution(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            BoundaryBandPlan source)
        {
            if (source == null)
            {
                return null;
            }

            RoomSide halfSide;
            bool doorNormal = source.Axis == TileLayoutAxis.X
                ? parameters.DoorOpening.Wall == RoomSide.West
                    || parameters.DoorOpening.Wall == RoomSide.East
                : parameters.DoorOpening.Wall == RoomSide.South
                    || parameters.DoorOpening.Wall == RoomSide.North;
            if (doorNormal)
            {
                halfSide = Opposite(parameters.DoorOpening.Wall);
            }
            else
            {
                halfSide = source.HalfTileSide ?? source.ControlSide;
            }

            BoundaryBandPlan completePlan = BuildRoomBoundaryRedistribution(
                room,
                source,
                source.Axis == TileLayoutAxis.X
                    ? parameters.TileWidth
                    : parameters.TileHeight,
                halfSide);
            if (completePlan == null)
            {
                return null;
            }

            if (source.UsesRedistribution
                && SameSegmentWidths(
                    source.SegmentWidths,
                    completePlan.SegmentWidths))
            {
                return null;
            }

            return completePlan;
        }

        private static bool SameSegmentWidths(
            IReadOnlyList<double> first,
            IReadOnlyList<double> second)
        {
            if (first == null || second == null || first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                if (!GeometryTolerance.NearlyEqual(
                    first[index],
                    second[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCompleteRoomNarrowRedistribution(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source)
        {
            if (parameters.MainSecondary != null
                || !IsPartialDoorOnValidatedRoomBoundary(
                    room,
                    parameters.ControlRegion,
                    parameters.DoorOpening))
            {
                return false;
            }

            BoundaryBandPlan x = BuildCompleteRoomNarrowRedistribution(
                room,
                parameters,
                source.GetAxisPlan(TileLayoutAxis.X));
            BoundaryBandPlan y = BuildCompleteRoomNarrowRedistribution(
                room,
                parameters,
                source.GetAxisPlan(TileLayoutAxis.Y));
            return x != null || y != null;
        }

        private static CandidateDiagnostic
            CreateCompleteRoomRedistributionDiagnostic(
                TileLayoutAxis axis,
                BoundaryBandPlan redistributed)
        {
            return new CandidateDiagnostic(
                CandidateDiagnosticCode.OrthogonalClipRedistributed,
                CandidateDiagnosticSeverity.Information,
                "The complete room envelope has a narrow remainder after the door-controlled phase was clipped; the frozen half-tile and transition-tile allocation was applied before candidate comparison.",
                axis,
                redistributed.TransitionTileSide,
                redistributed.NaturalRemainder,
                redistributed.MinimumCut);
        }

        private static LayoutCandidate BuildWholeRoomCandidate(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source,
            string id,
            bool confirmed,
            bool retainBelowMinimum,
            ConfirmedGridPhase phase,
            BoundaryBandPlan xPlanOverride = null,
            BoundaryBandPlan yPlanOverride = null,
            CandidateDiagnostic additionalDiagnostic = null,
            IList<GridPhaseSource> phaseSources = null,
            double? xPhaseOverride = null,
            double? yPhaseOverride = null)
        {
            List<double> xCuts;
            List<double> yCuts;
            var plans = new List<BoundaryBandPlan>();
            var diagnostics = new List<CandidateDiagnostic>();
            string reason;
            if (source != null)
            {
                BoundaryBandPlan xPlan = xPlanOverride
                    ?? source.GetAxisPlan(TileLayoutAxis.X);
                BoundaryBandPlan yPlan = yPlanOverride
                    ?? source.GetAxisPlan(TileLayoutAxis.Y);
                plans.Add(xPlan);
                plans.Add(yPlan);
                double xPhase = xPhaseOverride
                    ?? GetFirstPhaseCoordinate(
                        parameters.ControlRegion.West,
                        xPlan);
                double yPhase = yPhaseOverride
                    ?? GetFirstPhaseCoordinate(
                        parameters.ControlRegion.South,
                        yPlan);
                xCuts = GeneratePeriodicCuts(
                    room.West,
                    room.East,
                    xPhase,
                    parameters.GridTileWidth);
                yCuts = GeneratePeriodicCuts(
                    room.South,
                    room.North,
                    yPhase,
                    parameters.GridTileHeight);
                reason =
                    "The door-controlled rectangular phase is extended across the whole room and clipped by the validated orthogonal boundary.";
            }
            else
            {
                xCuts = GeneratePeriodicCuts(
                    room.West,
                    room.East,
                    phase.VerticalSeamCoordinate,
                    parameters.GridTileWidth);
                yCuts = GeneratePeriodicCuts(
                    room.South,
                    room.North,
                    phase.HorizontalSeamCoordinate,
                    parameters.GridTileHeight);
                plans.Add(BuildWholeRoomPhasePlan(
                    TileLayoutAxis.X,
                    room.West,
                    room.East,
                    parameters.TileWidth,
                    xCuts,
                    phaseSources,
                    parameters.GridTileWidth));
                plans.Add(BuildWholeRoomPhasePlan(
                    TileLayoutAxis.Y,
                    room.South,
                    room.North,
                    parameters.TileHeight,
                    yCuts,
                    phaseSources,
                    parameters.GridTileHeight));
                reason = phase.Reason;
            }

            diagnostics.Add(
                new CandidateDiagnostic(
                    CandidateDiagnosticCode.WholeRoomSinglePhaseGenerated,
                    CandidateDiagnosticSeverity.Information,
                    additionalDiagnostic != null
                        && additionalDiagnostic.Code == CandidateDiagnosticCode.AlternativeWholeRoomPhaseGenerated
                        ? "A bounded, explainable alternative whole-room phase was clipped by the validated orthogonal boundary."
                        : confirmed
                        ? "A confirmed whole-room phase was clipped by the validated orthogonal boundary."
                        : "A door-controlled whole-room phase was clipped by the validated orthogonal boundary."));
            if (additionalDiagnostic != null)
            {
                diagnostics.Add(additionalDiagnostic);
            }

            List<LineSegment3D> lines =
                OrthogonalTileGridCalculator.ClipDivisionLines(
                    room,
                    xCuts,
                    yCuts);
            List<TileFootprint> tiles = OrthogonalFootprintBuilder.Build(
                room,
                xCuts,
                yCuts,
                parameters.TileWidth,
                parameters.TileHeight,
                parameters.GroutWidthMm);
            IList<TileFootprintAssessment> tileAssessments;
            IList<WallCornerAssessment> wallCornerAssessments;
            LayoutCandidateMetrics metrics = BuildMetrics(
                room,
                parameters,
                tiles,
                lines,
                0.0,
                0,
                diagnostics,
                out tileAssessments,
                out wallCornerAssessments);
            AddDoorControlledPatternClippingDiagnostics(
                parameters,
                plans,
                phaseSources,
                metrics,
                tileAssessments,
                diagnostics);
            AddUnjustifiedLargeBoundaryCutDiagnostics(
                room,
                parameters,
                plans,
                phaseSources,
                tileAssessments,
                wallCornerAssessments,
                !confirmed,
                diagnostics);
            AddSmallBoundaryCutOppositeEligibilityDiagnostics(
                room,
                parameters,
                tileAssessments,
                wallCornerAssessments,
                true,
                diagnostics);
            var bounds = new AxisAlignedRectangle(
                room.West,
                room.East,
                room.South,
                room.North,
                room.Elevation);
            var structure = new LayoutCandidateStructure(
                OrthogonalCandidateKind.WholeRoomSinglePhase,
                new List<LayoutRegionPhase>
                {
                    new LayoutRegionPhase(
                        "whole-room",
                        LayoutRegionRole.WholeRoom,
                        bounds,
                        xCuts,
                        yCuts,
                        false,
                        false)
                },
                new List<RegionConnectionPlan>());
            return new LayoutCandidate(
                id,
                false,
                false,
                reason,
                plans,
                lines,
                tiles,
                diagnostics,
                metrics,
                structure,
                tileAssessments,
                wallCornerAssessments,
                phaseSources);
        }

        private static void AddDoorControlledPatternClippingDiagnostics(
            EngineeringOrthogonalLayoutParameters parameters,
            IList<BoundaryBandPlan> plans,
            IList<GridPhaseSource> phaseSources,
            LayoutCandidateMetrics metrics,
            IList<TileFootprintAssessment> tileAssessments,
            ICollection<CandidateDiagnostic> diagnostics)
        {
            if (parameters == null
                || metrics == null
                || metrics.BelowProjectAbsoluteMinimumBoundaryTileCount == 0
                || tileAssessments == null
                || diagnostics == null)
            {
                return;
            }

            foreach (TileLayoutAxis axis in new[]
            {
                TileLayoutAxis.X,
                TileLayoutAxis.Y
            })
            {
                if (!HasDoorControlledPatternForAxis(
                    plans,
                    phaseSources,
                    axis))
                {
                    continue;
                }

                BoundaryCutMeasurement measurement = tileAssessments
                    .SelectMany(assessment => assessment.Measurements)
                    .Where(value => value.Axis == axis
                        && value.Status
                            == ProjectCutStatus.BelowProjectAbsoluteMinimum)
                    .OrderBy(value => value.ActualValue)
                    .FirstOrDefault();
                if (measurement == null)
                {
                    continue;
                }

                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode
                            .DoorControlledBoundaryPatternClippedBelowAbsoluteMinimum,
                        CandidateDiagnosticSeverity.Rejection,
                        "The nominal G1 door-controlled half/full boundary pattern "
                            + "was generated, but clipping the same continuous phase "
                            + "to the complete orthogonal room created an independent "
                            + "boundary cut below the project absolute minimum; "
                            + "the pattern candidate is hard-eliminated and safe "
                            + "natural alternatives remain available.",
                        axis,
                        null,
                        measurement.ActualValue,
                        measurement.ProjectAbsoluteMinimum));
            }
        }

        private static bool HasDoorControlledPatternForAxis(
            IList<BoundaryBandPlan> plans,
            IList<GridPhaseSource> phaseSources,
            TileLayoutAxis axis)
        {
            if (plans != null
                && plans.Any(plan => plan.Axis == axis
                    && plan.UsesRedistribution))
            {
                return true;
            }

            return phaseSources != null
                && phaseSources.Any(source => source.Axis == axis
                    && (source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern
                        || source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution));
        }

        private static void AddUnjustifiedLargeBoundaryCutDiagnostics(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            IList<BoundaryBandPlan> plans,
            IList<GridPhaseSource> phaseSources,
            IList<TileFootprintAssessment> tileAssessments,
            IList<WallCornerAssessment> wallCornerAssessments,
            bool enforceQualityGate,
            ICollection<CandidateDiagnostic> diagnostics)
        {
            // This G3 quality gate is intentionally limited to complex rooms.
            // The four-line rectangular commands keep their existing TILE600 /
            // TILEDOORRECT behavior unchanged.
            if (room == null
                || room.Vertices.Count <= 4
                || parameters == null
                || tileAssessments == null
                || !enforceQualityGate
                || diagnostics == null)
            {
                return;
            }

            foreach (TileLayoutAxis axis in new[]
            {
                TileLayoutAxis.X,
                TileLayoutAxis.Y
            })
            {
                double tileSize = axis == TileLayoutAxis.X
                    ? parameters.TileWidth
                    : parameters.TileHeight;
                double half = tileSize * EngineeringLayoutRules.HalfTileRatio;
                double recommended = tileSize
                    * EngineeringLayoutRules.DefaultMinimumCutRatio;
                List<BoundaryCutMeasurement> measurements = tileAssessments
                    .SelectMany(assessment => assessment.Measurements)
                    .Where(measurement => measurement.Axis == axis)
                    .ToList();
                BoundaryCutMeasurement largeCut = measurements
                    .Where(measurement => measurement.ActualValue
                            > half + GeometryTolerance.Coordinate
                        && measurement.ActualValue + GeometryTolerance.Coordinate
                            < tileSize)
                    .OrderByDescending(measurement => measurement.ActualValue)
                    .FirstOrDefault();
                if (largeCut == null)
                {
                    continue;
                }

                bool hasExactCornerSeam = wallCornerAssessments != null
                    && wallCornerAssessments.Any(corner =>
                        corner.IsOptimizationTarget
                        && (axis == TileLayoutAxis.X
                            ? corner.HasVerticalSeam
                            : corner.HasHorizontalSeam));
                bool hasIntentionalTransition = HasDoorControlledPatternForAxis(
                    plans,
                    phaseSources,
                    axis);
                bool hasMaterialSavingBand = measurements.Any(measurement =>
                    measurement.ActualValue + GeometryTolerance.Coordinate
                        >= recommended
                    && measurement.ActualValue
                        <= half + GeometryTolerance.Coordinate);
                if (hasExactCornerSeam
                    || hasIntentionalTransition
                    || hasMaterialSavingBand)
                {
                    continue;
                }

                RoomSide? side = FindMeasurementSide(
                    tileAssessments,
                    largeCut,
                    axis);
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode
                            .LargeBoundaryCutWithoutCornerOrSavingBand,
                        CandidateDiagnosticSeverity.Rejection,
                        "A complex-room boundary axis contains a non-full cut "
                            + "larger than half a tile, but neither an accurate "
                            + "target-corner seam, an intentional G1 transition "
                            + "allocation, nor a same-axis recommended half-or-"
                            + "smaller saving band was found. The candidate is "
                            + "excluded from the compliant group.",
                        axis,
                        side,
                        largeCut.ActualValue,
                        half));
            }
        }

        private static RoomSide? FindMeasurementSide(
            IList<TileFootprintAssessment> assessments,
            BoundaryCutMeasurement target,
            TileLayoutAxis axis)
        {
            if (assessments == null || target == null)
            {
                return null;
            }

            foreach (TileFootprintAssessment assessment in assessments)
            {
                if (assessment == null || assessment.Footprint == null)
                {
                    continue;
                }

                foreach (BoundaryCutMeasurement measurement
                    in assessment.Measurements)
                {
                    if (!object.ReferenceEquals(measurement, target))
                    {
                        continue;
                    }

                    foreach (RoomSide side in assessment.Footprint.BoundarySides)
                    {
                        if ((axis == TileLayoutAxis.X
                                && (side == RoomSide.West
                                    || side == RoomSide.East))
                            || (axis == TileLayoutAxis.Y
                                && (side == RoomSide.South
                                    || side == RoomSide.North)))
                        {
                            return side;
                        }
                    }
                }
            }

            return null;
        }

        private static void AddSmallBoundaryCutOppositeEligibilityDiagnostics(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            IList<TileFootprintAssessment> tileAssessments,
            IList<WallCornerAssessment> wallCornerAssessments,
            bool enforceQualityGate,
            ICollection<CandidateDiagnostic> diagnostics)
        {
            // This rule is deliberately a complex-room quality gate.  The
            // original four-line rectangle commands retain their established
            // behavior.  A confirmed phase is still a user-supplied phase,
            // but it cannot bypass this frozen eligibility condition: a
            // candidate shown as satisfying the rules must obey it as well.
            if (room == null
                || room.Vertices.Count <= 4
                || parameters == null
                || tileAssessments == null
                || !enforceQualityGate
                || diagnostics == null)
            {
                return;
            }

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
                    ? parameters.TileWidth
                    : parameters.TileHeight;
                double half = tileSize * EngineeringLayoutRules.HalfTileRatio;

                foreach (RoomSide side in new[] { lowSide, highSide })
                {
                    BoundaryCutMeasurement smallCut =
                        GetBoundaryMeasurementsForSide(
                            tileAssessments,
                            side,
                            axis)
                        .Where(measurement =>
                            measurement.ActualValue
                                + GeometryTolerance.Coordinate
                                    >= measurement.RecommendedMinimum
                            && measurement.ActualValue
                                + GeometryTolerance.Coordinate < half)
                        .OrderByDescending(measurement =>
                            measurement.ActualValue)
                        .FirstOrDefault();
                    if (smallCut == null)
                    {
                        continue;
                    }

                    RoomSide opposite = Opposite(side);
                    bool oppositeIsFull =
                        HasOnlyFullBoundaryTilesAtSide(
                            tileAssessments,
                            opposite);
                    bool oppositeHasExactSeam =
                        HasExactTargetSeamOnSide(
                            room,
                            wallCornerAssessments,
                            axis,
                            opposite);
                    if (oppositeIsFull || oppositeHasExactSeam)
                    {
                        continue;
                    }

                    diagnostics.Add(
                        new CandidateDiagnostic(
                            CandidateDiagnosticCode
                                .SmallBoundaryCutWithoutOppositeFullOrSeam,
                            CandidateDiagnosticSeverity.Warning,
                            "A complex-room boundary cut reaches the "
                                + "recommended minimum but is smaller than a "
                                + "half tile; the opposite boundary is neither "
                                + "full-tile nor aligned to an accurate target "
                                + "wall-corner seam, so this material-saving "
                                + "edge allocation requires project review and "
                                + "is not an automatic satisfied-rule result.",
                            axis,
                            side,
                            smallCut.ActualValue,
                            smallCut.RecommendedMinimum));
                }
            }
        }

        private static List<BoundaryCutMeasurement>
            GetBoundaryMeasurementsForSide(
                IList<TileFootprintAssessment> assessments,
                RoomSide side,
                TileLayoutAxis axis)
        {
            var measurements = new List<BoundaryCutMeasurement>();
            if (assessments == null)
            {
                return measurements;
            }

            foreach (TileFootprintAssessment assessment in assessments)
            {
                if (assessment == null
                    || assessment.Footprint == null
                    || !assessment.Footprint.BoundarySides.Contains(side))
                {
                    continue;
                }

                foreach (BoundaryCutMeasurement measurement
                    in assessment.Measurements)
                {
                    if (measurement.Axis == axis)
                    {
                        measurements.Add(measurement);
                    }
                }
            }

            return measurements;
        }

        private static bool HasOnlyFullBoundaryTilesAtSide(
            IList<TileFootprintAssessment> assessments,
            RoomSide side)
        {
            bool foundBoundaryTile = false;
            if (assessments == null)
            {
                return false;
            }

            foreach (TileFootprintAssessment assessment in assessments)
            {
                if (assessment == null
                    || assessment.Footprint == null
                    || !assessment.Footprint.BoundarySides.Contains(side))
                {
                    continue;
                }

                foundBoundaryTile = true;
                if (!assessment.Footprint.IsFullTile)
                {
                    return false;
                }
            }

            return foundBoundaryTile;
        }

        private static bool HasExactTargetSeamOnSide(
            AxisAlignedOrthogonalPolygon room,
            IList<WallCornerAssessment> wallCornerAssessments,
            TileLayoutAxis axis,
            RoomSide side)
        {
            if (room == null || wallCornerAssessments == null)
            {
                return false;
            }

            foreach (WallCornerAssessment corner in wallCornerAssessments)
            {
                if (corner == null || !corner.IsOptimizationTarget
                    || (axis == TileLayoutAxis.X
                        ? !corner.HasVerticalSeam
                        : !corner.HasHorizontalSeam))
                {
                    continue;
                }

                double expected = side == RoomSide.West
                    ? room.West
                    : side == RoomSide.East
                    ? room.East
                    : side == RoomSide.South
                    ? room.South
                    : room.North;
                double actual = axis == TileLayoutAxis.X
                    ? corner.Position.X
                    : corner.Position.Y;
                if (Math.Abs(actual - expected)
                    <= GeometryTolerance.Coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        private static LayoutCandidate TryBuildOrthogonalClipRecovery(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source,
            LayoutCandidate primary)
        {
            if (parameters.MainSecondary != null
                || !IsPartialDoorOnValidatedRoomBoundary(
                    room,
                    parameters.ControlRegion,
                    parameters.DoorOpening))
            {
                return null;
            }

            bool primaryNeedsRecovery =
                primary.Metrics.BelowDefaultMinimumBoundaryTileCount > 0;
            bool completeRoomNeedsRecovery =
                HasCompleteRoomNarrowRedistribution(
                    room,
                    parameters,
                    source);
            if (!primaryNeedsRecovery && !completeRoomNeedsRecovery)
            {
                return null;
            }

            BoundaryBandPlan sourceXPlan = source.GetAxisPlan(
                TileLayoutAxis.X);
            BoundaryBandPlan sourceYPlan = source.GetAxisPlan(
                TileLayoutAxis.Y);
            bool xNeedsRecovery = HasBelowRecommendedBoundaryCut(
                primary,
                TileLayoutAxis.X);
            bool yNeedsRecovery = HasBelowRecommendedBoundaryCut(
                primary,
                TileLayoutAxis.Y);

            LayoutCandidate roomBoundaryPattern =
                TryBuildRoomBoundaryRedistribution(
                    room,
                    parameters,
                    source,
                    primary,
                    sourceXPlan,
                    sourceYPlan,
                    xNeedsRecovery,
                    yNeedsRecovery);
            if (roomBoundaryPattern != null)
            {
                return roomBoundaryPattern;
            }

            BoundaryBandPlan xRecoveryPlan = GetRecoveryPlan(
                sourceXPlan,
                xNeedsRecovery);
            BoundaryBandPlan yRecoveryPlan = GetRecoveryPlan(
                sourceYPlan,
                yNeedsRecovery);

            // If clipping makes both directions too narrow, apply the same
            // half-tile/transition rule to both axes before trying a
            // one-axis fallback. This keeps the two-dimensional candidate
            // internally consistent instead of repairing only the door
            // normal axis.
            if (xNeedsRecovery
                && yNeedsRecovery
                && xRecoveryPlan != null
                && yRecoveryPlan != null
                && (!object.ReferenceEquals(xRecoveryPlan, sourceXPlan)
                    || !object.ReferenceEquals(yRecoveryPlan, sourceYPlan)))
            {
                var diagnostics = new List<CandidateDiagnostic>();
                if (!object.ReferenceEquals(xRecoveryPlan, sourceXPlan))
                {
                    diagnostics.Add(CreateOrthogonalClipRedistributionDiagnostic(
                        TileLayoutAxis.X,
                        primary,
                        xRecoveryPlan));
                }

                if (!object.ReferenceEquals(yRecoveryPlan, sourceYPlan))
                {
                    diagnostics.Add(CreateOrthogonalClipRedistributionDiagnostic(
                        TileLayoutAxis.Y,
                        primary,
                        yRecoveryPlan));
                }

                LayoutCandidate biaxial = TryBuildRecoveryCandidate(
                    room,
                    parameters,
                    source,
                    "whole-" + source.Id
                        + "-orthogonal-redistributed-both-axes",
                    object.ReferenceEquals(xRecoveryPlan, sourceXPlan)
                        ? null
                        : xRecoveryPlan,
                    object.ReferenceEquals(yRecoveryPlan, sourceYPlan)
                        ? null
                        : yRecoveryPlan,
                    diagnostics);
                if (biaxial != null)
                {
                    return biaxial;
                }
            }

            BoundaryBandPlan doorNormalPlan = source.AxisPlans.Single(
                plan => plan.Role == DoorControlledAxisRole.DoorNormal);
            bool doorNormalNeedsRecovery = HasBelowRecommendedBoundaryCut(
                primary,
                doorNormalPlan.Axis);
            BoundaryBandPlan redistributed = GetRecoveryPlan(
                doorNormalPlan,
                doorNormalNeedsRecovery);
            if (doorNormalNeedsRecovery
                && redistributed != null
                && !object.ReferenceEquals(redistributed, doorNormalPlan))
            {
                LayoutCandidate redistributedCandidate =
                    TryBuildRecoveryCandidate(
                        room,
                        parameters,
                        source,
                        "whole-" + source.Id
                            + "-orthogonal-redistributed",
                        doorNormalPlan.Axis == TileLayoutAxis.X
                            ? redistributed
                            : null,
                        doorNormalPlan.Axis == TileLayoutAxis.Y
                            ? redistributed
                            : null,
                        new List<CandidateDiagnostic>
                        {
                            CreateOrthogonalClipRedistributionDiagnostic(
                                doorNormalPlan.Axis,
                                primary,
                                redistributed)
                        });
                if (redistributedCandidate != null)
                {
                    return redistributedCandidate;
                }
            }

            BoundaryBandPlan alongWallPlan = source.AxisPlans.Single(
                plan => plan.Role == DoorControlledAxisRole.AlongWall);
            bool alongWallNeedsRecovery = HasBelowRecommendedBoundaryCut(
                primary,
                alongWallPlan.Axis);
            BoundaryBandPlan alongWallFlipped = null;
            if (alongWallNeedsRecovery
                && !alongWallPlan.UsesRedistribution
                && (alongWallPlan.LowBoundary.Kind
                        == BoundaryBandKind.NaturalRemainder
                    || alongWallPlan.HighBoundary.Kind
                        == BoundaryBandKind.NaturalRemainder))
            {
                alongWallFlipped = ReversePlan(alongWallPlan);
            }

            // When both axes are in trouble, also try the existing
            // along-wall flip together with the door-normal redistribution.
            // This preserves the established north/south-door recovery while
            // still allowing the door-normal axis to be repaired in the same
            // candidate.
            if (xNeedsRecovery
                && yNeedsRecovery
                && redistributed != null
                && !object.ReferenceEquals(redistributed, doorNormalPlan)
                && alongWallFlipped != null)
            {
                var diagnostics = new List<CandidateDiagnostic>
                {
                    CreateOrthogonalClipRedistributionDiagnostic(
                        doorNormalPlan.Axis,
                        primary,
                        redistributed),
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.OrthogonalClipAlongWallFlipped,
                        CandidateDiagnosticSeverity.Information,
                        "The nearer-side along-wall allocation became too narrow after orthogonal clipping, so the opposite deterministic control side was applied and revalidated against the complete room.",
                        alongWallPlan.Axis,
                        alongWallFlipped.ControlSide,
                        primary.Metrics.MinimumBoundaryBandWidth,
                        alongWallPlan.MinimumCut)
                };
                LayoutCandidate combinedFallback = TryBuildRecoveryCandidate(
                    room,
                    parameters,
                    source,
                    "whole-" + source.Id
                        + "-orthogonal-redistributed-along-wall-flipped",
                    doorNormalPlan.Axis == TileLayoutAxis.X
                        ? redistributed
                        : alongWallFlipped,
                    doorNormalPlan.Axis == TileLayoutAxis.Y
                        ? redistributed
                        : alongWallFlipped,
                    diagnostics);
                if (combinedFallback != null)
                {
                    return combinedFallback;
                }
            }

            if (alongWallFlipped != null)
            {
                LayoutCandidate flippedCandidate = TryBuildRecoveryCandidate(
                    room,
                    parameters,
                    source,
                    "whole-" + source.Id
                        + "-orthogonal-along-wall-flipped",
                    alongWallPlan.Axis == TileLayoutAxis.X
                        ? alongWallFlipped
                        : null,
                    alongWallPlan.Axis == TileLayoutAxis.Y
                        ? alongWallFlipped
                        : null,
                    new List<CandidateDiagnostic>
                    {
                        new CandidateDiagnostic(
                            CandidateDiagnosticCode.OrthogonalClipAlongWallFlipped,
                            CandidateDiagnosticSeverity.Information,
                            "The nearer-side along-wall allocation became too narrow after orthogonal clipping, so the opposite deterministic control side was applied and revalidated against the complete room.",
                            alongWallPlan.Axis,
                            alongWallFlipped.ControlSide,
                            primary.Metrics.MinimumBoundaryBandWidth,
                            alongWallPlan.MinimumCut)
                    });
                if (flippedCandidate != null)
                {
                    return flippedCandidate;
                }
            }

            BoundaryBandPlan alongWallRedistributed = GetRecoveryPlan(
                alongWallPlan,
                alongWallNeedsRecovery);
            if (alongWallNeedsRecovery
                && alongWallRedistributed != null
                && !object.ReferenceEquals(
                    alongWallRedistributed,
                    alongWallPlan))
            {
                return TryBuildRecoveryCandidate(
                    room,
                    parameters,
                    source,
                    "whole-" + source.Id
                        + "-orthogonal-along-wall-redistributed",
                    alongWallPlan.Axis == TileLayoutAxis.X
                        ? alongWallRedistributed
                        : null,
                    alongWallPlan.Axis == TileLayoutAxis.Y
                        ? alongWallRedistributed
                        : null,
                    new List<CandidateDiagnostic>
                    {
                        CreateOrthogonalClipRedistributionDiagnostic(
                            alongWallPlan.Axis,
                            primary,
                            alongWallRedistributed)
                    });
            }

            return null;
        }

        private static LayoutCandidate TryBuildRoomBoundaryRedistribution(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source,
            LayoutCandidate primary,
            BoundaryBandPlan sourceXPlan,
            BoundaryBandPlan sourceYPlan,
            bool xNeedsRecovery,
            bool yNeedsRecovery)
        {
            BoundaryBandPlan xPlan =
                BuildCompleteRoomNarrowRedistribution(
                    room,
                    parameters,
                    sourceXPlan);
            BoundaryBandPlan yPlan =
                BuildCompleteRoomNarrowRedistribution(
                    room,
                    parameters,
                    sourceYPlan);
            if (!xNeedsRecovery
                && !yNeedsRecovery
                && xPlan == null
                && yPlan == null)
            {
                return null;
            }

            bool replaceX = xPlan != null
                && !object.ReferenceEquals(xPlan, sourceXPlan);
            bool replaceY = yPlan != null
                && !object.ReferenceEquals(yPlan, sourceYPlan);
            if (!replaceX && !replaceY)
            {
                return null;
            }

            var diagnostics = new List<CandidateDiagnostic>();
            if (replaceX)
            {
                diagnostics.Add(
                    HasCompleteRoomNarrowRedistribution(
                        room,
                        parameters,
                        source)
                        && !sourceXPlan.UsesRedistribution
                        ? CreateCompleteRoomRedistributionDiagnostic(
                            TileLayoutAxis.X,
                            xPlan)
                        : CreateRoomBoundaryRedistributionDiagnostic(
                            TileLayoutAxis.X,
                            primary,
                            xPlan));
            }

            if (replaceY)
            {
                diagnostics.Add(
                    HasCompleteRoomNarrowRedistribution(
                        room,
                        parameters,
                        source)
                        && !sourceYPlan.UsesRedistribution
                        ? CreateCompleteRoomRedistributionDiagnostic(
                            TileLayoutAxis.Y,
                            yPlan)
                        : CreateRoomBoundaryRedistributionDiagnostic(
                            TileLayoutAxis.Y,
                            primary,
                            yPlan));
            }

            LayoutCandidate candidate = BuildWholeRoomCandidate(
                room,
                parameters,
                source,
                "whole-" + source.Id + "-room-boundary-redistributed",
                false,
                false,
                null,
                replaceX ? xPlan : null,
                replaceY ? yPlan : null,
                diagnostics[0],
                null,
                replaceX
                    ? room.West + xPlan.SegmentWidths[0]
                    : (double?)null,
                replaceY
                    ? room.South + yPlan.SegmentWidths[0]
                    : (double?)null);
            for (int index = 1; index < diagnostics.Count; index++)
            {
                candidate = WithAdditionalDiagnostic(
                    candidate,
                    diagnostics[index]);
            }

            // A room-envelope redistribution is a real G1 recovery candidate
            // even when another boundary remains below the recommended value;
            // it must still satisfy the project absolute minimum to be retained.
            return candidate.IsRejected ? null : candidate;
        }

        private static BoundaryBandPlan BuildRoomBoundaryRedistribution(
            AxisAlignedOrthogonalPolygon room,
            BoundaryBandPlan source,
            double tileSize,
            RoomSide halfSide)
        {
            double length = source.Axis == TileLayoutAxis.X
                ? room.Width
                : room.Height;
            double gridTileSize = source.GridTileSize;
            TileSpanMetrics metrics = TileSpanCalculator.Calculate(
                length,
                gridTileSize);
            EngineeringRectangularLayoutCalculator.AxisPlanBuildResult build =
                EngineeringRectangularLayoutCalculator.BuildAxisPlan(
                    source.Axis,
                    source.Role,
                    length,
                    tileSize,
                    gridTileSize,
                    metrics,
                    source.ControlSide,
                    halfSide);
            return build.Plan != null && build.Plan.UsesRedistribution
                ? build.Plan
                : null;
        }

        private static bool HasBelowRecommendedBoundaryCut(
            LayoutCandidate candidate,
            TileLayoutAxis axis)
        {
            return candidate.TileAssessments.Any(assessment =>
                assessment.Measurements.Any(measurement =>
                    measurement.Axis == axis
                    && measurement.Status
                        != ProjectCutStatus.MeetsRecommendedMinimum));
        }

        private static BoundaryBandPlan GetRecoveryPlan(
            BoundaryBandPlan source,
            bool needsRecovery)
        {
            if (!needsRecovery)
            {
                return source;
            }

            return source.UsesRedistribution
                ? source
                : BuildOrthogonalClipRedistribution(source);
        }

        private static CandidateDiagnostic
            CreateOrthogonalClipRedistributionDiagnostic(
                TileLayoutAxis axis,
                LayoutCandidate primary,
                BoundaryBandPlan redistributed)
        {
            return new CandidateDiagnostic(
                CandidateDiagnosticCode.OrthogonalClipRedistributed,
                CandidateDiagnosticSeverity.Information,
                "The rectangular door-controlled phase became too narrow after orthogonal clipping, so the frozen half-tile and transition-tile allocation was applied on this axis and revalidated against the complete room.",
                axis,
                redistributed.TransitionTileSide,
                primary.Metrics.MinimumBoundaryBandWidth,
                redistributed.MinimumCut);
        }

        private static CandidateDiagnostic
            CreateRoomBoundaryRedistributionDiagnostic(
                TileLayoutAxis axis,
                LayoutCandidate primary,
                BoundaryBandPlan redistributed)
        {
            return new CandidateDiagnostic(
                CandidateDiagnosticCode.OrthogonalClipRedistributed,
                CandidateDiagnosticSeverity.Information,
                "The complete room envelope has a narrow boundary remainder after the door-controlled phase was clipped; the frozen half-tile and transition-tile allocation was applied on this axis and revalidated against every actual footprint.",
                axis,
                redistributed.TransitionTileSide,
                primary.Metrics.MinimumBoundaryBandWidth,
                redistributed.MinimumCut);
        }

        private static LayoutCandidate TryBuildRecoveryCandidate(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source,
            string id,
            BoundaryBandPlan xPlanOverride,
            BoundaryBandPlan yPlanOverride,
            IList<CandidateDiagnostic> diagnostics)
        {
            LayoutCandidate candidate = BuildWholeRoomCandidate(
                room,
                parameters,
                source,
                id,
                false,
                false,
                null,
                xPlanOverride,
                yPlanOverride,
                diagnostics == null || diagnostics.Count == 0
                    ? null
                    : diagnostics[0]);
            if (diagnostics != null)
            {
                for (int index = 1; index < diagnostics.Count; index++)
                {
                    candidate = WithAdditionalDiagnostic(
                        candidate,
                        diagnostics[index]);
                }
            }

            return !candidate.IsRejected
                && candidate.Metrics.BelowDefaultMinimumBoundaryTileCount == 0
                ? candidate
                : null;
        }

        private static bool IsPartialDoorOnValidatedRoomBoundary(
            AxisAlignedOrthogonalPolygon room,
            AxisAlignedRectangle controlRegion,
            DoorOpening opening)
        {
            bool vertical = opening.Wall == RoomSide.West
                || opening.Wall == RoomSide.East;
            double fixedCoordinate;
            switch (opening.Wall)
            {
                case RoomSide.West:
                    fixedCoordinate = controlRegion.West;
                    break;
                case RoomSide.East:
                    fixedCoordinate = controlRegion.East;
                    break;
                case RoomSide.South:
                    fixedCoordinate = controlRegion.South;
                    break;
                case RoomSide.North:
                    fixedCoordinate = controlRegion.North;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(opening));
            }

            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D start = room.Vertices[index];
                Point3D end = room.Vertices[
                    (index + 1) % room.Vertices.Count];
                bool edgeIsVertical = GeometryTolerance.NearlyEqual(
                    start.X,
                    end.X);
                if (edgeIsVertical != vertical)
                {
                    continue;
                }

                double edgeFixed = vertical ? start.X : start.Y;
                if (!GeometryTolerance.NearlyEqual(
                    edgeFixed,
                    fixedCoordinate))
                {
                    continue;
                }

                double edgeStart = vertical
                    ? Math.Min(start.Y, end.Y)
                    : Math.Min(start.X, end.X);
                double edgeEnd = vertical
                    ? Math.Max(start.Y, end.Y)
                    : Math.Max(start.X, end.X);
                bool contained = opening.AlongWallStart
                        >= edgeStart - GeometryTolerance.Coordinate
                    && opening.AlongWallEnd
                        <= edgeEnd + GeometryTolerance.Coordinate;
                if (!contained)
                {
                    continue;
                }

                bool coversWholeSegment = GeometryTolerance.NearlyEqual(
                        opening.AlongWallStart,
                        edgeStart)
                    && GeometryTolerance.NearlyEqual(
                        opening.AlongWallEnd,
                        edgeEnd);
                return !coversWholeSegment;
            }

            return false;
        }

        private static BoundaryBandPlan BuildOrthogonalClipRedistribution(
            BoundaryBandPlan source)
        {
            if (source.UsesRedistribution
                || source.NaturalRemainder <= GeometryTolerance.Coordinate
                || source.FullTileCount < 1)
            {
                return null;
            }

            bool remainderIsLow =
                source.LowBoundary.Kind == BoundaryBandKind.NaturalRemainder;
            bool remainderIsHigh =
                source.HighBoundary.Kind == BoundaryBandKind.NaturalRemainder;
            if (remainderIsLow == remainderIsHigh)
            {
                return null;
            }

            double grout = source.GridTileSize - source.TileSize;
            double half = source.TileSize
                * EngineeringLayoutRules.HalfTileRatio;
            double transition = half + source.NaturalRemainder;
            var widths = new List<double>(source.FullTileCount + 1)
            {
                (remainderIsLow ? transition : half) + grout
            };
            for (int index = 0; index < source.FullTileCount - 1; index++)
            {
                widths.Add(source.GridTileSize);
            }

            widths.Add((remainderIsLow ? half : transition) + grout);
            RoomSide halfSide = remainderIsLow
                ? source.HighBoundary.Side
                : source.LowBoundary.Side;
            return new BoundaryBandPlan(
                source.Axis,
                source.Role,
                source.TileSize,
                source.NaturalRemainder,
                source.ControlSide,
                halfSide,
                new AxisBoundaryBand(
                    source.LowBoundary.Side,
                    widths[0] - grout,
                    remainderIsLow
                        ? BoundaryBandKind.Transition
                        : BoundaryBandKind.HalfTile),
                new AxisBoundaryBand(
                    source.HighBoundary.Side,
                    widths[widths.Count - 1] - grout,
                    remainderIsLow
                        ? BoundaryBandKind.HalfTile
                        : BoundaryBandKind.Transition),
                source.FullTileCount - 1,
                source.FullTileCount - 1,
                true,
                widths,
                source.GridTileSize);
        }

        private static void AddMainSecondaryCandidates(
            ICollection<LayoutCandidate> target,
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source,
            ConnectionInfo connection)
        {
            BoundaryBandPlan parallelPlan = source.GetAxisPlan(
                connection.ParallelAxis);
            BoundaryBandPlan perpendicularPlan = source.GetAxisPlan(
                connection.PerpendicularAxis);
            double tileSize = connection.ParallelAxis == TileLayoutAxis.X
                ? parameters.TileWidth
                : parameters.TileHeight;
            double minimumCut = tileSize
                * EngineeringLayoutRules.DefaultMinimumCutRatio;
            double adjacentWidth = GetBoundaryWidth(
                parallelPlan,
                connection.ProtrusionSide);
            bool narrowProtrusion = connection.ProtrusionWidth
                > GeometryTolerance.Coordinate
                && connection.ProtrusionWidth + GeometryTolerance.Coordinate
                    < minimumCut;
            bool canAbsorb = narrowProtrusion
                && adjacentWidth + connection.ProtrusionWidth
                    <= tileSize + GeometryTolerance.Coordinate;

            target.Add(
                BuildMainSecondaryCandidate(
                    room,
                    parameters,
                    source,
                    parallelPlan,
                    perpendicularPlan,
                    connection,
                    ProtrusionBandTreatment.Independent,
                    narrowProtrusion && !canAbsorb,
                    canAbsorb,
                    false));

            if (canAbsorb)
            {
                target.Add(
                    BuildMainSecondaryCandidate(
                        room,
                        parameters,
                        source,
                        parallelPlan,
                        perpendicularPlan,
                        connection,
                        ProtrusionBandTreatment.Absorbed,
                        false,
                        true,
                        false));
            }

            if (narrowProtrusion)
            {
                BoundaryBandPlan mirrored = ReversePlan(parallelPlan);
                double mirroredAdjacent = GetBoundaryWidth(
                    mirrored,
                    connection.ProtrusionSide);
                bool mirroredCanAbsorb = mirroredAdjacent
                    + connection.ProtrusionWidth
                    <= tileSize + GeometryTolerance.Coordinate;
                if (mirroredCanAbsorb
                    && !GeometryTolerance.NearlyEqual(
                        mirroredAdjacent,
                        adjacentWidth))
                {
                    target.Add(
                        BuildMainSecondaryCandidate(
                            room,
                            parameters,
                            source,
                            mirrored,
                            perpendicularPlan,
                            connection,
                            ProtrusionBandTreatment.Absorbed,
                            false,
                            true,
                            true));
                }
            }
        }

        private static LayoutCandidate BuildMainSecondaryCandidate(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            LayoutCandidate source,
            BoundaryBandPlan mainParallelPlan,
            BoundaryBandPlan mainPerpendicularPlan,
            ConnectionInfo connection,
            ProtrusionBandTreatment treatment,
            bool retainUnresolvedMinimum,
            bool absorptionWasAvailable,
            bool mirroredForAbsorption)
        {
            AxisAlignedRectangle main = parameters.MainSecondary.MainRegion;
            AxisAlignedRectangle secondary =
                parameters.MainSecondary.SecondaryRegion;
            double perpendicularLength = connection.PerpendicularAxis
                == TileLayoutAxis.X ? secondary.Width : secondary.Height;
            double perpendicularTileSize = connection.PerpendicularAxis
                == TileLayoutAxis.X
                    ? parameters.TileWidth
                    : parameters.TileHeight;
            double perpendicularGridTileSize = connection.PerpendicularAxis
                == TileLayoutAxis.X
                    ? parameters.GridTileWidth
                    : parameters.GridTileHeight;
            TileSpanMetrics metrics = TileSpanCalculator.Calculate(
                perpendicularLength,
                perpendicularGridTileSize);
            EngineeringRectangularLayoutCalculator.AxisPlanBuildResult
                secondaryBuild =
                    EngineeringRectangularLayoutCalculator.BuildAxisPlan(
                        connection.PerpendicularAxis,
                        DoorControlledAxisRole.SecondaryFromConnection,
                        perpendicularLength,
                        perpendicularTileSize,
                        perpendicularGridTileSize,
                        metrics,
                        connection.SecondaryConnectionSide,
                        connection.SecondaryFarSide);

            var diagnostics = new List<CandidateDiagnostic>();
            diagnostics.Add(
                new CandidateDiagnostic(
                    CandidateDiagnosticCode.MainSecondaryLayoutGenerated,
                    CandidateDiagnosticSeverity.Information,
                    "The candidate combines an explicit door-controlled main region with an explicit connected secondary region."));
            diagnostics.Add(
                new CandidateDiagnostic(
                    CandidateDiagnosticCode.ParallelPhaseInherited,
                    CandidateDiagnosticSeverity.Information,
                    "The secondary region inherits the main-region joints parallel to the connection boundary.",
                    connection.ParallelAxis));
            diagnostics.Add(
                new CandidateDiagnostic(
                    CandidateDiagnosticCode.PerpendicularPhaseReset,
                    CandidateDiagnosticSeverity.Information,
                    "The perpendicular phase resets at the explicit connection boundary.",
                    connection.PerpendicularAxis));
            foreach (CandidateDiagnostic diagnostic in secondaryBuild.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }

            var plans = new List<BoundaryBandPlan>
            {
                mainParallelPlan,
                mainPerpendicularPlan
            };
            if (secondaryBuild.Plan == null)
            {
                return new LayoutCandidate(
                    GetRegionalId(source, treatment, mirroredForAbsorption),
                    false,
                    false,
                    "The explicit secondary region could not produce a perpendicular boundary-band plan.",
                    plans,
                    new List<LineSegment3D>(),
                    new List<TileFootprint>(),
                    diagnostics,
                    EmptyMetrics(),
                    new LayoutCandidateStructure(
                        OrthogonalCandidateKind.MainSecondary,
                        new List<LayoutRegionPhase>(),
                        new List<RegionConnectionPlan>()));
            }

            List<double> mainXCuts;
            List<double> mainYCuts;
            GetPlanCuts(
                main,
                mainParallelPlan,
                mainPerpendicularPlan,
                connection.ParallelAxis,
                out mainXCuts,
                out mainYCuts);
            List<double> secondaryParallelCuts = GetInheritedParallelCuts(
                main,
                secondary,
                mainParallelPlan,
                connection,
                treatment);
            List<double> secondaryPerpendicularCuts = GetPlanCuts(
                secondary,
                secondaryBuild.Plan);
            List<double> secondaryXCuts = connection.ParallelAxis
                == TileLayoutAxis.X
                    ? secondaryParallelCuts
                    : secondaryPerpendicularCuts;
            List<double> secondaryYCuts = connection.ParallelAxis
                == TileLayoutAxis.X
                    ? secondaryPerpendicularCuts
                    : secondaryParallelCuts;

            var lines = new List<LineSegment3D>();
            AxisAlignedOrthogonalPolygon mainPolygon = RectanglePolygon(main);
            AxisAlignedOrthogonalPolygon secondaryPolygon =
                RectanglePolygon(secondary);
            lines.AddRange(
                OrthogonalTileGridCalculator.ClipDivisionLines(
                    mainPolygon,
                    mainXCuts,
                    mainYCuts));
            lines.AddRange(
                OrthogonalTileGridCalculator.ClipDivisionLines(
                    secondaryPolygon,
                    secondaryXCuts,
                    secondaryYCuts));
            lines.Add(connection.Boundary);
            lines = MergeDivisionLines(lines);
            int maximum = TileLayoutRules.MaximumParameterizedDivisionLineCount;
            if (lines.Count > maximum)
            {
                throw new TileLayoutLimitExceededException(lines.Count, maximum);
            }

            var tiles = new List<TileFootprint>();
            AddRectangleTiles(
                tiles,
                room,
                main,
                mainXCuts,
                mainYCuts,
                parameters.TileWidth,
                parameters.TileHeight,
                parameters.GroutWidthMm);
            AddRectangleTiles(
                tiles,
                room,
                secondary,
                secondaryXCuts,
                secondaryYCuts,
                parameters.TileWidth,
                parameters.TileHeight,
                parameters.GroutWidthMm);

            double absorbedWidth = treatment == ProtrusionBandTreatment.Absorbed
                ? GetBoundaryWidth(mainParallelPlan, connection.ProtrusionSide)
                    + connection.ProtrusionWidth
                : 0.0;
            if (treatment == ProtrusionBandTreatment.Absorbed)
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.ProtrusionBandAbsorbed,
                        CandidateDiagnosticSeverity.Information,
                        "The narrow protrusion is absorbed by the adjacent tile without exceeding one tile module.",
                        connection.ParallelAxis,
                        connection.ProtrusionSide,
                        absorbedWidth,
                        connection.ParallelAxis == TileLayoutAxis.X
                            ? parameters.TileWidth
                            : parameters.TileHeight));
            }
            else if (connection.ProtrusionWidth > GeometryTolerance.Coordinate)
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.ProtrusionBandKeptIndependent,
                        CandidateDiagnosticSeverity.Information,
                        "The protrusion continues the main-region boundary as an independent band.",
                        connection.ParallelAxis,
                        connection.ProtrusionSide,
                        connection.ProtrusionWidth));
                if (!absorptionWasAvailable)
                {
                    diagnostics.Add(
                        new CandidateDiagnostic(
                            CandidateDiagnosticCode.ProtrusionBandCannotBeAbsorbed,
                            CandidateDiagnosticSeverity.Warning,
                            "The adjacent tile plus the protrusion would exceed one tile module.",
                            connection.ParallelAxis,
                            connection.ProtrusionSide,
                            GetBoundaryWidth(
                                mainParallelPlan,
                                connection.ProtrusionSide)
                                + connection.ProtrusionWidth,
                            connection.ParallelAxis == TileLayoutAxis.X
                                ? parameters.TileWidth
                                : parameters.TileHeight));
                }
            }

            IList<TileFootprintAssessment> tileAssessments;
            IList<WallCornerAssessment> wallCornerAssessments;
            LayoutCandidateMetrics candidateMetrics = BuildMetrics(
                room,
                parameters,
                tiles,
                lines,
                ConnectionLength(connection.Boundary),
                1,
                diagnostics,
                out tileAssessments,
                out wallCornerAssessments);
            AddUnjustifiedLargeBoundaryCutDiagnostics(
                room,
                parameters,
                plans,
                null,
                tileAssessments,
                wallCornerAssessments,
                true,
                diagnostics);
            AddSmallBoundaryCutOppositeEligibilityDiagnostics(
                room,
                parameters,
                tileAssessments,
                wallCornerAssessments,
                true,
                diagnostics);
            var structure = new LayoutCandidateStructure(
                OrthogonalCandidateKind.MainSecondary,
                new List<LayoutRegionPhase>
                {
                    new LayoutRegionPhase(
                        "main",
                        LayoutRegionRole.Main,
                        main,
                        mainXCuts,
                        mainYCuts,
                        false,
                        false),
                    new LayoutRegionPhase(
                        "secondary",
                        LayoutRegionRole.Secondary,
                        secondary,
                        secondaryXCuts,
                        secondaryYCuts,
                        true,
                        true)
                },
                new List<RegionConnectionPlan>
                {
                    new RegionConnectionPlan(
                        connection.Boundary,
                        connection.ParallelAxis,
                        true,
                        treatment,
                        connection.ProtrusionSide,
                        connection.ProtrusionWidth,
                        absorbedWidth)
                });
            return new LayoutCandidate(
                GetRegionalId(source, treatment, mirroredForAbsorption),
                false,
                false,
                mirroredForAbsorption
                    ? "A mirrored main-axis allocation is retained because it creates a confirmed absorbable protrusion candidate."
                    : "The explicit main and secondary regions are combined without guessing another connection.",
                plans,
                lines,
                tiles,
                diagnostics,
                candidateMetrics,
                structure,
                tileAssessments,
                wallCornerAssessments);
        }

        private static LayoutCandidateMetrics BuildMetrics(
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            IList<TileFootprint> tiles,
            IList<LineSegment3D> divisionLines,
            double transitionLength,
            int phaseResetCount,
            ICollection<CandidateDiagnostic> diagnostics,
            out IList<TileFootprintAssessment> tileAssessments,
            out IList<WallCornerAssessment> wallCornerAssessments)
        {
            long interiorNonFullCount = 0;
            double interiorNonFullArea = 0.0;
            long boundaryNonFullCount = 0;
            long belowMinimumCount = 0;
            long belowAbsoluteCount = 0;
            long projectReviewCount = 0;
            long entranceVisualBelowRecommendedCount = 0;
            long entranceBlindBelowRecommendedCount = 0;
            long irregularCount = 0;
            double minimumBoundaryWidth = double.PositiveInfinity;
            double minimumBelowRecommended = double.PositiveInfinity;
            double minimumBelowAbsolute = double.PositiveInfinity;
            var assessments = new List<TileFootprintAssessment>();
            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                TileFootprint tile = tiles[tileIndex];
                EntranceVisibility entranceVisibility =
                    EvaluateEntranceVisibility(
                        room,
                        parameters.DoorOpening,
                        tile,
                        parameters.TileWidth,
                        parameters.TileHeight);
                if (tile.IsContinuousIrregular)
                {
                    irregularCount++;
                }

                if (tile.IsFullTile)
                {
                    assessments.Add(new TileFootprintAssessment(
                        tileIndex,
                        tile,
                        new List<BoundaryCutMeasurement>(),
                        ProjectCutStatus.NotApplicableFullTile,
                        "Full tiles are outside the project boundary-cut rule.",
                        entranceVisibility.IsVisualZone,
                        entranceVisibility.IsVisualBlind));
                    continue;
                }

                if (tile.Classification == TileClassification.Interior)
                {
                    interiorNonFullCount++;
                    interiorNonFullArea += tile.Area;
                    assessments.Add(new TileFootprintAssessment(
                        tileIndex,
                        tile,
                        new List<BoundaryCutMeasurement>(),
                        ProjectCutStatus.InteriorNonFullDiagnostic,
                        "The non-full tile is interior; it is reported but is not a project boundary-cut measurement.",
                        entranceVisibility.IsVisualZone,
                        entranceVisibility.IsVisualBlind));
                    continue;
                }

                boundaryNonFullCount++;
                var measurements = new List<BoundaryCutMeasurement>();
                if (OrthogonalFootprintBuilder.TouchesVerticalBoundary(room, tile)
                    || parameters.GroutWidthMm > GeometryTolerance.Coordinate
                        && (HasBoundarySide(tile, RoomSide.West)
                            || HasBoundarySide(tile, RoomSide.East)))
                {
                    minimumBoundaryWidth = Math.Min(
                        minimumBoundaryWidth,
                        tile.NominalWidth);
                    measurements.Add(EvaluateBoundaryCut(
                        TileLayoutAxis.X,
                        tile.NominalWidth,
                        parameters.TileWidth,
                        parameters.Policy));
                }

                if (OrthogonalFootprintBuilder.TouchesHorizontalBoundary(room, tile)
                    || parameters.GroutWidthMm > GeometryTolerance.Coordinate
                        && (HasBoundarySide(tile, RoomSide.South)
                            || HasBoundarySide(tile, RoomSide.North)))
                {
                    minimumBoundaryWidth = Math.Min(
                        minimumBoundaryWidth,
                        tile.NominalHeight);
                    measurements.Add(EvaluateBoundaryCut(
                        TileLayoutAxis.Y,
                        tile.NominalHeight,
                        parameters.TileHeight,
                        parameters.Policy));
                }

                ProjectCutStatus status = AggregateStatus(measurements);
                if (status == ProjectCutStatus.RequiresProjectPolicy
                    || status == ProjectCutStatus.RequiresUserReview
                    || status == ProjectCutStatus.BelowProjectAbsoluteMinimum)
                {
                    belowMinimumCount++;
                    foreach (BoundaryCutMeasurement measurement in measurements)
                    {
                        if (measurement.Status != ProjectCutStatus.MeetsRecommendedMinimum)
                        {
                            minimumBelowRecommended = Math.Min(
                                minimumBelowRecommended,
                                measurement.ActualValue);
                        }
                    }
                }

                if (status == ProjectCutStatus.BelowProjectAbsoluteMinimum)
                {
                    belowAbsoluteCount++;
                    foreach (BoundaryCutMeasurement measurement in measurements)
                    {
                        if (measurement.Status == ProjectCutStatus.BelowProjectAbsoluteMinimum)
                        {
                            minimumBelowAbsolute = Math.Min(
                                minimumBelowAbsolute,
                                measurement.ActualValue);
                        }
                    }
                }
                else if (status == ProjectCutStatus.RequiresUserReview)
                {
                    projectReviewCount++;
                }

                if (status == ProjectCutStatus.RequiresProjectPolicy
                    || status == ProjectCutStatus.RequiresUserReview)
                {
                    if (entranceVisibility.IsVisualZone)
                    {
                        entranceVisualBelowRecommendedCount++;
                    }

                    if (entranceVisibility.IsVisualBlind)
                    {
                        entranceBlindBelowRecommendedCount++;
                    }
                }

                assessments.Add(new TileFootprintAssessment(
                    tileIndex,
                    tile,
                    measurements,
                    status,
                    GetAssessmentReason(status, tile.IsContinuousIrregular),
                    entranceVisibility.IsVisualZone,
                    entranceVisibility.IsVisualBlind));
            }

            tileAssessments = assessments;
            wallCornerAssessments = WallCornerEvaluator.Evaluate(
                room,
                divisionLines,
                tiles,
                parameters.TileWidth,
                parameters.TileHeight,
                parameters.GroutWidthMm);
            int targetCornerCount = wallCornerAssessments.Count(corner =>
                corner.IsOptimizationTarget);
            int exactIntersectionCount = wallCornerAssessments.Count(corner =>
                corner.IsOptimizationTarget
                && corner.IsExactGridIntersection);
            int exactSeamCount = wallCornerAssessments.Count(corner =>
                corner.IsOptimizationTarget
                && corner.HasAnyExactSeam);
            int safeDoubleCornerCount = wallCornerAssessments.Count(corner =>
                corner.IsSafeDoubleAlignment);
            int safeSingleCornerCount = wallCornerAssessments.Count(corner =>
                corner.IsSafeSingleAlignment);

            if (irregularCount > 0)
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.ContinuousIrregularTileRetained,
                        CandidateDiagnosticSeverity.Information,
                        "Continuous irregular footprints are evaluated as whole tiles; local legs are not independent narrow bands.",
                        null,
                        null,
                        irregularCount));
            }

            if (belowAbsoluteCount > 0)
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.MinimumCutNotMet,
                        CandidateDiagnosticSeverity.Rejection,
                        "At least one independent boundary cut is below the project absolute minimum.",
                        null,
                        null,
                        minimumBelowAbsolute,
                        parameters.Policy.ProjectAbsoluteMinimumCut));
            }
            else if (belowMinimumCount > 0
                && (parameters.Policy == null
                    || !parameters.Policy.HasProjectAbsoluteMinimum))
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.BelowDefaultMinimumRequiresPolicy,
                        CandidateDiagnosticSeverity.Warning,
                        "At least one independent boundary cut is below 0.42T; the project absolute minimum is required before it can be classified.",
                        null,
                        null,
                        minimumBelowRecommended));
            }
            else if (projectReviewCount > 0)
            {
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.BelowRecommendedMinimumRequiresReview,
                        CandidateDiagnosticSeverity.Warning,
                        "Every boundary cut meets the project absolute minimum, but at least one is below the recommended 0.42T and requires review.",
                        null,
                        null,
                        minimumBelowRecommended,
                        parameters.Policy.ProjectAbsoluteMinimumCut));
            }

            return new LayoutCandidateMetrics(
                interiorNonFullCount,
                interiorNonFullArea,
                transitionLength,
                boundaryNonFullCount,
                belowMinimumCount,
                double.IsPositiveInfinity(minimumBoundaryWidth)
                    ? 0.0
                    : minimumBoundaryWidth,
                phaseResetCount,
                irregularCount,
                0,
                exactSeamCount,
                belowAbsoluteCount,
                projectReviewCount,
                targetCornerCount,
                exactIntersectionCount,
                exactSeamCount,
                safeDoubleCornerCount,
                safeSingleCornerCount,
                entranceVisualBelowRecommendedCount,
                entranceBlindBelowRecommendedCount);
        }

        private static bool HasBoundarySide(
            TileFootprint tile,
            RoomSide side)
        {
            return tile != null
                && tile.BoundarySides != null
                && tile.BoundarySides.Contains(side);
        }

        private static BoundaryCutMeasurement EvaluateBoundaryCut(
            TileLayoutAxis axis,
            double actual,
            double tileSize,
            LayoutPolicyProfile policy)
        {
            double recommended = tileSize
                * EngineeringLayoutRules.DefaultMinimumCutRatio;
            double? absolute = policy == null
                ? null
                : policy.ProjectAbsoluteMinimumCut;
            ProjectCutStatus status;
            if (actual + GeometryTolerance.Coordinate >= recommended)
            {
                status = ProjectCutStatus.MeetsRecommendedMinimum;
            }
            else if (!absolute.HasValue)
            {
                status = ProjectCutStatus.RequiresProjectPolicy;
            }
            else if (actual + GeometryTolerance.Coordinate < absolute.Value)
            {
                status = ProjectCutStatus.BelowProjectAbsoluteMinimum;
            }
            else
            {
                status = ProjectCutStatus.RequiresUserReview;
            }

            return new BoundaryCutMeasurement(
                axis,
                actual,
                recommended,
                absolute,
                status);
        }

        private static EntranceVisibility EvaluateEntranceVisibility(
            AxisAlignedOrthogonalPolygon room,
            DoorOpening door,
            TileFootprint tile,
            double tileWidth,
            double tileHeight)
        {
            if (room == null || door == null || tile == null
                || tile.Outline == null || tile.Outline.Count == 0
                || tileWidth <= GeometryTolerance.Coordinate
                || tileHeight <= GeometryTolerance.Coordinate)
            {
                return EntranceVisibility.None;
            }

            double centerX = tile.Outline.Average(point => point.X);
            double centerY = tile.Outline.Average(point => point.Y);
            double doorX;
            double doorY;
            double inwardDistance;
            double tangentialDistance;
            switch (door.Wall)
            {
                case RoomSide.West:
                    doorX = room.West;
                    doorY = door.Center;
                    inwardDistance = centerX - room.West;
                    tangentialDistance = centerY - door.Center;
                    break;
                case RoomSide.East:
                    doorX = room.East;
                    doorY = door.Center;
                    inwardDistance = room.East - centerX;
                    tangentialDistance = centerY - door.Center;
                    break;
                case RoomSide.South:
                    doorX = door.Center;
                    doorY = room.South;
                    inwardDistance = centerY - room.South;
                    tangentialDistance = centerX - door.Center;
                    break;
                case RoomSide.North:
                    doorX = door.Center;
                    doorY = room.North;
                    inwardDistance = room.North - centerY;
                    tangentialDistance = centerX - door.Center;
                    break;
                default:
                    return EntranceVisibility.None;
            }

            // The first visual zone is deliberately fixed during G3: two
            // nominal tile depths from the door, with a half-depth side
            // allowance around the opening.  No ordinary-user tolerance field
            // is introduced.
            double depth = 2.0 * Math.Max(tileWidth, tileHeight);
            double tangentialLimit = (door.Width / 2.0) + (depth / 2.0);
            if (inwardDistance <= GeometryTolerance.Coordinate
                || inwardDistance > depth + GeometryTolerance.Coordinate
                || Math.Abs(tangentialDistance)
                    > tangentialLimit + GeometryTolerance.Coordinate)
            {
                return EntranceVisibility.None;
            }

            bool lineOfSightClear = SegmentStaysInsideRoom(
                room,
                doorX,
                doorY,
                centerX,
                centerY);
            return new EntranceVisibility(true, !lineOfSightClear);
        }

        private static bool SegmentStaysInsideRoom(
            AxisAlignedOrthogonalPolygon room,
            double startX,
            double startY,
            double endX,
            double endY)
        {
            const int sampleCount = 32;
            for (int index = 1; index <= sampleCount; index++)
            {
                double fraction = index / (double)sampleCount;
                double x = startX + ((endX - startX) * fraction);
                double y = startY + ((endY - startY) * fraction);
                if (!IsPointInsideOrBoundary(room, x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsPointInsideOrBoundary(
            AxisAlignedOrthogonalPolygon room,
            double x,
            double y)
        {
            int crossings = 0;
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D start = room.Vertices[index];
                Point3D end = room.Vertices[
                    (index + 1) % room.Vertices.Count];
                double lowX = Math.Min(start.X, end.X);
                double highX = Math.Max(start.X, end.X);
                double lowY = Math.Min(start.Y, end.Y);
                double highY = Math.Max(start.Y, end.Y);
                if (GeometryTolerance.NearlyEqual(start.X, end.X))
                {
                    if (Math.Abs(x - start.X) <= GeometryTolerance.Coordinate
                        && y >= lowY - GeometryTolerance.Coordinate
                        && y <= highY + GeometryTolerance.Coordinate)
                    {
                        return true;
                    }

                    if (y >= lowY && y < highY && start.X > x)
                    {
                        crossings++;
                    }
                }
                else if (Math.Abs(y - start.Y) <= GeometryTolerance.Coordinate
                    && x >= lowX - GeometryTolerance.Coordinate
                    && x <= highX + GeometryTolerance.Coordinate)
                {
                    return true;
                }
            }

            return (crossings & 1) == 1;
        }

        private static ProjectCutStatus AggregateStatus(
            IEnumerable<BoundaryCutMeasurement> measurements)
        {
            ProjectCutStatus status = ProjectCutStatus.MeetsRecommendedMinimum;
            foreach (BoundaryCutMeasurement measurement in measurements)
            {
                if (measurement.Status == ProjectCutStatus.BelowProjectAbsoluteMinimum)
                {
                    return ProjectCutStatus.BelowProjectAbsoluteMinimum;
                }

                if (measurement.Status == ProjectCutStatus.RequiresProjectPolicy)
                {
                    status = ProjectCutStatus.RequiresProjectPolicy;
                }
                else if (measurement.Status == ProjectCutStatus.RequiresUserReview
                    && status != ProjectCutStatus.RequiresProjectPolicy)
                {
                    status = ProjectCutStatus.RequiresUserReview;
                }
            }

            return status;
        }

        private static string GetAssessmentReason(
            ProjectCutStatus status,
            bool continuousIrregular)
        {
            string prefix = continuousIrregular
                ? "The continuous irregular footprint is assessed as one tile using its whole-footprint axis dimensions. "
                : string.Empty;
            switch (status)
            {
                case ProjectCutStatus.MeetsRecommendedMinimum:
                    return prefix + "Every applicable boundary cut meets the recommended minimum.";
                case ProjectCutStatus.RequiresProjectPolicy:
                    return prefix + "A boundary cut is below the recommended minimum and the project absolute minimum is unset.";
                case ProjectCutStatus.RequiresUserReview:
                    return prefix + "A boundary cut meets the project absolute minimum but is below the recommended minimum.";
                case ProjectCutStatus.BelowProjectAbsoluteMinimum:
                    return prefix + "A boundary cut is below the project absolute minimum.";
                default:
                    return prefix;
            }
        }

        private static CandidateGenerationReport AddGenericWholeRoomPhaseCandidates(
            IList<LayoutCandidate> candidates,
            AxisAlignedOrthogonalPolygon room,
            EngineeringOrthogonalLayoutParameters parameters,
            bool includeWallCornerAnchors)
        {
            bool xLimit;
            bool yLimit;
            List<PhaseOffset> xOffsets = BuildAxisPhaseOffsets(
                room,
                TileLayoutAxis.X,
                parameters.TileWidth,
                parameters.GridTileWidth,
                parameters.Policy,
                includeWallCornerAnchors,
                parameters.DoorOpening,
                out xLimit);
            List<PhaseOffset> yOffsets = BuildAxisPhaseOffsets(
                room,
                TileLayoutAxis.Y,
                parameters.TileHeight,
                parameters.GridTileHeight,
                parameters.Policy,
                includeWallCornerAnchors,
                parameters.DoorOpening,
                out yLimit);

            var pairs = new List<PhasePair>();
            foreach (PhaseOffset x in xOffsets)
            {
                foreach (PhaseOffset y in yOffsets)
                {
                    pairs.Add(new PhasePair(x, y));
                }
            }

            pairs.Sort(PhasePair.Compare);
            int availableDoubleAnchorPairs = pairs.Count(pair =>
                pair.HasDoubleTargetAnchor);
            int availableSingleAnchorPairs = pairs.Count(pair =>
                pair.HasAnyTargetAnchor && !pair.HasDoubleTargetAnchor);
            bool combinationLimit = pairs.Count
                > CandidateSearchLimits.MaximumWholeRoomPhaseCombinations;
            if (combinationLimit)
            {
                var selectedPairs = new List<PhasePair>();
                foreach (IGrouping<int, PhasePair> group in pairs.GroupBy(pair =>
                    pair.SelectionPriority).OrderBy(group => group.Key))
                {
                    if (selectedPairs.Count + group.Count()
                        > CandidateSearchLimits.MaximumWholeRoomPhaseCombinations)
                    {
                        break;
                    }

                    selectedPairs.AddRange(group);
                }

                pairs = selectedPairs;
            }

            int doubleAnchorPairs = pairs.Count(pair =>
                pair.HasDoubleTargetAnchor);
            int singleAnchorPairs = pairs.Count(pair =>
                pair.HasAnyTargetAnchor && !pair.HasDoubleTargetAnchor);
            bool anchorCombinationLimit =
                doubleAnchorPairs < availableDoubleAnchorPairs
                || singleAnchorPairs < availableSingleAnchorPairs;

            var existing = new List<PhaseKey>();
            foreach (LayoutCandidate candidate in candidates)
            {
                PhaseKey key;
                if (TryGetPhaseKey(
                    candidate,
                    room,
                    parameters.GridTileWidth,
                    parameters.GridTileHeight,
                    out key))
                {
                    AddPhaseKey(existing, key);
                }
            }

            int generated = 0;
            int duplicate = 0;
            foreach (PhasePair pair in pairs)
            {
                var key = new PhaseKey(pair.X.Value, pair.Y.Value);
                if (existing.Any(value => value.NearlyEquals(key)))
                {
                    duplicate++;
                    int existingIndex = FindCandidateByPhase(
                        candidates,
                        room,
                        parameters.GridTileWidth,
                        parameters.GridTileHeight,
                        key);
                    if (existingIndex >= 0)
                    {
                        candidates[existingIndex] = WithAdditionalPhaseSources(
                            candidates[existingIndex],
                            pair.Sources);
                    }
                    continue;
                }

                AddPhaseKey(existing, key);
                generated++;
                string id = "whole-phase-" + generated.ToString("D4");
                string reason = "Bounded whole-room phase generated from "
                    + pair.X.Reason + " on X and " + pair.Y.Reason
                    + " on Y; no aesthetic score is applied.";
                var phase = new ConfirmedGridPhase(
                    id,
                    room.West + pair.X.Value,
                    room.South + pair.Y.Value,
                    reason);
                candidates.Add(BuildWholeRoomCandidate(
                    room,
                    parameters,
                    null,
                    id,
                    false,
                    false,
                    phase,
                    null,
                    null,
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.AlternativeWholeRoomPhaseGenerated,
                        CandidateDiagnosticSeverity.Information,
                        reason),
                    pair.Sources));
            }

            return new CandidateGenerationReport(
                xOffsets.Count,
                yOffsets.Count,
                pairs.Count,
                generated,
                duplicate,
                0,
                0,
                xLimit,
                yLimit,
                combinationLimit,
                false,
                WallCornerEvaluator.Evaluate(room, null).Count(corner =>
                    corner.IsOptimizationTarget),
                xOffsets.Count(offset => offset.HasTargetCornerAnchor),
                yOffsets.Count(offset => offset.HasTargetCornerAnchor),
                doubleAnchorPairs,
                singleAnchorPairs,
                xOffsets.Sum(offset => Math.Max(0, offset.Sources.Count - 1))
                    + yOffsets.Sum(offset =>
                        Math.Max(0, offset.Sources.Count - 1)),
                anchorCombinationLimit,
                includeWallCornerAnchors,
                true);
        }

        private static List<PhaseOffset> BuildAxisPhaseOffsets(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutAxis axis,
            double tileSize,
            double gridTileSize,
            LayoutPolicyProfile policy,
            bool includeWallCornerAnchors,
            DoorOpening doorOpening,
            out bool limitReached)
        {
            var offsets = new List<PhaseOffset>();
            IList<WallCornerAssessment> corners =
                WallCornerEvaluator.Evaluate(room, null);
            AddDoorControlledRedistributionPhase(
                offsets,
                room,
                axis,
                tileSize,
                gridTileSize,
                doorOpening);
            double minimum = axis == TileLayoutAxis.X
                ? room.West
                : room.South;
            foreach (WallCornerAssessment corner in corners)
            {
                double coordinate = axis == TileLayoutAxis.X
                    ? corner.Position.X
                    : corner.Position.Y;
                bool target = includeWallCornerAnchors
                    && corner.IsOptimizationTarget;
                AddPhaseOffset(
                    offsets,
                    coordinate - minimum,
                    gridTileSize,
                    target ? 0 : 1,
                    axis,
                    target
                        ? GridPhaseSourceKind.TargetCornerAnchor
                        : GridPhaseSourceKind.BoundaryVertex,
                    target ? corner.Id : string.Empty,
                    target
                        ? "a target reflex-corner anchor"
                        : "an orthogonal boundary vertex");
            }

            List<double> residues = offsets
                .Select(offset => offset.Value)
                .OrderBy(value => value)
                .ToList();
            for (int index = 0; index < residues.Count; index++)
            {
                double first = residues[index];
                double second = index + 1 < residues.Count
                    ? residues[index + 1]
                    : residues[0] + gridTileSize;
                double gap = second - first;
                if (gap > GeometryTolerance.Coordinate)
                {
                    AddPhaseOffset(offsets, first + (gap / 2.0), gridTileSize, 2,
                        axis,
                        GridPhaseSourceKind.AdjacentBoundaryResidueMidpoint,
                        string.Empty,
                        "a midpoint between adjacent boundary residues");
                }
            }

            double recommended = tileSize
                * EngineeringLayoutRules.DefaultMinimumCutRatio;
            foreach (double residue in residues)
            {
                AddPhaseOffset(offsets, residue - recommended, gridTileSize, 3,
                    axis,
                    GridPhaseSourceKind.RecommendedMinimumContact,
                    string.Empty,
                    "a recommended-minimum threshold contact");
                AddPhaseOffset(offsets, residue + recommended, gridTileSize, 3,
                    axis,
                    GridPhaseSourceKind.RecommendedMinimumContact,
                    string.Empty,
                    "a recommended-minimum threshold contact");
                if (policy != null && policy.HasProjectAbsoluteMinimum)
                {
                    double absolute = policy.ProjectAbsoluteMinimumCut.Value;
                    AddPhaseOffset(offsets, residue - absolute, gridTileSize, 4,
                        axis,
                        GridPhaseSourceKind.ProjectAbsoluteMinimumContact,
                        string.Empty,
                        "a project-absolute-minimum threshold contact");
                    AddPhaseOffset(offsets, residue + absolute, gridTileSize, 4,
                        axis,
                        GridPhaseSourceKind.ProjectAbsoluteMinimumContact,
                        string.Empty,
                        "a project-absolute-minimum threshold contact");
                }
            }

            offsets.Sort(PhaseOffset.Compare);
            limitReached = offsets.Count
                > CandidateSearchLimits.MaximumPhaseCandidatesPerAxis;
            if (limitReached)
            {
                var selectedOffsets = new List<PhaseOffset>();
                foreach (IGrouping<int, PhaseOffset> group in offsets
                    .GroupBy(offset => offset.Priority)
                    .OrderBy(group => group.Key))
                {
                    if (selectedOffsets.Count + group.Count()
                        > CandidateSearchLimits.MaximumPhaseCandidatesPerAxis)
                    {
                        break;
                    }

                    selectedOffsets.AddRange(group);
                }

                offsets = selectedOffsets;
            }

            return offsets;
        }

        private static void AddPhaseOffset(
            ICollection<PhaseOffset> offsets,
            double value,
            double tileSize,
            int priority,
            TileLayoutAxis axis,
            GridPhaseSourceKind sourceKind,
            string cornerId,
            string reason)
        {
            double normalized = NormalizePhase(value, tileSize);
            var source = new GridPhaseSource(
                axis,
                sourceKind,
                normalized,
                cornerId,
                reason);
            PhaseOffset existing = offsets.FirstOrDefault(offset =>
                GeometryTolerance.NearlyEqual(offset.Value, normalized));
            if (existing != null)
            {
                existing.AddSource(source);
                if (priority < existing.Priority)
                {
                    existing.Priority = priority;
                    existing.Reason = reason;
                }

                return;
            }

            offsets.Add(new PhaseOffset(
                normalized,
                priority,
                reason,
                source));
        }

        private static void AddDoorControlledRedistributionPhase(
            ICollection<PhaseOffset> offsets,
            AxisAlignedOrthogonalPolygon room,
            TileLayoutAxis axis,
            double tileSize,
            double gridTileSize,
            DoorOpening doorOpening)
        {
            double length = axis == TileLayoutAxis.X
                ? room.Width
                : room.Height;
            double grout = gridTileSize - tileSize;
            double remainder = length % gridTileSize;
            double physicalLength = length - grout;
            double physicalRemainder = remainder <= GeometryTolerance.Coordinate
                ? 0.0
                : remainder - grout;
            double minimumCut = tileSize
                * EngineeringLayoutRules.DefaultMinimumCutRatio;
            if (remainder <= GeometryTolerance.Coordinate
                || gridTileSize - remainder <= GeometryTolerance.Coordinate)
            {
                return;
            }

            RoomSide lowSide = axis == TileLayoutAxis.X
                ? RoomSide.West
                : RoomSide.South;
            RoomSide preferredSide = GetDoorControlledHalfSide(
                room,
                axis,
                doorOpening);
            double half = tileSize * EngineeringLayoutRules.HalfTileRatio;
            double boundaryWidth;
            double oppositeWidth;
            bool halfPattern = TryFindBoundaryPattern(
                physicalLength,
                tileSize,
                minimumCut,
                half,
                out oppositeWidth,
                out boundaryWidth);
            if (halfPattern)
            {
                double firstWidth = preferredSide == lowSide
                    ? boundaryWidth
                    : oppositeWidth;
                bool narrowRemainder = physicalRemainder + GeometryTolerance.Coordinate
                    < minimumCut;
                AddPhaseOffset(
                    offsets,
                    firstWidth + grout,
                    gridTileSize,
                    1,
                    axis,
                    narrowRemainder
                        ? GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution
                        : GridPhaseSourceKind
                            .DoorControlledBoundaryPattern,
                    string.Empty,
                    narrowRemainder
                        ? "the G1 door-controlled half-tile and transition-tile boundary allocation"
                        : "the G1 door-controlled half-tile boundary pattern with a recommended transition");

                // Keep the opposite-side allocation as a lower-priority,
                // auditable alternative.  The preferred side remains first,
                // but a concave cut can make only one orientation viable after
                // the continuous phase is clipped to the actual room.
                if (Math.Abs(boundaryWidth - oppositeWidth)
                    > GeometryTolerance.Coordinate)
                {
                    double mirroredFirstWidth = preferredSide == lowSide
                        ? oppositeWidth
                        : boundaryWidth;
                    AddPhaseOffset(
                        offsets,
                        mirroredFirstWidth + grout,
                        gridTileSize,
                        2,
                        axis,
                        GridPhaseSourceKind.DoorControlledBoundaryPattern,
                        string.Empty,
                        "the mirrored G1 door-controlled half-tile boundary pattern fallback");
                }
                return;
            }

            // When the half-tile would leave the opposite boundary below the
            // recommendation, try a full tile on the preferred side. This
            // preserves the user's deterministic side preference without
            // creating a hidden narrow strip.
            double fullTile = tileSize;
            bool fullPattern = TryFindBoundaryPattern(
                physicalLength,
                tileSize,
                minimumCut,
                fullTile,
                out oppositeWidth,
                out boundaryWidth);
            if (!fullPattern)
            {
                return;
            }

            double fullFirstWidth = preferredSide == lowSide
                ? boundaryWidth
                : oppositeWidth;
            AddPhaseOffset(
                offsets,
                fullFirstWidth + grout,
                gridTileSize,
                1,
                axis,
                GridPhaseSourceKind.DoorControlledBoundaryPattern,
                string.Empty,
                "the G1 door-controlled full-tile boundary pattern with a recommended opposite remainder");

            if (Math.Abs(boundaryWidth - oppositeWidth)
                > GeometryTolerance.Coordinate)
            {
                double mirroredFirstWidth = preferredSide == lowSide
                    ? oppositeWidth
                    : boundaryWidth;
                AddPhaseOffset(
                    offsets,
                    mirroredFirstWidth + grout,
                    gridTileSize,
                    2,
                    axis,
                    GridPhaseSourceKind.DoorControlledBoundaryPattern,
                    string.Empty,
                    "the mirrored G1 door-controlled full-tile boundary pattern fallback");
            }
        }

        private static bool TryFindBoundaryPattern(
            double length,
            double tileSize,
            double minimumCut,
            double preferredBoundaryWidth,
            out double oppositeBoundaryWidth,
            out double selectedBoundaryWidth)
        {
            oppositeBoundaryWidth = 0.0;
            selectedBoundaryWidth = preferredBoundaryWidth;
            double remaining = length - preferredBoundaryWidth;
            if (remaining <= GeometryTolerance.Coordinate)
            {
                return false;
            }

            int maximumFullTileCount = (int)Math.Floor(
                remaining / tileSize) + 1;
            for (int fullTileCount = maximumFullTileCount;
                fullTileCount >= 0;
                fullTileCount--)
            {
                double opposite = remaining
                    - (fullTileCount * tileSize);
                if (opposite + GeometryTolerance.Coordinate
                        < minimumCut
                    || opposite - GeometryTolerance.Coordinate
                        > tileSize)
                {
                    continue;
                }

                oppositeBoundaryWidth = opposite;
                return true;
            }

            return false;
        }

        private static RoomSide GetDoorControlledHalfSide(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutAxis axis,
            DoorOpening doorOpening)
        {
            bool doorNormalIsX = doorOpening.Wall == RoomSide.West
                || doorOpening.Wall == RoomSide.East;
            if ((axis == TileLayoutAxis.X) == doorNormalIsX)
            {
                return Opposite(doorOpening.Wall);
            }

            double minimum = axis == TileLayoutAxis.X
                ? room.West
                : room.South;
            double maximum = axis == TileLayoutAxis.X
                ? room.East
                : room.North;
            double center = Math.Max(
                minimum,
                Math.Min(maximum, doorOpening.Center));
            double lowDistance = center - minimum;
            double highDistance = maximum - center;
            if (Math.Abs(lowDistance - highDistance)
                <= GeometryTolerance.Coordinate)
            {
                return axis == TileLayoutAxis.X
                    ? RoomSide.West
                    : RoomSide.North;
            }

            return lowDistance < highDistance
                ? (axis == TileLayoutAxis.X
                    ? RoomSide.West
                    : RoomSide.South)
                : (axis == TileLayoutAxis.X
                    ? RoomSide.East
                    : RoomSide.North);
        }

        private static double NormalizePhase(double value, double tileSize)
        {
            double normalized = value % tileSize;
            if (normalized < 0.0)
            {
                normalized += tileSize;
            }

            if (GeometryTolerance.NearlyEqual(normalized, tileSize)
                || GeometryTolerance.NearlyEqual(normalized, 0.0))
            {
                return 0.0;
            }

            return normalized;
        }

        private static bool TryGetPhaseKey(
            LayoutCandidate candidate,
            AxisAlignedOrthogonalPolygon room,
            double tileWidth,
            double tileHeight,
            out PhaseKey key)
        {
            key = default(PhaseKey);
            if (candidate.Structure == null
                || !candidate.Structure.UsesWholeRoomSinglePhase
                || candidate.Structure.Regions.Count != 1)
            {
                return false;
            }

            LayoutRegionPhase region = candidate.Structure.Regions[0];
            if (region.VerticalCuts.Count == 0
                || region.HorizontalCuts.Count == 0)
            {
                return false;
            }

            key = new PhaseKey(
                NormalizePhase(region.VerticalCuts[0] - room.West, tileWidth),
                NormalizePhase(region.HorizontalCuts[0] - room.South, tileHeight));
            return true;
        }

        private static void AddPhaseKey(
            ICollection<PhaseKey> keys,
            PhaseKey key)
        {
            if (!keys.Any(value => value.NearlyEquals(key)))
            {
                keys.Add(key);
            }
        }

        private static int FindCandidateByPhase(
            IList<LayoutCandidate> candidates,
            AxisAlignedOrthogonalPolygon room,
            double tileWidth,
            double tileHeight,
            PhaseKey key)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                PhaseKey candidateKey;
                if (TryGetPhaseKey(
                        candidates[index],
                        room,
                        tileWidth,
                        tileHeight,
                        out candidateKey)
                    && candidateKey.NearlyEquals(key))
                {
                    return index;
                }
            }

            return -1;
        }

        private static LayoutCandidate WithAdditionalPhaseSources(
            LayoutCandidate candidate,
            IEnumerable<GridPhaseSource> additionalSources)
        {
            var sources = new List<GridPhaseSource>(candidate.PhaseSources);
            foreach (GridPhaseSource source in additionalSources)
            {
                if (!sources.Any(existing =>
                    existing.Axis == source.Axis
                    && existing.Kind == source.Kind
                    && string.Equals(
                        existing.CornerId,
                        source.CornerId,
                        StringComparison.Ordinal)
                    && GeometryTolerance.NearlyEqual(
                        existing.PhaseOffset,
                        source.PhaseOffset)))
                {
                    sources.Add(source);
                }
            }

            return new LayoutCandidate(
                candidate.Id,
                candidate.IsDefault,
                candidate.IsFlippedAlternative,
                candidate.SelectionReason,
                new List<BoundaryBandPlan>(candidate.AxisPlans),
                new List<LineSegment3D>(candidate.DivisionLines),
                candidate.Tiles,
                new List<CandidateDiagnostic>(candidate.Diagnostics),
                candidate.Metrics,
                candidate.Structure,
                new List<TileFootprintAssessment>(candidate.TileAssessments),
                new List<WallCornerAssessment>(candidate.WallCornerAssessments),
                sources);
        }

        private static CandidateGenerationReport ApplyDominanceAndRetentionLimit(
            IList<LayoutCandidate> candidates,
            CandidateGenerationReport report)
        {
            var dominatedBy = new Dictionary<int, string>();
            for (int first = 0; first < candidates.Count; first++)
            {
                if (candidates[first].IsRejected
                    || !IsGeneratedPhaseCandidate(candidates[first]))
                {
                    continue;
                }

                for (int second = 0; second < candidates.Count; second++)
                {
                    if (first == second || candidates[second].IsRejected)
                    {
                        continue;
                    }

                    if (HasSameDecisionGroup(
                            candidates[second],
                            candidates[first])
                        // A confirmed G1 door-controlled boundary pattern
                        // is a product rule, not an aesthetic tie-breaker.
                        // Do not let any Pareto comparison erase it: this
                        // preserves both preferred and mirrored orientations
                        // so the complete-room clipping result can be audited
                        // and compared by the frozen decision order.
                        && !HasDoorControlledBoundaryPattern(
                            candidates[first])
                        && Dominates(
                            candidates[second].Metrics,
                            candidates[first].Metrics))
                    {
                        dominatedBy[first] = candidates[second].Id;
                        break;
                    }
                }
            }

            foreach (KeyValuePair<int, string> pair in dominatedBy)
            {
                candidates[pair.Key] = WithAdditionalDiagnostic(
                    candidates[pair.Key],
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.DominatedByCandidate,
                        CandidateDiagnosticSeverity.Rejection,
                        "The candidate is Pareto-dominated by " + pair.Value
                            + " using only the frozen objective metrics."));
            }

            List<int> retained = Enumerable.Range(0, candidates.Count)
                .Where(index => !candidates[index].IsRejected
                    && IsGeneratedPhaseCandidate(candidates[index]))
                .ToList();
            int nonGeneratedRetainedCount = candidates.Count(candidate =>
                !candidate.IsRejected
                && !IsGeneratedPhaseCandidate(candidate));
            int generatedRetentionLimit = Math.Max(
                0,
                CandidateSearchLimits.MaximumNonDominatedCandidates
                    - nonGeneratedRetainedCount);
            bool retentionLimit = retained.Count
                > generatedRetentionLimit;
            if (retentionLimit)
            {
                foreach (int index in retained.Skip(
                    generatedRetentionLimit))
                {
                    candidates[index] = WithAdditionalDiagnostic(
                        candidates[index],
                        new CandidateDiagnostic(
                            CandidateDiagnosticCode.CandidateSearchTruncated,
                            CandidateDiagnosticSeverity.Rejection,
                            "The non-dominated retention cap was reached; the search result is explicitly truncated."));
                }
            }

            int retainedCount = candidates.Count(candidate => !candidate.IsRejected);
            return new CandidateGenerationReport(
                report.XPhaseCount,
                report.YPhaseCount,
                report.PhaseCombinationCount,
                report.GeneratedAlternativeCount,
                report.DuplicatePhaseCount,
                dominatedBy.Count,
                retainedCount,
                report.XPhaseLimitReached,
                report.YPhaseLimitReached,
                report.CombinationLimitReached,
                retentionLimit,
                report.OptimizationTargetCornerCount,
                report.XTargetAnchorPhaseCount,
                report.YTargetAnchorPhaseCount,
                report.DoubleAnchorCombinationCount,
                report.SingleAnchorCombinationCount,
                report.MergedPhaseSourceCount,
                report.AnchorCombinationLimitReached,
                report.WallCornerSearchEnabled,
                report.PhaseSearchEnabled);
        }

        private static bool IsGeneratedPhaseCandidate(LayoutCandidate candidate)
        {
            return candidate.Diagnostics.Any(diagnostic =>
                diagnostic.Code == CandidateDiagnosticCode.AlternativeWholeRoomPhaseGenerated);
        }

        private static bool HasSameDecisionGroup(
            LayoutCandidate first,
            LayoutCandidate second)
        {
            return first.RequiresProjectPolicy == second.RequiresProjectPolicy
                && first.RequiresUserReview == second.RequiresUserReview;
        }

        private static bool HasDoorControlledBoundaryPattern(
            LayoutCandidate candidate)
        {
            return candidate != null
                && candidate.PhaseSources.Any(source =>
                    source.Kind == GridPhaseSourceKind
                        .DoorControlledBoundaryPattern
                    || source.Kind == GridPhaseSourceKind
                        .DoorControlledBoundaryRedistribution);
        }

        private static bool Dominates(
            LayoutCandidateMetrics first,
            LayoutCandidateMetrics second)
        {
            bool noWorse =
                first.InteriorNonFullTileCount <= second.InteriorNonFullTileCount
                && first.InteriorNonFullTileArea
                    <= second.InteriorNonFullTileArea + GeometryTolerance.Coordinate
                && first.InternalTransitionSeamLength
                    <= second.InternalTransitionSeamLength + GeometryTolerance.Coordinate
                && first.BoundaryNonFullTileCount <= second.BoundaryNonFullTileCount
                && first.BelowDefaultMinimumBoundaryTileCount
                    <= second.BelowDefaultMinimumBoundaryTileCount
                && first.PhaseResetCount <= second.PhaseResetCount
                && first.MinimumBoundaryBandWidth + GeometryTolerance.Coordinate
                    >= second.MinimumBoundaryBandWidth
                && first.ExactGridIntersectionCornerCount
                    >= second.ExactGridIntersectionCornerCount
                && first.ExactSeamAlignedCornerCount
                    >= second.ExactSeamAlignedCornerCount;
            if (!noWorse)
            {
                return false;
            }

            return first.InteriorNonFullTileCount < second.InteriorNonFullTileCount
                || first.InteriorNonFullTileArea + GeometryTolerance.Coordinate
                    < second.InteriorNonFullTileArea
                || first.InternalTransitionSeamLength + GeometryTolerance.Coordinate
                    < second.InternalTransitionSeamLength
                || first.BoundaryNonFullTileCount < second.BoundaryNonFullTileCount
                || first.BelowDefaultMinimumBoundaryTileCount
                    < second.BelowDefaultMinimumBoundaryTileCount
                || first.PhaseResetCount < second.PhaseResetCount
                || first.MinimumBoundaryBandWidth
                    > second.MinimumBoundaryBandWidth + GeometryTolerance.Coordinate
                || first.ExactGridIntersectionCornerCount
                    > second.ExactGridIntersectionCornerCount
                || first.ExactSeamAlignedCornerCount
                    > second.ExactSeamAlignedCornerCount;
        }

        private static LayoutCandidate WithAdditionalDiagnostic(
            LayoutCandidate candidate,
            CandidateDiagnostic diagnostic)
        {
            var diagnostics = new List<CandidateDiagnostic>(candidate.Diagnostics)
            {
                diagnostic
            };
            return new LayoutCandidate(
                candidate.Id,
                candidate.IsDefault,
                candidate.IsFlippedAlternative,
                candidate.SelectionReason,
                new List<BoundaryBandPlan>(candidate.AxisPlans),
                new List<LineSegment3D>(candidate.DivisionLines),
                candidate.Tiles,
                diagnostics,
                candidate.Metrics,
                candidate.Structure,
                new List<TileFootprintAssessment>(candidate.TileAssessments),
                new List<WallCornerAssessment>(candidate.WallCornerAssessments),
                new List<GridPhaseSource>(candidate.PhaseSources));
        }

        private static void AddMultipleCandidateDiagnostics(
            IList<LayoutCandidate> candidates)
        {
            int retained = 0;
            foreach (LayoutCandidate candidate in candidates)
            {
                if (!candidate.IsRejected)
                {
                    retained++;
                }
            }

            if (retained <= 1)
            {
                return;
            }

            for (int index = 0; index < candidates.Count; index++)
            {
                LayoutCandidate candidate = candidates[index];
                if (candidate.IsRejected)
                {
                    continue;
                }

                var diagnostics = new List<CandidateDiagnostic>(
                    candidate.Diagnostics);
                diagnostics.Add(
                    new CandidateDiagnostic(
                        CandidateDiagnosticCode.MultipleCandidatesRequireSelection,
                        CandidateDiagnosticSeverity.Warning,
                        "Multiple retained candidates remain because no frozen aesthetic ranking or total score exists."));
                candidates[index] = new LayoutCandidate(
                    candidate.Id,
                    candidate.IsDefault,
                    candidate.IsFlippedAlternative,
                    candidate.SelectionReason,
                    new List<BoundaryBandPlan>(candidate.AxisPlans),
                    new List<LineSegment3D>(candidate.DivisionLines),
                    candidate.Tiles,
                    diagnostics,
                    candidate.Metrics,
                    candidate.Structure,
                    new List<TileFootprintAssessment>(candidate.TileAssessments),
                    new List<WallCornerAssessment>(candidate.WallCornerAssessments),
                    new List<GridPhaseSource>(candidate.PhaseSources));
            }
        }

        private static void GetPlanCuts(
            AxisAlignedRectangle rectangle,
            BoundaryBandPlan parallelPlan,
            BoundaryBandPlan perpendicularPlan,
            TileLayoutAxis parallelAxis,
            out List<double> xCuts,
            out List<double> yCuts)
        {
            if (parallelAxis == TileLayoutAxis.X)
            {
                xCuts = GetPlanCuts(rectangle, parallelPlan);
                yCuts = GetPlanCuts(rectangle, perpendicularPlan);
            }
            else
            {
                xCuts = GetPlanCuts(rectangle, perpendicularPlan);
                yCuts = GetPlanCuts(rectangle, parallelPlan);
            }
        }

        private static List<double> GetPlanCuts(
            AxisAlignedRectangle rectangle,
            BoundaryBandPlan plan)
        {
            double minimum = plan.Axis == TileLayoutAxis.X
                ? rectangle.West
                : rectangle.South;
            var cuts = new List<double>();
            double coordinate = minimum;
            for (int index = 0; index < plan.SegmentWidths.Count - 1; index++)
            {
                coordinate += plan.SegmentWidths[index];
                cuts.Add(coordinate);
            }

            return cuts;
        }

        private static List<double> GetInheritedParallelCuts(
            AxisAlignedRectangle main,
            AxisAlignedRectangle secondary,
            BoundaryBandPlan mainParallelPlan,
            ConnectionInfo connection,
            ProtrusionBandTreatment treatment)
        {
            var cuts = new List<double>();
            foreach (double cut in GetPlanCuts(main, mainParallelPlan))
            {
                double minimum = connection.ParallelAxis == TileLayoutAxis.X
                    ? secondary.West
                    : secondary.South;
                double maximum = connection.ParallelAxis == TileLayoutAxis.X
                    ? secondary.East
                    : secondary.North;
                if (cut > minimum + GeometryTolerance.Coordinate
                    && cut < maximum - GeometryTolerance.Coordinate)
                {
                    cuts.Add(cut);
                }
            }

            if (treatment == ProtrusionBandTreatment.Independent
                && connection.ProtrusionSide.HasValue)
            {
                double boundary = GetRectangleSideCoordinate(
                    main,
                    connection.ProtrusionSide.Value);
                double secondaryMinimum = connection.ParallelAxis
                    == TileLayoutAxis.X ? secondary.West : secondary.South;
                double secondaryMaximum = connection.ParallelAxis
                    == TileLayoutAxis.X ? secondary.East : secondary.North;
                if (boundary > secondaryMinimum + GeometryTolerance.Coordinate
                    && boundary < secondaryMaximum - GeometryTolerance.Coordinate)
                {
                    cuts.Add(boundary);
                }

                double tileSize = mainParallelPlan.GridTileSize;
                if (connection.ProtrusionSide == GetHighSide(connection.ParallelAxis))
                {
                    for (double value = boundary + tileSize;
                        value < secondaryMaximum - GeometryTolerance.Coordinate;
                        value += tileSize)
                    {
                        cuts.Add(value);
                    }
                }
                else
                {
                    for (double value = boundary - tileSize;
                        value > secondaryMinimum + GeometryTolerance.Coordinate;
                        value -= tileSize)
                    {
                        cuts.Add(value);
                    }
                }
            }

            cuts.Sort();
            RemoveNearDuplicates(cuts);
            return cuts;
        }

        private static void AddRectangleTiles(
            ICollection<TileFootprint> target,
            AxisAlignedOrthogonalPolygon room,
            AxisAlignedRectangle rectangle,
            IList<double> xCuts,
            IList<double> yCuts,
            double tileWidth,
            double tileHeight,
            double groutWidthMm)
        {
            List<double> xs = AddBounds(rectangle.West, rectangle.East, xCuts);
            List<double> ys = AddBounds(rectangle.South, rectangle.North, yCuts);
            for (int x = 0; x < xs.Count - 1; x++)
            {
                for (int y = 0; y < ys.Count - 1; y++)
                {
                    target.Add(
                        OrthogonalFootprintBuilder.CreateRectangleFootprint(
                            room,
                            xs[x],
                            xs[x + 1],
                            ys[y],
                            ys[y + 1],
                            tileWidth,
                            tileHeight,
                            groutWidthMm));
                }
            }
        }

        private static List<double> AddBounds(
            double minimum,
            double maximum,
            IList<double> cuts)
        {
            var result = new List<double> { minimum };
            foreach (double cut in cuts)
            {
                if (cut > minimum + GeometryTolerance.Coordinate
                    && cut < maximum - GeometryTolerance.Coordinate)
                {
                    result.Add(cut);
                }
            }

            result.Add(maximum);
            result.Sort();
            RemoveNearDuplicates(result);
            return result;
        }

        private static List<double> GeneratePeriodicCuts(
            double minimum,
            double maximum,
            double phase,
            double tileSize)
        {
            double multiplier = Math.Ceiling((minimum - phase) / tileSize);
            double coordinate = phase + (multiplier * tileSize);
            if (coordinate <= minimum + GeometryTolerance.Coordinate)
            {
                coordinate += tileSize;
            }

            var cuts = new List<double>();
            int maximumCount =
                TileLayoutRules.MaximumParameterizedDivisionLineCount;
            while (coordinate < maximum - GeometryTolerance.Coordinate)
            {
                cuts.Add(coordinate);
                if (cuts.Count > maximumCount)
                {
                    throw new TileLayoutLimitExceededException(
                        cuts.Count,
                        maximumCount);
                }

                coordinate += tileSize;
            }

            return cuts;
        }

        private static BoundaryBandPlan BuildWholeRoomPhasePlan(
            TileLayoutAxis axis,
            double minimum,
            double maximum,
            double tileSize,
            IList<double> cuts,
            IList<GridPhaseSource> phaseSources,
            double gridTileSize = double.NaN)
        {
            double resolvedGridTileSize = double.IsNaN(gridTileSize)
                ? tileSize
                : gridTileSize;
            double grout = resolvedGridTileSize - tileSize;
            var segmentWidths = new List<double>(cuts.Count + 1);
            double previous = minimum;
            foreach (double cut in cuts)
            {
                segmentWidths.Add(cut - previous);
                previous = cut;
            }

            segmentWidths.Add(maximum - previous);
            RoomSide lowSide = axis == TileLayoutAxis.X
                ? RoomSide.West
                : RoomSide.South;
            RoomSide highSide = GetHighSide(axis);
            double lowWidth = Math.Max(0.0, segmentWidths[0] - grout);
            double highWidth = Math.Max(
                0.0,
                segmentWidths[segmentWidths.Count - 1] - grout);
            bool hasDoorControlledPattern = phaseSources != null
                && phaseSources.Any(source =>
                    source.Axis == axis
                    && (source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern
                        || source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution));
            int fullTileCount = segmentWidths.Count(width =>
                Math.Abs(width - resolvedGridTileSize)
                    <= GeometryTolerance.Coordinate);
            int interiorFullTileCount = segmentWidths
                .Skip(1)
                .Take(Math.Max(0, segmentWidths.Count - 2))
                .Count(width =>
                    Math.Abs(width - resolvedGridTileSize)
                        <= GeometryTolerance.Coordinate);
            double length = maximum - minimum;
            double rawRemainder = length
                - (Math.Floor(length / resolvedGridTileSize)
                    * resolvedGridTileSize);
            double naturalRemainder = rawRemainder
                <= GeometryTolerance.Coordinate
                ? 0.0
                : rawRemainder - grout;
            if (naturalRemainder <= GeometryTolerance.Coordinate
                || tileSize - naturalRemainder
                    <= GeometryTolerance.Coordinate)
            {
                naturalRemainder = 0.0;
            }

            return new BoundaryBandPlan(
                axis,
                DoorControlledAxisRole.WholeRoomPhase,
                tileSize,
                naturalRemainder,
                lowSide,
                lowSide,
                new AxisBoundaryBand(
                    lowSide,
                    lowWidth,
                    GetPhaseBoundaryKind(
                        lowWidth,
                        highWidth,
                        tileSize,
                        hasDoorControlledPattern)),
                new AxisBoundaryBand(
                    highSide,
                    highWidth,
                    GetPhaseBoundaryKind(
                        highWidth,
                        lowWidth,
                        tileSize,
                        hasDoorControlledPattern)),
                fullTileCount,
                interiorFullTileCount,
                false,
                segmentWidths,
                resolvedGridTileSize);
        }

        private static BoundaryBandKind GetPhaseBoundaryKind(
            double width,
            double oppositeWidth,
            double tileSize,
            bool hasDoorControlledPattern)
        {
            if (Math.Abs(width - tileSize)
                <= GeometryTolerance.Coordinate)
            {
                return BoundaryBandKind.FullTile;
            }

            if (hasDoorControlledPattern
                && Math.Abs(width
                    - (tileSize * EngineeringLayoutRules.HalfTileRatio))
                    <= GeometryTolerance.Coordinate)
            {
                return BoundaryBandKind.HalfTile;
            }

            if (hasDoorControlledPattern
                && Math.Abs(oppositeWidth
                    - (tileSize * EngineeringLayoutRules.HalfTileRatio))
                    <= GeometryTolerance.Coordinate)
            {
                return BoundaryBandKind.Transition;
            }

            return BoundaryBandKind.NaturalRemainder;
        }

        private static double GetFirstPhaseCoordinate(
            double minimum,
            BoundaryBandPlan plan)
        {
            return minimum + plan.SegmentWidths[0];
        }

        private static BoundaryBandPlan ReversePlan(BoundaryBandPlan plan)
        {
            var widths = new List<double>(plan.SegmentWidths);
            widths.Reverse();
            return new BoundaryBandPlan(
                plan.Axis,
                plan.Role,
                plan.TileSize,
                plan.NaturalRemainder,
                Opposite(plan.ControlSide),
                Opposite(plan.ConstructionStartSide),
                new AxisBoundaryBand(
                    plan.LowBoundary.Side,
                    plan.HighBoundary.Width,
                    plan.HighBoundary.Kind),
                new AxisBoundaryBand(
                    plan.HighBoundary.Side,
                    plan.LowBoundary.Width,
                    plan.LowBoundary.Kind),
                plan.FullTileCount,
                plan.InteriorFullTileCount,
                plan.UsesRedistribution,
                widths,
                plan.GridTileSize);
        }

        private static double GetBoundaryWidth(
            BoundaryBandPlan plan,
            RoomSide? side)
        {
            return side.HasValue ? plan.GetBoundary(side.Value).Width : 0.0;
        }

        private static string GetRegionalId(
            LayoutCandidate source,
            ProtrusionBandTreatment treatment,
            bool mirrored)
        {
            return "regional-" + source.Id + "-"
                + treatment.ToString().ToLowerInvariant()
                + (mirrored ? "-mirrored" : string.Empty);
        }

        private static LayoutCandidateMetrics EmptyMetrics()
        {
            return new LayoutCandidateMetrics(
                0,
                0.0,
                0.0,
                0,
                0,
                0.0,
                0,
                0,
                0,
                0);
        }

        private static AxisAlignedOrthogonalPolygon RectanglePolygon(
            AxisAlignedRectangle rectangle)
        {
            return new AxisAlignedOrthogonalPolygon(
                new List<Point3D>
                {
                    new Point3D(rectangle.West, rectangle.South, rectangle.Elevation),
                    new Point3D(rectangle.East, rectangle.South, rectangle.Elevation),
                    new Point3D(rectangle.East, rectangle.North, rectangle.Elevation),
                    new Point3D(rectangle.West, rectangle.North, rectangle.Elevation)
                },
                rectangle.Elevation);
        }

        private static List<LineSegment3D> MergeDivisionLines(
            IList<LineSegment3D> source)
        {
            var normalized = new List<NormalizedLine>();
            foreach (LineSegment3D line in source)
            {
                bool vertical = GeometryTolerance.NearlyEqual(
                    line.Start.X,
                    line.End.X);
                normalized.Add(
                    new NormalizedLine(
                        vertical,
                        vertical ? line.Start.X : line.Start.Y,
                        vertical
                            ? Math.Min(line.Start.Y, line.End.Y)
                            : Math.Min(line.Start.X, line.End.X),
                        vertical
                            ? Math.Max(line.Start.Y, line.End.Y)
                            : Math.Max(line.Start.X, line.End.X),
                        line.Start.Z));
            }

            normalized.Sort(NormalizedLine.Compare);
            var merged = new List<NormalizedLine>();
            foreach (NormalizedLine line in normalized)
            {
                if (merged.Count == 0)
                {
                    merged.Add(line);
                    continue;
                }

                NormalizedLine previous = merged[merged.Count - 1];
                if (previous.Vertical == line.Vertical
                    && GeometryTolerance.NearlyEqual(previous.Fixed, line.Fixed)
                    && line.Start <= previous.End + GeometryTolerance.Coordinate)
                {
                    merged[merged.Count - 1] = new NormalizedLine(
                        previous.Vertical,
                        previous.Fixed,
                        previous.Start,
                        Math.Max(previous.End, line.End),
                        previous.Elevation);
                }
                else
                {
                    merged.Add(line);
                }
            }

            var result = new List<LineSegment3D>(merged.Count);
            foreach (NormalizedLine line in merged)
            {
                result.Add(
                    line.Vertical
                        ? new LineSegment3D(
                            new Point3D(line.Fixed, line.Start, line.Elevation),
                            new Point3D(line.Fixed, line.End, line.Elevation))
                        : new LineSegment3D(
                            new Point3D(line.Start, line.Fixed, line.Elevation),
                            new Point3D(line.End, line.Fixed, line.Elevation)));
            }

            return result;
        }

        private static ConnectionInfo ValidateAndGetConnection(
            AxisAlignedOrthogonalPolygon room,
            MainSecondaryRegionDefinition definition)
        {
            ValidateRectangleInsideRoom(room, definition.MainRegion);
            ValidateRectangleInsideRoom(room, definition.SecondaryRegion);
            if (!GeometryTolerance.NearlyEqual(
                definition.MainRegion.Elevation,
                definition.SecondaryRegion.Elevation))
            {
                throw new ArgumentException(
                    "Main and secondary regions must have the same elevation.",
                    nameof(definition));
            }

            ConnectionInfo connection = GetConnection(
                definition.MainRegion,
                definition.SecondaryRegion);
            double roomArea = PolygonArea(room);
            double rectangleArea =
                (definition.MainRegion.Width * definition.MainRegion.Height)
                + (definition.SecondaryRegion.Width
                    * definition.SecondaryRegion.Height);
            double areaTolerance = GeometryTolerance.Coordinate
                * Math.Max(room.Width, room.Height)
                * room.Vertices.Count;
            if (Math.Abs(roomArea - rectangleArea) > areaTolerance)
            {
                throw new ArgumentException(
                    "The explicit main and secondary rectangles must exactly cover the room.",
                    nameof(definition));
            }

            return connection;
        }

        private static ConnectionInfo GetConnection(
            AxisAlignedRectangle main,
            AxisAlignedRectangle secondary)
        {
            if (GeometryTolerance.NearlyEqual(main.South, secondary.North))
            {
                return CreateHorizontalConnection(
                    main,
                    secondary,
                    main.South,
                    RoomSide.North,
                    RoomSide.South);
            }

            if (GeometryTolerance.NearlyEqual(main.North, secondary.South))
            {
                return CreateHorizontalConnection(
                    main,
                    secondary,
                    main.North,
                    RoomSide.South,
                    RoomSide.North);
            }

            if (GeometryTolerance.NearlyEqual(main.West, secondary.East))
            {
                return CreateVerticalConnection(
                    main,
                    secondary,
                    main.West,
                    RoomSide.East,
                    RoomSide.West);
            }

            if (GeometryTolerance.NearlyEqual(main.East, secondary.West))
            {
                return CreateVerticalConnection(
                    main,
                    secondary,
                    main.East,
                    RoomSide.West,
                    RoomSide.East);
            }

            throw new ArgumentException(
                "Main and secondary regions must share one positive-length orthogonal boundary.");
        }

        private static ConnectionInfo CreateHorizontalConnection(
            AxisAlignedRectangle main,
            AxisAlignedRectangle secondary,
            double y,
            RoomSide secondaryConnectionSide,
            RoomSide secondaryFarSide)
        {
            double start = Math.Max(main.West, secondary.West);
            double end = Math.Min(main.East, secondary.East);
            if (end - start <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentException(
                    "The region connection must have positive length.");
            }

            GetProtrusion(
                main.West,
                main.East,
                secondary.West,
                secondary.East,
                RoomSide.West,
                RoomSide.East,
                out RoomSide? side,
                out double width);
            return new ConnectionInfo(
                new LineSegment3D(
                    new Point3D(start, y, main.Elevation),
                    new Point3D(end, y, main.Elevation)),
                TileLayoutAxis.X,
                TileLayoutAxis.Y,
                secondaryConnectionSide,
                secondaryFarSide,
                side,
                width);
        }

        private static ConnectionInfo CreateVerticalConnection(
            AxisAlignedRectangle main,
            AxisAlignedRectangle secondary,
            double x,
            RoomSide secondaryConnectionSide,
            RoomSide secondaryFarSide)
        {
            double start = Math.Max(main.South, secondary.South);
            double end = Math.Min(main.North, secondary.North);
            if (end - start <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentException(
                    "The region connection must have positive length.");
            }

            GetProtrusion(
                main.South,
                main.North,
                secondary.South,
                secondary.North,
                RoomSide.South,
                RoomSide.North,
                out RoomSide? side,
                out double width);
            return new ConnectionInfo(
                new LineSegment3D(
                    new Point3D(x, start, main.Elevation),
                    new Point3D(x, end, main.Elevation)),
                TileLayoutAxis.Y,
                TileLayoutAxis.X,
                secondaryConnectionSide,
                secondaryFarSide,
                side,
                width);
        }

        private static void GetProtrusion(
            double mainLow,
            double mainHigh,
            double secondaryLow,
            double secondaryHigh,
            RoomSide lowSide,
            RoomSide highSide,
            out RoomSide? side,
            out double width)
        {
            double lowExtension = mainLow - secondaryLow;
            double highExtension = secondaryHigh - mainHigh;
            bool hasLow = lowExtension > GeometryTolerance.Coordinate;
            bool hasHigh = highExtension > GeometryTolerance.Coordinate;
            if (hasLow && hasHigh)
            {
                throw new ArgumentException(
                    "DOR2 supports one explicit protrusion side per main-secondary connection.");
            }

            if (hasLow)
            {
                side = lowSide;
                width = lowExtension;
            }
            else if (hasHigh)
            {
                side = highSide;
                width = highExtension;
            }
            else
            {
                side = null;
                width = 0.0;
            }
        }

        private static void ValidateRectangleInsideRoom(
            AxisAlignedOrthogonalPolygon room,
            AxisAlignedRectangle rectangle)
        {
            if (!GeometryTolerance.NearlyEqual(room.Elevation, rectangle.Elevation))
            {
                throw new ArgumentException(
                    "The layout region must use the room elevation.");
            }

            double expected = rectangle.Width * rectangle.Height;
            double actual = OrthogonalFootprintBuilder.CalculateIntersectionArea(
                room,
                rectangle);
            double areaTolerance = GeometryTolerance.Coordinate
                * Math.Max(rectangle.Width, rectangle.Height)
                * room.Vertices.Count;
            if (Math.Abs(expected - actual) > areaTolerance)
            {
                throw new ArgumentException(
                    "The layout region must lie completely inside the room.");
            }
        }

        private static void ValidateSameRectangle(
            AxisAlignedRectangle first,
            AxisAlignedRectangle second,
            string message)
        {
            if (!GeometryTolerance.NearlyEqual(first.West, second.West)
                || !GeometryTolerance.NearlyEqual(first.East, second.East)
                || !GeometryTolerance.NearlyEqual(first.South, second.South)
                || !GeometryTolerance.NearlyEqual(first.North, second.North)
                || !GeometryTolerance.NearlyEqual(
                    first.Elevation,
                    second.Elevation))
            {
                throw new ArgumentException(message);
            }
        }

        private static double PolygonArea(AxisAlignedOrthogonalPolygon room)
        {
            double originX = room.Vertices[0].X;
            double originY = room.Vertices[0].Y;
            double twice = 0.0;
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D first = room.Vertices[index];
                Point3D second = room.Vertices[
                    (index + 1) % room.Vertices.Count];
                twice += ((first.X - originX) * (second.Y - originY))
                    - ((second.X - originX) * (first.Y - originY));
            }

            return Math.Abs(twice) / 2.0;
        }

        private static double ConnectionLength(LineSegment3D boundary)
        {
            return Math.Abs(boundary.End.X - boundary.Start.X)
                + Math.Abs(boundary.End.Y - boundary.Start.Y);
        }

        private static double GetRectangleSideCoordinate(
            AxisAlignedRectangle rectangle,
            RoomSide side)
        {
            switch (side)
            {
                case RoomSide.West:
                    return rectangle.West;
                case RoomSide.East:
                    return rectangle.East;
                case RoomSide.South:
                    return rectangle.South;
                case RoomSide.North:
                    return rectangle.North;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
            }
        }

        private static RoomSide GetHighSide(TileLayoutAxis axis)
        {
            return axis == TileLayoutAxis.X ? RoomSide.East : RoomSide.North;
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
                case RoomSide.North:
                    return RoomSide.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
            }
        }

        private static void RemoveNearDuplicates(IList<double> values)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                if (GeometryTolerance.NearlyEqual(values[index], values[index - 1]))
                {
                    values.RemoveAt(index);
                }
            }
        }

        private struct EntranceVisibility
        {
            public EntranceVisibility(bool isVisualZone, bool isVisualBlind)
            {
                IsVisualZone = isVisualZone;
                IsVisualBlind = isVisualBlind;
            }

            public bool IsVisualZone { get; }

            public bool IsVisualBlind { get; }

            public static EntranceVisibility None =>
                new EntranceVisibility(false, false);
        }

        private sealed class PhaseOffset
        {
            private readonly List<GridPhaseSource> sources =
                new List<GridPhaseSource>();

            public PhaseOffset(
                double value,
                int priority,
                string reason,
                GridPhaseSource source)
            {
                Value = value;
                Priority = priority;
                Reason = reason;
                AddSource(source);
            }

            public double Value { get; }

            public int Priority { get; set; }

            public string Reason { get; set; }

            public IReadOnlyList<GridPhaseSource> Sources => sources;

            public bool HasTargetCornerAnchor => sources.Any(source =>
                source.IsTargetCornerAnchor);

            public IEnumerable<string> TargetCornerIds => sources
                .Where(source => source.IsTargetCornerAnchor)
                .Select(source => source.CornerId)
                .Where(value => !string.IsNullOrWhiteSpace(value));

            public void AddSource(GridPhaseSource source)
            {
                if (source == null
                    || sources.Any(existing =>
                        existing.Axis == source.Axis
                        && existing.Kind == source.Kind
                        && string.Equals(
                            existing.CornerId,
                            source.CornerId,
                            StringComparison.Ordinal)))
                {
                    return;
                }

                sources.Add(source);
            }

            public static int Compare(PhaseOffset first, PhaseOffset second)
            {
                int priority = first.Priority.CompareTo(second.Priority);
                return priority != 0
                    ? priority
                    : first.Value.CompareTo(second.Value);
            }
        }

        private struct PhasePair
        {
            public PhasePair(PhaseOffset x, PhaseOffset y)
            {
                X = x;
                Y = y;
            }

            public PhaseOffset X { get; }

            public PhaseOffset Y { get; }

            public int SelectionPriority
            {
                get
                {
                    bool sameTarget = X.TargetCornerIds.Intersect(
                        Y.TargetCornerIds,
                        StringComparer.Ordinal).Any();
                    if (sameTarget)
                    {
                        return 0;
                    }

                    if (X.HasTargetCornerAnchor
                        || Y.HasTargetCornerAnchor)
                    {
                        return 1;
                    }

                    return 10 + X.Priority + Y.Priority;
                }
            }

            public bool HasDoubleTargetAnchor => X.TargetCornerIds.Intersect(
                Y.TargetCornerIds,
                StringComparer.Ordinal).Any();

            public bool HasAnyTargetAnchor => X.HasTargetCornerAnchor
                || Y.HasTargetCornerAnchor;

            public IList<GridPhaseSource> Sources => X.Sources
                .Concat(Y.Sources)
                .ToList();

            public static int Compare(PhasePair first, PhasePair second)
            {
                int priority = first.SelectionPriority.CompareTo(
                    second.SelectionPriority);
                if (priority != 0)
                {
                    return priority;
                }

                int x = first.X.Value.CompareTo(second.X.Value);
                return x != 0 ? x : first.Y.Value.CompareTo(second.Y.Value);
            }
        }

        private struct PhaseKey
        {
            public PhaseKey(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }

            public bool NearlyEquals(PhaseKey other)
            {
                return GeometryTolerance.NearlyEqual(X, other.X)
                    && GeometryTolerance.NearlyEqual(Y, other.Y);
            }
        }

        private struct ConnectionInfo
        {
            public ConnectionInfo(
                LineSegment3D boundary,
                TileLayoutAxis parallelAxis,
                TileLayoutAxis perpendicularAxis,
                RoomSide secondaryConnectionSide,
                RoomSide secondaryFarSide,
                RoomSide? protrusionSide,
                double protrusionWidth)
            {
                Boundary = boundary;
                ParallelAxis = parallelAxis;
                PerpendicularAxis = perpendicularAxis;
                SecondaryConnectionSide = secondaryConnectionSide;
                SecondaryFarSide = secondaryFarSide;
                ProtrusionSide = protrusionSide;
                ProtrusionWidth = protrusionWidth;
            }

            public LineSegment3D Boundary { get; }

            public TileLayoutAxis ParallelAxis { get; }

            public TileLayoutAxis PerpendicularAxis { get; }

            public RoomSide SecondaryConnectionSide { get; }

            public RoomSide SecondaryFarSide { get; }

            public RoomSide? ProtrusionSide { get; }

            public double ProtrusionWidth { get; }
        }

        private struct NormalizedLine
        {
            public NormalizedLine(
                bool vertical,
                double fixedCoordinate,
                double start,
                double end,
                double elevation)
            {
                Vertical = vertical;
                Fixed = fixedCoordinate;
                Start = start;
                End = end;
                Elevation = elevation;
            }

            public bool Vertical { get; }

            public double Fixed { get; }

            public double Start { get; }

            public double End { get; }

            public double Elevation { get; }

            public static int Compare(NormalizedLine first, NormalizedLine second)
            {
                int orientation = second.Vertical.CompareTo(first.Vertical);
                if (orientation != 0)
                {
                    return orientation;
                }

                int fixedResult = first.Fixed.CompareTo(second.Fixed);
                return fixedResult != 0
                    ? fixedResult
                    : first.Start.CompareTo(second.Start);
            }
        }
    }
}
