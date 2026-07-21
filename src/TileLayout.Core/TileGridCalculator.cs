using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class TileGridCalculator
    {
        public static TileLayoutResult Calculate(AxisAlignedRectangle room)
        {
            return CalculateCore(
                room,
                TileLayoutRules.Fixed600Parameters,
                null);
        }

        public static TileLayoutResult Calculate(
            AxisAlignedRectangle room,
            TileLayoutParameters parameters)
        {
            return CalculateCore(
                room,
                parameters,
                TileLayoutRules.MaximumParameterizedDivisionLineCount);
        }

        private static TileLayoutResult CalculateCore(
            AxisAlignedRectangle room,
            TileLayoutParameters parameters,
            int? maximumDivisionLineCount)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            SpanMetrics columns = GetSpanMetrics(room.Width, parameters.TileWidth);
            SpanMetrics rows = GetSpanMetrics(room.Height, parameters.TileHeight);
            double estimatedDivisionLineCount =
                columns.InternalLineCount + rows.InternalLineCount;

            if (maximumDivisionLineCount.HasValue
                && estimatedDivisionLineCount > maximumDivisionLineCount.Value)
            {
                throw new TileLayoutLimitExceededException(
                    estimatedDivisionLineCount,
                    maximumDivisionLineCount.Value);
            }

            int divisionLineCount = checked((int)estimatedDivisionLineCount);
            int fullColumnCount = checked((int)columns.FullSpanCount);
            int fullRowCount = checked((int)rows.FullSpanCount);
            int verticalLineCount = checked((int)columns.InternalLineCount);
            int horizontalLineCount = checked((int)rows.InternalLineCount);

            var divisionLines = new List<LineSegment3D>(divisionLineCount);
            AddVerticalLines(
                room,
                parameters.TileWidth,
                verticalLineCount,
                parameters.StartsFromEast,
                divisionLines);
            AddHorizontalLines(
                room,
                parameters.TileHeight,
                horizontalLineCount,
                parameters.StartsFromNorth,
                divisionLines);

            return new TileLayoutResult(
                room,
                parameters,
                fullColumnCount,
                fullRowCount,
                columns.Remainder,
                rows.Remainder,
                divisionLines);
        }

        private static void AddVerticalLines(
            AxisAlignedRectangle room,
            double tileWidth,
            int lineCount,
            bool startsFromEast,
            ICollection<LineSegment3D> divisionLines)
        {
            for (int index = 1; index <= lineCount; index++)
            {
                double offset = tileWidth * index;
                double x = startsFromEast
                    ? room.East - offset
                    : room.West + offset;

                divisionLines.Add(
                    new LineSegment3D(
                        new Point3D(x, room.South, room.Elevation),
                        new Point3D(x, room.North, room.Elevation)));
            }
        }

        private static void AddHorizontalLines(
            AxisAlignedRectangle room,
            double tileHeight,
            int lineCount,
            bool startsFromNorth,
            ICollection<LineSegment3D> divisionLines)
        {
            for (int index = 1; index <= lineCount; index++)
            {
                double offset = tileHeight * index;
                double y = startsFromNorth
                    ? room.North - offset
                    : room.South + offset;

                divisionLines.Add(
                    new LineSegment3D(
                        new Point3D(room.West, y, room.Elevation),
                        new Point3D(room.East, y, room.Elevation)));
            }
        }

        private static SpanMetrics GetSpanMetrics(double length, double tileSize)
        {
            double quotient = length / tileSize;
            double nearestInteger = Math.Round(quotient);
            bool isMultipleWithinTolerance =
                !double.IsInfinity(quotient)
                && Math.Abs(length - (nearestInteger * tileSize))
                    <= GeometryTolerance.Coordinate;

            double fullSpanCount = isMultipleWithinTolerance
                ? nearestInteger
                : Math.Floor(quotient);
            double internalLineCount = isMultipleWithinTolerance
                ? Math.Max(0.0, nearestInteger - 1.0)
                : fullSpanCount;
            double remainder = isMultipleWithinTolerance
                ? 0.0
                : length - (fullSpanCount * tileSize);

            return new SpanMetrics(fullSpanCount, internalLineCount, remainder);
        }

        private struct SpanMetrics
        {
            public SpanMetrics(
                double fullSpanCount,
                double internalLineCount,
                double remainder)
            {
                FullSpanCount = fullSpanCount;
                InternalLineCount = internalLineCount;
                Remainder = remainder;
            }

            public double FullSpanCount { get; }

            public double InternalLineCount { get; }

            public double Remainder { get; }
        }
    }
}
