using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class GeometryToleranceTests
    {
        [TestMethod]
        public void NearlyEqual_DifferenceAtTolerance_ReturnsTrue()
        {
            Assert.IsTrue(
                GeometryTolerance.NearlyEqual(100.0, 100.0 + GeometryTolerance.Coordinate));
        }

        [TestMethod]
        public void NearlyEqual_DifferenceBeyondTolerance_ReturnsFalse()
        {
            Assert.IsFalse(
                GeometryTolerance.NearlyEqual(
                    100.0,
                    100.0 + (GeometryTolerance.Coordinate * 2.0)));
        }
    }
}
