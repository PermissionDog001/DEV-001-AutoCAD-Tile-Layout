using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class OrthogonalRoomOffsetter
    {
        public static OrthogonalRoomOffsetResult Offset(
            AxisAlignedOrthogonalPolygon room,
            double thickness)
        {
            if (room == null)
            {
                return OrthogonalRoomOffsetResult.Failure(
                    "A validated orthogonal room is required before an inward offset can be calculated.");
            }

            if (double.IsNaN(thickness)
                || double.IsInfinity(thickness)
                || thickness < 0.0)
            {
                return OrthogonalRoomOffsetResult.Failure(
                    "Plaster thickness must be a finite non-negative number.");
            }

            if (thickness <= GeometryTolerance.Coordinate)
            {
                return OrthogonalRoomOffsetResult.Success(room);
            }

            if (room.Vertices == null || room.Vertices.Count < 4)
            {
                return OrthogonalRoomOffsetResult.Failure(
                    "The room boundary cannot produce a valid finished face.");
            }

            var shiftedEdges = new List<ShiftedEdge>(room.Vertices.Count);
            for (int index = 0; index < room.Vertices.Count; index++)
            {
                Point3D start = room.Vertices[index];
                Point3D end = room.Vertices[(index + 1) % room.Vertices.Count];
                ShiftedEdge shifted;
                if (!TryShiftEdge(start, end, thickness, out shifted))
                {
                    return OrthogonalRoomOffsetResult.Failure(
                        "The finished face offset requires finite WCS axis-aligned boundary edges.");
                }

                shiftedEdges.Add(shifted);
            }

            var vertices = new List<Point3D>(room.Vertices.Count);
            for (int index = 0; index < shiftedEdges.Count; index++)
            {
                ShiftedEdge previous = shiftedEdges[
                    (index - 1 + shiftedEdges.Count) % shiftedEdges.Count];
                ShiftedEdge next = shiftedEdges[index];
                Point3D intersection;
                if (!TryIntersect(previous, next, room.Elevation, out intersection))
                {
                    return OrthogonalRoomOffsetResult.Failure(
                        "The finished face offset produced parallel or invalid adjacent edges.");
                }

                vertices.Add(intersection);
            }

            if (!PreservesBoundaryEdgeOrder(shiftedEdges, vertices))
            {
                return OrthogonalRoomOffsetResult.Failure(
                    "The finished face offset collapses or reverses a boundary edge.");
            }

            var lines = new List<LineSegment3D>(vertices.Count);
            for (int index = 0; index < vertices.Count; index++)
            {
                lines.Add(
                    new LineSegment3D(
                        vertices[index],
                        vertices[(index + 1) % vertices.Count]));
            }

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);
            if (!validation.IsValid)
            {
                return OrthogonalRoomOffsetResult.Failure(
                    "The finished face offset is invalid: "
                        + validation.ErrorMessage);
            }

            if (validation.Room.Width <= GeometryTolerance.Coordinate
                || validation.Room.Height <= GeometryTolerance.Coordinate)
            {
                return OrthogonalRoomOffsetResult.Failure(
                    "The finished face offset leaves no usable room area.");
            }

            return OrthogonalRoomOffsetResult.Success(validation.Room);
        }

        private static bool PreservesBoundaryEdgeOrder(
            IReadOnlyList<ShiftedEdge> shiftedEdges,
            IReadOnlyList<Point3D> vertices)
        {
            for (int index = 0; index < shiftedEdges.Count; index++)
            {
                ShiftedEdge edge = shiftedEdges[index];
                Point3D start = vertices[index];
                Point3D end = vertices[(index + 1) % vertices.Count];
                double actualStart = edge.IsVertical
                    ? start.Y
                    : start.X;
                double actualEnd = edge.IsVertical
                    ? end.Y
                    : end.X;
                double originalDelta = edge.EndCoordinate
                    - edge.StartCoordinate;
                double actualDelta = actualEnd - actualStart;
                if (originalDelta > GeometryTolerance.Coordinate
                    && actualDelta <= GeometryTolerance.Coordinate)
                {
                    return false;
                }

                if (originalDelta < -GeometryTolerance.Coordinate
                    && actualDelta >= -GeometryTolerance.Coordinate)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryShiftEdge(
            Point3D start,
            Point3D end,
            double thickness,
            out ShiftedEdge shifted)
        {
            shifted = default(ShiftedEdge);
            if (!IsFinite(start) || !IsFinite(end))
            {
                return false;
            }

            double deltaX = end.X - start.X;
            double deltaY = end.Y - start.Y;
            if (Math.Abs(deltaX) <= GeometryTolerance.Coordinate
                && Math.Abs(deltaY) <= GeometryTolerance.Coordinate)
            {
                return false;
            }

            if (Math.Abs(deltaX) > GeometryTolerance.Coordinate
                && Math.Abs(deltaY) > GeometryTolerance.Coordinate)
            {
                return false;
            }

            if (Math.Abs(deltaX) > GeometryTolerance.Coordinate)
            {
                double shiftedY = start.Y
                    + (deltaX > 0.0 ? thickness : -thickness);
                shifted = new ShiftedEdge(
                    false,
                    shiftedY,
                    start.X,
                    end.X);
            }
            else
            {
                double shiftedX = start.X
                    + (deltaY < 0.0 ? thickness : -thickness);
                shifted = new ShiftedEdge(
                    true,
                    shiftedX,
                    start.Y,
                    end.Y);
            }

            return true;
        }

        private static bool TryIntersect(
            ShiftedEdge first,
            ShiftedEdge second,
            double elevation,
            out Point3D point)
        {
            point = default(Point3D);
            if (first.IsVertical == second.IsVertical)
            {
                return false;
            }

            ShiftedEdge vertical = first.IsVertical ? first : second;
            ShiftedEdge horizontal = first.IsVertical ? second : first;
            double x = vertical.FixedCoordinate;
            double y = horizontal.FixedCoordinate;
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(elevation))
            {
                return false;
            }

            point = new Point3D(x, y, elevation);
            return true;
        }

        private static bool IsFinite(Point3D point)
        {
            return IsFinite(point.X)
                && IsFinite(point.Y)
                && IsFinite(point.Z);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value);
        }

        private struct ShiftedEdge
        {
            public ShiftedEdge(
                bool isVertical,
                double fixedCoordinate,
                double startCoordinate,
                double endCoordinate)
            {
                IsVertical = isVertical;
                FixedCoordinate = fixedCoordinate;
                StartCoordinate = startCoordinate;
                EndCoordinate = endCoordinate;
            }

            public bool IsVertical { get; }

            public double FixedCoordinate { get; }

            public double StartCoordinate { get; }

            public double EndCoordinate { get; }
        }
    }
}
