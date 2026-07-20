namespace TileLayout.Core.Models
{
    public struct LineSegment3D
    {
        public LineSegment3D(Point3D start, Point3D end)
        {
            Start = start;
            End = end;
        }

        public Point3D Start { get; }

        public Point3D End { get; }
    }
}
