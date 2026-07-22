using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalRoomValidatorTests
    {
        [TestMethod]
        public void Validate_ThreeLines_IsRejected()
        {
            LineSegment3D[] lines = LinesFromVertices(
                    new Point3D(0.0, 0.0),
                    new Point3D(1000.0, 0.0),
                    new Point3D(1000.0, 800.0),
                    new Point3D(0.0, 800.0))
                .Take(3)
                .ToArray();

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(OrthogonalRoomValidationError.IncorrectLineCount, result.Error);
        }

        [TestMethod]
        public void Validate_NonAxisAlignedLine_IsRejected()
        {
            var lines = new[]
            {
                Segment(0.0, 0.0, 1000.0, 10.0),
                Segment(1000.0, 10.0, 1000.0, 800.0),
                Segment(1000.0, 800.0, 0.0, 800.0),
                Segment(0.0, 800.0, 0.0, 0.0)
            };

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.NonAxisAlignedLine,
                result.Error);
        }

        [TestMethod]
        public void Validate_DifferentElevation_IsRejected()
        {
            LineSegment3D[] lines = LinesFromVertices(
                new Point3D(0.0, 0.0),
                new Point3D(1000.0, 0.0),
                new Point3D(1000.0, 800.0),
                new Point3D(0.0, 800.0));
            lines[0] = new LineSegment3D(
                new Point3D(0.0, 0.0, GeometryTolerance.Coordinate * 2.0),
                lines[0].End);

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(OrthogonalRoomValidationError.NonCoplanar, result.Error);
        }

        [TestMethod]
        public void Validate_FragmentedRectangle_MergesCollinearSegmentsCanonically()
        {
            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(
                FragmentedRectangleLines());

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            AssertVertices(
                result.Room,
                new Point3D(0.0, 0.0),
                new Point3D(3600.0, 0.0),
                new Point3D(3600.0, 3000.0),
                new Point3D(0.0, 3000.0));
        }

        [TestMethod]
        public void Validate_LRoom_ReturnsCounterClockwiseCanonicalLoop()
        {
            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(
                LinesFromVertices(
                    new Point3D(1800.0, 600.0),
                    new Point3D(600.0, 600.0),
                    new Point3D(600.0, 1800.0),
                    new Point3D(0.0, 1800.0),
                    new Point3D(0.0, 0.0),
                    new Point3D(1800.0, 0.0)));

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            AssertVertices(
                result.Room,
                new Point3D(0.0, 0.0),
                new Point3D(1800.0, 0.0),
                new Point3D(1800.0, 600.0),
                new Point3D(600.0, 600.0),
                new Point3D(600.0, 1800.0),
                new Point3D(0.0, 1800.0));
        }

        [TestMethod]
        public void Validate_LargeWcsOffset_PreservesPositiveArea()
        {
            const double offset = 1e12;
            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(
                LinesFromVertices(
                    new Point3D(offset, offset),
                    new Point3D(offset + 3600.0, offset),
                    new Point3D(offset + 3600.0, offset + 3000.0),
                    new Point3D(offset, offset + 3000.0)));

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            Assert.AreEqual(3600.0, result.Room.Width, GeometryTolerance.Coordinate);
            Assert.AreEqual(3000.0, result.Room.Height, GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void Validate_ShuffledAndReversedInput_IsSelectionOrderIndependent()
        {
            LineSegment3D[] original = FragmentedRectangleLines().ToArray();
            var shuffled = new[]
            {
                Reverse(original[3]),
                Reverse(original[0]),
                Reverse(original[4]),
                Reverse(original[2]),
                Reverse(original[1])
            };

            AxisAlignedOrthogonalPolygon first =
                OrthogonalRoomValidator.Validate(original).Room;
            AxisAlignedOrthogonalPolygon second =
                OrthogonalRoomValidator.Validate(shuffled).Room;

            Assert.AreEqual(first.Vertices.Count, second.Vertices.Count);
            for (int index = 0; index < first.Vertices.Count; index++)
            {
                AssertPoint(first.Vertices[index], second.Vertices[index]);
            }
        }

        [TestMethod]
        public void Validate_EndpointGapWithinTolerance_SnapsDeterministically()
        {
            double deviation = GeometryTolerance.Coordinate * 0.5;
            var lines = new[]
            {
                Segment(0.0, 0.0, 1000.0, 0.0),
                Segment(1000.0 + deviation, 0.0, 1000.0 + deviation, 800.0),
                Segment(1000.0 + deviation, 800.0, 0.0, 800.0),
                Segment(0.0, 800.0, 0.0, 0.0)
            };

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.IsTrue(result.IsValid, result.ErrorMessage);
            Assert.AreEqual(
                1000.0 + (deviation / 2.0),
                result.Room.East,
                GeometryTolerance.Coordinate / 10.0);
        }

        [TestMethod]
        public void Validate_ChainedToleranceCluster_IsRejectedAsAmbiguous()
        {
            double step = GeometryTolerance.Coordinate * 0.75;
            var lines = new[]
            {
                Segment(0.0, 0.0, 1000.0, 0.0),
                Segment(1000.0 + step, 0.0, 1000.0 + (step * 2.0), 800.0),
                Segment(1000.0 + (step * 2.0), 800.0, 0.0, 800.0),
                Segment(0.0, 800.0, 0.0, 0.0)
            };

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.AmbiguousToleranceCluster,
                result.Error);
        }

        [TestMethod]
        public void Validate_GapBeyondTolerance_IsRejected()
        {
            double gap = GeometryTolerance.Coordinate * 2.0;
            var lines = new[]
            {
                Segment(0.0, 0.0, 1000.0, 0.0),
                Segment(1000.0 + gap, 0.0, 1000.0 + gap, 800.0),
                Segment(1000.0 + gap, 800.0, 0.0, 800.0),
                Segment(0.0, 800.0, 0.0, 0.0)
            };

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(OrthogonalRoomValidationError.InvalidVertexDegree, result.Error);
        }

        [TestMethod]
        public void Validate_ReverseDuplicate_IsRejected()
        {
            LineSegment3D[] rectangle = LinesFromVertices(
                new Point3D(0.0, 0.0),
                new Point3D(1000.0, 0.0),
                new Point3D(1000.0, 800.0),
                new Point3D(0.0, 800.0));
            LineSegment3D[] lines = rectangle
                .Concat(new[] { Reverse(rectangle[0]) })
                .ToArray();

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.DuplicateOrOverlappingLine,
                result.Error);
        }

        [TestMethod]
        public void Validate_PartialOverlap_IsRejected()
        {
            var lines = new[]
            {
                Segment(0.0, 0.0, 1000.0, 0.0),
                Segment(500.0, 0.0, 1500.0, 0.0),
                Segment(1000.0, 0.0, 1000.0, 800.0),
                Segment(1000.0, 800.0, 0.0, 800.0),
                Segment(0.0, 800.0, 0.0, 0.0)
            };

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.DuplicateOrOverlappingLine,
                result.Error);
        }

        [TestMethod]
        public void Validate_TJunction_IsRejected()
        {
            var lines = LinesFromVertices(
                    new Point3D(0.0, 0.0),
                    new Point3D(1000.0, 0.0),
                    new Point3D(1000.0, 800.0),
                    new Point3D(0.0, 800.0))
                .Concat(new[] { Segment(500.0, 0.0, 500.0, 400.0) })
                .ToArray();

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.IntersectingOrTouchingBoundary,
                result.Error);
        }

        [TestMethod]
        public void Validate_TwoSeparatedLoops_AreRejected()
        {
            LineSegment3D[] lines = LinesFromVertices(
                    new Point3D(0.0, 0.0),
                    new Point3D(1000.0, 0.0),
                    new Point3D(1000.0, 800.0),
                    new Point3D(0.0, 800.0))
                .Concat(LinesFromVertices(
                    new Point3D(2000.0, 0.0),
                    new Point3D(3000.0, 0.0),
                    new Point3D(3000.0, 800.0),
                    new Point3D(2000.0, 800.0)))
                .ToArray();

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.MultipleDisconnectedLoops,
                result.Error);
        }

        [TestMethod]
        public void Validate_SelfIntersection_IsRejected()
        {
            LineSegment3D[] lines = LinesFromVertices(
                new Point3D(0.0, 0.0),
                new Point3D(4.0, 0.0),
                new Point3D(4.0, 4.0),
                new Point3D(1.0, 4.0),
                new Point3D(1.0, 1.0),
                new Point3D(3.0, 1.0),
                new Point3D(3.0, 3.0),
                new Point3D(0.0, 3.0));

            OrthogonalRoomValidationResult result = OrthogonalRoomValidator.Validate(lines);

            Assert.AreEqual(
                OrthogonalRoomValidationError.IntersectingOrTouchingBoundary,
                result.Error);
        }

        internal static LineSegment3D[] LinesFromVertices(params Point3D[] vertices)
        {
            var lines = new LineSegment3D[vertices.Length];
            for (int index = 0; index < vertices.Length; index++)
            {
                lines[index] = new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]);
            }

            return lines;
        }

        private static IReadOnlyCollection<LineSegment3D> FragmentedRectangleLines()
        {
            return new[]
            {
                Segment(3600.0, 0.0, 1800.0, 0.0),
                Segment(0.0, 3000.0, 3600.0, 3000.0),
                Segment(0.0, 0.0, 0.0, 3000.0),
                Segment(3600.0, 3000.0, 3600.0, 0.0),
                Segment(0.0, 0.0, 1800.0, 0.0)
            };
        }

        private static LineSegment3D Segment(
            double startX,
            double startY,
            double endX,
            double endY)
        {
            return new LineSegment3D(
                new Point3D(startX, startY),
                new Point3D(endX, endY));
        }

        private static LineSegment3D Reverse(LineSegment3D line)
        {
            return new LineSegment3D(line.End, line.Start);
        }

        private static void AssertVertices(
            AxisAlignedOrthogonalPolygon room,
            params Point3D[] expected)
        {
            Assert.AreEqual(expected.Length, room.Vertices.Count);
            for (int index = 0; index < expected.Length; index++)
            {
                AssertPoint(expected[index], room.Vertices[index]);
            }
        }

        private static void AssertPoint(Point3D expected, Point3D actual)
        {
            Assert.AreEqual(expected.X, actual.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.Y, actual.Y, GeometryTolerance.Coordinate);
            Assert.AreEqual(expected.Z, actual.Z, GeometryTolerance.Coordinate);
        }
    }
}
