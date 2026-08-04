using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class NeutralRegionConnection
    {
        internal NeutralRegionConnection(
            string firstRegionId,
            string secondRegionId,
            LineSegment3D sharedEdge)
        {
            FirstRegionId = firstRegionId;
            SecondRegionId = secondRegionId;
            SharedEdge = sharedEdge;
        }

        public string FirstRegionId { get; }

        public string SecondRegionId { get; }

        public LineSegment3D SharedEdge { get; }
    }
}
