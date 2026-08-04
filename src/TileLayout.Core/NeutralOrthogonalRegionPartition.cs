using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class NeutralOrthogonalRegionPartition
    {
        internal NeutralOrthogonalRegionPartition(
            AxisAlignedOrthogonalPolygon room,
            IList<NeutralOrthogonalRegion> regions,
            IList<NeutralRegionConnection> connections)
        {
            Room = room;
            Regions = new ReadOnlyCollection<NeutralOrthogonalRegion>(regions);
            Connections = new ReadOnlyCollection<NeutralRegionConnection>(connections);
        }

        public AxisAlignedOrthogonalPolygon Room { get; }

        public IReadOnlyList<NeutralOrthogonalRegion> Regions { get; }

        public IReadOnlyList<NeutralRegionConnection> Connections { get; }

        public double CoveredArea
        {
            get
            {
                double area = 0.0;
                foreach (NeutralOrthogonalRegion region in Regions)
                {
                    area += region.Area;
                }

                return area;
            }
        }
    }
}
