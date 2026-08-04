using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class RegionConnectionPlan
    {
        internal RegionConnectionPlan(
            LineSegment3D boundary,
            TileLayoutAxis parallelAxis,
            bool perpendicularPhaseReset,
            ProtrusionBandTreatment protrusionTreatment,
            RoomSide? protrusionSide,
            double protrusionWidth,
            double absorbedWidth)
        {
            Boundary = boundary;
            ParallelAxis = parallelAxis;
            PerpendicularPhaseReset = perpendicularPhaseReset;
            ProtrusionTreatment = protrusionTreatment;
            ProtrusionSide = protrusionSide;
            ProtrusionWidth = protrusionWidth;
            AbsorbedWidth = absorbedWidth;
        }

        public LineSegment3D Boundary { get; }

        public TileLayoutAxis ParallelAxis { get; }

        public bool PerpendicularPhaseReset { get; }

        public ProtrusionBandTreatment ProtrusionTreatment { get; }

        public RoomSide? ProtrusionSide { get; }

        public double ProtrusionWidth { get; }

        public double AbsorbedWidth { get; }
    }
}
