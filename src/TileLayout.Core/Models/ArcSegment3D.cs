namespace TileLayout.Core.Models
{
    public struct ArcSegment3D
    {
        public ArcSegment3D(
            Point3D center,
            Point3D start,
            Point3D end,
            double radius)
        {
            Center = center;
            Start = start;
            End = end;
            Radius = radius;
        }

        public Point3D Center { get; }

        public Point3D Start { get; }

        public Point3D End { get; }

        public double Radius { get; }
    }
}
