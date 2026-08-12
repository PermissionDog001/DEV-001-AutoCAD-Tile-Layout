using System;
using System.Collections.Generic;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    internal static class GuidedBoundaryPolylineConverter
    {
        internal static IReadOnlyCollection<LineSegment3D> BuildSegments(
            IReadOnlyList<Point3D> inputVertices,
            bool allowNearEndpointClosure = false)
        {
            if (inputVertices == null)
            {
                throw new ArgumentNullException(nameof(inputVertices));
            }

            var vertices = new List<Point3D>(inputVertices.Count);
            for (int index = 0; index < inputVertices.Count; index++)
            {
                Point3D point = inputVertices[index];
                if (vertices.Count == 0
                    || !SamePoint(vertices[vertices.Count - 1], point))
                {
                    vertices.Add(point);
                }
            }

            while (vertices.Count > 1
                && (SamePoint(vertices[0], vertices[vertices.Count - 1])
                    || allowNearEndpointClosure
                        && NearEndpoint(
                            vertices[0],
                            vertices[vertices.Count - 1])))
            {
                vertices.RemoveAt(vertices.Count - 1);
            }

            if (vertices.Count < 4)
            {
                throw new ArgumentException(
                    "A closed polyline requires at least four deterministic vertices.",
                    nameof(inputVertices));
            }

            var segments = new List<LineSegment3D>(vertices.Count);
            for (int index = 0; index < vertices.Count; index++)
            {
                segments.Add(
                    new LineSegment3D(
                        vertices[index],
                        vertices[(index + 1) % vertices.Count]));
            }

            return segments;
        }

        private static bool SamePoint(Point3D first, Point3D second)
        {
            return Math.Abs(first.X - second.X)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Y - second.Y)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Z - second.Z)
                    <= GeometryTolerance.Coordinate;
        }

        private static bool NearEndpoint(Point3D first, Point3D second)
        {
            if (Math.Abs(first.Z - second.Z)
                > GeometryTolerance.Coordinate)
            {
                return false;
            }

            double deltaX = first.X - second.X;
            double deltaY = first.Y - second.Y;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY))
                <= GeometryTolerance.NearOrthogonalEndpointJoinTolerance;
        }
    }
}
