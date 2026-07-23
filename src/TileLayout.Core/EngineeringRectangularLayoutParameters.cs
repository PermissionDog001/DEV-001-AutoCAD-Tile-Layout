using System;

namespace TileLayout.Core
{
    public sealed class EngineeringRectangularLayoutParameters
    {
        public EngineeringRectangularLayoutParameters(
            double tileWidth,
            double tileHeight,
            DoorOpening doorOpening)
        {
            ValidateTileSize(tileWidth, nameof(tileWidth));
            ValidateTileSize(tileHeight, nameof(tileHeight));

            DoorOpening = doorOpening
                ?? throw new ArgumentNullException(nameof(doorOpening));
            TileWidth = tileWidth;
            TileHeight = tileHeight;
        }

        public double TileWidth { get; }

        public double TileHeight { get; }

        public DoorOpening DoorOpening { get; }

        public double MinimumCutRatio =>
            EngineeringLayoutRules.DefaultMinimumCutRatio;

        public double HalfTileRatio => EngineeringLayoutRules.HalfTileRatio;

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
    }
}
