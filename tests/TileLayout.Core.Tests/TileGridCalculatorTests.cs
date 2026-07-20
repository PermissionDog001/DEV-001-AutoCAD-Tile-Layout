using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class TileGridCalculatorTests
    {
        [TestMethod]
        public void Calculate_DivisibleDimensions_ReturnsNoRemainders()
        {
            TileLayoutResult result = Calculate(3600.0, 3000.0);

            AssertLayout(result, 6, 5, 0.0, 0.0, 9);
            AssertVerticalLine(result.DivisionLines[0], 600.0, 0.0, 3000.0);
            AssertVerticalLine(result.DivisionLines[4], 3000.0, 0.0, 3000.0);
            AssertHorizontalLine(result.DivisionLines[5], 600.0, 0.0, 3600.0);
            AssertHorizontalLine(result.DivisionLines[8], 2400.0, 0.0, 3600.0);
        }

        [TestMethod]
        public void Calculate_EastRemainderOnly_ReturnsExpectedLayout()
        {
            TileLayoutResult result = Calculate(4250.0, 3000.0);

            AssertLayout(result, 7, 5, 50.0, 0.0, 11);
            AssertVerticalLine(result.DivisionLines[6], 4200.0, 0.0, 3000.0);
        }

        [TestMethod]
        public void Calculate_NorthRemainderOnly_ReturnsExpectedLayout()
        {
            TileLayoutResult result = Calculate(3600.0, 3100.0);

            AssertLayout(result, 6, 5, 0.0, 100.0, 10);
            AssertHorizontalLine(result.DivisionLines[9], 3000.0, 0.0, 3600.0);
        }

        [TestMethod]
        public void Calculate_RemaindersInBothDirections_ReturnsExpectedLayout()
        {
            TileLayoutResult result = Calculate(4250.0, 3100.0);

            AssertLayout(result, 7, 5, 50.0, 100.0, 12);
        }

        [TestMethod]
        [DataRow(500.0, 1200.0, 0, 2, 500.0, 0.0, 1)]
        [DataRow(1200.0, 500.0, 2, 0, 0.0, 500.0, 1)]
        [DataRow(500.0, 500.0, 0, 0, 500.0, 500.0, 0)]
        public void Calculate_DimensionBelowTileSize_ReturnsValidLayout(
            double width,
            double height,
            int columns,
            int rows,
            double eastRemainder,
            double northRemainder,
            int lineCount)
        {
            TileLayoutResult result = Calculate(width, height);

            AssertLayout(
                result,
                columns,
                rows,
                eastRemainder,
                northRemainder,
                lineCount);
        }

        [TestMethod]
        public void Calculate_ExactlyOneTile_ReturnsNoInternalLines()
        {
            TileLayoutResult result = Calculate(600.0, 600.0);

            AssertLayout(result, 1, 1, 0.0, 0.0, 0);
        }

        [TestMethod]
        public void Calculate_NearTileMultipleWithinTolerance_SnapsRemainderToZero()
        {
            double width = 1200.0 - (GeometryTolerance.Coordinate * 0.5);

            TileLayoutResult result = Calculate(width, 600.0);

            AssertLayout(result, 2, 1, 0.0, 0.0, 1);
            AssertVerticalLine(result.DivisionLines[0], 600.0, 0.0, 600.0);
        }

        [TestMethod]
        public void Calculate_NearTileMultipleBeyondTolerance_DoesNotSnap()
        {
            double width = 1200.0 - (GeometryTolerance.Coordinate * 2.0);

            TileLayoutResult result = Calculate(width, 600.0);

            Assert.AreEqual(1, result.FullColumnCount);
            Assert.AreEqual(
                600.0 - (GeometryTolerance.Coordinate * 2.0),
                result.EastRemainder,
                GeometryTolerance.Coordinate / 10.0);
        }

        [TestMethod]
        public void Calculate_OffsetRectangle_PreservesOriginElevationAndLineOrder()
        {
            var room = new AxisAlignedRectangle(100.0, 1350.0, 200.0, 850.0, 25.0);

            TileLayoutResult result = TileGridCalculator.Calculate(room);

            Assert.AreEqual(3, result.DivisionLines.Count);
            AssertVerticalLine(result.DivisionLines[0], 700.0, 200.0, 850.0, 25.0);
            AssertVerticalLine(result.DivisionLines[1], 1300.0, 200.0, 850.0, 25.0);
            AssertHorizontalLine(result.DivisionLines[2], 800.0, 100.0, 1350.0, 25.0);
        }

        private static TileLayoutResult Calculate(double width, double height)
        {
            return TileGridCalculator.Calculate(
                new AxisAlignedRectangle(0.0, width, 0.0, height));
        }

        private static void AssertLayout(
            TileLayoutResult result,
            int columns,
            int rows,
            double eastRemainder,
            double northRemainder,
            int lineCount)
        {
            Assert.AreEqual(columns, result.FullColumnCount);
            Assert.AreEqual(rows, result.FullRowCount);
            Assert.AreEqual(
                eastRemainder,
                result.EastRemainder,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                northRemainder,
                result.NorthRemainder,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(lineCount, result.DivisionLines.Count);
        }

        private static void AssertVerticalLine(
            LineSegment3D line,
            double x,
            double south,
            double north,
            double elevation = 0.0)
        {
            Assert.AreEqual(x, line.Start.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(x, line.End.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(south, line.Start.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(north, line.End.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(elevation, line.Start.Z, GeometryTolerance.Coordinate);
            Assert.AreEqual(elevation, line.End.Z, GeometryTolerance.Coordinate);
        }

        private static void AssertHorizontalLine(
            LineSegment3D line,
            double y,
            double west,
            double east,
            double elevation = 0.0)
        {
            Assert.AreEqual(west, line.Start.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(east, line.End.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(y, line.Start.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(y, line.End.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(elevation, line.Start.Z, GeometryTolerance.Coordinate);
            Assert.AreEqual(elevation, line.End.Z, GeometryTolerance.Coordinate);
        }
    }
}
