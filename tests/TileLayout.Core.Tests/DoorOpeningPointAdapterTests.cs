using System.Collections.Generic;
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

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_ExternalDoorFindsUniqueAdjacentRegion()
        {
            AxisAlignedOrthogonalPolygon room = CreateLRoom();

            OrthogonalDoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    new Point3D(2000.0, 200.0),
                    new Point3D(2000.0, 800.0));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(RoomSide.East, result.Opening.Wall);
            Assert.AreEqual(0.0, result.ControlRegion.West);
            Assert.AreEqual(2000.0, result.ControlRegion.East);
            Assert.AreEqual(0.0, result.ControlRegion.South);
            Assert.AreEqual(1000.0, result.ControlRegion.North);
        }

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_NearNormalizedBoundaryUsesScopedMatchTolerance()
        {
            AxisAlignedOrthogonalPolygon room = CreateLRoom();
            Point3D first = new Point3D(2002.0, 200.0);
            Point3D second = new Point3D(2002.0, 800.0);

            OrthogonalDoorOpeningProjectionResult strict =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    first,
                    second);
            OrthogonalDoorOpeningProjectionResult tolerant =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    first,
                    second,
                    3.0);

            Assert.IsFalse(strict.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                strict.Projection.Error);
            Assert.IsTrue(tolerant.IsValid, tolerant.Projection.ErrorMessage);
            Assert.AreEqual(RoomSide.East, tolerant.Opening.Wall);
        }

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_InteractivePickToleranceAcceptsVisibleBoundaryPick()
        {
            AxisAlignedOrthogonalPolygon room = CreateLRoom();

            OrthogonalDoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    new Point3D(2002.0, 200.0),
                    new Point3D(2002.0, 800.0),
                    GeometryTolerance.NearOrthogonalEndpointJoinTolerance);

            Assert.IsTrue(result.IsValid, result.Projection.ErrorMessage);
            Assert.AreEqual(RoomSide.East, result.Opening.Wall);
            Assert.AreEqual(2000.0, result.Projection.FirstProjectedPoint.X);
            Assert.AreEqual(2000.0, result.Projection.SecondProjectedPoint.X);
        }

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_SourceBoundaryPickMapsToFinishedFace()
        {
            AxisAlignedOrthogonalPolygon source = CreateRectangleRoom(
                0.0,
                3000.0,
                0.0,
                3000.0);
            OrthogonalRoomOffsetResult offset =
                OrthogonalRoomOffsetter.Offset(source, 100.0);
            Assert.IsTrue(offset.IsValid, offset.ErrorMessage);

            OrthogonalDoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    offset.Room,
                    source,
                    new Point3D(0.0, 1000.0),
                    new Point3D(0.0, 1800.0),
                    GeometryTolerance.NearOrthogonalEndpointJoinTolerance);

            Assert.IsTrue(result.IsValid, result.Projection.ErrorMessage);
            Assert.AreEqual(RoomSide.West, result.Opening.Wall);
            Assert.AreEqual(100.0, result.Projection.FirstProjectedPoint.X);
            Assert.AreEqual(100.0, result.Projection.SecondProjectedPoint.X);
            Assert.AreEqual(1000.0, result.Opening.AlongWallStart);
            Assert.AreEqual(1800.0, result.Opening.AlongWallEnd);
        }

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_InternalSharedEdgeIsNotAWall()
        {
            AxisAlignedOrthogonalPolygon room = CreateLRoom();

            OrthogonalDoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    new Point3D(200.0, 1000.0),
                    new Point3D(800.0, 1000.0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                result.Projection.Error);
        }

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_ConcaveOuterWallUsesCorrectRegionSide()
        {
            AxisAlignedOrthogonalPolygon room = CreateLRoom();

            OrthogonalDoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    new Point3D(1200.0, 1000.0),
                    new Point3D(1800.0, 1000.0));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(RoomSide.North, result.Opening.Wall);
            Assert.AreEqual(0.0, result.ControlRegion.South);
            Assert.AreEqual(1000.0, result.ControlRegion.North);
        }

        [TestMethod]
        public void ProjectToOrthogonalRoomWall_DoorAcrossAutomaticPartitionUsesCommonInteriorRectangle()
        {
            AxisAlignedOrthogonalPolygon room =
                ComplexOrthogonalBoundaryFixture.CreateRoom();
            const double wallX = 832.19436286384735;
            const double firstY = 3218.4086;
            const double secondY = 3818.4086;

            OrthogonalDoorOpeningProjectionResult result =
                DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                    room,
                    new Point3D(wallX, firstY),
                    new Point3D(wallX, secondY));

            Assert.IsTrue(result.IsValid, result.Projection.ErrorMessage);
            Assert.AreEqual(RoomSide.West, result.Opening.Wall);
            Assert.AreEqual(
                firstY,
                result.Opening.AlongWallStart,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                secondY,
                result.Opening.AlongWallEnd,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                wallX,
                result.ControlRegion.West,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                11477.147362370844,
                result.ControlRegion.East,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                1213.6564359078461,
                result.ControlRegion.South,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                5907.04836491891,
                result.ControlRegion.North,
                GeometryTolerance.Coordinate);
        }

        private static AxisAlignedOrthogonalPolygon CreateLRoom()
        {
            var vertices = new[]
            {
                new Point3D(0.0, 0.0),
                new Point3D(2000.0, 0.0),
                new Point3D(2000.0, 1000.0),
                new Point3D(1000.0, 1000.0),
                new Point3D(1000.0, 2000.0),
                new Point3D(0.0, 2000.0)
            };
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]));
            }

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);
            Assert.IsTrue(validation.IsValid);
            return validation.Room;
        }

        private static AxisAlignedOrthogonalPolygon CreateRectangleRoom(
            double west,
            double east,
            double south,
            double north)
        {
            var vertices = new[]
            {
                new Point3D(west, south),
                new Point3D(east, south),
                new Point3D(east, north),
                new Point3D(west, north)
            };
            var lines = new List<LineSegment3D>();
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]));
            }

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);
            Assert.IsTrue(validation.IsValid);
            return validation.Room;
        }
    }
}
