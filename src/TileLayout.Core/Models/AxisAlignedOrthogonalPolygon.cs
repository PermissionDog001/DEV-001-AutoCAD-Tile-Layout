using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TileLayout.Core.Models
{
    public sealed class AxisAlignedOrthogonalPolygon
    {
        internal AxisAlignedOrthogonalPolygon(
            IList<Point3D> vertices,
            double elevation)
        {
            Vertices = new ReadOnlyCollection<Point3D>(vertices);
            Elevation = elevation;

            double west = vertices[0].X;
            double east = vertices[0].X;
            double south = vertices[0].Y;
            double north = vertices[0].Y;
            foreach (Point3D vertex in vertices)
            {
                if (vertex.X < west)
                {
                    west = vertex.X;
                }

                if (vertex.X > east)
                {
                    east = vertex.X;
                }

                if (vertex.Y < south)
                {
                    south = vertex.Y;
                }

                if (vertex.Y > north)
                {
                    north = vertex.Y;
                }
            }

            West = west;
            East = east;
            South = south;
            North = north;
        }

        public IReadOnlyList<Point3D> Vertices { get; }

        public double West { get; }

        public double East { get; }

        public double South { get; }

        public double North { get; }

        public double Elevation { get; }

        public double Width => East - West;

        public double Height => North - South;
    }
}
