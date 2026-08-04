using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class OrthogonalTileGridCalculator
    {
        internal static List<LineSegment3D> ClipDivisionLines(
            AxisAlignedOrthogonalPolygon room,
            IList<double> verticalCoordinates,
            IList<double> horizontalCoordinates)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            int maximum = TileLayoutRules.MaximumParameterizedDivisionLineCount;
            int count = 0;
            foreach (double coordinate in verticalCoordinates)
            {
                count = AddCheckedCount(
                    count,
                    GetInteriorIntervals(room, coordinate, true).Count,
                    maximum);
            }

            foreach (double coordinate in horizontalCoordinates)
            {
                count = AddCheckedCount(
                    count,
                    GetInteriorIntervals(room, coordinate, false).Count,
                    maximum);
            }

            var result = new List<LineSegment3D>(count);
            foreach (double coordinate in verticalCoordinates)
            {
                foreach (Interval interval in GetInteriorIntervals(
                    room,
                    coordinate,
                    true))
                {
                    result.Add(
                        new LineSegment3D(
                            new Point3D(
                                coordinate,
                                interval.Start,
                                room.Elevation),
                            new Point3D(
                                coordinate,
                                interval.End,
                                room.Elevation)));
                }
            }

            foreach (double coordinate in horizontalCoordinates)
            {
                foreach (Interval interval in GetInteriorIntervals(
                    room,
                    coordinate,
                    false))
                {
                    result.Add(
                        new LineSegment3D(
                            new Point3D(
                                interval.Start,
                                coordinate,
                                room.Elevation),
                            new Point3D(
                                interval.End,
                                coordinate,
                                room.Elevation)));
                }
            }

            return result;
        }

        public static OrthogonalTileLayoutResult Calculate(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            TileSpanMetrics columns = TileSpanCalculator.Calculate(
                room.Width,
                parameters.TileWidth);
            TileSpanMetrics rows = TileSpanCalculator.Calculate(
                room.Height,
                parameters.TileHeight);
            double candidateLineCount =
                columns.InternalLineCount + rows.InternalLineCount;
            int maximum = TileLayoutRules.MaximumParameterizedDivisionLineCount;
            if (candidateLineCount > maximum)
            {
                throw new TileLayoutLimitExceededException(
                    candidateLineCount,
                    maximum);
            }

            int verticalLineCount = checked((int)columns.InternalLineCount);
            int horizontalLineCount = checked((int)rows.InternalLineCount);
            int fragmentCount = CountFragments(
                room,
                parameters,
                verticalLineCount,
                horizontalLineCount,
                maximum);

            var divisionLines = new List<LineSegment3D>(fragmentCount);
            AddVerticalFragments(
                room,
                parameters,
                verticalLineCount,
                divisionLines);
            AddHorizontalFragments(
                room,
                parameters,
                horizontalLineCount,
                divisionLines);

            return new OrthogonalTileLayoutResult(
                room,
                parameters,
                checked((int)columns.FullSpanCount),
                checked((int)rows.FullSpanCount),
                columns.Remainder,
                rows.Remainder,
                divisionLines);
        }

        private static int CountFragments(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters,
            int verticalLineCount,
            int horizontalLineCount,
            int maximum)
        {
            int count = 0;
            for (int index = 1; index <= verticalLineCount; index++)
            {
                double x = GetVerticalCoordinate(room, parameters, index);
                count = AddCheckedCount(
                    count,
                    GetInteriorIntervals(room, x, true).Count,
                    maximum);
            }

            for (int index = 1; index <= horizontalLineCount; index++)
            {
                double y = GetHorizontalCoordinate(room, parameters, index);
                count = AddCheckedCount(
                    count,
                    GetInteriorIntervals(room, y, false).Count,
                    maximum);
            }

            return count;
        }

        private static int AddCheckedCount(int current, int increment, int maximum)
        {
            long next = (long)current + increment;
            if (next > maximum)
            {
                throw new TileLayoutLimitExceededException(next, maximum);
            }

            return (int)next;
        }

        private static void AddVerticalFragments(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters,
            int lineCount,
            ICollection<LineSegment3D> divisionLines)
        {
            for (int index = 1; index <= lineCount; index++)
            {
                double x = GetVerticalCoordinate(room, parameters, index);
                foreach (Interval interval in GetInteriorIntervals(room, x, true))
                {
                    divisionLines.Add(
                        new LineSegment3D(
                            new Point3D(x, interval.Start, room.Elevation),
                            new Point3D(x, interval.End, room.Elevation)));
                }
            }
        }

        private static void AddHorizontalFragments(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters,
            int lineCount,
            ICollection<LineSegment3D> divisionLines)
        {
            for (int index = 1; index <= lineCount; index++)
            {
                double y = GetHorizontalCoordinate(room, parameters, index);
                foreach (Interval interval in GetInteriorIntervals(room, y, false))
                {
                    divisionLines.Add(
                        new LineSegment3D(
                            new Point3D(interval.Start, y, room.Elevation),
                            new Point3D(interval.End, y, room.Elevation)));
                }
            }
        }

        private static double GetVerticalCoordinate(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters,
            int index)
        {
            double offset = parameters.TileWidth * index;
            return parameters.StartsFromEast
                ? room.East - offset
                : room.West + offset;
        }

        private static double GetHorizontalCoordinate(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters,
            int index)
        {
            double offset = parameters.TileHeight * index;
            return parameters.StartsFromNorth
                ? room.North - offset
                : room.South + offset;
        }

        private static List<Interval> GetInteriorIntervals(
            AxisAlignedOrthogonalPolygon room,
            double fixedCoordinate,
            bool verticalScan)
        {
            var intersections = new List<double>();
            IReadOnlyList<Point3D> vertices = room.Vertices;
            for (int index = 0; index < vertices.Count; index++)
            {
                Point3D start = vertices[index];
                Point3D end = vertices[(index + 1) % vertices.Count];
                bool edgeIsHorizontal = start.Y == end.Y;
                if (verticalScan != edgeIsHorizontal)
                {
                    continue;
                }

                double first = verticalScan ? start.X : start.Y;
                double second = verticalScan ? end.X : end.Y;
                double minimum = Math.Min(first, second);
                double maximum = Math.Max(first, second);
                if (IsInHalfOpenRange(fixedCoordinate, minimum, maximum))
                {
                    intersections.Add(verticalScan ? start.Y : start.X);
                }
            }

            intersections.Sort();
            RemoveNearDuplicates(intersections);
            if ((intersections.Count & 1) != 0)
            {
                throw new InvalidOperationException(
                    "A validated orthogonal room produced an odd scan-line intersection count.");
            }

            var intervals = new List<Interval>(intersections.Count / 2);
            for (int index = 0; index < intersections.Count; index += 2)
            {
                AddOrMergeInterval(
                    intervals,
                    intersections[index],
                    intersections[index + 1]);
            }

            return SubtractCoincidentBoundary(
                intervals,
                room,
                fixedCoordinate,
                verticalScan);
        }

        private static List<Interval> SubtractCoincidentBoundary(
            IEnumerable<Interval> source,
            AxisAlignedOrthogonalPolygon room,
            double fixedCoordinate,
            bool verticalScan)
        {
            var result = new List<Interval>(source);
            IReadOnlyList<Point3D> vertices = room.Vertices;
            for (int index = 0; index < vertices.Count; index++)
            {
                Point3D start = vertices[index];
                Point3D end = vertices[(index + 1) % vertices.Count];
                bool edgeIsVertical = start.X == end.X;
                if (verticalScan != edgeIsVertical)
                {
                    continue;
                }

                double edgeFixedCoordinate = verticalScan ? start.X : start.Y;
                if (!GeometryTolerance.NearlyEqual(
                    fixedCoordinate,
                    edgeFixedCoordinate))
                {
                    continue;
                }

                double edgeStart = verticalScan
                    ? Math.Min(start.Y, end.Y)
                    : Math.Min(start.X, end.X);
                double edgeEnd = verticalScan
                    ? Math.Max(start.Y, end.Y)
                    : Math.Max(start.X, end.X);
                result = SubtractInterval(result, edgeStart, edgeEnd);
            }

            return result;
        }

        private static List<Interval> SubtractInterval(
            IEnumerable<Interval> source,
            double removalStart,
            double removalEnd)
        {
            var result = new List<Interval>();
            foreach (Interval interval in source)
            {
                if (removalEnd <= interval.Start + GeometryTolerance.Coordinate
                    || removalStart >= interval.End - GeometryTolerance.Coordinate)
                {
                    AddOrMergeInterval(result, interval.Start, interval.End);
                    continue;
                }

                AddOrMergeInterval(
                    result,
                    interval.Start,
                    Math.Min(removalStart, interval.End));
                AddOrMergeInterval(
                    result,
                    Math.Max(removalEnd, interval.Start),
                    interval.End);
            }

            return result;
        }

        private static bool IsInHalfOpenRange(
            double value,
            double minimum,
            double maximum)
        {
            bool atOrAfterMinimum = value > minimum
                || GeometryTolerance.NearlyEqual(value, minimum);
            bool beforeMaximum = value < maximum
                && !GeometryTolerance.NearlyEqual(value, maximum);
            return atOrAfterMinimum && beforeMaximum;
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

        private static void AddOrMergeInterval(
            IList<Interval> intervals,
            double start,
            double end)
        {
            if (end - start <= GeometryTolerance.Coordinate)
            {
                return;
            }

            if (intervals.Count == 0)
            {
                intervals.Add(new Interval(start, end));
                return;
            }

            Interval previous = intervals[intervals.Count - 1];
            if (start - previous.End <= GeometryTolerance.Coordinate)
            {
                intervals[intervals.Count - 1] = new Interval(
                    previous.Start,
                    Math.Max(previous.End, end));
                return;
            }

            intervals.Add(new Interval(start, end));
        }

        private struct Interval
        {
            public Interval(double start, double end)
            {
                Start = start;
                End = end;
            }

            public double Start { get; }

            public double End { get; }
        }
    }
}
