using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class EngineeringOrthogonalLayoutParameters
    {
        public EngineeringOrthogonalLayoutParameters(
            double tileWidth,
            double tileHeight,
            AxisAlignedRectangle controlRegion,
            DoorOpening doorOpening,
            MainSecondaryRegionDefinition mainSecondary = null,
            IList<ConfirmedGridPhase> confirmedWholeRoomPhases = null,
            LayoutPolicyProfile policy = null,
            bool preferWallCornerAlignment = false,
            double groutWidthMm = 0.0,
            AxisAlignedOrthogonalPolygon sourceRoom = null,
            double plasterThicknessMm = 0.0)
        {
            ValidateTileSize(tileWidth, nameof(tileWidth));
            ValidateTileSize(tileHeight, nameof(tileHeight));
            ValidateNonNegativeFinite(groutWidthMm, nameof(groutWidthMm));
            ValidateNonNegativeFinite(
                plasterThicknessMm,
                nameof(plasterThicknessMm));
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            GroutWidthMm = groutWidthMm;
            PlasterThicknessMm = plasterThicknessMm;
            SourceRoom = sourceRoom;
            ControlRegion = controlRegion
                ?? throw new ArgumentNullException(nameof(controlRegion));
            DoorOpening = doorOpening
                ?? throw new ArgumentNullException(nameof(doorOpening));
            MainSecondary = mainSecondary;
            ConfirmedWholeRoomPhases = new ReadOnlyCollection<ConfirmedGridPhase>(
                confirmedWholeRoomPhases
                    ?? new List<ConfirmedGridPhase>());
            if (policy != null && policy.HasProjectAbsoluteMinimum)
            {
                double smallestRecommended = Math.Min(
                    tileWidth * policy.DefaultMinimumCutRatio,
                    tileHeight * policy.DefaultMinimumCutRatio);
                if (policy.ProjectAbsoluteMinimumCut.Value
                    > smallestRecommended + GeometryTolerance.Coordinate)
                {
                    throw new ArgumentException(
                        "The project absolute minimum cannot exceed the recommended minimum for either tile axis.",
                        nameof(policy));
                }
            }

            Policy = policy;
            PreferWallCornerAlignment = preferWallCornerAlignment;
        }

        public double TileWidth { get; }

        public double TileHeight { get; }

        /// <summary>
        /// The nominal physical tile width.  Grout is deliberately not part
        /// of this value; it is added only when a grid pitch is required.
        /// </summary>
        public double GroutWidthMm { get; }

        public double PlasterThicknessMm { get; }

        public double GridTileWidth => TileWidth + GroutWidthMm;

        public double GridTileHeight => TileHeight + GroutWidthMm;

        /// <summary>
        /// The original input boundary used for room identity and duplicate
        /// write-back checks.  Room passed to the calculator may be the
        /// inward-offset finished face.
        /// </summary>
        public AxisAlignedOrthogonalPolygon SourceRoom { get; }

        public AxisAlignedRectangle ControlRegion { get; }

        public DoorOpening DoorOpening { get; }

        public MainSecondaryRegionDefinition MainSecondary { get; }

        public IReadOnlyList<ConfirmedGridPhase> ConfirmedWholeRoomPhases { get; }

        public LayoutPolicyProfile Policy { get; }

        /// <summary>
        /// Enables the optional wall-corner quality pass. When enabled, the
        /// bounded wall-corner phase branch is generated and an accurate
        /// target-corner seam is compared before the G1 boundary pattern.
        /// When disabled, G1 boundary-pattern search remains available, but
        /// wall-corner facts are read-only and do not affect candidate order.
        /// </summary>
        public bool PreferWallCornerAlignment { get; }

        private static void ValidateTileSize(double value, string name)
        {
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    "Tile dimensions must be finite and greater than the coordinate tolerance.");
            }
        }

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
