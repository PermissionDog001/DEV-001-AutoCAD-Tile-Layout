using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class OrthogonalTileLayoutResult
    {
        internal OrthogonalTileLayoutResult(
            AxisAlignedOrthogonalPolygon room,
            TileLayoutParameters parameters,
            int fullColumnCount,
            int fullRowCount,
            double horizontalRemainder,
            double verticalRemainder,
            IList<LineSegment3D> divisionLines)
        {
            Room = room;
            Parameters = parameters;
            FullColumnCount = fullColumnCount;
            FullRowCount = fullRowCount;
            HorizontalRemainder = horizontalRemainder;
            VerticalRemainder = verticalRemainder;
            DivisionLines = new ReadOnlyCollection<LineSegment3D>(divisionLines);
        }

        public AxisAlignedOrthogonalPolygon Room { get; }

        public TileLayoutParameters Parameters { get; }

        public int FullColumnCount { get; }

        public int FullRowCount { get; }

        public double HorizontalRemainder { get; }

        public double VerticalRemainder { get; }

        public double WestRemainder =>
            Parameters.StartsFromEast ? HorizontalRemainder : 0.0;

        public double EastRemainder =>
            Parameters.StartsFromEast ? 0.0 : HorizontalRemainder;

        public double SouthRemainder =>
            Parameters.StartsFromNorth ? VerticalRemainder : 0.0;

        public double NorthRemainder =>
            Parameters.StartsFromNorth ? 0.0 : VerticalRemainder;

        public IReadOnlyList<LineSegment3D> DivisionLines { get; }
    }
}
