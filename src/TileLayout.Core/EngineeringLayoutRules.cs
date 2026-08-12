using System;

namespace TileLayout.Core
{
    public static class EngineeringLayoutRules
    {
        // Retained for the legacy rectangular/simple-room commands.  The
        // guided project workflow uses GuidedDefaultMinimumCutRatio instead.
        public const double DefaultMinimumCutRatio = 0.42;

        public const double GuidedDefaultMinimumCutRatio = 0.50;

        public const double MaximumConfigurableMinimumCutRatio = 0.75;

        // A corner seam is considered visually safe only when both adjacent
        // tile spans on that axis are at least two thirds of a nominal tile.
        // This is a soft recommendation and never replaces the project
        // absolute-minimum hard rule.
        public const double WallCornerSafeAdjacentTileRatio = 2.0 / 3.0;

        public const double HalfTileRatio = 0.5;

        public static void ValidateMinimumCutRatio(
            double value,
            string parameterName)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value <= 0.0
                || value > MaximumConfigurableMinimumCutRatio)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "The recommended minimum cut ratio must be finite, greater than zero, and no greater than 0.75.");
            }
        }
    }
}
