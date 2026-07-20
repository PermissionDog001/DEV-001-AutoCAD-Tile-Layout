using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class RectangleValidatorTests
    {
        [TestMethod]
        public void Validate_UnorderedAndReversedRectangleLines_ReturnsRectangle()
        {
            RectangleValidationResult result = RectangleValidator.Validate(
                TestGeometry.RectangleLines(100.0, 3700.0, 200.0, 3200.0, 25.0));

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(RectangleValidationError.None, result.Error);
            Assert.AreEqual(100.0, result.Rectangle.West, GeometryTolerance.Coordinate);
            Assert.AreEqual(3700.0, result.Rectangle.East, GeometryTolerance.Coordinate);
            Assert.AreEqual(200.0, result.Rectangle.South, GeometryTolerance.Coordinate);
            Assert.AreEqual(3200.0, result.Rectangle.North, GeometryTolerance.Coordinate);
            Assert.AreEqual(25.0, result.Rectangle.Elevation, GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void Validate_ThreeLines_ReturnsIncorrectLineCount()
        {
            LineSegment3D[] lines = TestGeometry
                .RectangleLines(0.0, 3600.0, 0.0, 3000.0)
                .Take(3)
                .ToArray();

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.IncorrectLineCount, result.Error);
            Assert.IsNull(result.Rectangle);
        }

        [TestMethod]
        public void Validate_FourLinesWithDuplicateSide_ReturnsDuplicateOrMissingSide()
        {
            IReadOnlyCollection<LineSegment3D> rectangle =
                TestGeometry.RectangleLines(0.0, 3600.0, 0.0, 3000.0);
            LineSegment3D south = rectangle.ElementAt(1);
            var lines = new[]
            {
                rectangle.ElementAt(0),
                south,
                rectangle.ElementAt(2),
                new LineSegment3D(south.End, south.Start)
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(
                RectangleValidationError.DuplicateOrMissingSide,
                result.Error);
        }

        [TestMethod]
        public void Validate_NonClosedBoundary_ReturnsNonClosedBoundary()
        {
            var lines = new[]
            {
                new LineSegment3D(new Point3D(0.0, 0.0), new Point3D(3600.0, 0.0)),
                new LineSegment3D(new Point3D(3600.0, 0.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(100.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.NonClosedBoundary, result.Error);
        }

        [TestMethod]
        public void Validate_NonAxisAlignedLine_ReturnsNonAxisAlignedLine()
        {
            var lines = new[]
            {
                new LineSegment3D(new Point3D(0.0, 0.0), new Point3D(3600.0, 10.0)),
                new LineSegment3D(new Point3D(3600.0, 10.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.NonAxisAlignedLine, result.Error);
        }

        [TestMethod]
        public void Validate_NonFiniteCoordinate_ReturnsNonFiniteCoordinate()
        {
            var lines = new[]
            {
                new LineSegment3D(new Point3D(double.NaN, 0.0), new Point3D(3600.0, 0.0)),
                new LineSegment3D(new Point3D(3600.0, 0.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.NonFiniteCoordinate, result.Error);
        }

        [TestMethod]
        public void Validate_DegenerateLine_ReturnsDegenerateLine()
        {
            var lines = new[]
            {
                new LineSegment3D(
                    new Point3D(0.0, 0.0),
                    new Point3D(GeometryTolerance.Coordinate * 0.5, 0.0)),
                new LineSegment3D(new Point3D(3600.0, 0.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.DegenerateLine, result.Error);
        }

        [TestMethod]
        public void Validate_CollinearLines_ReturnsNonPositiveDimensions()
        {
            var lines = new[]
            {
                new LineSegment3D(new Point3D(0.0, 0.0), new Point3D(0.0, 1000.0)),
                new LineSegment3D(new Point3D(0.0, 1000.0), new Point3D(0.0, 2000.0)),
                new LineSegment3D(new Point3D(0.0, 2000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.NonPositiveDimensions, result.Error);
        }

        [TestMethod]
        public void Validate_ElevationBeyondTolerance_ReturnsNonCoplanar()
        {
            LineSegment3D[] lines = TestGeometry
                .RectangleLines(0.0, 3600.0, 0.0, 3000.0)
                .ToArray();
            lines[0] = new LineSegment3D(
                new Point3D(3600.0, 3000.0, GeometryTolerance.Coordinate * 2.0),
                lines[0].End);

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.NonCoplanar, result.Error);
        }

        [TestMethod]
        public void Validate_CoordinateDeviationWithinTolerance_IsAccepted()
        {
            double deviation = GeometryTolerance.Coordinate * 0.5;
            var lines = new[]
            {
                new LineSegment3D(new Point3D(0.0, 0.0), new Point3D(3600.0, deviation)),
                new LineSegment3D(new Point3D(3600.0, 0.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
        }

        [TestMethod]
        public void Validate_CoordinateDeviationBeyondTolerance_IsRejected()
        {
            double deviation = GeometryTolerance.Coordinate * 2.0;
            var lines = new[]
            {
                new LineSegment3D(new Point3D(0.0, 0.0), new Point3D(3600.0, deviation)),
                new LineSegment3D(new Point3D(3600.0, 0.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult result = RectangleValidator.Validate(lines);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(RectangleValidationError.NonAxisAlignedLine, result.Error);
        }
    }
}
