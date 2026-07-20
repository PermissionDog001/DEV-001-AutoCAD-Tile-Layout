using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class TileLayoutResult
    {
        internal TileLayoutResult(
            AxisAlignedRectangle room,
            int fullColumnCount,
            int fullRowCount,
            double eastRemainder,
            double northRemainder,
            IList<LineSegment3D> divisionLines)
        {
            Room = room;
            FullColumnCount = fullColumnCount;
            FullRowCount = fullRowCount;
            EastRemainder = eastRemainder;
            NorthRemainder = northRemainder;
            DivisionLines = new ReadOnlyCollection<LineSegment3D>(divisionLines);
        }

        public AxisAlignedRectangle Room { get; }

        public int FullColumnCount { get; }

        public int FullRowCount { get; }

        public double EastRemainder { get; }

        public double NorthRemainder { get; }

        public IReadOnlyList<LineSegment3D> DivisionLines { get; }
    }
}
