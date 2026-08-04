using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class DoorOpeningInputSessionTests
    {
        [TestMethod]
        public void DirectTwoPointInput_RemainsDefaultPathToPreview()
        {
            var session = new DoorOpeningInputSession();

            Assert.AreEqual(
                DoorOpeningInputState.AwaitingFirstPoint,
                session.State);
            session.AcceptFirstPoint();
            Assert.AreEqual(
                DoorOpeningInputState.AwaitingSecondPoint,
                session.State);
            session.AcceptSecondPoint();

            Assert.AreEqual(
                DoorOpeningInputState.ReadyForPreview,
                session.State);
        }

        [TestMethod]
        public void RejectedObject_ReturnsToExistingFirstPointEntry()
        {
            var session = new DoorOpeningInputSession();

            session.RejectRecognizedObject();

            Assert.AreEqual(
                DoorOpeningInputState.AwaitingFirstPoint,
                session.State);
            Assert.IsFalse(session.IsTerminal);
        }

        [TestMethod]
        public void ObjectSelectionEscape_CancelsInsteadOfForcingFallback()
        {
            var session = new DoorOpeningInputSession();

            session.Cancel();

            Assert.AreEqual(DoorOpeningInputState.Cancelled, session.State);
            Assert.IsTrue(session.IsTerminal);
        }

        [TestMethod]
        public void HighObjectResult_EntersSamePreviewReadyState()
        {
            var session = new DoorOpeningInputSession();

            session.AcceptRecognizedObject();

            Assert.AreEqual(
                DoorOpeningInputState.ReadyForPreview,
                session.State);
        }

        [TestMethod]
        public void HighFrozenStaticObject_UsesSamePreviewReadyState()
        {
            var session = new DoorOpeningInputSession();

            session.AcceptRecognizedObject();

            Assert.AreEqual(
                DoorOpeningInputState.ReadyForPreview,
                session.State);
            Assert.IsTrue(session.IsTerminal);
        }

        [TestMethod]
        public void SecondPointEscape_CancelsWithoutReadyState()
        {
            var session = new DoorOpeningInputSession();
            session.AcceptFirstPoint();

            session.Cancel();

            Assert.AreEqual(DoorOpeningInputState.Cancelled, session.State);
        }
    }
}
