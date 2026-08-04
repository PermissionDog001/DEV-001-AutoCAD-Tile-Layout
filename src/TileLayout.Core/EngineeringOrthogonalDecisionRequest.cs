using System;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class EngineeringOrthogonalDecisionRequest
    {
        public EngineeringOrthogonalDecisionRequest(
            AxisAlignedOrthogonalPolygon room,
            double tileWidth,
            double tileHeight,
            LayoutPolicyProfile policy,
            RoomDecision roomDecision,
            CandidateDecision candidateDecision = null,
            LayoutDecisionMode mode = LayoutDecisionMode.Research,
            bool preferWallCornerAlignment = false,
            double groutWidthMm = 0.0,
            double plasterThicknessMm = 0.0,
            AxisAlignedOrthogonalPolygon sourceRoom = null)
        {
            Room = room ?? throw new ArgumentNullException(nameof(room));
            if (tileWidth <= GeometryTolerance.Coordinate
                || tileHeight <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tileWidth),
                    "Tile dimensions must be positive.");
            }

            ValidateNonNegativeFinite(groutWidthMm, nameof(groutWidthMm));
            ValidateNonNegativeFinite(
                plasterThicknessMm,
                nameof(plasterThicknessMm));

            if (!Enum.IsDefined(typeof(LayoutDecisionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            TileWidth = tileWidth;
            TileHeight = tileHeight;
            GroutWidthMm = groutWidthMm;
            PlasterThicknessMm = plasterThicknessMm;
            OriginalRoom = sourceRoom ?? room;
            Policy = policy;
            RoomDecision = roomDecision;
            CandidateDecision = candidateDecision;
            Mode = mode;
            PreferWallCornerAlignment = preferWallCornerAlignment;
        }

        public AxisAlignedOrthogonalPolygon Room { get; }
        public double TileWidth { get; }
        public double TileHeight { get; }

        public double GroutWidthMm { get; }

        public double PlasterThicknessMm { get; }

        /// <summary>
        /// The original selected boundary.  <see cref="Room"/> is the
        /// calculation boundary and may be the finished face after an inward
        /// plaster offset.
        /// </summary>
        public AxisAlignedOrthogonalPolygon OriginalRoom { get; }
        public LayoutPolicyProfile Policy { get; }
        public RoomDecision RoomDecision { get; }
        public CandidateDecision CandidateDecision { get; }
        public LayoutDecisionMode Mode { get; }

        /// <summary>
        /// Enables the optional wall-corner candidate search and recommendation
        /// ordering. False keeps the pre-G3 candidate generation path.
        /// </summary>
        public bool PreferWallCornerAlignment { get; }

        private static void ValidateNonNegativeFinite(
            double value,
            string name)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "The value must be a finite non-negative number.");
            }
        }
    }
}
