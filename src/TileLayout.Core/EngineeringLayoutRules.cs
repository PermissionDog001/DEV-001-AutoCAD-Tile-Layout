namespace TileLayout.Core
{
    public static class EngineeringLayoutRules
    {
        public const double DefaultMinimumCutRatio = 0.42;

        // A corner seam is considered visually safe only when both adjacent
        // tile spans on that axis are at least two thirds of a nominal tile.
        // This is a soft recommendation and never replaces the project
        // absolute-minimum hard rule.
        public const double WallCornerSafeAdjacentTileRatio = 2.0 / 3.0;

        public const double HalfTileRatio = 0.5;
    }
}
