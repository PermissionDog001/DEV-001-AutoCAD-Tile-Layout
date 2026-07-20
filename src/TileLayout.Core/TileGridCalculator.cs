using System;
using System.Collections.Generic;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class TileGridCalculator
    {
        public static TileLayoutResult Calculate(AxisAlignedRectangle room)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            int fullColumnCount = GetFullSpanCount(room.Width, TileLayoutRules.TileWidth);
            int fullRowCount = GetFullSpanCount(room.Height, TileLayoutRules.TileHeight);
            double eastRemainder = GetRemainder(
                room.Width,
                TileLayoutRules.TileWidth,
                fullColumnCount);
            double northRemainder = GetRemainder(
                room.Height,
                TileLayoutRules.TileHeight,
                fullRowCount);

            var divisionLines = new List<LineSegment3D>();
            AddVerticalLines(room, divisionLines);
            AddHorizontalLines(room, divisionLines);

            return new TileLayoutResult(
                room,
                fullColumnCount,
                fullRowCount,
                eastRemainder,
                northRemainder,
                divisionLines);
        }

        private static void AddVerticalLines(
            AxisAlignedRectangle room,
            ICollection<LineSegment3D> divisionLines)
        {
            for (int index = 1; ; index++)
            {
                double x = room.West + (TileLayoutRules.TileWidth * index);
                if (x >= room.East - GeometryTolerance.Coordinate)
                {
                    return;
                }

                divisionLines.Add(
                    new LineSegment3D(
                        new Point3D(x, room.South, room.Elevation),
                        new Point3D(x, room.North, room.Elevation)));
            }
        }

        private static void AddHorizontalLines(
            AxisAlignedRectangle room,
            ICollection<LineSegment3D> divisionLines)
        {
            for (int index = 1; ; index++)
            {
                double y = room.South + (TileLayoutRules.TileHeight * index);
                if (y >= room.North - GeometryTolerance.Coordinate)
                {
                    return;
                }

                divisionLines.Add(
                    new LineSegment3D(
                        new Point3D(room.West, y, room.Elevation),
                        new Point3D(room.East, y, room.Elevation)));
            }
        }

        private static int GetFullSpanCount(double length, double tileSize)
        {
            double quotient = length / tileSize;
            double nearestInteger = Math.Round(quotient);

            if (Math.Abs(length - (nearestInteger * tileSize))
                <= GeometryTolerance.Coordinate)
            {
                return checked((int)nearestInteger);
            }

            return checked((int)Math.Floor(quotient));
        }

        private static double GetRemainder(
            double length,
            double tileSize,
            int fullSpanCount)
        {
            double remainder = length - (fullSpanCount * tileSize);
            if (Math.Abs(remainder) <= GeometryTolerance.Coordinate)
            {
                return 0.0;
            }

            return remainder;
        }
    }
}
