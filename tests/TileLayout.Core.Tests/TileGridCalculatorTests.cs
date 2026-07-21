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

        [TestMethod]
        [DataRow(4250.0, 3100.0, 600.0, 600.0, 7, 5, 50.0, 100.0, 12)]
        [DataRow(4250.0, 3100.0, 600.0, 1200.0, 7, 2, 50.0, 700.0, 9)]
        [DataRow(4250.0, 3100.0, 800.0, 800.0, 5, 3, 250.0, 700.0, 8)]
        [DataRow(3600.0, 3000.0, 600.0, 1000.0, 6, 3, 0.0, 0.0, 7)]
        [DataRow(500.0, 500.0, 600.0, 1200.0, 0, 0, 500.0, 500.0, 0)]
        public void Calculate_ParameterizedFrozenExamples_ReturnExpectedLayout(
            double roomWidth,
            double roomHeight,
            double tileWidth,
            double tileHeight,
            int columns,
            int rows,
            double eastRemainder,
            double northRemainder,
            int lineCount)
        {
            TileLayoutResult result = Calculate(
                roomWidth,
                roomHeight,
                tileWidth,
                tileHeight);

            AssertLayout(
                result,
                columns,
                rows,
                eastRemainder,
                northRemainder,
                lineCount);
            Assert.AreEqual(tileWidth, result.Parameters.TileWidth);
            Assert.AreEqual(tileHeight, result.Parameters.TileHeight);
        }

        [TestMethod]
        public void Calculate_SwappedRectangularTileDimensions_ChangesWcsDirections()
        {
            TileLayoutResult wide = Calculate(2500.0, 2500.0, 1000.0, 600.0);
            TileLayoutResult tall = Calculate(2500.0, 2500.0, 600.0, 1000.0);

            AssertVerticalLine(wide.DivisionLines[0], 1000.0, 0.0, 2500.0);
            AssertHorizontalLine(wide.DivisionLines[2], 600.0, 0.0, 2500.0);
            AssertVerticalLine(tall.DivisionLines[3], 2400.0, 0.0, 2500.0);
            AssertHorizontalLine(tall.DivisionLines[4], 1000.0, 0.0, 2500.0);
        }

        [TestMethod]
        public void Calculate_ParameterizedOffsetRoom_PreservesOriginElevationAndOrder()
        {
            var room = new AxisAlignedRectangle(100.0, 1350.0, 200.0, 850.0, 25.0);
            var parameters = new TileLayoutParameters(500.0, 300.0);

            TileLayoutResult result = TileGridCalculator.Calculate(room, parameters);

            Assert.AreSame(parameters, result.Parameters);
            Assert.AreEqual(4, result.DivisionLines.Count);
            AssertVerticalLine(result.DivisionLines[0], 600.0, 200.0, 850.0, 25.0);
            AssertVerticalLine(result.DivisionLines[1], 1100.0, 200.0, 850.0, 25.0);
            AssertHorizontalLine(result.DivisionLines[2], 500.0, 100.0, 1350.0, 25.0);
            AssertHorizontalLine(result.DivisionLines[3], 800.0, 100.0, 1350.0, 25.0);
        }

        [TestMethod]
        public void Calculate_AllStartCorners_ControlLineOrderAndRemainderSides()
        {
            var room = new AxisAlignedRectangle(100.0, 1350.0, 200.0, 850.0, 25.0);

            TileLayoutResult southWest = CalculateFromCorner(
                room,
                TileLayoutStartCorner.SouthWest);
            AssertCornerLayout(
                southWest,
                600.0,
                1100.0,
                500.0,
                800.0,
                0.0,
                250.0,
                0.0,
                50.0);

            TileLayoutResult southEast = CalculateFromCorner(
                room,
                TileLayoutStartCorner.SouthEast);
            AssertCornerLayout(
                southEast,
                850.0,
                350.0,
                500.0,
                800.0,
                250.0,
                0.0,
                0.0,
                50.0);

            TileLayoutResult northWest = CalculateFromCorner(
                room,
                TileLayoutStartCorner.NorthWest);
            AssertCornerLayout(
                northWest,
                600.0,
                1100.0,
                550.0,
                250.0,
                0.0,
                250.0,
                50.0,
                0.0);

            TileLayoutResult northEast = CalculateFromCorner(
                room,
                TileLayoutStartCorner.NorthEast);
            AssertCornerLayout(
                northEast,
                850.0,
                350.0,
                550.0,
                250.0,
                250.0,
                0.0,
                50.0,
                0.0);
        }

        [TestMethod]
        public void Calculate_NorthEastWithExactMultiples_DoesNotDuplicateBoundaries()
        {
            var room = new AxisAlignedRectangle(100.0, 1600.0, 200.0, 800.0, 25.0);
            TileLayoutResult result = TileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(
                    500.0,
                    300.0,
                    TileLayoutStartCorner.NorthEast));

            Assert.AreEqual(3, result.FullColumnCount);
            Assert.AreEqual(2, result.FullRowCount);
            Assert.AreEqual(3, result.DivisionLines.Count);
            AssertVerticalLine(result.DivisionLines[0], 1100.0, 200.0, 800.0, 25.0);
            AssertVerticalLine(result.DivisionLines[1], 600.0, 200.0, 800.0, 25.0);
            AssertHorizontalLine(result.DivisionLines[2], 500.0, 100.0, 1600.0, 25.0);
            Assert.AreEqual(0.0, result.HorizontalRemainder);
            Assert.AreEqual(0.0, result.VerticalRemainder);
        }

        [TestMethod]
        public void Calculate_DecimalTileDimensions_ReturnExpectedLayout()
        {
            TileLayoutResult result = Calculate(2.6, 1.65, 0.5, 0.4);

            AssertLayout(result, 5, 4, 0.1, 0.05, 9);
            AssertVerticalLine(result.DivisionLines[4], 2.5, 0.0, 1.65);
            AssertHorizontalLine(result.DivisionLines[8], 1.6, 0.0, 2.6);
        }

        [TestMethod]
        public void Calculate_ParameterizedMultipleWithinTolerance_SnapsRemainderToZero()
        {
            double width = 2.0 - (GeometryTolerance.Coordinate * 0.5);

            TileLayoutResult result = Calculate(width, 1.0, 0.5, 0.5);

            AssertLayout(result, 4, 2, 0.0, 0.0, 4);
        }

        [TestMethod]
        public void Calculate_TileLargerThanRoom_ReturnsNoInternalLines()
        {
            TileLayoutResult result = Calculate(500.0, 500.0, 800.0, 1200.0);

            AssertLayout(result, 0, 0, 500.0, 500.0, 0);
        }

        [TestMethod]
        public void Calculate_ExactlyMaximumParameterizedLines_IsAllowed()
        {
            TileLayoutResult result = Calculate(10001.0, 0.5, 1.0, 1.0);

            Assert.AreEqual(
                TileLayoutRules.MaximumParameterizedDivisionLineCount,
                result.DivisionLines.Count);
            AssertVerticalLine(
                result.DivisionLines[result.DivisionLines.Count - 1],
                10000.0,
                0.0,
                0.5);
        }

        [TestMethod]
        public void Calculate_OneAboveMaximumParameterizedLines_IsRejected()
        {
            TileLayoutLimitExceededException exception = AssertLimitExceeded(
                new AxisAlignedRectangle(0.0, 10002.0, 0.0, 0.5),
                new TileLayoutParameters(1.0, 1.0));

            Assert.AreEqual(10001.0, exception.EstimatedDivisionLineCount);
            Assert.AreEqual(
                TileLayoutRules.MaximumParameterizedDivisionLineCount,
                exception.MaximumDivisionLineCount);
        }

        [TestMethod]
        [Timeout(2000)]
        public void Calculate_ExtremelySmallValidTile_RejectsWithoutOverflowOrLargeAllocation()
        {
            double tinyTile = GeometryTolerance.Coordinate * 1.0001;

            TileLayoutLimitExceededException exception = AssertLimitExceeded(
                new AxisAlignedRectangle(0.0, 1e300, 0.0, 1.0),
                new TileLayoutParameters(tinyTile, tinyTile));

            Assert.IsTrue(
                exception.EstimatedDivisionLineCount
                    > TileLayoutRules.MaximumParameterizedDivisionLineCount);
        }

        [TestMethod]
        public void Calculate_Parameterized600_MatchesLegacyGeometryAndStatistics()
        {
            var room = new AxisAlignedRectangle(100.0, 4350.0, 200.0, 3300.0, 25.0);

            TileLayoutResult legacy = TileGridCalculator.Calculate(room);
            TileLayoutResult parameterized = TileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(600.0, 600.0));

            Assert.AreEqual(legacy.FullColumnCount, parameterized.FullColumnCount);
            Assert.AreEqual(legacy.FullRowCount, parameterized.FullRowCount);
            Assert.AreEqual(legacy.EastRemainder, parameterized.EastRemainder);
            Assert.AreEqual(legacy.NorthRemainder, parameterized.NorthRemainder);
            Assert.AreEqual(legacy.DivisionLines.Count, parameterized.DivisionLines.Count);
            for (int index = 0; index < legacy.DivisionLines.Count; index++)
            {
                AssertLineEqual(legacy.DivisionLines[index], parameterized.DivisionLines[index]);
            }
        }

        [TestMethod]
        public void Calculate_LegacyEntryReportsFixed600Parameters()
        {
            TileLayoutResult result = Calculate(4250.0, 3100.0);

            Assert.AreEqual(600.0, result.Parameters.TileWidth);
            Assert.AreEqual(600.0, result.Parameters.TileHeight);
        }

        [TestMethod]
        public void Calculate_LegacyEntry_IsNotSubjectToParameterizedLimit()
        {
            TileLayoutResult result = Calculate(600.0 * 10002.0, 500.0);

            Assert.AreEqual(10001, result.DivisionLines.Count);
        }

        private static TileLayoutResult Calculate(double width, double height)
        {
            return TileGridCalculator.Calculate(
                new AxisAlignedRectangle(0.0, width, 0.0, height));
        }

        private static TileLayoutResult Calculate(
            double roomWidth,
            double roomHeight,
            double tileWidth,
            double tileHeight)
        {
            return TileGridCalculator.Calculate(
                new AxisAlignedRectangle(0.0, roomWidth, 0.0, roomHeight),
                new TileLayoutParameters(tileWidth, tileHeight));
        }

        private static TileLayoutLimitExceededException AssertLimitExceeded(
            AxisAlignedRectangle room,
            TileLayoutParameters parameters)
        {
            try
            {
                TileGridCalculator.Calculate(room, parameters);
                Assert.Fail("Expected a TileLayoutLimitExceededException.");
                return null;
            }
            catch (TileLayoutLimitExceededException exception)
            {
                return exception;
            }
        }

        private static TileLayoutResult CalculateFromCorner(
            AxisAlignedRectangle room,
            TileLayoutStartCorner startCorner)
        {
            return TileGridCalculator.Calculate(
                room,
                new TileLayoutParameters(500.0, 300.0, startCorner));
        }

        private static void AssertCornerLayout(
            TileLayoutResult result,
            double firstVerticalX,
            double secondVerticalX,
            double firstHorizontalY,
            double secondHorizontalY,
            double westRemainder,
            double eastRemainder,
            double southRemainder,
            double northRemainder)
        {
            Assert.AreEqual(2, result.FullColumnCount);
            Assert.AreEqual(2, result.FullRowCount);
            Assert.AreEqual(4, result.DivisionLines.Count);
            AssertVerticalLine(
                result.DivisionLines[0],
                firstVerticalX,
                200.0,
                850.0,
                25.0);
            AssertVerticalLine(
                result.DivisionLines[1],
                secondVerticalX,
                200.0,
                850.0,
                25.0);
            AssertHorizontalLine(
                result.DivisionLines[2],
                firstHorizontalY,
                100.0,
                1350.0,
                25.0);
            AssertHorizontalLine(
                result.DivisionLines[3],
                secondHorizontalY,
                100.0,
                1350.0,
                25.0);
            Assert.AreEqual(
                westRemainder,
                result.WestRemainder,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                eastRemainder,
                result.EastRemainder,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                southRemainder,
                result.SouthRemainder,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                northRemainder,
                result.NorthRemainder,
                GeometryTolerance.Coordinate);
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

        private static void AssertLineEqual(LineSegment3D expected, LineSegment3D actual)
        {
            Assert.AreEqual(expected.Start.X, actual.Start.X);
            Assert.AreEqual(expected.Start.Y, actual.Start.Y);
            Assert.AreEqual(expected.Start.Z, actual.Start.Z);
            Assert.AreEqual(expected.End.X, actual.End.X);
            Assert.AreEqual(expected.End.Y, actual.End.Y);
            Assert.AreEqual(expected.End.Z, actual.End.Z);
        }
    }
}
