using System;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class MainSecondaryRegionDefinition
    {
        public MainSecondaryRegionDefinition(
            AxisAlignedRectangle mainRegion,
            AxisAlignedRectangle secondaryRegion)
        {
            MainRegion = mainRegion
                ?? throw new ArgumentNullException(nameof(mainRegion));
            SecondaryRegion = secondaryRegion
                ?? throw new ArgumentNullException(nameof(secondaryRegion));
        }

        public AxisAlignedRectangle MainRegion { get; }

        public AxisAlignedRectangle SecondaryRegion { get; }
    }
}
