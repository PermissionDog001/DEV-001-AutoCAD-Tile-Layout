using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public enum LayoutDrawingStartPointTileKind
    {
        FullTile,
        HalfTile
    }

    /// <summary>
    /// The deterministic four-tile grout intersection selected for the
    /// current drawing plan. Directions are expressed in WCS room sides so
    /// the CAD adapter can draw arrows without reinterpreting the layout.
    /// </summary>
    public sealed class LayoutDrawingStartPoint
    {
        internal LayoutDrawingStartPoint(
            string id,
            Point3D position,
            RoomSide farWall,
            RoomSide inwardDirection,
            RoomSide alongWallDirection,
            TileLayoutAxis alongWallAxis,
            string wallTileId,
            LayoutDrawingStartPointTileKind wallTileKind)
        {
            Id = id;
            Position = position;
            FarWall = farWall;
            InwardDirection = inwardDirection;
            AlongWallDirection = alongWallDirection;
            AlongWallAxis = alongWallAxis;
            WallTileId = wallTileId;
            WallTileKind = wallTileKind;
        }

        public string Id { get; }

        public Point3D Position { get; }

        public RoomSide FarWall { get; }

        /// <summary>
        /// Direction of the arrow from the far wall into the room.
        /// </summary>
        public RoomSide InwardDirection { get; }

        /// <summary>
        /// Direction of the arrow along the actual construction sequence on
        /// the far-wall band. This is the direction from the construction
        /// start side toward the opposite side.
        /// </summary>
        public RoomSide AlongWallDirection { get; }

        public TileLayoutAxis AlongWallAxis { get; }

        /// <summary>
        /// The wall-side tile whose full/half span determines the point along
        /// the shared grout seam.
        /// </summary>
        public string WallTileId { get; }

        public LayoutDrawingStartPointTileKind WallTileKind { get; }
    }
}
