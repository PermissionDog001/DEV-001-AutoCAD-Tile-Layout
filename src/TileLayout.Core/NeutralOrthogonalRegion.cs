using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class NeutralOrthogonalRegion
    {
        internal NeutralOrthogonalRegion(string id, AxisAlignedRectangle bounds)
        {
            Id = id;
            Bounds = bounds;
        }

        public string Id { get; }

        public AxisAlignedRectangle Bounds { get; }

        public double Area => Bounds.Width * Bounds.Height;
    }
}
