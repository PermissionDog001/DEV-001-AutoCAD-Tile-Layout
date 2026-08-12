using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class LayoutDrawingPlanBuilder
    {
        public static LayoutDrawingPlan Build(
            EngineeringOrthogonalDecisionResult result,
            string candidateId)
        {
            return Build(result, candidateId, false);
        }

        public static LayoutDrawingPlan Build(
            EngineeringOrthogonalDecisionResult result,
            string candidateId,
            bool includeDimensions)
        {
            return Build(
                result,
                candidateId,
                includeDimensions,
                LayoutDrawingDimensionPlacement.OutsideRoom,
                LayoutDrawingColorSettings.Default,
                true);
        }

        public static LayoutDrawingPlan Build(
            EngineeringOrthogonalDecisionResult result,
            string candidateId,
            bool includeDimensions,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            LayoutDrawingColorSettings colorSettings)
        {
            return Build(
                result,
                candidateId,
                includeDimensions,
                dimensionPlacement,
                colorSettings,
                true);
        }

        public static LayoutDrawingPlan Build(
            EngineeringOrthogonalDecisionResult result,
            string candidateId,
            bool includeDimensions,
            LayoutDrawingDimensionPlacement dimensionPlacement,
            LayoutDrawingColorSettings colorSettings,
            bool includeRoomFeatureDimensions)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (result.RawResult == null)
            {
                throw new InvalidOperationException(
                    "A drawing plan requires a real DOR3 layout result.");
            }

            if (string.IsNullOrWhiteSpace(candidateId))
            {
                throw new ArgumentException(
                    "A candidate identifier is required.",
                    nameof(candidateId));
            }

            EvaluatedLayoutCandidate evaluated = result.Candidates.SingleOrDefault(
                item => string.Equals(
                    item.Id,
                    candidateId,
                    StringComparison.Ordinal));
            if (evaluated == null || !evaluated.HasRawCandidate)
            {
                throw new ArgumentException(
                    "The requested candidate does not exist in the DOR3 result.",
                    nameof(candidateId));
            }

            if (evaluated.State == LayoutCandidateState.Eliminated
                || evaluated.State == LayoutCandidateState.InputUntrusted
                || evaluated.State == LayoutCandidateState.CapabilityUnsupported)
            {
                throw new InvalidOperationException(
                    "An unavailable candidate cannot produce a drawing plan.");
            }

            LayoutCandidate candidate = evaluated.Candidate;
            AxisAlignedOrthogonalPolygon room = result.RawResult.Room;
            AxisAlignedOrthogonalPolygon sourceRoom =
                result.RawResult.SourceRoom ?? room;
            var roomOutline = new List<Point3D>(room.Vertices);
            var divisionLines = BuildDivisionLines(
                candidate,
                room,
                result.RawResult.Parameters.GroutWidthMm,
                result.RawResult.Parameters.PlasterThicknessMm
                    > GeometryTolerance.Coordinate);

            var tiles = new List<LayoutDrawingTile>();
            var assessments = candidate.TileAssessments.ToDictionary(
                assessment => assessment.TileIndex);
            for (int index = 0; index < candidate.Tiles.Count; index++)
            {
                TileFootprint tile = candidate.Tiles[index];
                TileFootprintAssessment assessment;
                assessments.TryGetValue(index, out assessment);
                var measurements = new List<LayoutDrawingCutMeasurement>();
                if (assessment != null)
                {
                    foreach (BoundaryCutMeasurement measurement
                        in assessment.Measurements)
                    {
                        measurements.Add(new LayoutDrawingCutMeasurement(
                            measurement.Axis,
                            measurement.ActualValue,
                            measurement.RecommendedMinimum,
                            measurement.ProjectAbsoluteMinimum,
                            measurement.Status));
                    }
                }
                tiles.Add(new LayoutDrawingTile(
                    StableId("tile", index),
                    new List<Point3D>(tile.Outline),
                    tile.Classification,
                    tile.IsFullTile,
                    tile.IsContinuousIrregular,
                    tile.NominalWidth,
                    tile.NominalHeight,
                    new List<RoomSide>(tile.BoundarySides),
                    assessment == null
                        ? tile.IsFullTile
                            ? ProjectCutStatus.NotApplicableFullTile
                            : ProjectCutStatus.InteriorNonFullDiagnostic
                        : assessment.Status,
                    assessment == null ? string.Empty : assessment.Reason,
                    measurements,
                    assessment != null && assessment.IsEntranceVisualZone,
                    assessment != null && assessment.IsEntranceVisualBlind));
            }

            var regions = new List<LayoutDrawingRegion>();
            for (int index = 0; index < candidate.Structure.Regions.Count; index++)
            {
                LayoutRegionPhase region = candidate.Structure.Regions[index];
                regions.Add(new LayoutDrawingRegion(
                    string.IsNullOrWhiteSpace(region.Id)
                        ? StableId("region", index)
                        : region.Id,
                    region.Role,
                    region.Bounds));
            }

            var connections = new List<LayoutDrawingLine>();
            for (int index = 0;
                index < candidate.Structure.Connections.Count;
                index++)
            {
                connections.Add(new LayoutDrawingLine(
                    StableId("connection", index),
                    candidate.Structure.Connections[index].Boundary,
                    LayoutDrawingLineSemantic.Connection));
            }

            var neutralRegions = new List<LayoutDrawingNeutralRegion>();
            foreach (NeutralOrthogonalRegion region
                in result.RawResult.NeutralRegionPartition.Regions)
            {
                neutralRegions.Add(new LayoutDrawingNeutralRegion(
                    region.Id,
                    region.Bounds,
                    region.Area));
            }

            var neutralConnections = new List<LayoutDrawingLine>();
            for (int index = 0;
                index < result.RawResult.NeutralRegionPartition.Connections.Count;
                index++)
            {
                NeutralRegionConnection connection =
                    result.RawResult.NeutralRegionPartition.Connections[index];
                neutralConnections.Add(new LayoutDrawingLine(
                    StableId("neutral-connection", index),
                    connection.SharedEdge,
                    LayoutDrawingLineSemantic.Connection));
            }

            var wallCorners = new List<LayoutDrawingWallCorner>();
            foreach (WallCornerAssessment corner
                in candidate.WallCornerAssessments)
            {
                wallCorners.Add(new LayoutDrawingWallCorner(
                    corner.Id,
                    corner.Position,
                    corner.GeometryType,
                    corner.IsOptimizationTarget,
                    corner.HasVerticalSeam,
                    corner.HasHorizontalSeam,
                    corner.NearestVerticalSeamDistance,
                    corner.NearestHorizontalSeamDistance,
                    corner.Reason,
                    corner.HasSafeVerticalSeam,
                    corner.HasSafeHorizontalSeam,
                    corner.VerticalAdjacentSpanA,
                    corner.VerticalAdjacentSpanB,
                    corner.HorizontalAdjacentSpanA,
                    corner.HorizontalAdjacentSpanB));
            }
            var dimensions = includeDimensions
                ? LayoutDrawingDimensionBuilder.Build(
                    room,
                    candidate.Tiles,
                    result.RawResult.Parameters.TileWidth,
                    result.RawResult.Parameters.TileHeight,
                    dimensionPlacement,
                    includeRoomFeatureDimensions)
                    .ToList()
                : new List<LayoutDrawingDimension>();
            LayoutDrawingStartPoint startPoint;
            string startPointUnavailableReason;
            LayoutDrawingStartPointBuilder.TryBuild(
                room,
                candidate,
                result.RawResult.Parameters.DoorOpening,
                result.RawResult.Parameters.TileWidth,
                result.RawResult.Parameters.TileHeight,
                out startPoint,
                out startPointUnavailableReason);
            return new LayoutDrawingPlan(
                candidate.Id,
                evaluated.State,
                room.West,
                room.East,
                room.South,
                room.North,
                room.Elevation,
                roomOutline,
                divisionLines,
                tiles,
                regions,
                connections,
                neutralRegions,
                neutralConnections,
                wallCorners,
                sourceRoom.West,
                sourceRoom.East,
                sourceRoom.South,
                sourceRoom.North,
                result.RawResult.Parameters.GroutWidthMm,
                result.RawResult.Parameters.PlasterThicknessMm,
                dimensions,
                dimensionPlacement,
                colorSettings,
                includeRoomFeatureDimensions,
                startPoint,
                startPointUnavailableReason);
        }

        private static List<LayoutDrawingLine> BuildDivisionLines(
            LayoutCandidate candidate,
            AxisAlignedOrthogonalPolygon room,
            double groutWidthMm,
            bool includeFinishedFaceOutline)
        {
            var lines = new List<LayoutDrawingLine>();
            if (includeFinishedFaceOutline)
            {
                for (int index = 0; index < room.Vertices.Count; index++)
                {
                    lines.Add(new LayoutDrawingLine(
                        StableId("finished-face", index),
                        new LineSegment3D(
                            room.Vertices[index],
                            room.Vertices[(index + 1) % room.Vertices.Count]),
                        LayoutDrawingLineSemantic.FinishedFaceOutline));
                }
            }

            if (groutWidthMm <= GeometryTolerance.Coordinate)
            {
                for (int index = 0; index < candidate.DivisionLines.Count; index++)
                {
                    lines.Add(new LayoutDrawingLine(
                        StableId("division", index),
                        candidate.DivisionLines[index],
                        LayoutDrawingLineSemantic.Division));
                }

                return lines;
            }

            double half = groutWidthMm / 2.0;
            int seamIndex = 0;
            foreach (LineSegment3D centerLine in candidate.DivisionLines)
            {
                LineSegment3D first;
                LineSegment3D second;
                if (GeometryTolerance.NearlyEqual(
                        centerLine.Start.X,
                        centerLine.End.X))
                {
                    first = OffsetVertical(centerLine, -half);
                    second = OffsetVertical(centerLine, half);
                }
                else
                {
                    first = OffsetHorizontal(centerLine, -half);
                    second = OffsetHorizontal(centerLine, half);
                }

                lines.Add(new LayoutDrawingLine(
                    StableId("grout-division", seamIndex++),
                    first,
                    LayoutDrawingLineSemantic.GroutBoundary));
                lines.Add(new LayoutDrawingLine(
                    StableId("grout-division", seamIndex++),
                    second,
                    LayoutDrawingLineSemantic.GroutBoundary));
            }

            int wallIndex = 0;
            foreach (LineSegment3D wallLine in MergeCollinearContinuousLines(
                BuildWallGroutBoundaries(
                    room,
                    candidate.Tiles,
                    groutWidthMm),
                groutWidthMm))
            {
                lines.Add(new LayoutDrawingLine(
                    StableId("wall-grout", wallIndex++),
                    wallLine,
                    LayoutDrawingLineSemantic.GroutBoundary));
            }

            return lines;
        }

        private static List<LineSegment3D> MergeCollinearContinuousLines(
            IEnumerable<LineSegment3D> source,
            double maximumGap)
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
                    && GeometryTolerance.NearlyEqual(
                        previous.Fixed,
                        line.Fixed)
                    && GeometryTolerance.NearlyEqual(
                        previous.Elevation,
                        line.Elevation)
                    && line.Start <= previous.End
                        + maximumGap
                        + GeometryTolerance.Coordinate)
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
                            new Point3D(
                                line.Fixed,
                                line.Start,
                                line.Elevation),
                            new Point3D(
                                line.Fixed,
                                line.End,
                                line.Elevation))
                        : new LineSegment3D(
                            new Point3D(
                                line.Start,
                                line.Fixed,
                                line.Elevation),
                            new Point3D(
                                line.End,
                                line.Fixed,
                                line.Elevation)));
            }

            return result;
        }

        private static LineSegment3D OffsetVertical(
            LineSegment3D line,
            double offset)
        {
            return new LineSegment3D(
                new Point3D(
                    line.Start.X + offset,
                    line.Start.Y,
                    line.Start.Z),
                new Point3D(
                    line.End.X + offset,
                    line.End.Y,
                    line.End.Z));
        }

        private static LineSegment3D OffsetHorizontal(
            LineSegment3D line,
            double offset)
        {
            return new LineSegment3D(
                new Point3D(
                    line.Start.X,
                    line.Start.Y + offset,
                    line.Start.Z),
                new Point3D(
                    line.End.X,
                    line.End.Y + offset,
                    line.End.Z));
        }

        private static IEnumerable<LineSegment3D> BuildWallGroutBoundaries(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<TileFootprint> tiles,
            double groutWidthMm)
        {
            double half = groutWidthMm / 2.0;
            for (int roomIndex = 0; roomIndex < room.Vertices.Count; roomIndex++)
            {
                Point3D roomStart = room.Vertices[roomIndex];
                Point3D roomEnd = room.Vertices[
                    (roomIndex + 1) % room.Vertices.Count];
                bool roomVertical = GeometryTolerance.NearlyEqual(
                    roomStart.X,
                    roomEnd.X);
                double expectedFixed = roomVertical
                    ? roomStart.X + (roomEnd.Y > roomStart.Y ? -half : half)
                    : roomStart.Y + (roomEnd.X > roomStart.X ? half : -half);
                double roomLow = roomVertical
                    ? Math.Min(roomStart.Y, roomEnd.Y)
                    : Math.Min(roomStart.X, roomEnd.X);
                double roomHigh = roomVertical
                    ? Math.Max(roomStart.Y, roomEnd.Y)
                    : Math.Max(roomStart.X, roomEnd.X);

                foreach (TileFootprint tile in tiles)
                {
                    for (int tileIndex = 0;
                        tileIndex < tile.Outline.Count;
                        tileIndex++)
                    {
                        Point3D tileStart = tile.Outline[tileIndex];
                        Point3D tileEnd = tile.Outline[
                            (tileIndex + 1) % tile.Outline.Count];
                        bool tileVertical = GeometryTolerance.NearlyEqual(
                            tileStart.X,
                            tileEnd.X);
                        if (tileVertical != roomVertical)
                        {
                            continue;
                        }

                        double fixedCoordinate = tileVertical
                            ? tileStart.X
                            : tileStart.Y;
                        if (!GeometryTolerance.NearlyEqual(
                            fixedCoordinate,
                            expectedFixed))
                        {
                            continue;
                        }

                        double tileLow = tileVertical
                            ? Math.Min(tileStart.Y, tileEnd.Y)
                            : Math.Min(tileStart.X, tileEnd.X);
                        double tileHigh = tileVertical
                            ? Math.Max(tileStart.Y, tileEnd.Y)
                            : Math.Max(tileStart.X, tileEnd.X);
                        double low = Math.Max(roomLow, tileLow);
                        double high = Math.Min(roomHigh, tileHigh);
                        if (high - low <= GeometryTolerance.Coordinate)
                        {
                            continue;
                        }

                        yield return roomVertical
                            ? new LineSegment3D(
                                new Point3D(expectedFixed, low, room.Elevation),
                                new Point3D(expectedFixed, high, room.Elevation))
                            : new LineSegment3D(
                                new Point3D(low, expectedFixed, room.Elevation),
                                new Point3D(high, expectedFixed, room.Elevation));
                    }
                }
            }
        }


        private static string StableId(string prefix, int zeroBasedIndex)
        {
            return prefix + "-" + (zeroBasedIndex + 1).ToString(
                "D4",
                CultureInfo.InvariantCulture);
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

            public static int Compare(
                NormalizedLine first,
                NormalizedLine second)
            {
                int comparison = first.Vertical.CompareTo(second.Vertical);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = first.Fixed.CompareTo(second.Fixed);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = first.Start.CompareTo(second.Start);
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = first.End.CompareTo(second.End);
                if (comparison != 0)
                {
                    return comparison;
                }

                return first.Elevation.CompareTo(second.Elevation);
            }
        }
    }
}
