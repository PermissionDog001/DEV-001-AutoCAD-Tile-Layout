namespace TileLayout.Core.Models
{
    public struct Point3D
    {
        public Point3D(double x, double y, double z = 0.0)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }
    }
}
