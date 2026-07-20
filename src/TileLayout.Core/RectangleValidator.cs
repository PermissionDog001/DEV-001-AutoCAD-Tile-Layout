using System;
using System.Collections.Generic;
using System.Linq;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class RectangleValidator
    {
        public static RectangleValidationResult Validate(
            IReadOnlyCollection<LineSegment3D> lines)
        {
            if (lines == null || lines.Count != 4)
            {
                return RectangleValidationResult.Failure(
                    RectangleValidationError.IncorrectLineCount,
                    "必须且只能提供四条线。");
            }

            LineSegment3D[] lineArray = lines.ToArray();
            Point3D[] points = lineArray
                .SelectMany(line => new[] { line.Start, line.End })
                .ToArray();

            if (points.Any(point => !IsFinite(point.X)
                || !IsFinite(point.Y)
                || !IsFinite(point.Z)))
            {
                return RectangleValidationResult.Failure(
                    RectangleValidationError.NonFiniteCoordinate,
                    "线端点坐标必须是有限数值。");
            }

            double elevation = points[0].Z;
            if (points.Any(point => !GeometryTolerance.NearlyEqual(point.Z, elevation)))
            {
                return RectangleValidationResult.Failure(
                    RectangleValidationError.NonCoplanar,
                    "四条线必须位于同一 WCS 高程。");
            }

            foreach (LineSegment3D line in lineArray)
            {
                double deltaX = Math.Abs(line.End.X - line.Start.X);
                double deltaY = Math.Abs(line.End.Y - line.Start.Y);

                if (deltaX <= GeometryTolerance.Coordinate
                    && deltaY <= GeometryTolerance.Coordinate)
                {
                    return RectangleValidationResult.Failure(
                        RectangleValidationError.DegenerateLine,
                        "边界中存在长度小于等于公差的线。");
                }

                if (deltaX > GeometryTolerance.Coordinate
                    && deltaY > GeometryTolerance.Coordinate)
                {
                    return RectangleValidationResult.Failure(
                        RectangleValidationError.NonAxisAlignedLine,
                        "四条线必须与 WCS X/Y 轴平行。");
                }
            }

            double west = points.Min(point => point.X);
            double east = points.Max(point => point.X);
            double south = points.Min(point => point.Y);
            double north = points.Max(point => point.Y);

            if (east - west <= GeometryTolerance.Coordinate
                || north - south <= GeometryTolerance.Coordinate)
            {
                return RectangleValidationResult.Failure(
                    RectangleValidationError.NonPositiveDimensions,
                    "矩形宽度和高度必须大于公差。");
            }

            var sides = new bool[4]; // south, east, north, west
            foreach (LineSegment3D line in lineArray)
            {
                int side = ClassifySide(line, west, east, south, north);
                if (side < 0)
                {
                    return RectangleValidationResult.Failure(
                        RectangleValidationError.NonClosedBoundary,
                        "四条线必须完整连接矩形四角并形成闭合边界。");
                }

                if (sides[side])
                {
                    return RectangleValidationResult.Failure(
                        RectangleValidationError.DuplicateOrMissingSide,
                        "矩形边界存在重复边或缺失边。");
                }

                sides[side] = true;
            }

            if (sides.Any(side => !side))
            {
                return RectangleValidationResult.Failure(
                    RectangleValidationError.DuplicateOrMissingSide,
                    "矩形边界存在重复边或缺失边。");
            }

            return RectangleValidationResult.Success(
                new AxisAlignedRectangle(west, east, south, north, elevation));
        }

        private static int ClassifySide(
            LineSegment3D line,
            double west,
            double east,
            double south,
            double north)
        {
            double deltaX = Math.Abs(line.End.X - line.Start.X);
            double deltaY = Math.Abs(line.End.Y - line.Start.Y);
            bool horizontal = deltaX > GeometryTolerance.Coordinate
                && deltaY <= GeometryTolerance.Coordinate;
            bool vertical = deltaY > GeometryTolerance.Coordinate
                && deltaX <= GeometryTolerance.Coordinate;

            if (horizontal && Spans(line.Start.X, line.End.X, west, east))
            {
                if (GeometryTolerance.NearlyEqual(line.Start.Y, south)
                    && GeometryTolerance.NearlyEqual(line.End.Y, south))
                {
                    return 0;
                }

                if (GeometryTolerance.NearlyEqual(line.Start.Y, north)
                    && GeometryTolerance.NearlyEqual(line.End.Y, north))
                {
                    return 2;
                }
            }
            else if (vertical && Spans(line.Start.Y, line.End.Y, south, north))
            {
                if (GeometryTolerance.NearlyEqual(line.Start.X, east)
                    && GeometryTolerance.NearlyEqual(line.End.X, east))
                {
                    return 1;
                }

                if (GeometryTolerance.NearlyEqual(line.Start.X, west)
                    && GeometryTolerance.NearlyEqual(line.End.X, west))
                {
                    return 3;
                }
            }

            return -1;
        }

        private static bool Spans(
            double first,
            double second,
            double minimum,
            double maximum)
        {
            return GeometryTolerance.NearlyEqual(Math.Min(first, second), minimum)
                && GeometryTolerance.NearlyEqual(Math.Max(first, second), maximum);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
