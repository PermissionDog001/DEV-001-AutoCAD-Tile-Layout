using System;

namespace TileLayout.AutoCAD.Adapter
{
    public enum DoorOpeningInputState
    {
        AwaitingFirstPoint,
        AwaitingSecondPoint,
        ReadyForPreview,
        Cancelled
    }

    public sealed class DoorOpeningInputSession
    {
        public DoorOpeningInputSession()
        {
            State = DoorOpeningInputState.AwaitingFirstPoint;
        }

        public DoorOpeningInputState State { get; private set; }

        public bool IsTerminal => State == DoorOpeningInputState.ReadyForPreview
            || State == DoorOpeningInputState.Cancelled;

        public void AcceptFirstPoint()
        {
            RequireState(DoorOpeningInputState.AwaitingFirstPoint);
            State = DoorOpeningInputState.AwaitingSecondPoint;
        }

        public void AcceptSecondPoint()
        {
            RequireState(DoorOpeningInputState.AwaitingSecondPoint);
            State = DoorOpeningInputState.ReadyForPreview;
        }

        public void AcceptRecognizedObject()
        {
            RequireState(DoorOpeningInputState.AwaitingFirstPoint);
            State = DoorOpeningInputState.ReadyForPreview;
        }

        public void RejectRecognizedObject()
        {
            RequireState(DoorOpeningInputState.AwaitingFirstPoint);
        }

        public void Cancel()
        {
            if (IsTerminal)
            {
                throw new InvalidOperationException(
                    "A completed door-opening input session cannot be cancelled.");
            }

            State = DoorOpeningInputState.Cancelled;
        }

        private void RequireState(DoorOpeningInputState expected)
        {
            if (State != expected)
            {
                throw new InvalidOperationException(
                    "The door-opening input action is not valid in the current state.");
            }
        }
    }
}
