using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class LayoutRegionPhase
    {
        internal LayoutRegionPhase(
            string id,
            LayoutRegionRole role,
            AxisAlignedRectangle bounds,
            IList<double> verticalCuts,
            IList<double> horizontalCuts,
            bool inheritsParallelPhase,
            bool resetsPerpendicularPhase)
        {
            Id = id;
            Role = role;
            Bounds = bounds;
            VerticalCuts = new ReadOnlyCollection<double>(verticalCuts);
            HorizontalCuts = new ReadOnlyCollection<double>(horizontalCuts);
            InheritsParallelPhase = inheritsParallelPhase;
            ResetsPerpendicularPhase = resetsPerpendicularPhase;
        }

        public string Id { get; }

        public LayoutRegionRole Role { get; }

        public AxisAlignedRectangle Bounds { get; }

        public IReadOnlyList<double> VerticalCuts { get; }

        public IReadOnlyList<double> HorizontalCuts { get; }

        public bool InheritsParallelPhase { get; }

        public bool ResetsPerpendicularPhase { get; }
    }
}
