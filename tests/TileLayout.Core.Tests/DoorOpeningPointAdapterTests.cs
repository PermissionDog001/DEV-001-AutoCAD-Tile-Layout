using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class DoorOpeningPointAdapterTests
    {
        [TestMethod]
        public void ProjectToRoomWall_OffsetsWithinTolerance_ProjectsToWestWall()
        {
            var room = new AxisAlignedRectangle(
                100.0,
                1400.0,
                200.0,
                1500.0,
                25.0);
            double offset = GeometryTolerance.Coordinate / 2.0;

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(100.0 - offset, 1100.0, 25.0 + offset),
                    new Point3D(100.0 + offset, 400.0, 25.0 - offset));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(RoomSide.West, result.Opening.Wall);
            Assert.AreEqual(400.0, result.Opening.AlongWallStart);
            Assert.AreEqual(1100.0, result.Opening.AlongWallEnd);
            Assert.AreEqual(100.0, result.FirstProjectedPoint.X);
            Assert.AreEqual(1100.0, result.FirstProjectedPoint.Y);
            Assert.AreEqual(25.0, result.FirstProjectedPoint.Z);
        }

        [TestMethod]
        public void ProjectToRoomWall_CornerAndNorthPoint_SelectUniqueCommonWall()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(0.0, 1300.0),
                    new Point3D(900.0, 1300.0));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(RoomSide.North, result.Opening.Wall);
            Assert.AreEqual(0.0, result.Opening.AlongWallStart);
            Assert.AreEqual(900.0, result.Opening.AlongWallEnd);
        }

        [TestMethod]
        public void ProjectToRoomWall_CoincidentProjectedPoints_AreRejected()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(0.0, 400.0),
                    new Point3D(
                        GeometryTolerance.Coordinate / 2.0,
                        400.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.CoincidentPoints,
                result.Error);
        }

        [TestMethod]
        public void ProjectToRoomWall_PointsOnDifferentWalls_AreRejected()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(0.0, 400.0),
                    new Point3D(800.0, 1300.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.PointsOnDifferentWalls,
                result.Error);
        }

        [TestMethod]
        public void ProjectToRoomWall_PointBeyondWallSegment_IsRejected()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(0.0, 1400.0),
                    new Point3D(0.0, 500.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.PointOutsideWallSegment,
                result.Error);
        }

        [TestMethod]
        public void ProjectToRoomWall_PointAwayFromAllWalls_IsRejected()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(100.0, 400.0),
                    new Point3D(100.0, 800.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                result.Error);
        }

        [TestMethod]
        public void ProjectToRoomWall_PointOnDifferentElevation_IsRejected()
        {
            var room = new AxisAlignedRectangle(
                0.0,
                1300.0,
                0.0,
                1300.0,
                25.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(0.0, 400.0, 25.0),
                    new Point3D(0.0, 800.0, 26.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.DifferentElevation,
                result.Error);
        }

        [TestMethod]
        public void ProjectToRoomWall_NonFinitePoint_IsRejected()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);

            DoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(double.NaN, 400.0),
                    new Point3D(0.0, 800.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.NonFinitePoint,
                result.Error);
        }
    }
}
