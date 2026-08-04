using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public enum LayoutDrawingLineSemantic
    {
        Division,
        Connection,
        FinishedFaceOutline,
        GroutBoundary
    }

    public sealed class LayoutDrawingLine
    {
        internal LayoutDrawingLine(
            string id,
            LineSegment3D geometry,
            LayoutDrawingLineSemantic semantic)
        {
            Id = id;
            Geometry = geometry;
            Semantic = semantic;
        }

        public string Id { get; }

        public LineSegment3D Geometry { get; }

        public LayoutDrawingLineSemantic Semantic { get; }
    }

    public sealed class LayoutDrawingTile
    {
        internal LayoutDrawingTile(
            string id,
            IList<Point3D> outline,
            TileClassification classification,
            bool isFullTile,
            bool isContinuousIrregular,
            double nominalWidth,
            double nominalHeight,
            IList<RoomSide> boundarySides,
            ProjectCutStatus assessmentStatus,
            string assessmentReason,
            IList<LayoutDrawingCutMeasurement> cutMeasurements,
            bool isEntranceVisualZone = false,
            bool isEntranceVisualBlind = false)
        {
            Id = id;
            Outline = new ReadOnlyCollection<Point3D>(outline);
            Classification = classification;
            IsFullTile = isFullTile;
            IsContinuousIrregular = isContinuousIrregular;
            NominalWidth = nominalWidth;
            NominalHeight = nominalHeight;
            BoundarySides = new ReadOnlyCollection<RoomSide>(boundarySides);
            AssessmentStatus = assessmentStatus;
            AssessmentReason = assessmentReason ?? string.Empty;
            CutMeasurements = new ReadOnlyCollection<LayoutDrawingCutMeasurement>(
                cutMeasurements);
            IsEntranceVisualZone = isEntranceVisualZone;
            IsEntranceVisualBlind = isEntranceVisualBlind;
        }

        public string Id { get; }

        public IReadOnlyList<Point3D> Outline { get; }

        public TileClassification Classification { get; }

        public bool IsFullTile { get; }

        public bool IsContinuousIrregular { get; }

        public double NominalWidth { get; }

        public double NominalHeight { get; }

        public IReadOnlyList<RoomSide> BoundarySides { get; }

        public ProjectCutStatus AssessmentStatus { get; }

        public string AssessmentReason { get; }

        public IReadOnlyList<LayoutDrawingCutMeasurement> CutMeasurements { get; }

        public bool IsEntranceVisualZone { get; }

        public bool IsEntranceVisualBlind { get; }

        public bool IsBelowRecommended => CutMeasurements.Any(measurement =>
            measurement.Status == ProjectCutStatus.RequiresProjectPolicy
                || measurement.Status == ProjectCutStatus.RequiresUserReview
                || measurement.Status
                    == ProjectCutStatus.BelowProjectAbsoluteMinimum);

        public bool HasApplicableBoundaryCut => CutMeasurements.Count > 0;
    }

    public sealed class LayoutDrawingCutMeasurement
    {
        internal LayoutDrawingCutMeasurement(
            TileLayoutAxis axis,
            double actualValue,
            double recommendedMinimum,
            double? projectAbsoluteMinimum,
            ProjectCutStatus status)
        {
            Axis = axis;
            ActualValue = actualValue;
            RecommendedMinimum = recommendedMinimum;
            ProjectAbsoluteMinimum = projectAbsoluteMinimum;
            Status = status;
        }

        public TileLayoutAxis Axis { get; }

        public double ActualValue { get; }

        public double RecommendedMinimum { get; }

        public double? ProjectAbsoluteMinimum { get; }

        public ProjectCutStatus Status { get; }
    }

    public sealed class LayoutDrawingRegion
    {
        internal LayoutDrawingRegion(
            string id,
            LayoutRegionRole role,
            AxisAlignedRectangle bounds)
        {
            Id = id;
            Role = role;
            Bounds = bounds;
        }

        public string Id { get; }

        public LayoutRegionRole Role { get; }

        public AxisAlignedRectangle Bounds { get; }
    }

    public sealed class LayoutDrawingNeutralRegion
    {
        internal LayoutDrawingNeutralRegion(
            string id,
            AxisAlignedRectangle bounds,
            double area)
        {
            Id = id;
            Bounds = bounds;
            Area = area;
        }

        public string Id { get; }

        public AxisAlignedRectangle Bounds { get; }

        public double Area { get; }
    }

    public sealed class LayoutDrawingWallCorner
    {
        internal LayoutDrawingWallCorner(
            string id,
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

    public sealed class LayoutDrawingPlan
    {
        internal LayoutDrawingPlan(
            string candidateId,
            LayoutCandidateState candidateState,
            double west,
            double east,
            double south,
            double north,
            double elevation,
            IList<Point3D> roomOutline,
            IList<LayoutDrawingLine> divisionLines,
            IList<LayoutDrawingTile> tiles,
            IList<LayoutDrawingRegion> regions,
            IList<LayoutDrawingLine> connections,
            IList<LayoutDrawingNeutralRegion> neutralRegions,
            IList<LayoutDrawingLine> neutralConnections,
            IList<LayoutDrawingWallCorner> wallCorners,
            double sourceWest = double.NaN,
            double sourceEast = double.NaN,
            double sourceSouth = double.NaN,
            double sourceNorth = double.NaN,
            double groutWidthMm = 0.0,
            double plasterThicknessMm = 0.0)
        {
            CandidateId = candidateId;
            CandidateState = candidateState;
            West = west;
            East = east;
            South = south;
            North = north;
            SourceWest = double.IsNaN(sourceWest) ? west : sourceWest;
            SourceEast = double.IsNaN(sourceEast) ? east : sourceEast;
            SourceSouth = double.IsNaN(sourceSouth) ? south : sourceSouth;
            SourceNorth = double.IsNaN(sourceNorth) ? north : sourceNorth;
            GroutWidthMm = groutWidthMm;
            PlasterThicknessMm = plasterThicknessMm;
            Elevation = elevation;
            RoomOutline = new ReadOnlyCollection<Point3D>(roomOutline);
            DivisionLines = new ReadOnlyCollection<LayoutDrawingLine>(divisionLines);
            Tiles = new ReadOnlyCollection<LayoutDrawingTile>(tiles);
            Regions = new ReadOnlyCollection<LayoutDrawingRegion>(regions);
            Connections = new ReadOnlyCollection<LayoutDrawingLine>(connections);
            NeutralRegions = new ReadOnlyCollection<LayoutDrawingNeutralRegion>(
                neutralRegions);
            NeutralConnections = new ReadOnlyCollection<LayoutDrawingLine>(
                neutralConnections);
            WallCorners = new ReadOnlyCollection<LayoutDrawingWallCorner>(
                wallCorners);
        }

        public string CandidateId { get; }

        public LayoutCandidateState CandidateState { get; }

        public double West { get; }

        public double East { get; }

        public double South { get; }

        public double North { get; }

        /// <summary>
        /// Original selected room range used for duplicate protection.  West,
        /// East, South and North may instead describe the inward finished face.
        /// </summary>
        public double SourceWest { get; }

        public double SourceEast { get; }

        public double SourceSouth { get; }

        public double SourceNorth { get; }

        public double GroutWidthMm { get; }

        public double PlasterThicknessMm { get; }

        public double Elevation { get; }

        public double Width => East - West;

        public double Height => North - South;

        public IReadOnlyList<Point3D> RoomOutline { get; }

        public IReadOnlyList<LayoutDrawingLine> DivisionLines { get; }

        public IReadOnlyList<LayoutDrawingTile> Tiles { get; }

        public IReadOnlyList<LayoutDrawingRegion> Regions { get; }

        public IReadOnlyList<LayoutDrawingLine> Connections { get; }

        public IReadOnlyList<LayoutDrawingNeutralRegion> NeutralRegions { get; }

        public IReadOnlyList<LayoutDrawingLine> NeutralConnections { get; }

        public IReadOnlyList<LayoutDrawingWallCorner> WallCorners { get; }
    }
}
