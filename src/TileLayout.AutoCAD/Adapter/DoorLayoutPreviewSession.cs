using System;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    public enum DoorLayoutInteractionAction
    {
        Accept,
        Flip,
        Reselect,
        Cancel
    }

    public enum DoorLayoutInteractionState
    {
        Previewing,
        Accepted,
        ReselectRequested,
        Cancelled
    }

    public sealed class DoorLayoutPreviewSession
    {
        private readonly EngineeringRectangularLayoutResult layout;

        public DoorLayoutPreviewSession(
            EngineeringRectangularLayoutResult layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!layout.IsSuccessful)
            {
                throw new ArgumentException(
                    "A preview session requires a viable default candidate.",
                    nameof(layout));
            }

            this.layout = layout;
            SelectedCandidate = layout.DefaultCandidate;
            State = DoorLayoutInteractionState.Previewing;
        }

        public DoorLayoutInteractionState State { get; private set; }

        public LayoutCandidate SelectedCandidate { get; private set; }

        public bool CanFlip => layout.FlippedCandidate != null;

        public void Apply(DoorLayoutInteractionAction action)
        {
            if (State != DoorLayoutInteractionState.Previewing)
            {
                throw new InvalidOperationException(
                    "The preview session is no longer accepting actions.");
            }

            switch (action)
            {
                case DoorLayoutInteractionAction.Accept:
                    State = DoorLayoutInteractionState.Accepted;
                    return;
                case DoorLayoutInteractionAction.Reselect:
                    State = DoorLayoutInteractionState.ReselectRequested;
                    return;
                case DoorLayoutInteractionAction.Cancel:
                    State = DoorLayoutInteractionState.Cancelled;
                    return;
                case DoorLayoutInteractionAction.Flip:
                    if (!CanFlip)
                    {
                        throw new InvalidOperationException(
                            "Only a DR2 centered-door equivalent candidate can be flipped.");
                    }

                    SelectedCandidate = ReferenceEquals(
                        SelectedCandidate,
                        layout.DefaultCandidate)
                        ? layout.FlippedCandidate
                        : layout.DefaultCandidate;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
    }
}
