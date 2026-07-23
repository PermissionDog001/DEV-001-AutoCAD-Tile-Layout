namespace TileLayout.Core
{
    public sealed class AxisBoundaryBand
    {
        internal AxisBoundaryBand(
            RoomSide side,
            double width,
            BoundaryBandKind kind)
        {
            Side = side;
            Width = width;
            Kind = kind;
        }

        public RoomSide Side { get; }

        public double Width { get; }

        public BoundaryBandKind Kind { get; }

        public bool IsCut => Kind != BoundaryBandKind.FullTile;
    }
}
