using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    internal static class OrthogonalFootprintBuilder
    {
        public static List<TileFootprint> Build(
            AxisAlignedOrthogonalPolygon room,
            IList<double> verticalCuts,
            IList<double> horizontalCuts,
            double tileWidth,
            double tileHeight,
            double groutWidthMm = 0.0)
        {
            var xs = BuildCoordinates(room.West, room.East, verticalCuts);
            var ys = BuildCoordinates(room.South, room.North, horizontalCuts);
            var footprints = new List<TileFootprint>();
            for (int x = 0; x < xs.Count - 1; x++)
            {
                for (int y = 0; y < ys.Count - 1; y++)
                {
                    foreach (IList<Point3D> outline in ClipCell(
                        room,
                        xs[x],
                        xs[x + 1],
                        ys[y],
                        ys[y + 1]))
                    {
                        footprints.Add(
                            CreateFootprint(
                                room,
                                outline,
                                tileWidth,
                                tileHeight,
                                groutWidthMm));
                    }
                }
            }

            return footprints;
        }

        public static double CalculateIntersectionArea(
            AxisAlignedOrthogonalPolygon room,
            AxisAlignedRectangle rectangle)
        {
            double area = 0.0;
            foreach (IList<Point3D> outline in ClipCell(
                room,
                rectangle.West,
                rectangle.East,
                rectangle.South,
                rectangle.North))
            {
                area += CalculateArea(outline);
            }

            return area;
        }

        public static TileFootprint CreateRectangleFootprint(
            AxisAlignedOrthogonalPolygon room,
            double west,
            double east,
            double south,
            double north,
            double tileWidth,
            double tileHeight,
            double groutWidthMm = 0.0)
        {
            return CreateFootprint(
                room,
                new List<Point3D>
                {
                    new Point3D(west, south, room.Elevation),
                    new Point3D(east, south, room.Elevation),
                    new Point3D(east, north, room.Elevation),
                    new Point3D(west, north, room.Elevation)
                },
                tileWidth,
                tileHeight,
                groutWidthMm);
        }

        private static IList<double> BuildCoordinates(
            double minimum,
            double maximum,
            IList<double> cuts)
        {
            var values = new List<double>(cuts.Count + 2) { minimum };
            foreach (double cut in cuts)
            {
                if (cut > minimum + GeometryTolerance.Coordinate
                    && cut < maximum - GeometryTolerance.Coordinate)
                {
                    values.Add(cut);
                }
            }

            values.Add(maximum);
            values.Sort();
            RemoveNearDuplicates(values);
            return values;
        }

        private static IEnumerable<IList<Point3D>> ClipCell(
            AxisAlignedOrthogonalPolygon room,
            double west,
            double east,
            double south,
            double north)
        {
            var xs = new List<double> { west, east };
            var ys = new List<double> { south, north };
            foreach (Point3D vertex in room.Vertices)
            {
                if (vertex.X > west + GeometryTolerance.Coordinate
                    && vertex.X < east - GeometryTolerance.Coordinate)
                {
                    xs.Add(vertex.X);
                }

                if (vertex.Y > south + GeometryTolerance.Coordinate
                    && vertex.Y < north - GeometryTolerance.Coordinate)
                {
                    ys.Add(vertex.Y);
                }
            }

            xs.Sort();
            ys.Sort();
            RemoveNearDuplicates(xs);
            RemoveNearDuplicates(ys);

            int width = xs.Count - 1;
            int height = ys.Count - 1;
            var inside = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double centerX = xs[x] + ((xs[x + 1] - xs[x]) / 2.0);
                    double centerY = ys[y] + ((ys[y + 1] - ys[y]) / 2.0);
                    inside[x, y] = IsPointInside(room, centerX, centerY);
                }
            }

            var visited = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!inside[x, y] || visited[x, y])
                    {
                        continue;
                    }

                    var atoms = CollectComponent(inside, visited, x, y);
                    yield return TraceOutline(atoms, xs, ys, room.Elevation);
                }
            }
        }

        private static List<GridAtom> CollectComponent(
            bool[,] inside,
            bool[,] visited,
            int startX,
            int startY)
        {
            int width = inside.GetLength(0);
            int height = inside.GetLength(1);
            var result = new List<GridAtom>();
            var queue = new Queue<GridAtom>();
            queue.Enqueue(new GridAtom(startX, startY));
            visited[startX, startY] = true;
            while (queue.Count > 0)
            {
                GridAtom atom = queue.Dequeue();
                result.Add(atom);
                TryEnqueue(atom.X - 1, atom.Y);
                TryEnqueue(atom.X + 1, atom.Y);
                TryEnqueue(atom.X, atom.Y - 1);
                TryEnqueue(atom.X, atom.Y + 1);
            }

            return result;

            void TryEnqueue(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height
                    || !inside[x, y] || visited[x, y])
                {
                    return;
                }

                visited[x, y] = true;
                queue.Enqueue(new GridAtom(x, y));
            }
        }

        private static IList<Point3D> TraceOutline(
            IList<GridAtom> atoms,
            IList<double> xs,
            IList<double> ys,
            double elevation)
        {
            var atomSet = new HashSet<GridAtom>(atoms);
            var edges = new List<GridEdge>();
            foreach (GridAtom atom in atoms)
            {
                if (!atomSet.Contains(new GridAtom(atom.X, atom.Y - 1)))
                {
                    edges.Add(
                        new GridEdge(
                            new GridNode(atom.X, atom.Y),
                            new GridNode(atom.X + 1, atom.Y)));
                }

                if (!atomSet.Contains(new GridAtom(atom.X + 1, atom.Y)))
                {
                    edges.Add(
                        new GridEdge(
                            new GridNode(atom.X + 1, atom.Y),
                            new GridNode(atom.X + 1, atom.Y + 1)));
                }

                if (!atomSet.Contains(new GridAtom(atom.X, atom.Y + 1)))
                {
                    edges.Add(
                        new GridEdge(
                            new GridNode(atom.X + 1, atom.Y + 1),
                            new GridNode(atom.X, atom.Y + 1)));
                }

                if (!atomSet.Contains(new GridAtom(atom.X - 1, atom.Y)))
                {
                    edges.Add(
                        new GridEdge(
                            new GridNode(atom.X, atom.Y + 1),
                            new GridNode(atom.X, atom.Y)));
                }
            }

            var next = new Dictionary<GridNode, GridNode>();
            foreach (GridEdge edge in edges)
            {
                next.Add(edge.Start, edge.End);
            }

            GridNode start = edges[0].Start;
            foreach (GridEdge edge in edges)
            {
                if (edge.Start.X < start.X
                    || (edge.Start.X == start.X && edge.Start.Y < start.Y))
                {
                    start = edge.Start;
                }
            }

            var outline = new List<Point3D>();
            GridNode current = start;
            do
            {
                outline.Add(new Point3D(xs[current.X], ys[current.Y], elevation));
                if (!next.TryGetValue(current, out GridNode following))
                {
                    throw new InvalidOperationException(
                        "An orthogonal tile component produced an open outline.");
                }

                current = following;
            }
            while (!current.Equals(start));

            RemoveCollinearVertices(outline);
            return outline;
        }

        private static TileFootprint CreateFootprint(
            AxisAlignedOrthogonalPolygon room,
            IList<Point3D> outline,
            double tileWidth,
            double tileHeight,
            double groutWidthMm)
        {
            var sourceOutline = new List<Point3D>(outline);
            IList<RoomSide> boundarySides = GetBoundarySides(
                room,
                sourceOutline);
            bool boundary = TouchesBoundary(room, sourceOutline);
            var outlinePoints = InsetOutline(
                sourceOutline,
                room.Elevation,
                groutWidthMm);
            double west = outlinePoints[0].X;
            double east = outlinePoints[0].X;
            double south = outlinePoints[0].Y;
            double north = outlinePoints[0].Y;
            foreach (Point3D point in outlinePoints)
            {
                west = Math.Min(west, point.X);
                east = Math.Max(east, point.X);
                south = Math.Min(south, point.Y);
                north = Math.Max(north, point.Y);
            }

            double area = CalculateArea(outlinePoints);
            double nominalWidth = east - west;
            double nominalHeight = north - south;
            bool irregular = outlinePoints.Count != 4
                || !GeometryTolerance.NearlyEqual(
                    area,
                    nominalWidth * nominalHeight);
            bool full = !irregular
                && GeometryTolerance.NearlyEqual(nominalWidth, tileWidth)
                && GeometryTolerance.NearlyEqual(nominalHeight, tileHeight);
            return new TileFootprint(
                outlinePoints,
                boundary ? TileClassification.Boundary : TileClassification.Interior,
                full,
                irregular,
                nominalWidth,
                nominalHeight,
                area,
                boundarySides);
        }

        private static List<Point3D> InsetOutline(
            IList<Point3D> outline,
            double elevation,
            double groutWidthMm)
        {
            if (groutWidthMm <= GeometryTolerance.Coordinate)
            {
                return new List<Point3D>(outline);
            }

            var lines = new List<LineSegment3D>(outline.Count);
            for (int index = 0; index < outline.Count; index++)
            {
                lines.Add(
                    new LineSegment3D(
                        outline[index],
                        outline[(index + 1) % outline.Count]));
            }

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);
            if (!validation.IsValid)
            {
                throw new GroutAllowanceException(
                    "A tile footprint cannot be inset because its slot outline is invalid.",
                    nameof(outline));
            }

            OrthogonalRoomOffsetResult offset =
                OrthogonalRoomOffsetter.Offset(
                    validation.Room,
                    groutWidthMm / 2.0);
            if (!offset.IsValid)
            {
                throw new GroutAllowanceException(
                    "A tile footprint disappears when the grout allowance is applied.",
                    nameof(groutWidthMm));
            }

            return new List<Point3D>(offset.Room.Vertices);
        }

        internal static bool TouchesVerticalBoundary(
            AxisAlignedOrthogonalPolygon room,
            TileFootprint footprint)
        {
            return TouchesBoundaryWithOrientation(room, footprint.Outline, true);
        }

        internal static bool TouchesHorizontalBoundary(
            AxisAlignedOrthogonalPolygon room,
            TileFootprint footprint)
        {
            return TouchesBoundaryWithOrientation(room, footprint.Outline, false);
        }

        private static bool TouchesBoundary(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<Point3D> outline)
        {
            return TouchesBoundaryWithOrientation(room, outline, true)
                || TouchesBoundaryWithOrientation(room, outline, false);
        }

        private static bool TouchesBoundaryWithOrientation(
            AxisAlignedOrthogonalPolygon room,
            IReadOnlyList<Point3D> outline,
            bool vertical)
        {
            for (int firstIndex = 0; firstIndex < outline.Count; firstIndex++)
            {
                Point3D firstStart = outline[firstIndex];
                Point3D firstEnd = outline[(firstIndex + 1) % outline.Count];
                bool firstVertical = GeometryTolerance.NearlyEqual(
                    firstStart.X,
                    firstEnd.X);
                if (firstVertical != vertical)
                {
                    continue;
                }

                for (int secondIndex = 0;
                    secondIndex < room.Vertices.Count;
                    secondIndex++)
                {
                    Point3D secondStart = room.Vertices[secondIndex];
                    Point3D secondEnd = room.Vertices[
                        (secondIndex + 1) % room.Vertices.Count];
                    bool secondVertical = GeometryTolerance.NearlyEqual(
                        secondStart.X,
                        secondEnd.X);
                    if (secondVertical != vertical)
                    {
                        continue;
                    }

                    double firstFixed = vertical ? firstStart.X : firstStart.Y;
                    double secondFixed = vertical ? secondStart.X : secondStart.Y;
                    if (!GeometryTolerance.NearlyEqual(firstFixed, secondFixed))
                    {
                        continue;
                    }

                    double firstLow = vertical
                        ? Math.Min(firstStart.Y, firstEnd.Y)
                        : Math.Min(firstStart.X, firstEnd.X);
                    double firstHigh = vertical
                        ? Math.Max(firstStart.Y, firstEnd.Y)
                        : Math.Max(firstStart.X, firstEnd.X);
                    double secondLow = vertical
                        ? Math.Min(secondStart.Y, secondEnd.Y)
                        : Math.Min(secondStart.X, secondEnd.X);
                    double secondHigh = vertical
                        ? Math.Max(secondStart.Y, secondEnd.Y)
                        : Math.Max(secondStart.X, secondEnd.X);
                    if (Math.Min(firstHigh, secondHigh)
                        - Math.Max(firstLow, secondLow)
                        > GeometryTolerance.Coordinate)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IList<RoomSide> GetBoundarySides(
            AxisAlignedOrthogonalPolygon room,
            IList<Point3D> outline)
        {
            var sides = new List<RoomSide>();
            AddSideIfPresent(RoomSide.West, room.West, true);
            AddSideIfPresent(RoomSide.East, room.East, true);
            AddSideIfPresent(RoomSide.South, room.South, false);
            AddSideIfPresent(RoomSide.North, room.North, false);
            return sides;

            void AddSideIfPresent(RoomSide side, double coordinate, bool vertical)
            {
                for (int index = 0; index < outline.Count; index++)
                {
                    Point3D start = outline[index];
                    Point3D end = outline[(index + 1) % outline.Count];
                    bool isVertical = GeometryTolerance.NearlyEqual(start.X, end.X);
                    double fixedCoordinate = vertical ? start.X : start.Y;
                    if (isVertical == vertical
                        && GeometryTolerance.NearlyEqual(fixedCoordinate, coordinate))
                    {
                        sides.Add(side);
                        return;
                    }
                }
            }
        }

        private static bool IsPointInside(
            AxisAlignedOrthogonalPolygon room,
            double x,
            double y)
        {
            int crossings = 0;
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D start = room.Vertices[index];
                Point3D end = room.Vertices[(index + 1) % room.Vertices.Count];
                if (!GeometryTolerance.NearlyEqual(start.X, end.X))
                {
                    continue;
                }

                double low = Math.Min(start.Y, end.Y);
                double high = Math.Max(start.Y, end.Y);
                if (y >= low && y < high && start.X > x)
                {
                    crossings++;
                }
            }

            return (crossings & 1) == 1;
        }

        private static double CalculateArea(IList<Point3D> outline)
        {
            double originX = outline[0].X;
            double originY = outline[0].Y;
            double twiceArea = 0.0;
            for (int index = 0; index < outline.Count; index++)
            {
                Point3D first = outline[index];
                Point3D second = outline[(index + 1) % outline.Count];
                twiceArea += ((first.X - originX) * (second.Y - originY))
                    - ((second.X - originX) * (first.Y - originY));
            }

            return Math.Abs(twiceArea) / 2.0;
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

        private static void RemoveCollinearVertices(IList<Point3D> points)
        {
            bool changed;
            do
            {
                changed = false;
                for (int index = points.Count - 1; index >= 0; index--)
                {
                    Point3D previous = points[(index + points.Count - 1) % points.Count];
                    Point3D current = points[index];
                    Point3D next = points[(index + 1) % points.Count];
                    bool sameX = GeometryTolerance.NearlyEqual(previous.X, current.X)
                        && GeometryTolerance.NearlyEqual(current.X, next.X);
                    bool sameY = GeometryTolerance.NearlyEqual(previous.Y, current.Y)
                        && GeometryTolerance.NearlyEqual(current.Y, next.Y);
                    if (sameX || sameY)
                    {
                        points.RemoveAt(index);
                        changed = true;
                    }
                }
            }
            while (changed && points.Count > 4);
        }

        private struct GridAtom : IEquatable<GridAtom>
        {
            public GridAtom(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }

            public int Y { get; }

            public bool Equals(GridAtom other) => X == other.X && Y == other.Y;

            public override bool Equals(object obj) =>
                obj is GridAtom other && Equals(other);

            public override int GetHashCode() => (X * 397) ^ Y;
        }

        private struct GridNode : IEquatable<GridNode>
        {
            public GridNode(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }

            public int Y { get; }

            public bool Equals(GridNode other) => X == other.X && Y == other.Y;

            public override bool Equals(object obj) =>
                obj is GridNode other && Equals(other);

            public override int GetHashCode() => (X * 397) ^ Y;
        }

        private struct GridEdge
        {
            public GridEdge(GridNode start, GridNode end)
            {
                Start = start;
                End = end;
            }

            public GridNode Start { get; }

            public GridNode End { get; }
        }
    }

    internal sealed class GroutAllowanceException : ArgumentException
    {
        public GroutAllowanceException(string message, string parameterName)
            : base(message, parameterName)
        {
        }
    }
}
