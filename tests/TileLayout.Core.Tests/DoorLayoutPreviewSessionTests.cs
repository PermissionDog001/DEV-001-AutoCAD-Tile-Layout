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

            Assert.IsFalse(session.IsWriteAuthorized);
            session.Apply(DoorLayoutInteractionAction.Accept);

            Assert.AreEqual(
                DoorLayoutInteractionState.Accepted,
                session.State);
            Assert.IsTrue(session.IsWriteAuthorized);
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
            Assert.IsFalse(session.IsWriteAuthorized);
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
            Assert.IsFalse(session.IsWriteAuthorized);
            Assert.AreSame(layout.DefaultCandidate, session.SelectedCandidate);
        }

        [TestMethod]
        public void FrozenStaticRecognition_EntersOriginalPreviewAndRequiresAcceptForWrite()
        {
            DoorObjectRecognitionResult recognition =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 2000.0, 0.0, 2000.0),
                    StaticDoorLines(),
                    new[] { StaticDoorArc() },
                    DoorBlockRecognitionRoute.FrozenStaticSignature);
            Assert.IsTrue(recognition.IsHigh);

            EngineeringRectangularLayoutResult layout =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 2000.0, 0.0, 2000.0),
                    new EngineeringRectangularLayoutParameters(
                        600.0,
                        600.0,
                        recognition.Projection.Opening));
            var session = new DoorLayoutPreviewSession(layout);

            Assert.IsFalse(session.IsWriteAuthorized);
            session.Apply(DoorLayoutInteractionAction.Accept);
            Assert.IsTrue(session.IsWriteAuthorized);
        }

        [TestMethod]
        public void FrozenStaticRecognition_CancelledPreviewNeverAuthorizesWrite()
        {
            var session = new DoorLayoutPreviewSession(
                Calculate(new DoorOpening(RoomSide.West, 100.0, 500.0)));

            session.Apply(DoorLayoutInteractionAction.Cancel);

            Assert.AreEqual(DoorLayoutInteractionState.Cancelled, session.State);
            Assert.IsFalse(session.IsWriteAuthorized);
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

        private static LineSegment3D[] StaticDoorLines()
        {
            return new[]
            {
                new LineSegment3D(
                    new Point3D(975.0, 0.0),
                    new Point3D(975.0, 50.0)),
                new LineSegment3D(
                    new Point3D(0.0, 0.0),
                    new Point3D(0.0, 50.0)),
                new LineSegment3D(
                    new Point3D(0.0, 0.0),
                    new Point3D(975.0, 0.0)),
                new LineSegment3D(
                    new Point3D(0.0, 50.0),
                    new Point3D(975.0, 50.0))
            };
        }

        private static ArcSegment3D StaticDoorArc()
        {
            return new ArcSegment3D(
                new Point3D(0.0, 25.0),
                new Point3D(0.0, 1000.0),
                new Point3D(
                    Math.Sqrt((975.0 * 975.0) - (25.0 * 25.0)),
                    50.0),
                975.0);
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
