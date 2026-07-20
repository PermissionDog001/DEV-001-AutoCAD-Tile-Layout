using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    internal static class TestGeometry
    {
        public static IReadOnlyCollection<LineSegment3D> RectangleLines(
            double west,
            double east,
            double south,
            double north,
            double elevation = 0.0)
        {
            var southWest = new Point3D(west, south, elevation);
            var southEast = new Point3D(east, south, elevation);
            var northEast = new Point3D(east, north, elevation);
            var northWest = new Point3D(west, north, elevation);

            return new[]
            {
                new LineSegment3D(northEast, southEast),
                new LineSegment3D(southWest, southEast),
                new LineSegment3D(northWest, southWest),
                new LineSegment3D(northEast, northWest)
            };
        }
    }
}
