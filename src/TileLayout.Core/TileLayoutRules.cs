namespace TileLayout.Core
{
    public static class TileLayoutRules
    {
        public const double TileWidth = 600.0;

        public const double TileHeight = 600.0;

        public const double GroutWidth = 0.0;

        public const int MaximumParameterizedDivisionLineCount = 10000;

        public static TileLayoutParameters Fixed600Parameters { get; } =
            new TileLayoutParameters(TileWidth, TileHeight);
    }
}
