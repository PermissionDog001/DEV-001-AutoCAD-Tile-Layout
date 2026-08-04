using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class OrthogonalBoundaryNormalizer
    {
        public static OrthogonalBoundaryNormalizationResult Analyze(
            IReadOnlyCollection<LineSegment3D> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return OrthogonalBoundaryNormalizationResult.Rejected(
                    new List<LineSegment3D>(),
                    new List<OrthogonalBoundaryLineDiagnostic>(),
                    0.0,
                    0.0,
                    "没有可供近似正交诊断的边界线。" );
            }

            LineSegment3D[] source = lines.ToArray();
            var diagnostics = new List<OrthogonalBoundaryLineDiagnostic>(source.Length);
            var drafts = new List<LineDraft>(source.Length);
            double maximumAngleDeviationDegrees = 0.0;
            double maximumEndpointCorrection = 0.0;
            double minimumZ = double.PositiveInfinity;
            double maximumZ = double.NegativeInfinity;
            bool requiresNormalization = false;

            for (int index = 0; index < source.Length; index++)
            {
                LineSegment3D line = source[index];
                if (!IsFinite(line.Start) || !IsFinite(line.End))
                {
                    return OrthogonalBoundaryNormalizationResult.Rejected(
                        OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                        OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                        maximumAngleDeviationDegrees,
                        maximumEndpointCorrection,
                        "边界线端点存在非有限坐标，不能执行近似正交归一化。" );
                }

                minimumZ = Math.Min(minimumZ, Math.Min(line.Start.Z, line.End.Z));
                maximumZ = Math.Max(maximumZ, Math.Max(line.Start.Z, line.End.Z));

                double deltaX = line.End.X - line.Start.X;
                double deltaY = line.End.Y - line.Start.Y;
                double length = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                if (length <= GeometryTolerance.Coordinate)
                {
                    return OrthogonalBoundaryNormalizationResult.Rejected(
                        OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                        OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                        maximumAngleDeviationDegrees,
                        maximumEndpointCorrection,
                        string.Format(
                            "第 {0} 条边长度不超过坐标公差，不能执行近似正交归一化。",
                            index + 1));
                }

                double angle = Math.Atan2(Math.Abs(deltaY), Math.Abs(deltaX));
                double horizontalDeviation = angle;
                double verticalDeviation = (Math.PI / 2.0) - angle;
                OrthogonalBoundaryLineAxis axis = horizontalDeviation
                    <= verticalDeviation
                    ? OrthogonalBoundaryLineAxis.X
                    : OrthogonalBoundaryLineAxis.Y;
                double deviation = Math.Min(
                    horizontalDeviation,
                    verticalDeviation);
                double deviationDegrees = deviation * 180.0 / Math.PI;
                Point3D normalizedStart;
                Point3D normalizedEnd;
                double correction;
                if (axis == OrthogonalBoundaryLineAxis.X)
                {
                    double fixedCoordinate =
                        (line.Start.Y + line.End.Y) / 2.0;
                    normalizedStart = new Point3D(
                        line.Start.X,
                        fixedCoordinate,
                        line.Start.Z);
                    normalizedEnd = new Point3D(
                        line.End.X,
                        fixedCoordinate,
                        line.End.Z);
                    correction = Math.Max(
                        Math.Abs(line.Start.Y - fixedCoordinate),
                        Math.Abs(line.End.Y - fixedCoordinate));
                }
                else
                {
                    double fixedCoordinate =
                        (line.Start.X + line.End.X) / 2.0;
                    normalizedStart = new Point3D(
                        fixedCoordinate,
                        line.Start.Y,
                        line.Start.Z);
                    normalizedEnd = new Point3D(
                        fixedCoordinate,
                        line.End.Y,
                        line.End.Z);
                    correction = Math.Max(
                        Math.Abs(line.Start.X - fixedCoordinate),
                        Math.Abs(line.End.X - fixedCoordinate));
                }

                diagnostics.Add(new OrthogonalBoundaryLineDiagnostic(
                    index + 1,
                    axis,
                    deviationDegrees,
                    correction));
                drafts.Add(new LineDraft(
                    index,
                    axis,
                    normalizedStart,
                    normalizedEnd,
                    axis == OrthogonalBoundaryLineAxis.X
                        ? normalizedStart.Y
                        : normalizedStart.X));
                maximumAngleDeviationDegrees = Math.Max(
                    maximumAngleDeviationDegrees,
                    deviationDegrees);
                maximumEndpointCorrection = Math.Max(
                    maximumEndpointCorrection,
                    correction);
                requiresNormalization = requiresNormalization
                    || deltaX > GeometryTolerance.Coordinate
                        && deltaY > GeometryTolerance.Coordinate
                    || Math.Abs(deltaX) > GeometryTolerance.Coordinate
                        && Math.Abs(deltaY) > GeometryTolerance.Coordinate;

                if (deviation > GeometryTolerance.NearOrthogonalAngleRadians)
                {
                    return OrthogonalBoundaryNormalizationResult.Rejected(
                        OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                        OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                        maximumAngleDeviationDegrees,
                        maximumEndpointCorrection,
                        string.Format(
                            "第 {0} 条边偏离最近 WCS 轴 {1:0.######}°，超过近似正交阈值 {2:0.######}°。",
                            index + 1,
                            deviationDegrees,
                            GeometryTolerance.NearOrthogonalAngleDegrees));
                }

                if (correction
                    > GeometryTolerance.NearOrthogonalMaximumEndpointCorrection
                        + GeometryTolerance.Coordinate)
                {
                    return OrthogonalBoundaryNormalizationResult.Rejected(
                        OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                        OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                        maximumAngleDeviationDegrees,
                        maximumEndpointCorrection,
                        string.Format(
                            "第 {0} 条边需要 {1:0.###} mm 端点修正，超过近似正交上限 {2:0.###} mm。",
                            index + 1,
                            correction,
                            GeometryTolerance.NearOrthogonalMaximumEndpointCorrection));
                }
            }

            if (maximumZ - minimumZ > GeometryTolerance.Coordinate)
            {
                return OrthogonalBoundaryNormalizationResult.Rejected(
                    OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                    OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                    maximumAngleDeviationDegrees,
                    maximumEndpointCorrection,
                    "边界线不在同一 WCS 高程，近似正交归一化不处理高程偏差。" );
            }

            if (!requiresNormalization)
            {
                return OrthogonalBoundaryNormalizationResult.Exact(
                    OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                    OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics));
            }

            List<LineSegment3D> normalizedLines;
            string failure;
            if (!TryCanonicalizeEndpoints(
                source,
                drafts,
                out normalizedLines,
                out maximumEndpointCorrection,
                out failure))
            {
                return OrthogonalBoundaryNormalizationResult.Rejected(
                    OrthogonalBoundaryNormalizationResult.ReadOnlyLines(source),
                    OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                    maximumAngleDeviationDegrees,
                    maximumEndpointCorrection,
                    failure);
            }

            return OrthogonalBoundaryNormalizationResult.NearOrthogonal(
                OrthogonalBoundaryNormalizationResult.ReadOnlyLines(normalizedLines),
                OrthogonalBoundaryNormalizationResult.ReadOnlyDiagnostics(diagnostics),
                maximumAngleDeviationDegrees,
                maximumEndpointCorrection);
        }

        private static bool TryCanonicalizeEndpoints(
            IReadOnlyList<LineSegment3D> source,
            IReadOnlyList<LineDraft> drafts,
            out List<LineSegment3D> normalizedLines,
            out double maximumEndpointCorrection,
            out string failure)
        {
            normalizedLines = new List<LineSegment3D>();
            maximumEndpointCorrection = 0.0;
            failure = string.Empty;
            int endpointCount = source.Count * 2;
            var parent = Enumerable.Range(0, endpointCount).ToArray();
            for (int first = 0; first < endpointCount; first++)
            {
                for (int second = first + 1; second < endpointCount; second++)
                {
                    Point3D firstPoint = Endpoint(source, first);
                    Point3D secondPoint = Endpoint(source, second);
                    if (Math.Abs(firstPoint.Z - secondPoint.Z)
                            <= GeometryTolerance.Coordinate
                        && Distance2D(firstPoint, secondPoint)
                            <= GeometryTolerance.NearOrthogonalEndpointJoinTolerance)
                    {
                        Union(parent, first, second);
                    }
                }
            }

            var components = new Dictionary<int, List<int>>();
            for (int index = 0; index < endpointCount; index++)
            {
                int root = Find(parent, index);
                List<int> members;
                if (!components.TryGetValue(root, out members))
                {
                    members = new List<int>();
                    components.Add(root, members);
                }

                members.Add(index);
            }

            var canonical = new Point3D[endpointCount];
            foreach (List<int> members in components.Values)
            {
                for (int first = 0; first < members.Count; first++)
                {
                    for (int second = first + 1;
                        second < members.Count;
                        second++)
                    {
                        if (Distance2D(
                                Endpoint(source, members[first]),
                                Endpoint(source, members[second]))
                            > GeometryTolerance.NearOrthogonalEndpointJoinTolerance
                                + GeometryTolerance.Coordinate)
                        {
                            failure = "端点归一化出现链式邻接或歧义，无法安全合并房间闭环。";
                            return false;
                        }
                    }
                }

                double x = 0.0;
                double y = 0.0;
                double z = 0.0;
                double xConstraint = 0.0;
                double yConstraint = 0.0;
                int xConstraintCount = 0;
                int yConstraintCount = 0;
                foreach (int member in members)
                {
                    Point3D point = DraftEndpoint(drafts, member);
                    x += point.X;
                    y += point.Y;
                    z += point.Z;
                    LineDraft draft = drafts[member / 2];
                    if (draft.Axis == OrthogonalBoundaryLineAxis.Y)
                    {
                        xConstraint += draft.FixedCoordinate;
                        xConstraintCount++;
                    }
                    else
                    {
                        yConstraint += draft.FixedCoordinate;
                        yConstraintCount++;
                    }
                }

                Point3D average = new Point3D(
                    xConstraintCount == 0
                        ? x / members.Count
                        : xConstraint / xConstraintCount,
                    yConstraintCount == 0
                        ? y / members.Count
                        : yConstraint / yConstraintCount,
                    z / members.Count);
                foreach (int member in members)
                {
                    canonical[member] = average;
                }
            }

            for (int index = 0; index < drafts.Count; index++)
            {
                Point3D start = canonical[index * 2];
                Point3D end = canonical[(index * 2) + 1];

                if (Distance2D(start, end) <= GeometryTolerance.Coordinate)
                {
                    failure = "端点归一化后出现退化边界线，未继续使用计算副本。";
                    return false;
                }

                maximumEndpointCorrection = Math.Max(
                    maximumEndpointCorrection,
                    Distance2D(Endpoint(source, index * 2), start));
                maximumEndpointCorrection = Math.Max(
                    maximumEndpointCorrection,
                    Distance2D(Endpoint(source, (index * 2) + 1), end));
                if (maximumEndpointCorrection
                    > GeometryTolerance.NearOrthogonalMaximumEndpointCorrection
                        + GeometryTolerance.Coordinate)
                {
                    failure = string.Format(
                        "端点归一化最大修正达到 {0:0.###} mm，超过上限 {1:0.###} mm。",
                        maximumEndpointCorrection,
                        GeometryTolerance.NearOrthogonalMaximumEndpointCorrection);
                    return false;
                }

                normalizedLines.Add(new LineSegment3D(start, end));
            }

            return true;
        }

        private static Point3D Endpoint(
            IReadOnlyList<LineSegment3D> lines,
            int endpointIndex)
        {
            LineSegment3D line = lines[endpointIndex / 2];
            return endpointIndex % 2 == 0 ? line.Start : line.End;
        }

        private static Point3D DraftEndpoint(
            IReadOnlyList<LineDraft> drafts,
            int endpointIndex)
        {
            LineDraft draft = drafts[endpointIndex / 2];
            return endpointIndex % 2 == 0
                ? draft.NormalizedStart
                : draft.NormalizedEnd;
        }

        private static double Distance2D(Point3D first, Point3D second)
        {
            double deltaX = first.X - second.X;
            double deltaY = first.Y - second.Y;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }

        private static bool IsFinite(Point3D point)
        {
            return IsFinite(point.X)
                && IsFinite(point.Y)
                && IsFinite(point.Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static int Find(int[] parent, int value)
        {
            int root = value;
            while (parent[root] != root)
            {
                root = parent[root];
            }

            while (parent[value] != value)
            {
                int next = parent[value];
                parent[value] = root;
                value = next;
            }

            return root;
        }

        private static void Union(int[] parent, int first, int second)
        {
            int firstRoot = Find(parent, first);
            int secondRoot = Find(parent, second);
            if (firstRoot != secondRoot)
            {
                parent[secondRoot] = firstRoot;
            }
        }

        private sealed class LineDraft
        {
            public LineDraft(
                int sourceIndex,
                OrthogonalBoundaryLineAxis axis,
                Point3D normalizedStart,
                Point3D normalizedEnd,
                double fixedCoordinate)
            {
                SourceIndex = sourceIndex;
                Axis = axis;
                NormalizedStart = normalizedStart;
                NormalizedEnd = normalizedEnd;
                FixedCoordinate = fixedCoordinate;
            }

            public int SourceIndex { get; }

            public OrthogonalBoundaryLineAxis Axis { get; }

            public Point3D NormalizedStart { get; }

            public Point3D NormalizedEnd { get; }

            public double FixedCoordinate { get; }
        }
    }
}
