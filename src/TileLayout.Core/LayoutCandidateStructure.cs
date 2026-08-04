using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TileLayout.Core
{
    public sealed class LayoutCandidateStructure
    {
        private static readonly LayoutCandidateStructure rectangular =
            new LayoutCandidateStructure(
                OrthogonalCandidateKind.RectangularDoorControlled,
                new List<LayoutRegionPhase>(),
                new List<RegionConnectionPlan>());

        internal LayoutCandidateStructure(
            OrthogonalCandidateKind kind,
            IList<LayoutRegionPhase> regions,
            IList<RegionConnectionPlan> connections)
        {
            Kind = kind;
            Regions = new ReadOnlyCollection<LayoutRegionPhase>(regions);
            Connections =
                new ReadOnlyCollection<RegionConnectionPlan>(connections);
        }

        public static LayoutCandidateStructure Rectangular => rectangular;

        public OrthogonalCandidateKind Kind { get; }

        public IReadOnlyList<LayoutRegionPhase> Regions { get; }

        public IReadOnlyList<RegionConnectionPlan> Connections { get; }

        public bool UsesWholeRoomSinglePhase =>
            Kind == OrthogonalCandidateKind.WholeRoomSinglePhase;
    }
}
