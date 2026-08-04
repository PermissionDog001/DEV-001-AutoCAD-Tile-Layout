using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalTileGridCalculatorTests
    {
        [TestMethod]
        public void Calculate_LRoom_VertexHitsKeepOnlyInteriorFragments()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                new Point3D(0.0, 0.0),
                new Point3D(1800.0, 0.0),
                new Point3D(1800.0, 600.0),
                new Point3D(600.0, 600.0),
                new Point3D(600.0, 1800.0),
                new Point3D(0.0, 1800.0));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(600.0, 600.0));

            Assert.AreEqual(3, result.FullColumnCount);
            Assert.AreEqual(3, result.FullRowCount);
            Assert.AreEqual(4, result.DivisionLines.Count);
            AssertVertical(result.DivisionLines[0], 600.0, 0.0, 600.0);
            AssertVertical(result.DivisionLines[1], 1200.0, 0.0, 600.0);
            AssertHorizontal(result.DivisionLines[2], 600.0, 0.0, 600.0);
            AssertHorizontal(result.DivisionLines[3], 1200.0, 0.0, 600.0);
        }

        [TestMethod]
        public void Calculate_URoom_OneGridLineCanProduceMultipleInteriorIntervals()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                new Point3D(0.0, 0.0),
                new Point3D(3000.0, 0.0),
                new Point3D(3000.0, 3000.0),
                new Point3D(2400.0, 3000.0),
                new Point3D(2400.0, 600.0),
                new Point3D(600.0, 600.0),
                new Point3D(600.0, 3000.0),
                new Point3D(0.0, 3000.0));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(600.0, 600.0));

            Assert.AreEqual(12, result.DivisionLines.Count);
            AssertHorizontal(result.DivisionLines[4], 600.0, 0.0, 600.0);
            AssertHorizontal(result.DivisionLines[5], 600.0, 2400.0, 3000.0);
            AssertHorizontal(result.DivisionLines[6], 1200.0, 0.0, 600.0);
            AssertHorizontal(result.DivisionLines[7], 1200.0, 2400.0, 3000.0);
        }

        [TestMethod]
        public void Calculate_ComplexSteppedRoom_CutsEveryCandidateToInteriorFragments()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                OrthogonalRoomValidatorTests.ComplexSteppedRoomVertices(0.0, 0.0));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(600.0, 600.0));

            Assert.AreEqual(16, result.DivisionLines.Count);
            AssertVertical(result.DivisionLines[4], 3000.0, 0.0, 3000.0);
            AssertVertical(result.DivisionLines[5], 3600.0, 0.0, 1200.0);
            AssertVertical(result.DivisionLines[6], 3600.0, 2400.0, 3600.0);
            AssertHorizontal(result.DivisionLines[9], 600.0, 0.0, 4800.0);
            AssertHorizontal(result.DivisionLines[13], 3000.0, 1200.0, 2400.0);
            AssertHorizontal(result.DivisionLines[14], 3000.0, 3000.0, 4800.0);
            AssertHorizontal(result.DivisionLines[15], 3600.0, 1200.0, 2400.0);
        }

        [TestMethod]
        public void Calculate_NarrowCorridor_PreservesPositiveLengthInteriorFragments()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                new Point3D(0.0, 0.0),
                new Point3D(3000.0, 0.0),
                new Point3D(3000.0, 200.0),
                new Point3D(200.0, 200.0),
                new Point3D(200.0, 3000.0),
                new Point3D(0.0, 3000.0));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(600.0, 600.0));

            Assert.AreEqual(8, result.DivisionLines.Count);
            AssertVertical(result.DivisionLines[0], 600.0, 0.0, 200.0);
            AssertHorizontal(result.DivisionLines[4], 600.0, 0.0, 200.0);
        }

        [TestMethod]
        public void Calculate_ComplexSteppedRoomAtLargeWcsOffset_PreservesInteriorFragments()
        {
            const double offset = 1e12;
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                OrthogonalRoomValidatorTests.ComplexSteppedRoomVertices(
                    offset,
                    offset));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(600.0, 600.0));

            Assert.AreEqual(16, result.DivisionLines.Count);
            AssertVertical(
                result.DivisionLines[4],
                offset + 3000.0,
                offset,
                offset + 3000.0);
        }

        [TestMethod]
        public void Calculate_LRoomNorthEastAnchor_CanBeOutsideRoomAndStillDefinesGridPhase()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                new Point3D(0.0, 0.0),
                new Point3D(1800.0, 0.0),
                new Point3D(1800.0, 600.0),
                new Point3D(600.0, 600.0),
                new Point3D(600.0, 1800.0),
                new Point3D(0.0, 1800.0));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(
                    700.0,
                    700.0,
                    TileLayoutStartCorner.NorthEast));

            Assert.AreEqual(4, result.DivisionLines.Count);
            AssertVertical(result.DivisionLines[0], 1100.0, 0.0, 600.0);
            AssertVertical(result.DivisionLines[1], 400.0, 0.0, 1800.0);
            AssertHorizontal(result.DivisionLines[2], 1100.0, 0.0, 600.0);
            AssertHorizontal(result.DivisionLines[3], 400.0, 0.0, 1800.0);
            Assert.AreEqual(400.0, result.WestRemainder);
            Assert.AreEqual(400.0, result.SouthRemainder);
        }

        [TestMethod]
        public void Calculate_FragmentedRectangle_AllAnchorsMatchExistingRectangleGrid()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                new Point3D(100.0, 200.0, 25.0),
                new Point3D(600.0, 200.0, 25.0),
                new Point3D(1350.0, 200.0, 25.0),
                new Point3D(1350.0, 850.0, 25.0),
                new Point3D(100.0, 850.0, 25.0));
            var rectangle = new AxisAlignedRectangle(
                100.0,
                1350.0,
                200.0,
                850.0,
                25.0);

            foreach (TileLayoutStartCorner corner in new[]
            {
                TileLayoutStartCorner.SouthWest,
                TileLayoutStartCorner.SouthEast,
                TileLayoutStartCorner.NorthWest,
                TileLayoutStartCorner.NorthEast
            })
            {
                var parameters = new TileLayoutParameters(500.0, 300.0, corner);
                TileLayoutResult expected = TileGridCalculator.Calculate(
                    rectangle,
                    parameters);
                OrthogonalTileLayoutResult actual =
                    OrthogonalTileGridCalculator.Calculate(room, parameters);

                Assert.AreEqual(expected.DivisionLines.Count, actual.DivisionLines.Count);
                for (int index = 0; index < expected.DivisionLines.Count; index++)
                {
                    AssertLine(expected.DivisionLines[index], actual.DivisionLines[index]);
                }
            }
        }

        [TestMethod]
        public void Calculate_LegalFragmentJustAboveTolerance_IsPreserved()
        {
            double narrowWidth = GeometryTolerance.Coordinate * 2.0;
            AxisAlignedOrthogonalPolygon room = ValidateRoom(
                new Point3D(0.0, 0.0),
                new Point3D(2.0, 0.0),
                new Point3D(2.0, 1.0),
                new Point3D(narrowWidth, 1.0),
                new Point3D(narrowWidth, 2.0),
                new Point3D(0.0, 2.0));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(10.0, 1.0));

            Assert.AreEqual(1, result.DivisionLines.Count);
            AssertHorizontal(result.DivisionLines[0], 1.0, 0.0, narrowWidth);
        }

        [TestMethod]
        [Timeout(5000)]
        public void Calculate_ExactlyTenThousandFinalFragments_IsAllowed()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(BuildCombVertices(100));

            OrthogonalTileLayoutResult result = OrthogonalTileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(1000.0, 1.0));

            Assert.AreEqual(10000, result.DivisionLines.Count);
        }

        [TestMethod]
        [Timeout(5000)]
        public void Calculate_TenThousandOneOrMoreFinalFragments_IsRejected()
        {
            AxisAlignedOrthogonalPolygon room = ValidateRoom(BuildCombVertices(101));

            TileLayoutLimitExceededException exception = null;
            try
            {
                OrthogonalTileGridCalculator.Calculate(
                    room,
                    new TileLayoutParameters(1000.0, 1.0));
            }
            catch (TileLayoutLimitExceededException caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception.EstimatedDivisionLineCount > 10000.0);
            Assert.AreEqual(10000, exception.MaximumDivisionLineCount);
        }

        private static AxisAlignedOrthogonalPolygon ValidateRoom(
            params Point3D[] vertices)
        {
            OrthogonalRoomValidationResult validation = OrthogonalRoomValidator.Validate(
                OrthogonalRoomValidatorTests.LinesFromVertices(vertices));
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            return validation.Room;
        }

        private static Point3D[] BuildCombVertices(int fingerCount)
        {
            double width = (fingerCount * 2.0) - 1.0;
            var vertices = new List<Point3D>
            {
                new Point3D(0.0, 0.0),
                new Point3D(width, 0.0),
                new Point3D(width, 101.0)
            };

            for (int finger = fingerCount - 1; finger >= 0; finger--)
            {
                double left = finger * 2.0;
                vertices.Add(new Point3D(left, 101.0));
                if (finger > 0)
                {
                    vertices.Add(new Point3D(left, 1.0));
                    vertices.Add(new Point3D(left - 1.0, 1.0));
                    vertices.Add(new Point3D(left - 1.0, 101.0));
                }
            }

            vertices.Add(new Point3D(0.0, 0.0));
            vertices.RemoveAt(vertices.Count - 1);
            return vertices.ToArray();
        }

        private static void AssertVertical(
            LineSegment3D line,
            double x,
            double south,
            double north)
        {
            Assert.AreEqual(x, line.Start.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(x, line.End.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(south, line.Start.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(north, line.End.Y, GeometryTolerance.Coordinate);
        }

        private static void AssertHorizontal(
            LineSegment3D line,
            double y,
            double west,
            double east)
        {
            Assert.AreEqual(west, line.Start.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(east, line.End.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(y, line.Start.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(y, line.End.Y, GeometryTolerance.Coordinate);
        }

        private static void AssertLine(LineSegment3D expected, LineSegment3D actual)
        {
            Assert.AreEqual(expected.Start.X, actual.Start.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.Start.Y, actual.Start.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.Start.Z, actual.Start.Z, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.End.X, actual.End.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.End.Y, actual.End.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.End.Z, actual.End.Z, GeometryTolerance.Coordinate);
        }
    }
}
