using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class NeutralOrthogonalRegionPartitioner
    {
        public static NeutralOrthogonalRegionPartition Create(
            AxisAlignedOrthogonalPolygon room)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            List<MutableRectangle> horizontal = BuildHorizontalRectangles(room);
            AxisAlignedOrthogonalPolygon transposed = new AxisAlignedOrthogonalPolygon(
                room.Vertices.Select(vertex =>
                    new Point3D(vertex.Y, vertex.X, vertex.Z)).ToList(),
                room.Elevation);
            List<MutableRectangle> vertical = BuildHorizontalRectangles(transposed)
                .Select(rectangle => new MutableRectangle(
                    rectangle.South,
                    rectangle.North,
                    rectangle.West,
                    rectangle.East))
                .ToList();
            List<MutableRectangle> rectangles = vertical.Count < horizontal.Count
                ? vertical
                : horizontal;

            rectangles.Sort(MutableRectangle.Compare);
            var regions = new List<NeutralOrthogonalRegion>();
            for (int index = 0; index < rectangles.Count; index++)
            {
                MutableRectangle rectangle = rectangles[index];
                regions.Add(new NeutralOrthogonalRegion(
                    "region-" + (index + 1).ToString("D3"),
                    new AxisAlignedRectangle(
                        rectangle.West,
                        rectangle.East,
                        rectangle.South,
                        rectangle.North,
                        room.Elevation)));
            }

            var connections = new List<NeutralRegionConnection>();
            for (int first = 0; first < regions.Count - 1; first++)
            {
                for (int second = first + 1; second < regions.Count; second++)
                {
                    LineSegment3D? shared = GetSharedEdge(
                        regions[first].Bounds,
                        regions[second].Bounds);
                    if (shared.HasValue)
                    {
                        connections.Add(new NeutralRegionConnection(
                            regions[first].Id,
                            regions[second].Id,
                            shared.Value));
                    }
                }
            }

            double roomArea = PolygonArea(room);
            double coveredArea = regions.Sum(region => region.Area);
            if (Math.Abs(roomArea - coveredArea)
                > GeometryTolerance.Coordinate * Math.Max(1.0, roomArea))
            {
                throw new InvalidOperationException(
                    "The neutral rectangle partition did not preserve the room area.");
            }

            return new NeutralOrthogonalRegionPartition(
                room,
                regions,
                connections);
        }

        private static List<MutableRectangle> BuildHorizontalRectangles(
            AxisAlignedOrthogonalPolygon room)
        {
            List<double> ys = room.Vertices
                .Select(vertex => vertex.Y)
                .OrderBy(value => value)
                .ToList();
            RemoveNearDuplicates(ys);
            var rectangles = new List<MutableRectangle>();
            var active = new List<MutableRectangle>();
            for (int yIndex = 0; yIndex < ys.Count - 1; yIndex++)
            {
                double south = ys[yIndex];
                double north = ys[yIndex + 1];
                double middle = (south + north) / 2.0;
                List<Interval> intervals = GetInteriorIntervals(room, middle);
                var nextActive = new List<MutableRectangle>();
                foreach (Interval interval in intervals)
                {
                    MutableRectangle continuation = active.FirstOrDefault(value =>
                        GeometryTolerance.NearlyEqual(value.West, interval.West)
                        && GeometryTolerance.NearlyEqual(value.East, interval.East)
                        && GeometryTolerance.NearlyEqual(value.North, south));
                    if (continuation == null)
                    {
                        continuation = new MutableRectangle(
                            interval.West,
                            interval.East,
                            south,
                            north);
                        rectangles.Add(continuation);
                    }
                    else
                    {
                        continuation.North = north;
                    }

                    nextActive.Add(continuation);
                }

                active = nextActive;
            }

            return rectangles;
        }

        private static List<Interval> GetInteriorIntervals(
            AxisAlignedOrthogonalPolygon room,
            double y)
        {
            var xs = new List<double>();
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D first = room.Vertices[index];
                Point3D second = room.Vertices[(index + 1) % room.Vertices.Count];
                if (!GeometryTolerance.NearlyEqual(first.X, second.X))
                {
                    continue;
                }

                double low = Math.Min(first.Y, second.Y);
                double high = Math.Max(first.Y, second.Y);
                if (y > low + GeometryTolerance.Coordinate
                    && y < high - GeometryTolerance.Coordinate)
                {
                    xs.Add(first.X);
                }
            }

            xs.Sort();
            RemoveNearDuplicates(xs);
            if ((xs.Count % 2) != 0)
            {
                throw new InvalidOperationException(
                    "The orthogonal room produced an odd scan-line intersection count.");
            }

            var intervals = new List<Interval>();
            for (int index = 0; index < xs.Count; index += 2)
            {
                intervals.Add(new Interval(xs[index], xs[index + 1]));
            }

            return intervals;
        }

        private static LineSegment3D? GetSharedEdge(
            AxisAlignedRectangle first,
            AxisAlignedRectangle second)
        {
            if (GeometryTolerance.NearlyEqual(first.East, second.West)
                || GeometryTolerance.NearlyEqual(second.East, first.West))
            {
                double x = GeometryTolerance.NearlyEqual(first.East, second.West)
                    ? first.East
                    : first.West;
                double start = Math.Max(first.South, second.South);
                double end = Math.Min(first.North, second.North);
                if (end - start > GeometryTolerance.Coordinate)
                {
                    return new LineSegment3D(
                        new Point3D(x, start, first.Elevation),
                        new Point3D(x, end, first.Elevation));
                }
            }

            if (GeometryTolerance.NearlyEqual(first.North, second.South)
                || GeometryTolerance.NearlyEqual(second.North, first.South))
            {
                double y = GeometryTolerance.NearlyEqual(first.North, second.South)
                    ? first.North
                    : first.South;
                double start = Math.Max(first.West, second.West);
                double end = Math.Min(first.East, second.East);
                if (end - start > GeometryTolerance.Coordinate)
                {
                    return new LineSegment3D(
                        new Point3D(start, y, first.Elevation),
                        new Point3D(end, y, first.Elevation));
                }
            }

            return null;
        }

        private static double PolygonArea(AxisAlignedOrthogonalPolygon room)
        {
            double twice = 0.0;
            Point3D origin = room.Vertices[0];
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D first = room.Vertices[index];
                Point3D second = room.Vertices[(index + 1) % room.Vertices.Count];
                double firstX = first.X - origin.X;
                double firstY = first.Y - origin.Y;
                double secondX = second.X - origin.X;
                double secondY = second.Y - origin.Y;
                twice += (firstX * secondY) - (secondX * firstY);
            }

            return Math.Abs(twice) / 2.0;
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

        private struct Interval
        {
            public Interval(double west, double east)
            {
                West = west;
                East = east;
            }

            public double West { get; }

            public double East { get; }
        }

        private sealed class MutableRectangle
        {
            public MutableRectangle(
                double west,
                double east,
                double south,
                double north)
            {
                West = west;
                East = east;
                South = south;
                North = north;
            }

            public double West { get; }

            public double East { get; }

            public double South { get; }

            public double North { get; set; }

            public static int Compare(MutableRectangle first, MutableRectangle second)
            {
                int south = first.South.CompareTo(second.South);
                if (south != 0) return south;
                int west = first.West.CompareTo(second.West);
                if (west != 0) return west;
                int north = first.North.CompareTo(second.North);
                return north != 0 ? north : first.East.CompareTo(second.East);
            }
        }
    }
}
