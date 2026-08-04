using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class OrthogonalBoundaryNormalizationResult
    {
        private OrthogonalBoundaryNormalizationResult(
            OrthogonalBoundaryNormalizationStatus status,
            IReadOnlyList<LineSegment3D> lines,
            IReadOnlyList<OrthogonalBoundaryLineDiagnostic> diagnostics,
            double maximumAngleDeviationDegrees,
            double maximumEndpointCorrection,
            string message)
        {
            Status = status;
            Lines = lines;
            LineDiagnostics = diagnostics;
            MaximumAngleDeviationDegrees = maximumAngleDeviationDegrees;
            MaximumEndpointCorrection = maximumEndpointCorrection;
            Message = message ?? string.Empty;
        }

        public OrthogonalBoundaryNormalizationStatus Status { get; }

        public bool IsAccepted => Status != OrthogonalBoundaryNormalizationStatus.Rejected;

        public bool WasNormalized =>
            Status == OrthogonalBoundaryNormalizationStatus.NearOrthogonal;

        public IReadOnlyList<LineSegment3D> Lines { get; }

        public IReadOnlyList<OrthogonalBoundaryLineDiagnostic> LineDiagnostics { get; }

        public double MaximumAngleDeviationDegrees { get; }

        public double MaximumEndpointCorrection { get; }

        public double PointMatchTolerance => WasNormalized
            ? Math.Max(
                GeometryTolerance.Coordinate,
                MaximumEndpointCorrection + GeometryTolerance.Coordinate)
            : GeometryTolerance.Coordinate;

        public string Message { get; }

        internal static OrthogonalBoundaryNormalizationResult Exact(
            IReadOnlyList<LineSegment3D> lines,
            IReadOnlyList<OrthogonalBoundaryLineDiagnostic> diagnostics)
        {
            return new OrthogonalBoundaryNormalizationResult(
                OrthogonalBoundaryNormalizationStatus.ExactWcs,
                lines,
                diagnostics,
                MaximumAngle(diagnostics),
                MaximumCorrection(diagnostics),
                "全部边界线已与 WCS X/Y 轴对齐。" );
        }

        internal static OrthogonalBoundaryNormalizationResult NearOrthogonal(
            IReadOnlyList<LineSegment3D> lines,
            IReadOnlyList<OrthogonalBoundaryLineDiagnostic> diagnostics,
            double maximumAngleDeviationDegrees,
            double maximumEndpointCorrection)
        {
            return new OrthogonalBoundaryNormalizationResult(
                OrthogonalBoundaryNormalizationStatus.NearOrthogonal,
                lines,
                diagnostics,
                maximumAngleDeviationDegrees,
                maximumEndpointCorrection,
                string.Format(
                    "检测到轻微方向偏差；已生成只读正交计算副本。最大角度偏差 {0:0.######}°，最大端点修正 {1:0.###} mm。原始 LINE 未修改。",
                    maximumAngleDeviationDegrees,
                    maximumEndpointCorrection));
        }

        internal static OrthogonalBoundaryNormalizationResult Rejected(
            IReadOnlyList<LineSegment3D> lines,
            IReadOnlyList<OrthogonalBoundaryLineDiagnostic> diagnostics,
            double maximumAngleDeviationDegrees,
            double maximumEndpointCorrection,
            string message)
        {
            return new OrthogonalBoundaryNormalizationResult(
                OrthogonalBoundaryNormalizationStatus.Rejected,
                lines,
                diagnostics,
                maximumAngleDeviationDegrees,
                maximumEndpointCorrection,
                message);
        }

        private static double MaximumAngle(
            IReadOnlyList<OrthogonalBoundaryLineDiagnostic> diagnostics)
        {
            double maximum = 0.0;
            if (diagnostics == null)
            {
                return maximum;
            }

            foreach (OrthogonalBoundaryLineDiagnostic diagnostic in diagnostics)
            {
                maximum = Math.Max(maximum, diagnostic.AngleDeviationDegrees);
            }

            return maximum;
        }

        private static double MaximumCorrection(
            IReadOnlyList<OrthogonalBoundaryLineDiagnostic> diagnostics)
        {
            double maximum = 0.0;
            if (diagnostics == null)
            {
                return maximum;
            }

            foreach (OrthogonalBoundaryLineDiagnostic diagnostic in diagnostics)
            {
                maximum = Math.Max(maximum, diagnostic.MaximumEndpointCorrection);
            }

            return maximum;
        }

        internal static IReadOnlyList<OrthogonalBoundaryLineDiagnostic> ReadOnlyDiagnostics(
            IList<OrthogonalBoundaryLineDiagnostic> diagnostics)
        {
            return new ReadOnlyCollection<OrthogonalBoundaryLineDiagnostic>(
                diagnostics ?? new List<OrthogonalBoundaryLineDiagnostic>());
        }

        internal static IReadOnlyList<LineSegment3D> ReadOnlyLines(
            IList<LineSegment3D> lines)
        {
            return new ReadOnlyCollection<LineSegment3D>(
                lines ?? new List<LineSegment3D>());
        }
    }
}
