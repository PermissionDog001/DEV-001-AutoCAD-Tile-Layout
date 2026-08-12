using System.Globalization;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public enum LayoutDrawingDimensionKind
    {
        TileSize,
        BoundaryFeature
    }

    public sealed class LayoutDrawingDimension
    {
        internal LayoutDrawingDimension(
            string id,
            string sourceId,
            LineSegment3D measuredSegment,
            Point3D dimensionLinePoint,
            TileLayoutAxis axis,
            LayoutDrawingDimensionKind kind,
            int displayMillimetres)
        {
            Id = id;
            SourceId = sourceId;
            MeasuredSegment = measuredSegment;
            DimensionLinePoint = dimensionLinePoint;
            Axis = axis;
            Kind = kind;
            DisplayMillimetres = displayMillimetres;
        }

        public string Id { get; }

        public string SourceId { get; }

        public LineSegment3D MeasuredSegment { get; }

        public Point3D DimensionLinePoint { get; }

        public TileLayoutAxis Axis { get; }

        public LayoutDrawingDimensionKind Kind { get; }

        public int DisplayMillimetres { get; }

        public string DisplayText => DisplayMillimetres.ToString(
            CultureInfo.InvariantCulture);

        public double ActualMillimetres => Axis == TileLayoutAxis.X
            ? System.Math.Abs(
                MeasuredSegment.End.X - MeasuredSegment.Start.X)
            : System.Math.Abs(
                MeasuredSegment.End.Y - MeasuredSegment.Start.Y);
    }
}
