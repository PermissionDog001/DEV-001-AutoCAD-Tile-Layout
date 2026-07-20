using System;

namespace TileLayout.Core.Models
{
    public sealed class AxisAlignedRectangle
    {
        public AxisAlignedRectangle(
            double west,
            double east,
            double south,
            double north,
            double elevation = 0.0)
        {
            if (!IsFinite(west)
                || !IsFinite(east)
                || !IsFinite(south)
                || !IsFinite(north)
                || !IsFinite(elevation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(west),
                    "Rectangle coordinates must be finite numbers.");
            }

            if (east - west <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(east),
                    "East must be greater than west by more than the coordinate tolerance.");
            }

            if (north - south <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(north),
                    "North must be greater than south by more than the coordinate tolerance.");
            }

            West = west;
            East = east;
            South = south;
            North = north;
            Elevation = elevation;
        }

        public double West { get; }

        public double East { get; }

        public double South { get; }

        public double North { get; }

        public double Elevation { get; }

        public double Width => East - West;

        public double Height => North - South;

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
