using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    internal static class WallCornerEvaluator
    {
        public static IList<WallCornerAssessment> Evaluate(
            AxisAlignedOrthogonalPolygon room,
            IEnumerable<LineSegment3D> divisionLines,
            IEnumerable<TileFootprint> tiles = null,
            double tileWidth = 0.0,
            double tileHeight = 0.0,
            double groutWidthMm = 0.0)
        {
            var lines = divisionLines == null
                ? new List<LineSegment3D>()
                : divisionLines.ToList();
            var assessments = new List<WallCornerAssessment>(
                room.Vertices.Count);
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D previous = room.Vertices[
                    (index - 1 + room.Vertices.Count) % room.Vertices.Count];
                Point3D current = room.Vertices[index];
                Point3D next = room.Vertices[
                    (index + 1) % room.Vertices.Count];
                double cross = ((current.X - previous.X)
                        * (next.Y - current.Y))
                    - ((current.Y - previous.Y)
                        * (next.X - current.X));
                WallCornerGeometryType type = cross < 0.0
                    ? WallCornerGeometryType.Reflex270
                    : WallCornerGeometryType.Convex90;
                bool target = type == WallCornerGeometryType.Reflex270;
                bool vertical = lines.Any(line =>
                    IsVertical(line)
                    && ContainsWithPositiveExtension(line, current));
                bool horizontal = lines.Any(line =>
                    IsHorizontal(line)
                    && ContainsWithPositiveExtension(line, current));
                double? nearestVertical = NearestDistance(
                    lines.Where(IsVertical), current);
                double? nearestHorizontal = NearestDistance(
                    lines.Where(IsHorizontal), current);
                IList<double> verticalSpans = vertical
                    ? AdjacentSpans(tiles, current, true, groutWidthMm)
                    : new List<double>();
                IList<double> horizontalSpans = horizontal
                    ? AdjacentSpans(tiles, current, false, groutWidthMm)
                    : new List<double>();
                bool safeVertical = IsSafe(verticalSpans, tileWidth);
                bool safeHorizontal = IsSafe(horizontalSpans, tileHeight);
                string reason = !target
                    ? "The 90-degree room corner is retained for read-only diagnostics and is not an optimization target."
                    : vertical && horizontal
                    ? FormatTargetReason(
                        "Actual clipped vertical and horizontal division lines extend from the target corner into the room.",
                        safeVertical,
                        safeHorizontal,
                        verticalSpans,
                        horizontalSpans)
                    : vertical || horizontal
                    ? FormatTargetReason(
                        "One actual clipped division line extends from the target corner into the room.",
                        safeVertical,
                        safeHorizontal,
                        verticalSpans,
                        horizontalSpans)
                    : "No actual clipped division line extends from the target corner into the room.";
                assessments.Add(new WallCornerAssessment(
                    "corner-" + (index + 1).ToString(
                        "D4",
                        CultureInfo.InvariantCulture),
                    index,
                    current,
                    type,
                    target,
                    vertical,
                    horizontal,
                    nearestVertical,
                    nearestHorizontal,
                    reason,
                    safeVertical,
                    safeHorizontal,
                    GetSpan(verticalSpans, 0),
                    GetSpan(verticalSpans, 1),
                    GetSpan(horizontalSpans, 0),
                    GetSpan(horizontalSpans, 1)));
            }

            return assessments;
        }

        public static bool HasOptimizationTarget(
            AxisAlignedOrthogonalPolygon room)
        {
            return Evaluate(room, null).Any(corner =>
                corner.IsOptimizationTarget);
        }

        private static bool IsVertical(LineSegment3D line)
        {
            return GeometryTolerance.NearlyEqual(line.Start.X, line.End.X)
                && Math.Abs(line.End.Y - line.Start.Y)
                    > GeometryTolerance.Coordinate;
        }

        private static bool IsHorizontal(LineSegment3D line)
        {
            return GeometryTolerance.NearlyEqual(line.Start.Y, line.End.Y)
                && Math.Abs(line.End.X - line.Start.X)
                    > GeometryTolerance.Coordinate;
        }

        private static bool ContainsWithPositiveExtension(
            LineSegment3D line,
            Point3D corner)
        {
            if (IsVertical(line))
            {
                if (!GeometryTolerance.NearlyEqual(line.Start.X, corner.X)
                    || corner.Y < Math.Min(line.Start.Y, line.End.Y)
                        - GeometryTolerance.Coordinate
                    || corner.Y > Math.Max(line.Start.Y, line.End.Y)
                        + GeometryTolerance.Coordinate)
                {
                    return false;
                }

                return Math.Abs(line.Start.Y - corner.Y)
                        > GeometryTolerance.Coordinate
                    || Math.Abs(line.End.Y - corner.Y)
                        > GeometryTolerance.Coordinate;
            }

            if (!IsHorizontal(line)
                || !GeometryTolerance.NearlyEqual(line.Start.Y, corner.Y)
                || corner.X < Math.Min(line.Start.X, line.End.X)
                    - GeometryTolerance.Coordinate
                || corner.X > Math.Max(line.Start.X, line.End.X)
                    + GeometryTolerance.Coordinate)
            {
                return false;
            }

            return Math.Abs(line.Start.X - corner.X)
                    > GeometryTolerance.Coordinate
                || Math.Abs(line.End.X - corner.X)
                    > GeometryTolerance.Coordinate;
        }

        private static double? NearestDistance(
            IEnumerable<LineSegment3D> lines,
            Point3D point)
        {
            double nearest = double.PositiveInfinity;
            foreach (LineSegment3D line in lines)
            {
                double x = Math.Max(
                    Math.Min(line.Start.X, line.End.X),
                    Math.Min(point.X, Math.Max(line.Start.X, line.End.X)));
                double y = Math.Max(
                    Math.Min(line.Start.Y, line.End.Y),
                    Math.Min(point.Y, Math.Max(line.Start.Y, line.End.Y)));
                double deltaX = point.X - x;
                double deltaY = point.Y - y;
                nearest = Math.Min(
                    nearest,
                    Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)));
            }

            return double.IsPositiveInfinity(nearest)
                ? (double?)null
                : nearest;
        }

        private static IList<double> AdjacentSpans(
            IEnumerable<TileFootprint> tiles,
            Point3D corner,
            bool verticalSeam,
            double groutWidthMm)
        {
            var lowSideSpans = new List<double>();
            var highSideSpans = new List<double>();
            if (tiles == null)
            {
                return new List<double>();
            }

            foreach (TileFootprint tile in tiles)
            {
                if (tile == null || tile.Outline == null
                    || tile.Outline.Count < 2)
                {
                    continue;
                }

                IReadOnlyList<Point3D> outline = ExpandForGrout(
                    tile.Outline,
                    groutWidthMm);
                bool touchesCorner = false;
                for (int index = 0; index < outline.Count; index++)
                {
                    Point3D start = outline[index];
                    Point3D end = outline[(index + 1) % outline.Count];
                    bool edgeVertical = GeometryTolerance.NearlyEqual(
                        start.X,
                        end.X);
                    if (edgeVertical != verticalSeam)
                    {
                        continue;
                    }

                    double fixedCoordinate = verticalSeam ? start.X : start.Y;
                    double cornerCoordinate = verticalSeam ? corner.X : corner.Y;
                    double low = verticalSeam
                        ? Math.Min(start.Y, end.Y)
                        : Math.Min(start.X, end.X);
                    double high = verticalSeam
                        ? Math.Max(start.Y, end.Y)
                        : Math.Max(start.X, end.X);
                    double cornerAlong = verticalSeam ? corner.Y : corner.X;
                    double endpointDistance = Math.Min(
                        Math.Abs(low - cornerAlong),
                        Math.Abs(high - cornerAlong));
                    if (GeometryTolerance.NearlyEqual(
                            fixedCoordinate,
                            cornerCoordinate)
                        && endpointDistance <= GeometryTolerance.Coordinate
                        && high - low > GeometryTolerance.Coordinate)
                    {
                        touchesCorner = true;
                        break;
                    }
                }

                if (!touchesCorner)
                {
                    continue;
                }

                double lowCoordinate = verticalSeam
                    ? outline.Min(point => point.X)
                    : outline.Min(point => point.Y);
                double highCoordinate = verticalSeam
                    ? outline.Max(point => point.X)
                    : outline.Max(point => point.Y);
                double cornerCoordinateForSpan = verticalSeam
                    ? corner.X
                    : corner.Y;
                double lowSpan = cornerCoordinateForSpan - lowCoordinate;
                double highSpan = highCoordinate - cornerCoordinateForSpan;
                if (lowSpan > GeometryTolerance.Coordinate
                    && highSpan <= GeometryTolerance.Coordinate)
                {
                    lowSideSpans.Add(lowSpan);
                }
                else if (highSpan > GeometryTolerance.Coordinate
                    && lowSpan <= GeometryTolerance.Coordinate)
                {
                    highSideSpans.Add(highSpan);
                }
            }

            var spans = new List<double>();
            if (lowSideSpans.Count > 0)
            {
                spans.Add(lowSideSpans.Min());
            }

            if (highSideSpans.Count > 0)
            {
                spans.Add(highSideSpans.Min());
            }

            return spans;
        }

        private static IReadOnlyList<Point3D> ExpandForGrout(
            IReadOnlyList<Point3D> outline,
            double groutWidthMm)
        {
            if (groutWidthMm <= GeometryTolerance.Coordinate
                || outline == null
                || outline.Count < 4)
            {
                return outline;
            }

            double half = groutWidthMm / 2.0;
            var shifted = new List<ShiftedEdge>(outline.Count);
            for (int index = 0; index < outline.Count; index++)
            {
                Point3D start = outline[index];
                Point3D end = outline[(index + 1) % outline.Count];
                double deltaX = end.X - start.X;
                double deltaY = end.Y - start.Y;
                if (Math.Abs(deltaX) > GeometryTolerance.Coordinate
                    && Math.Abs(deltaY) > GeometryTolerance.Coordinate)
                {
                    return outline;
                }

                if (Math.Abs(deltaX) > GeometryTolerance.Coordinate)
                {
                    shifted.Add(
                        new ShiftedEdge(
                            false,
                            start.Y + (deltaX > 0.0 ? -half : half)));
                }
                else if (Math.Abs(deltaY) > GeometryTolerance.Coordinate)
                {
                    shifted.Add(
                        new ShiftedEdge(
                            true,
                            start.X + (deltaY > 0.0 ? half : -half)));
                }
                else
                {
                    return outline;
                }
            }

            var expanded = new List<Point3D>(shifted.Count);
            for (int index = 0; index < shifted.Count; index++)
            {
                ShiftedEdge previous = shifted[
                    (index - 1 + shifted.Count) % shifted.Count];
                ShiftedEdge next = shifted[index];
                if (previous.IsVertical == next.IsVertical)
                {
                    return outline;
                }

                ShiftedEdge vertical = previous.IsVertical ? previous : next;
                ShiftedEdge horizontal = previous.IsVertical ? next : previous;
                expanded.Add(
                    new Point3D(
                        vertical.FixedCoordinate,
                        horizontal.FixedCoordinate,
                        outline[0].Z));
            }

            return expanded;
        }

        private struct ShiftedEdge
        {
            public ShiftedEdge(bool isVertical, double fixedCoordinate)
            {
                IsVertical = isVertical;
                FixedCoordinate = fixedCoordinate;
            }

            public bool IsVertical { get; }

            public double FixedCoordinate { get; }
        }

        private static bool IsSafe(IList<double> spans, double nominalSize)
        {
            if (spans == null || spans.Count < 2
                || nominalSize <= GeometryTolerance.Coordinate)
            {
                return false;
            }

            double minimum = nominalSize
                * EngineeringLayoutRules.WallCornerSafeAdjacentTileRatio;
            return spans[0] + GeometryTolerance.Coordinate >= minimum
                && spans[1] + GeometryTolerance.Coordinate >= minimum;
        }

        private static double? GetSpan(IList<double> spans, int index)
        {
            return spans != null && spans.Count > index
                ? (double?)spans[index]
                : null;
        }

        private static string FormatTargetReason(
            string prefix,
            bool verticalSafe,
            bool horizontalSafe,
            IList<double> verticalSpans,
            IList<double> horizontalSpans)
        {
            string vertical = verticalSafe
                ? "vertical seam is safe (both adjacent spans meet 2/3T)"
                : verticalSpans.Count == 0
                    ? "vertical seam adjacent spans are unavailable"
                    : "vertical seam is not safe (an adjacent span is below 2/3T)";
            string horizontal = horizontalSafe
                ? "horizontal seam is safe (both adjacent spans meet 2/3T)"
                : horizontalSpans.Count == 0
                    ? "horizontal seam adjacent spans are unavailable"
                    : "horizontal seam is not safe (an adjacent span is below 2/3T)";
            return prefix + " " + vertical + "; " + horizontal + ".";
        }
    }
}
