using System;

namespace TileLayout.Core
{
    public sealed class EngineeringRectangularLayoutParameters
    {
        public EngineeringRectangularLayoutParameters(
            double tileWidth,
            double tileHeight,
            DoorOpening doorOpening,
            double groutWidthMm = 0.0)
        {
            ValidateTileSize(tileWidth, nameof(tileWidth));
            ValidateTileSize(tileHeight, nameof(tileHeight));
            ValidateNonNegativeFinite(groutWidthMm, nameof(groutWidthMm));

            DoorOpening = doorOpening
                ?? throw new ArgumentNullException(nameof(doorOpening));
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            GroutWidthMm = groutWidthMm;
        }

        public double TileWidth { get; }

        public double TileHeight { get; }

        public double GroutWidthMm { get; }

        public double GridTileWidth => TileWidth + GroutWidthMm;

        public double GridTileHeight => TileHeight + GroutWidthMm;

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

        private static void ValidateNonNegativeFinite(
            double value,
            string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Grout width must be a finite non-negative number.");
            }
        }
    }
}
