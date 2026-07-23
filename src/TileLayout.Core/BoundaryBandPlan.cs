using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TileLayout.Core
{
    public sealed class BoundaryBandPlan
    {
        internal BoundaryBandPlan(
            TileLayoutAxis axis,
            DoorControlledAxisRole role,
            double tileSize,
            double naturalRemainder,
            RoomSide controlSide,
            RoomSide constructionStartSide,
            AxisBoundaryBand lowBoundary,
            AxisBoundaryBand highBoundary,
            int fullTileCount,
            int interiorFullTileCount,
            bool usesRedistribution,
            IList<double> segmentWidths)
        {
            Axis = axis;
            Role = role;
            TileSize = tileSize;
            NaturalRemainder = naturalRemainder;
            ControlSide = controlSide;
            ConstructionStartSide = constructionStartSide;
            LowBoundary = lowBoundary;
            HighBoundary = highBoundary;
            FullTileCount = fullTileCount;
            InteriorFullTileCount = interiorFullTileCount;
            UsesRedistribution = usesRedistribution;
            SegmentWidths = new ReadOnlyCollection<double>(segmentWidths);
        }

        public TileLayoutAxis Axis { get; }

        public DoorControlledAxisRole Role { get; }

        public double TileSize { get; }

        public double NaturalRemainder { get; }

        public double MinimumCut =>
            TileSize * EngineeringLayoutRules.DefaultMinimumCutRatio;

        public RoomSide ControlSide { get; }

        public RoomSide ConstructionStartSide { get; }

        public AxisBoundaryBand LowBoundary { get; }

        public AxisBoundaryBand HighBoundary { get; }

        public int FullTileCount { get; }

        public int InteriorFullTileCount { get; }

        public bool UsesRedistribution { get; }

        public IReadOnlyList<double> SegmentWidths { get; }

        public RoomSide? HalfTileSide
        {
            get
            {
                if (!UsesRedistribution)
                {
                    return null;
                }

                return LowBoundary.Kind == BoundaryBandKind.HalfTile
                    ? (RoomSide?)LowBoundary.Side
                    : HighBoundary.Side;
            }
        }

        public RoomSide? TransitionTileSide
        {
            get
            {
                if (!UsesRedistribution)
                {
                    return null;
                }

                return LowBoundary.Kind == BoundaryBandKind.Transition
                    ? (RoomSide?)LowBoundary.Side
                    : HighBoundary.Side;
            }
        }

        public AxisBoundaryBand GetBoundary(RoomSide side)
        {
            if (side == LowBoundary.Side)
            {
                return LowBoundary;
            }

            if (side == HighBoundary.Side)
            {
                return HighBoundary;
            }

            throw new ArgumentOutOfRangeException(
                nameof(side),
                "The requested side does not belong to this axis.");
        }
    }
}
