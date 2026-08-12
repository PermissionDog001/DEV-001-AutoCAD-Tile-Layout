using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class EngineeringOrthogonalDecisionCalculator
    {
        public static EngineeringOrthogonalDecisionResult Calculate(EngineeringOrthogonalDecisionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // The source boundary is authoritative for the finished-face
            // calculation.  This keeps the order explicit: source boundary
            // -> inward finished face -> all door/region/layout calculations.
            // A caller may already be previewing the same finished face in
            // request.Room; recomputing it from OriginalRoom is deterministic
            // and avoids ever offsetting an offset face a second time.
            AxisAlignedOrthogonalPolygon calculationRoom = request.Room;
            if (request.PlasterThicknessMm > GeometryTolerance.Coordinate)
            {
                OrthogonalRoomOffsetResult offset =
                    OrthogonalRoomOffsetter.Offset(
                        request.OriginalRoom,
                        request.PlasterThicknessMm);
                if (!offset.IsValid)
                {
                    return FinishedFaceFailureResult(offset.ErrorMessage);
                }

                calculationRoom = offset.Room;
            }

            var requirements = new List<DecisionRequirement>();
            RoomDecision roomDecision = request.RoomDecision;
            if (roomDecision == null || roomDecision.ControlDoor == null)
                requirements.Add(RoomRequirement(DecisionRequirementCode.RoomControlDoor, "A control door must be selected from the room boundary before door-controlled candidates can be calculated.", "Select the controlling door opening on the drawing."));
            if (roomDecision == null || roomDecision.ControlRegion == null)
                requirements.Add(RoomRequirement(DecisionRequirementCode.RoomControlRegion, "A control region is required; the calculator must not guess which wing a door controls.", "Select the main control region on the drawing."));
            if (roomDecision == null || !roomDecision.LayoutIntent.HasValue)
                requirements.Add(RoomRequirement(DecisionRequirementCode.RoomLayoutIntent, "The room has no declared layout intent, so whole-room and main-secondary layouts cannot be silently mixed.", "Choose whole-room continuity or an explicit main-secondary layout.", "WholeRoomSinglePhase", "MainSecondary"));
            else if (roomDecision.LayoutIntent.Value == RoomLayoutIntent.MainSecondary)
            {
                if (roomDecision.MainSecondary == null)
                    requirements.Add(RoomRequirement(DecisionRequirementCode.RoomMainSecondaryDefinition, "A main-secondary layout requires explicit regions; no concave corner or connection is inferred automatically.", "Select the main region and the secondary region on the drawing."));
                else if (roomDecision.SelectedConnectionEdge == null)
                    requirements.Add(RoomRequirement(DecisionRequirementCode.RoomConnectionEdge, "A main-secondary layout requires the intended shared connection edge.", "Select the shared connection edge on the drawing."));
            }
            if (requirements.Count > 0)
                return new EngineeringOrthogonalDecisionResult(null, new List<EvaluatedLayoutCandidate>(), requirements, null);
            if (roomDecision.LayoutIntent.Value == RoomLayoutIntent.Unsupported)
                return TerminalResult(LayoutCandidateState.CapabilityUnsupported, DecisionRequirementCode.CapabilityUnsupported, "The selected room-layout intent is outside the frozen DOR3 capability boundary.");

            EngineeringOrthogonalLayoutResult raw;
            try
            {
                raw = EngineeringOrthogonalLayoutCalculator.Calculate(calculationRoom,
                    new EngineeringOrthogonalLayoutParameters(request.TileWidth, request.TileHeight,
                        roomDecision.ControlRegion, roomDecision.ControlDoor,
                        roomDecision.LayoutIntent.Value == RoomLayoutIntent.MainSecondary ? roomDecision.MainSecondary : null,
                        roomDecision.ConfirmedGridPhases.ToList(), request.Policy,
                        request.PreferWallCornerAlignment,
                        request.GroutWidthMm,
                        request.OriginalRoom,
                        request.PlasterThicknessMm));
            }
            catch (NotSupportedException error)
            {
                return TerminalResult(LayoutCandidateState.CapabilityUnsupported, DecisionRequirementCode.CapabilityUnsupported, error.Message);
            }
            catch (ArgumentException error)
            {
                return TerminalResult(LayoutCandidateState.InputUntrusted, DecisionRequirementCode.InputUntrusted, error.Message);
            }
            if (roomDecision.LayoutIntent.Value == RoomLayoutIntent.MainSecondary && !MatchesSelectedConnectionEdge(raw, roomDecision.SelectedConnectionEdge.Value))
                return TerminalResult(LayoutCandidateState.InputUntrusted, DecisionRequirementCode.InputUntrusted, "The selected connection edge does not match the validated main-secondary connection.");

            var candidates = new List<EvaluatedLayoutCandidate>();
            var policyAffected = new List<string>();
            for (int index = 0; index < raw.Candidates.Count; index++)
            {
                LayoutCandidate candidate = raw.Candidates[index];
                if (candidate.IsRejected)
                    candidates.Add(new EvaluatedLayoutCandidate(candidate, LayoutCandidateState.Eliminated, "The candidate violates a hard project rule or is dominated by another retained candidate.", index + 1));
                else if (candidate.RequiresProjectPolicy)
                {
                    policyAffected.Add(candidate.Id);
                    candidates.Add(new EvaluatedLayoutCandidate(candidate, LayoutCandidateState.RequiresProjectPolicy, "The candidate contains a boundary cut below the recommended minimum, but the project absolute minimum has not been set.", index + 1));
                }
                else if (candidate.RequiresUserReview)
                    candidates.Add(new EvaluatedLayoutCandidate(candidate, LayoutCandidateState.RequiresUserDecision, candidate.Diagnostics.Any(diagnostic => diagnostic.Code == CandidateDiagnosticCode.SmallBoundaryCutWithoutOppositeFullOrSeam) ? "The candidate keeps a recommended-to-half-tile saving cut, but its opposite boundary is neither axis-full nor an accurate wall-corner seam; compare it as a project-review exception rather than a satisfied-rule candidate." : "Every boundary cut meets the project absolute minimum, but at least one is below the recommended minimum and requires accountable review.", index + 1));
                else
                    candidates.Add(new EvaluatedLayoutCandidate(candidate, LayoutCandidateState.AutomaticUsable, "The candidate satisfies the frozen DOR2 rules.", index + 1));
            }

            bool hasDoorControlledPatternFacts = candidates.Any(
                candidate => candidate.HasRawCandidate
                    && candidate.Candidate.PhaseSources.Any(source =>
                        source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern
                        || source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution));
            if (request.PreferWallCornerAlignment
                || hasDoorControlledPatternFacts)
            {
                // Keep the product order lexicographic: hard state and
                // entrance-visible cuts are always first.  When the optional
                // corner preference is enabled, an accurate corner seam is
                // then compared before the G1 boundary pattern.  This makes
                // the checkbox observable: a no-hit pattern cannot outrank a
                // same-state candidate that accurately aligns a target wall
                // corner.  No aesthetic total score is created.
                TileLayoutAxis doorNormalAxis =
                    request.RoomDecision.ControlDoor.Wall == RoomSide.West
                        || request.RoomDecision.ControlDoor.Wall
                            == RoomSide.East
                        ? TileLayoutAxis.X
                        : TileLayoutAxis.Y;
                candidates = candidates
                    .OrderBy(candidate => StateRecommendationOrder(candidate.State))
                    .ThenBy(candidate => EntranceNarrowCount(candidate))
                    .ThenByDescending(candidate => request
                        .PreferWallCornerAlignment
                        ? ExactGridIntersectionCornerCount(candidate)
                        : 0)
                    .ThenByDescending(candidate => request
                        .PreferWallCornerAlignment
                        ? ExactSeamCornerCount(candidate)
                        : 0)
                    .ThenByDescending(candidate => request
                        .PreferWallCornerAlignment
                        ? SafeDoubleCornerCount(candidate)
                        : 0)
                    .ThenByDescending(candidate => request
                        .PreferWallCornerAlignment
                        ? SafeSingleCornerCount(candidate)
                        : 0)
                    .ThenByDescending(candidate =>
                        DoorControlledPatternPreference(
                            candidate,
                            doorNormalAxis,
                            calculationRoom,
                            request.RoomDecision.ControlDoor,
                            doorNormalAxis == TileLayoutAxis.X
                                ? request.TileWidth
                                : request.TileHeight))
                    .ThenByDescending(candidate =>
                        DoorControlledPatternPreference(
                            candidate,
                            doorNormalAxis == TileLayoutAxis.X
                                ? TileLayoutAxis.Y
                                : TileLayoutAxis.X,
                            calculationRoom,
                            request.RoomDecision.ControlDoor,
                            doorNormalAxis == TileLayoutAxis.X
                                ? request.TileHeight
                                : request.TileWidth))
                    .ThenByDescending(candidate =>
                        DoorControlledRedistributionAxisCount(candidate))
                    .ThenBy(candidate => EntranceBlindNarrowCount(candidate))
                    .ThenBy(candidate => BoundaryReviewCount(candidate))
                    .ThenBy(candidate => candidate.OriginalIndex)
                    .ToList();
            }
            bool allowsVisualConfirmation = request.Policy != null
                && request.Policy.AllowsVisualConfirmation;
            bool hasCandidateThatDoesNotNeedMissingPolicy = candidates.Any(
                candidate => candidate.State == LayoutCandidateState.AutomaticUsable
                    || candidate.State == LayoutCandidateState.RequiresUserDecision);
            if (policyAffected.Count > 0
                && !hasCandidateThatDoesNotNeedMissingPolicy
                && !allowsVisualConfirmation)
                requirements.Add(new DecisionRequirement(DecisionRequirementCode.ProjectSecondAbsoluteMinimum, DecisionRequirementLevel.ProjectPolicy, "At least one candidate contains a boundary cut below the recommended minimum and the project absolute minimum is unset.", "Set the project absolute minimum once in millimetres or as a tile-size ratio; it will be compared with every applicable boundary cut.", new List<string> { "SetPolicy", "KeepUnset" }, policyAffected));

            List<EvaluatedLayoutCandidate> retained = candidates.Where(candidate =>
                candidate.State == LayoutCandidateState.AutomaticUsable
                || candidate.State == LayoutCandidateState.RequiresUserDecision
                || allowsVisualConfirmation
                    && candidate.State == LayoutCandidateState.RequiresProjectPolicy)
                .ToList();
            DecisionRecord record = request.CandidateDecision == null ? null : request.CandidateDecision.Record;
            bool selectedRecordValid = record != null && retained.Any(candidate => candidate.Id == record.CandidateId) && (request.Policy == null || record.PolicyVersion == request.Policy.Version);
            if (retained.Count > 1 && !selectedRecordValid)
                requirements.Add(new DecisionRequirement(DecisionRequirementCode.CandidateSelection, DecisionRequirementLevel.CandidateSelection, "Multiple retained candidates remain after the frozen rules; DOR3 does not calculate an aesthetic score.", "Compare the retained candidate previews and select one.", retained.Select(candidate => candidate.Id).ToList(), retained.Select(candidate => candidate.Id).ToList()));
            else if (retained.Any(candidate => candidate.State == LayoutCandidateState.RequiresUserDecision) && !selectedRecordValid)
                requirements.Add(new DecisionRequirement(DecisionRequirementCode.CandidateExceptionAcceptance, DecisionRequirementLevel.CandidateSelection, "A retained candidate meets the project absolute minimum but contains cuts below the recommended minimum.", request.Mode == LayoutDecisionMode.Research ? "Review the located narrow tiles, select a candidate, and record the reason." : "Review the located narrow tiles and record the accountable decision; this is not an automatic compliance result.", retained.Select(candidate => candidate.Id).ToList(), retained.Select(candidate => candidate.Id).ToList()));
            return new EngineeringOrthogonalDecisionResult(raw, candidates, requirements, selectedRecordValid ? record : null);
        }

        private static EngineeringOrthogonalDecisionResult TerminalResult(LayoutCandidateState state, DecisionRequirementCode code, string reason)
        {
            return new EngineeringOrthogonalDecisionResult(null, new List<EvaluatedLayoutCandidate> { new EvaluatedLayoutCandidate(null, state, reason) }, new List<DecisionRequirement> { new DecisionRequirement(code, DecisionRequirementLevel.RoomSemantics, reason, "Correct the trusted input or use a supported room capability.") }, null);
        }

        private static EngineeringOrthogonalDecisionResult FinishedFaceFailureResult(
            string reason)
        {
            return new EngineeringOrthogonalDecisionResult(
                null,
                new List<EvaluatedLayoutCandidate>(),
                new List<DecisionRequirement>
                {
                    new DecisionRequirement(
                        DecisionRequirementCode.InputUntrusted,
                        DecisionRequirementLevel.RoomSemantics,
                        reason,
                        "Correct the plaster thickness or source room boundary before calculating a layout.")
                },
                null);
        }

        private static int StateRecommendationOrder(LayoutCandidateState state)
        {
            switch (state)
            {
                case LayoutCandidateState.AutomaticUsable:
                    return 0;
                case LayoutCandidateState.RequiresUserDecision:
                    return 1;
                case LayoutCandidateState.RequiresProjectPolicy:
                    return 2;
                default:
                    return 3;
            }
        }

        private static long EntranceNarrowCount(EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics
                    .EntranceVisualBelowRecommendedBoundaryTileCount
                : long.MaxValue;
        }

        private static int DoorControlledRedistributionAxisCount(
            EvaluatedLayoutCandidate candidate)
        {
            if (candidate == null || !candidate.HasRawCandidate)
            {
                return int.MinValue;
            }

            var axes = new HashSet<TileLayoutAxis>();
            foreach (BoundaryBandPlan plan in candidate.Candidate.AxisPlans)
            {
                if (plan.UsesRedistribution)
                {
                    axes.Add(plan.Axis);
                }
            }

            // A generated phase can share its geometry with an existing G1
            // candidate.  In that case the plan may remain a natural phase,
            // while the merged source still records the deterministic G1
            // boundary allocation.  Count that source as well so the
            // recommendation does not lose the G1 rule during de-duplication.
            foreach (GridPhaseSource source in candidate.Candidate.PhaseSources)
            {
                if (source.Kind == GridPhaseSourceKind
                    .DoorControlledBoundaryRedistribution
                    || source.Kind == GridPhaseSourceKind
                        .DoorControlledBoundaryPattern)
                {
                    axes.Add(source.Axis);
                }
            }

            return axes.Count;
        }

        private static int DoorControlledPatternAxisCount(
            EvaluatedLayoutCandidate candidate,
            TileLayoutAxis axis)
        {
            if (candidate == null || !candidate.HasRawCandidate)
            {
                return int.MinValue;
            }

            BoundaryBandPlan plan = candidate.Candidate.AxisPlans
                .FirstOrDefault(value => value.Axis == axis);
            bool hasPatternSource = candidate.Candidate.PhaseSources
                .Any(source => source.Axis == axis
                    && (source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern
                        || source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution));
            return plan != null
                && (plan.UsesRedistribution || hasPatternSource)
                ? 1
                : 0;
        }

        private static int DoorControlledPatternPreference(
            EvaluatedLayoutCandidate candidate,
            TileLayoutAxis axis,
            AxisAlignedOrthogonalPolygon room,
            DoorOpening door,
            double tileSize)
        {
            if (candidate == null
                || !candidate.HasRawCandidate
                || room == null
                || door == null)
            {
                return int.MinValue;
            }

            BoundaryBandPlan plan = candidate.Candidate.AxisPlans
                .FirstOrDefault(value => value.Axis == axis);
            bool hasPatternSource = candidate.Candidate.PhaseSources
                .Any(source => source.Axis == axis
                    && (source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern
                        || source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution));
            if (plan == null || (!plan.UsesRedistribution && !hasPatternSource))
            {
                return 0;
            }

            RoomSide preferredSide = GetDoorControlledPreferredSide(
                room,
                axis,
                door);
            AxisBoundaryBand preferred = plan.GetBoundary(preferredSide);
            AxisBoundaryBand opposite = plan.GetBoundary(
                Opposite(preferredSide));
            bool preferredPattern = preferred.Kind == BoundaryBandKind.HalfTile
                || preferred.Kind == BoundaryBandKind.FullTile;
            bool oppositePattern = opposite.Kind == BoundaryBandKind.HalfTile
                || opposite.Kind == BoundaryBandKind.FullTile;
            if (preferredPattern)
            {
                return 2;
            }

            return oppositePattern ? 1 : 0;
        }

        private static RoomSide GetDoorControlledPreferredSide(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutAxis axis,
            DoorOpening door)
        {
            bool doorNormalIsX = door.Wall == RoomSide.West
                || door.Wall == RoomSide.East;
            if ((axis == TileLayoutAxis.X) == doorNormalIsX)
            {
                return Opposite(door.Wall);
            }

            double minimum = axis == TileLayoutAxis.X
                ? room.West
                : room.South;
            double maximum = axis == TileLayoutAxis.X
                ? room.East
                : room.North;
            double center = Math.Max(
                minimum,
                Math.Min(maximum, door.Center));
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

        private static int SafeDoubleCornerCount(EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics
                    .SafeDoubleWallCornerAlignmentCount
                : int.MinValue;
        }

        private static int ExactGridIntersectionCornerCount(
            EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics.ExactGridIntersectionCornerCount
                : int.MinValue;
        }

        private static int ExactSeamCornerCount(
            EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics.ExactSeamAlignedCornerCount
                : int.MinValue;
        }

        private static int SafeSingleCornerCount(EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics
                    .SafeSingleWallCornerAlignmentCount
                : int.MinValue;
        }

        private static long EntranceBlindNarrowCount(EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics
                    .EntranceBlindBelowRecommendedBoundaryTileCount
                : long.MaxValue;
        }

        private static long BoundaryReviewCount(EvaluatedLayoutCandidate candidate)
        {
            return candidate != null && candidate.HasRawCandidate
                ? candidate.Candidate.Metrics
                    .BelowDefaultMinimumBoundaryTileCount
                : long.MaxValue;
        }

        private static DecisionRequirement RoomRequirement(DecisionRequirementCode code, string reason, string requiredInput, params string[] options)
        {
            return new DecisionRequirement(code, DecisionRequirementLevel.RoomSemantics, reason, requiredInput, new List<string>(options));
        }

        private static bool MatchesSelectedConnectionEdge(EngineeringOrthogonalLayoutResult result, LineSegment3D selected)
        {
            foreach (LayoutCandidate candidate in result.Candidates)
                foreach (RegionConnectionPlan connection in candidate.Structure.Connections)
                    if (SameSegment(connection.Boundary, selected)) return true;
            return false;
        }

        private static bool SameSegment(LineSegment3D first, LineSegment3D second)
        {
            return (SamePoint(first.Start, second.Start) && SamePoint(first.End, second.End)) || (SamePoint(first.Start, second.End) && SamePoint(first.End, second.Start));
        }

        private static bool SamePoint(Point3D first, Point3D second)
        {
            return GeometryTolerance.NearlyEqual(first.X, second.X) && GeometryTolerance.NearlyEqual(first.Y, second.Y) && GeometryTolerance.NearlyEqual(first.Z, second.Z);
        }
    }
}
