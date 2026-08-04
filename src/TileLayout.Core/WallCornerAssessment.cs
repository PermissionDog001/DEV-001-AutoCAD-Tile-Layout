using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public enum WallCornerGeometryType
    {
        Convex90,
        Reflex270
    }

    public sealed class WallCornerAssessment
    {
        internal WallCornerAssessment(
            string id,
            int vertexIndex,
            Point3D position,
            WallCornerGeometryType geometryType,
            bool isOptimizationTarget,
            bool hasVerticalSeam,
            bool hasHorizontalSeam,
            double? nearestVerticalSeamDistance,
            double? nearestHorizontalSeamDistance,
            string reason,
            bool hasSafeVerticalSeam = false,
            bool hasSafeHorizontalSeam = false,
            double? verticalAdjacentSpanA = null,
            double? verticalAdjacentSpanB = null,
            double? horizontalAdjacentSpanA = null,
            double? horizontalAdjacentSpanB = null)
        {
            Id = id;
            VertexIndex = vertexIndex;
            Position = position;
            GeometryType = geometryType;
            IsOptimizationTarget = isOptimizationTarget;
            HasVerticalSeam = hasVerticalSeam;
            HasHorizontalSeam = hasHorizontalSeam;
            NearestVerticalSeamDistance = nearestVerticalSeamDistance;
            NearestHorizontalSeamDistance = nearestHorizontalSeamDistance;
            HasSafeVerticalSeam = hasSafeVerticalSeam;
            HasSafeHorizontalSeam = hasSafeHorizontalSeam;
            VerticalAdjacentSpanA = verticalAdjacentSpanA;
            VerticalAdjacentSpanB = verticalAdjacentSpanB;
            HorizontalAdjacentSpanA = horizontalAdjacentSpanA;
            HorizontalAdjacentSpanB = horizontalAdjacentSpanB;
            Reason = reason ?? string.Empty;
        }

        public string Id { get; }

        public int VertexIndex { get; }

        public Point3D Position { get; }

        public WallCornerGeometryType GeometryType { get; }

        public bool IsOptimizationTarget { get; }

        public bool HasVerticalSeam { get; }

        public bool HasHorizontalSeam { get; }

        public bool HasAnyExactSeam => HasVerticalSeam || HasHorizontalSeam;

        public bool IsExactGridIntersection =>
            HasVerticalSeam && HasHorizontalSeam;

        public double? NearestVerticalSeamDistance { get; }

        public double? NearestHorizontalSeamDistance { get; }

        public bool HasSafeVerticalSeam { get; }

        public bool HasSafeHorizontalSeam { get; }

        public double? VerticalAdjacentSpanA { get; }

        public double? VerticalAdjacentSpanB { get; }

        public double? HorizontalAdjacentSpanA { get; }

        public double? HorizontalAdjacentSpanB { get; }

        public bool IsSafeDoubleAlignment => IsOptimizationTarget
            && IsExactGridIntersection
            && HasSafeVerticalSeam
            && HasSafeHorizontalSeam;

        public bool IsSafeSingleAlignment => IsOptimizationTarget
            && HasAnyExactSeam
            && !IsSafeDoubleAlignment
            && ((HasVerticalSeam && HasSafeVerticalSeam)
                || (HasHorizontalSeam && HasSafeHorizontalSeam));

        public string Reason { get; }
    }
}
