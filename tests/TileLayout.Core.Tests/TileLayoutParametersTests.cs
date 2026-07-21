using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class TileLayoutParametersTests
    {
        [TestMethod]
        public void Constructor_SquareAndRectangularTiles_PreserveDimensions()
        {
            var square = new TileLayoutParameters(600.0, 600.0);
            var rectangle = new TileLayoutParameters(600.0, 1200.0);

            Assert.AreEqual(600.0, square.TileWidth);
            Assert.AreEqual(600.0, square.TileHeight);
            Assert.AreEqual(TileLayoutStartCorner.SouthWest, square.StartCorner);
            Assert.AreEqual(600.0, rectangle.TileWidth);
            Assert.AreEqual(1200.0, rectangle.TileHeight);
            Assert.AreEqual(TileLayoutStartCorner.SouthWest, rectangle.StartCorner);
        }

        [TestMethod]
        public void Contract_DimensionsAreImmutable()
        {
            Assert.IsFalse(
                typeof(TileLayoutParameters)
                    .GetProperty(nameof(TileLayoutParameters.TileWidth))
                    .CanWrite);
            Assert.IsFalse(
                typeof(TileLayoutParameters)
                    .GetProperty(nameof(TileLayoutParameters.TileHeight))
                    .CanWrite);
            Assert.IsFalse(
                typeof(TileLayoutParameters)
                    .GetProperty(nameof(TileLayoutParameters.StartCorner))
                    .CanWrite);
        }

        [TestMethod]
        public void Constructor_AllFourStartCorners_PreserveDirectionFlags()
        {
            var southWest = new TileLayoutParameters(
                600.0,
                600.0,
                TileLayoutStartCorner.SouthWest);
            var southEast = new TileLayoutParameters(
                600.0,
                600.0,
                TileLayoutStartCorner.SouthEast);
            var northWest = new TileLayoutParameters(
                600.0,
                600.0,
                TileLayoutStartCorner.NorthWest);
            var northEast = new TileLayoutParameters(
                600.0,
                600.0,
                TileLayoutStartCorner.NorthEast);

            Assert.IsFalse(southWest.StartsFromEast);
            Assert.IsFalse(southWest.StartsFromNorth);
            Assert.IsTrue(southEast.StartsFromEast);
            Assert.IsFalse(southEast.StartsFromNorth);
            Assert.IsFalse(northWest.StartsFromEast);
            Assert.IsTrue(northWest.StartsFromNorth);
            Assert.IsTrue(northEast.StartsFromEast);
            Assert.IsTrue(northEast.StartsFromNorth);
        }

        [TestMethod]
        public void Constructor_InvalidStartCorner_ThrowsArgumentOutOfRangeException()
        {
            try
            {
                new TileLayoutParameters(
                    600.0,
                    600.0,
                    (TileLayoutStartCorner)99);
                Assert.Fail("Expected an ArgumentOutOfRangeException.");
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Assert.AreEqual("startCorner", exception.ParamName);
            }
        }

        [TestMethod]
        public void Constructor_InvalidWidth_ThrowsArgumentOutOfRangeException()
        {
            foreach (double invalidValue in InvalidTileSizes())
            {
                AssertInvalidDimension(invalidValue, 600.0, "tileWidth");
            }
        }

        [TestMethod]
        public void Constructor_InvalidHeight_ThrowsArgumentOutOfRangeException()
        {
            foreach (double invalidValue in InvalidTileSizes())
            {
                AssertInvalidDimension(600.0, invalidValue, "tileHeight");
            }
        }

        [TestMethod]
        public void Constructor_DimensionsAboveTolerance_AreAccepted()
        {
            double validSize = GeometryTolerance.Coordinate * 1.0001;

            var parameters = new TileLayoutParameters(validSize, validSize);

            Assert.AreEqual(validSize, parameters.TileWidth);
            Assert.AreEqual(validSize, parameters.TileHeight);
        }

        private static double[] InvalidTileSizes()
        {
            return new[]
            {
                0.0,
                -1.0,
                GeometryTolerance.Coordinate,
                GeometryTolerance.Coordinate * 0.5,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity
            };
        }

        private static void AssertInvalidDimension(
            double tileWidth,
            double tileHeight,
            string expectedParameterName)
        {
            try
            {
                new TileLayoutParameters(tileWidth, tileHeight);
                Assert.Fail("Expected an ArgumentOutOfRangeException.");
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Assert.AreEqual(expectedParameterName, exception.ParamName);
            }
        }
    }
}
