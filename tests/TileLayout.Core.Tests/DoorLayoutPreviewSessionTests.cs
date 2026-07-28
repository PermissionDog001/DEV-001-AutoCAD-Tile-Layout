using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class DoorLayoutPreviewSessionTests
    {
        [TestMethod]
        public void Apply_Flip_UsesOnlyDr2CenteredEquivalentAndToggles()
        {
            EngineeringRectangularLayoutResult layout = Calculate(
                new DoorOpening(RoomSide.West, 500.0, 800.0));
            var session = new DoorLayoutPreviewSession(layout);

            Assert.IsTrue(session.CanFlip);
            Assert.AreSame(layout.DefaultCandidate, session.SelectedCandidate);

            session.Apply(DoorLayoutInteractionAction.Flip);
            Assert.AreSame(layout.FlippedCandidate, session.SelectedCandidate);
            Assert.AreEqual(
                DoorLayoutInteractionState.Previewing,
                session.State);

            session.Apply(DoorLayoutInteractionAction.Flip);
            Assert.AreSame(layout.DefaultCandidate, session.SelectedCandidate);
        }

        [TestMethod]
        public void Apply_Flip_WhenDr2HasNoEquivalentCandidate_IsRejected()
        {
            EngineeringRectangularLayoutResult layout = Calculate(
                new DoorOpening(RoomSide.West, 100.0, 500.0));
            var session = new DoorLayoutPreviewSession(layout);

            Assert.IsFalse(session.CanFlip);
            AssertInvalidOperation(
                () => session.Apply(DoorLayoutInteractionAction.Flip));
            Assert.AreSame(layout.DefaultCandidate, session.SelectedCandidate);
            Assert.AreEqual(
                DoorLayoutInteractionState.Previewing,
                session.State);
        }

        [TestMethod]
        public void Apply_Accept_MakesSessionTerminal()
        {
            var session = new DoorLayoutPreviewSession(
                Calculate(new DoorOpening(RoomSide.West, 100.0, 500.0)));

            session.Apply(DoorLayoutInteractionAction.Accept);

            Assert.AreEqual(
                DoorLayoutInteractionState.Accepted,
                session.State);
            AssertInvalidOperation(
                () => session.Apply(DoorLayoutInteractionAction.Cancel));
        }

        [TestMethod]
        public void Apply_Reselect_ReturnsToDoorInputState()
        {
            var session = new DoorLayoutPreviewSession(
                Calculate(new DoorOpening(RoomSide.West, 100.0, 500.0)));

            session.Apply(DoorLayoutInteractionAction.Reselect);

            Assert.AreEqual(
                DoorLayoutInteractionState.ReselectRequested,
                session.State);
        }

        [TestMethod]
        public void Apply_Cancel_MakesSessionTerminalWithoutCandidateMutation()
        {
            EngineeringRectangularLayoutResult layout = Calculate(
                new DoorOpening(RoomSide.West, 100.0, 500.0));
            var session = new DoorLayoutPreviewSession(layout);

            session.Apply(DoorLayoutInteractionAction.Cancel);

            Assert.AreEqual(
                DoorLayoutInteractionState.Cancelled,
                session.State);
            Assert.AreSame(layout.DefaultCandidate, session.SelectedCandidate);
        }

        private static EngineeringRectangularLayoutResult Calculate(
            DoorOpening opening)
        {
            return EngineeringRectangularLayoutCalculator.Calculate(
                new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0),
                new EngineeringRectangularLayoutParameters(
                    600.0,
                    600.0,
                    opening));
        }

        private static void AssertInvalidOperation(Action action)
        {
            InvalidOperationException exception = null;
            try
            {
                action();
            }
            catch (InvalidOperationException caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception);
        }
    }
}
