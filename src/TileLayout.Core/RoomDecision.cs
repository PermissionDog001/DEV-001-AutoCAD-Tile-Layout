using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public sealed class RoomDecision
    {
        public RoomDecision(
            AxisAlignedRectangle controlRegion,
            DoorOpening controlDoor,
            RoomLayoutIntent? layoutIntent = null,
            MainSecondaryRegionDefinition mainSecondary = null,
            LineSegment3D? selectedConnectionEdge = null,
            IList<ConfirmedGridPhase> confirmedGridPhases = null)
        {
            ControlRegion = controlRegion;
            ControlDoor = controlDoor;
            LayoutIntent = layoutIntent;
            MainSecondary = mainSecondary;
            SelectedConnectionEdge = selectedConnectionEdge;
            ConfirmedGridPhases = new ReadOnlyCollection<ConfirmedGridPhase>(
                confirmedGridPhases ?? new List<ConfirmedGridPhase>());
        }

        public AxisAlignedRectangle ControlRegion { get; }

        public DoorOpening ControlDoor { get; }

        public RoomLayoutIntent? LayoutIntent { get; }

        public MainSecondaryRegionDefinition MainSecondary { get; }

        public LineSegment3D? SelectedConnectionEdge { get; }

        public IReadOnlyList<ConfirmedGridPhase> ConfirmedGridPhases { get; }
    }
}
