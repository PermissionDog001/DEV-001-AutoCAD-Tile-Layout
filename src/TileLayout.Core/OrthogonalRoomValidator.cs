using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class OrthogonalRoomValidator
    {
        public static OrthogonalRoomValidationResult Validate(
            IReadOnlyCollection<LineSegment3D> lines)
        {
            if (lines == null || lines.Count < 4)
            {
                return Failure(
                    OrthogonalRoomValidationError.IncorrectLineCount,
                    "必须提供至少四条 LINE。");
            }

            LineSegment3D[] lineArray = lines.ToArray();
            Point3D[] points = lineArray
                .SelectMany(line => new[] { line.Start, line.End })
                .ToArray();
            if (points.Any(point => !IsFinite(point.X)
                || !IsFinite(point.Y)
                || !IsFinite(point.Z)))
            {
                return Failure(
                    OrthogonalRoomValidationError.NonFiniteCoordinate,
                    "线端点坐标必须是有限数值。");
            }

            double minimumZ = points.Min(point => point.Z);
            double maximumZ = points.Max(point => point.Z);
            if (maximumZ - minimumZ > GeometryTolerance.Coordinate)
            {
                return Failure(
                    OrthogonalRoomValidationError.NonCoplanar,
                    "全部边界线必须位于同一 WCS 高程。");
            }

            foreach (LineSegment3D line in lineArray)
            {
                double deltaX = Math.Abs(line.End.X - line.Start.X);
                double deltaY = Math.Abs(line.End.Y - line.Start.Y);
                if (deltaX <= GeometryTolerance.Coordinate
                    && deltaY <= GeometryTolerance.Coordinate)
                {
                    return Failure(
                        OrthogonalRoomValidationError.DegenerateLine,
                        "边界中存在长度小于等于公差的线。");
                }

                if (deltaX > GeometryTolerance.Coordinate
                    && deltaY > GeometryTolerance.Coordinate)
                {
                    return Failure(
                        OrthogonalRoomValidationError.NonAxisAlignedLine,
                        "全部边界线必须与 WCS X/Y 轴平行。");
                }
            }

            Dictionary<double, double> normalizedX;
            Dictionary<double, double> normalizedY;
            if (!TryCreateCoordinateMap(
                    points.Select(point => point.X),
                    out normalizedX)
                || !TryCreateCoordinateMap(
                    points.Select(point => point.Y),
                    out normalizedY))
            {
                return Failure(
                    OrthogonalRoomValidationError.AmbiguousToleranceCluster,
                    "端点存在链式公差吸附歧义，请清理相邻但不能唯一归并的坐标。");
            }

            var edges = new NormalizedEdge[lineArray.Length];
            for (int index = 0; index < lineArray.Length; index++)
            {
                VertexKey start = Normalize(
                    lineArray[index].Start,
                    normalizedX,
                    normalizedY);
                VertexKey end = Normalize(
                    lineArray[index].End,
                    normalizedX,
                    normalizedY);
                if (start.Equals(end))
                {
                    return Failure(
                        OrthogonalRoomValidationError.DegenerateLine,
                        "端点归并后存在退化边界线。");
                }

                edges[index] = new NormalizedEdge(start, end);
            }

            OrthogonalRoomValidationResult intersectionFailure =
                ValidateIntersections(edges);
            if (intersectionFailure != null)
            {
                return intersectionFailure;
            }

            var incidentEdges = new Dictionary<VertexKey, List<int>>();
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                AddIncidentEdge(incidentEdges, edges[edgeIndex].Start, edgeIndex);
                AddIncidentEdge(incidentEdges, edges[edgeIndex].End, edgeIndex);
            }

            if (incidentEdges.Values.Any(indices => indices.Count != 2))
            {
                return Failure(
                    OrthogonalRoomValidationError.InvalidVertexDegree,
                    "边界存在断口、悬挂边或 T 形分叉；每个归并端点必须恰好连接两条线。");
            }

            List<VertexKey> loop;
            if (!TryTraverseSingleLoop(edges, incidentEdges, out loop))
            {
                return Failure(
                    OrthogonalRoomValidationError.MultipleDisconnectedLoops,
                    "所选边界必须形成且只形成一个连通闭环，不支持分离环或洞口。");
            }

            SimplifyCollinearVertices(loop);
            double signedAreaTwice = GetSignedAreaTwice(loop);
            if (loop.Count < 4
                || !IsFinite(signedAreaTwice)
                || Math.Abs(signedAreaTwice) <= GeometryTolerance.Coordinate)
            {
                return Failure(
                    OrthogonalRoomValidationError.NonPositiveArea,
                    "闭合边界必须围成面积大于公差的正交房间。");
            }

            if (signedAreaTwice < 0.0)
            {
                loop.Reverse();
            }

            RotateToCanonicalStart(loop);
            double elevation = minimumZ + ((maximumZ - minimumZ) / 2.0);
            var vertices = loop
                .Select(vertex => new Point3D(vertex.X, vertex.Y, elevation))
                .ToList();
            return OrthogonalRoomValidationResult.Success(
                new AxisAlignedOrthogonalPolygon(vertices, elevation));
        }

        private static OrthogonalRoomValidationResult ValidateIntersections(
            IReadOnlyList<NormalizedEdge> edges)
        {
            for (int firstIndex = 0; firstIndex < edges.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                    secondIndex < edges.Count;
                    secondIndex++)
                {
                    NormalizedEdge first = edges[firstIndex];
                    NormalizedEdge second = edges[secondIndex];
                    if (first.IsHorizontal == second.IsHorizontal)
                    {
                        if (!GeometryTolerance.NearlyEqual(
                            first.FixedCoordinate,
                            second.FixedCoordinate))
                        {
                            continue;
                        }

                        double overlap = Math.Min(first.Maximum, second.Maximum)
                            - Math.Max(first.Minimum, second.Minimum);
                        if (overlap > 0.0)
                        {
                            return Failure(
                                OrthogonalRoomValidationError.DuplicateOrOverlappingLine,
                                "边界中存在重复、反向重复或部分重叠的共线 LINE。");
                        }

                        continue;
                    }

                    NormalizedEdge horizontal = first.IsHorizontal ? first : second;
                    NormalizedEdge vertical = first.IsHorizontal ? second : first;
                    if (vertical.FixedCoordinate < horizontal.Minimum
                        || vertical.FixedCoordinate > horizontal.Maximum
                        || horizontal.FixedCoordinate < vertical.Minimum
                        || horizontal.FixedCoordinate > vertical.Maximum)
                    {
                        continue;
                    }

                    var intersection = new VertexKey(
                        vertical.FixedCoordinate,
                        horizontal.FixedCoordinate);
                    bool isSharedEndpoint = horizontal.IsEndpoint(intersection)
                        && vertical.IsEndpoint(intersection);
                    if (!isSharedEndpoint)
                    {
                        return Failure(
                            OrthogonalRoomValidationError.IntersectingOrTouchingBoundary,
                            "边界存在自交、非相邻接触或端点落在另一条线内部。");
                    }
                }
            }

            return null;
        }

        private static bool TryCreateCoordinateMap(
            IEnumerable<double> coordinates,
            out Dictionary<double, double> coordinateMap)
        {
            double[] sorted = coordinates.Distinct().OrderBy(value => value).ToArray();
            coordinateMap = new Dictionary<double, double>();
            int start = 0;
            while (start < sorted.Length)
            {
                int end = start;
                while (end + 1 < sorted.Length
                    && sorted[end + 1] - sorted[end]
                        <= GeometryTolerance.Coordinate)
                {
                    end++;
                }

                if (sorted[end] - sorted[start] > GeometryTolerance.Coordinate)
                {
                    coordinateMap = null;
                    return false;
                }

                double canonical = sorted[start]
                    + ((sorted[end] - sorted[start]) / 2.0);
                for (int index = start; index <= end; index++)
                {
                    coordinateMap.Add(sorted[index], canonical);
                }

                start = end + 1;
            }

            return true;
        }

        private static VertexKey Normalize(
            Point3D point,
            IReadOnlyDictionary<double, double> normalizedX,
            IReadOnlyDictionary<double, double> normalizedY)
        {
            return new VertexKey(normalizedX[point.X], normalizedY[point.Y]);
        }

        private static void AddIncidentEdge(
            IDictionary<VertexKey, List<int>> incidentEdges,
            VertexKey vertex,
            int edgeIndex)
        {
            List<int> indices;
            if (!incidentEdges.TryGetValue(vertex, out indices))
            {
                indices = new List<int>(2);
                incidentEdges.Add(vertex, indices);
            }

            indices.Add(edgeIndex);
        }

        private static bool TryTraverseSingleLoop(
            IReadOnlyList<NormalizedEdge> edges,
            IReadOnlyDictionary<VertexKey, List<int>> incidentEdges,
            out List<VertexKey> loop)
        {
            VertexKey start = incidentEdges.Keys
                .OrderBy(vertex => vertex.X)
                .ThenBy(vertex => vertex.Y)
                .First();
            List<int> firstEdges = incidentEdges[start];
            int currentEdge = Compare(
                    edges[firstEdges[0]].Other(start),
                    edges[firstEdges[1]].Other(start))
                <= 0
                ? firstEdges[0]
                : firstEdges[1];
            VertexKey current = start;
            var usedEdges = new HashSet<int>();
            loop = new List<VertexKey>(edges.Count);

            while (true)
            {
                loop.Add(current);
                if (!usedEdges.Add(currentEdge))
                {
                    return false;
                }

                VertexKey next = edges[currentEdge].Other(current);
                if (next.Equals(start))
                {
                    return usedEdges.Count == edges.Count;
                }

                List<int> nextEdges = incidentEdges[next];
                int followingEdge = nextEdges[0] == currentEdge
                    ? nextEdges[1]
                    : nextEdges[0];
                current = next;
                currentEdge = followingEdge;

                if (loop.Count > edges.Count)
                {
                    return false;
                }
            }
        }

        private static void SimplifyCollinearVertices(IList<VertexKey> vertices)
        {
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < vertices.Count && vertices.Count >= 4; index++)
                {
                    VertexKey previous = vertices[
                        (index - 1 + vertices.Count) % vertices.Count];
                    VertexKey current = vertices[index];
                    VertexKey next = vertices[(index + 1) % vertices.Count];
                    if ((previous.X == current.X && current.X == next.X)
                        || (previous.Y == current.Y && current.Y == next.Y))
                    {
                        vertices.RemoveAt(index);
                        changed = true;
                        break;
                    }
                }
            }
            while (changed);
        }

        private static double GetSignedAreaTwice(IReadOnlyList<VertexKey> vertices)
        {
            VertexKey origin = vertices[0];
            double areaTwice = 0.0;
            for (int index = 0; index < vertices.Count; index++)
            {
                VertexKey current = vertices[index];
                VertexKey next = vertices[(index + 1) % vertices.Count];
                double currentX = current.X - origin.X;
                double currentY = current.Y - origin.Y;
                double nextX = next.X - origin.X;
                double nextY = next.Y - origin.Y;
                areaTwice += (currentX * nextY) - (nextX * currentY);
            }

            return areaTwice;
        }

        private static void RotateToCanonicalStart(IList<VertexKey> vertices)
        {
            int startIndex = 0;
            for (int index = 1; index < vertices.Count; index++)
            {
                if (Compare(vertices[index], vertices[startIndex]) < 0)
                {
                    startIndex = index;
                }
            }

            if (startIndex == 0)
            {
                return;
            }

            VertexKey[] rotated = new VertexKey[vertices.Count];
            for (int index = 0; index < vertices.Count; index++)
            {
                rotated[index] = vertices[(startIndex + index) % vertices.Count];
            }

            for (int index = 0; index < vertices.Count; index++)
            {
                vertices[index] = rotated[index];
            }
        }

        private static int Compare(VertexKey first, VertexKey second)
        {
            int xComparison = first.X.CompareTo(second.X);
            return xComparison != 0 ? xComparison : first.Y.CompareTo(second.Y);
        }

        private static OrthogonalRoomValidationResult Failure(
            OrthogonalRoomValidationError error,
            string message)
        {
            return OrthogonalRoomValidationResult.Failure(error, message);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private struct VertexKey : IEquatable<VertexKey>
        {
            public VertexKey(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }

            public bool Equals(VertexKey other)
            {
                return X.Equals(other.X) && Y.Equals(other.Y);
            }

            public override bool Equals(object obj)
            {
                return obj is VertexKey && Equals((VertexKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X.GetHashCode() * 397) ^ Y.GetHashCode();
                }
            }
        }

        private struct NormalizedEdge
        {
            public NormalizedEdge(VertexKey start, VertexKey end)
            {
                Start = start;
                End = end;
                IsHorizontal = start.Y == end.Y;
            }

            public VertexKey Start { get; }

            public VertexKey End { get; }

            public bool IsHorizontal { get; }

            public double FixedCoordinate => IsHorizontal ? Start.Y : Start.X;

            public double Minimum => IsHorizontal
                ? Math.Min(Start.X, End.X)
                : Math.Min(Start.Y, End.Y);

            public double Maximum => IsHorizontal
                ? Math.Max(Start.X, End.X)
                : Math.Max(Start.Y, End.Y);

            public bool IsEndpoint(VertexKey vertex)
            {
                return Start.Equals(vertex) || End.Equals(vertex);
            }

            public VertexKey Other(VertexKey vertex)
            {
                return Start.Equals(vertex) ? End : Start;
            }
        }
    }
}
