using System;

namespace TileLayout.Core
{
    public sealed class TileLayoutParameters
    {
        public TileLayoutParameters(double tileWidth, double tileHeight)
            : this(tileWidth, tileHeight, TileLayoutStartCorner.SouthWest)
        {
        }

        public TileLayoutParameters(
            double tileWidth,
            double tileHeight,
            TileLayoutStartCorner startCorner)
        {
            ValidateTileSize(tileWidth, nameof(tileWidth));
            ValidateTileSize(tileHeight, nameof(tileHeight));
            ValidateStartCorner(startCorner);

            TileWidth = tileWidth;
            TileHeight = tileHeight;
            StartCorner = startCorner;
        }

        public double TileWidth { get; }

        public double TileHeight { get; }

        public TileLayoutStartCorner StartCorner { get; }

        public bool StartsFromEast =>
            StartCorner == TileLayoutStartCorner.SouthEast
            || StartCorner == TileLayoutStartCorner.NorthEast;

        public bool StartsFromNorth =>
            StartCorner == TileLayoutStartCorner.NorthWest
            || StartCorner == TileLayoutStartCorner.NorthEast;

        private static void ValidateTileSize(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Tile dimensions must be finite numbers.");
            }

            if (value <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Tile dimensions must be greater than the coordinate tolerance.");
            }
        }

        private static void ValidateStartCorner(TileLayoutStartCorner startCorner)
        {
            if (!Enum.IsDefined(typeof(TileLayoutStartCorner), startCorner))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startCorner),
                    "The start corner must be one of the four WCS room corners.");
            }
        }
    }
}
