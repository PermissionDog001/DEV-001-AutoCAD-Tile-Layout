using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalRoomOffsetterTests
    {
        [TestMethod]
        public void Offset_RectangleShrinksEverySideByThickness()
        {
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(
                    TestGeometry.RectangleLines(0.0, 3600.0, 0.0, 3000.0));

            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);

            OrthogonalRoomOffsetResult result =
                OrthogonalRoomOffsetter.Offset(validation.Room, 100.0);

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            Assert.AreEqual(100.0, result.Room.West, GeometryTolerance.Coordinate);
            Assert.AreEqual(3500.0, result.Room.East, GeometryTolerance.Coordinate);
            Assert.AreEqual(100.0, result.Room.South, GeometryTolerance.Coordinate);
            Assert.AreEqual(2900.0, result.Room.North, GeometryTolerance.Coordinate);
            Assert.AreEqual(validation.Room.Elevation, result.Room.Elevation);
        }

        [TestMethod]
        public void Offset_ConcaveOrthogonalRoomUsesDeterministicMiterJoins()
        {
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(
                    LinesFromVertices(
                        new Point3D(0.0, 0.0),
                        new Point3D(1800.0, 0.0),
                        new Point3D(1800.0, 600.0),
                        new Point3D(600.0, 600.0),
                        new Point3D(600.0, 1800.0),
                        new Point3D(0.0, 1800.0)));

            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);

            OrthogonalRoomOffsetResult result =
                OrthogonalRoomOffsetter.Offset(validation.Room, 100.0);

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            Assert.AreEqual(6, result.Room.Vertices.Count);
            AssertPoint(new Point3D(100.0, 100.0), result.Room.Vertices[0]);
            AssertPoint(new Point3D(1700.0, 100.0), result.Room.Vertices[1]);
            AssertPoint(new Point3D(1700.0, 500.0), result.Room.Vertices[2]);
            AssertPoint(new Point3D(500.0, 500.0), result.Room.Vertices[3]);
            AssertPoint(new Point3D(500.0, 1700.0), result.Room.Vertices[4]);
            AssertPoint(new Point3D(100.0, 1700.0), result.Room.Vertices[5]);
        }

        [TestMethod]
        public void Offset_ZeroReturnsOriginalRoomWithoutChangingIdentity()
        {
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(
                    TestGeometry.RectangleLines(0.0, 3600.0, 0.0, 3000.0));

            OrthogonalRoomOffsetResult result =
                OrthogonalRoomOffsetter.Offset(validation.Room, 0.0);

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            Assert.AreSame(validation.Room, result.Room);
        }

        [TestMethod]
        public void Offset_NegativeThicknessIsRejected()
        {
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(
                    TestGeometry.RectangleLines(0.0, 3600.0, 0.0, 3000.0));

            OrthogonalRoomOffsetResult result =
                OrthogonalRoomOffsetter.Offset(validation.Room, -0.1);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("non-negative"));
        }

        private static LineSegment3D[] LinesFromVertices(
            params Point3D[] vertices)
        {
            return Enumerable.Range(0, vertices.Length)
                .Select(index => new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]))
                .ToArray();
        }

        private static void AssertPoint(Point3D expected, Point3D actual)
        {
            Assert.AreEqual(expected.X, actual.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.Y, actual.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.Z, actual.Z, GeometryTolerance.Coordinate);
        }
    }
}
