using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class DoorGeometryRecognizerTests
    {
        [TestMethod]
        public void Recognize_UniqueSingleSwingSignature_ReturnsHingeAndClosedJamb()
        {
            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 1000.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 1000.0),
                            new Point3D(1000.0, 0.0),
                            1000.0)
                    });

            Assert.AreEqual(DoorGeometryRecognitionStatus.High, result.Status);
            Assert.AreEqual(DoorGeometryRejectionCode.None, result.RejectionCode);
            AssertPoint(0.0, 0.0, result.FirstOpeningPoint);
            AssertPoint(1000.0, 0.0, result.SecondOpeningPoint);
        }

        [TestMethod]
        public void Recognize_ReversedLineAndArcEndpoints_NormalizesSameOpening()
        {
            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 1000.0),
                            new Point3D(0.0, 0.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(1000.0, 0.0),
                            new Point3D(0.0, 1000.0),
                            1000.0)
                    });

            Assert.IsTrue(result.IsHigh);
            AssertPoint(0.0, 0.0, result.FirstOpeningPoint);
            AssertPoint(1000.0, 0.0, result.SecondOpeningPoint);
        }

        [TestMethod]
        public void Recognize_RotatedAndMirroredGeometry_PreservesUniqueSignature()
        {
            var sourceLine = new LineSegment3D(
                new Point3D(1200.0, 300.0),
                new Point3D(1200.0, 1300.0));
            var sourceArc = new ArcSegment3D(
                new Point3D(1200.0, 300.0),
                new Point3D(1200.0, 1300.0),
                new Point3D(2200.0, 300.0),
                1000.0);

            foreach (double quarterTurns in new[] { 0.0, 1.0, 2.0, 3.0 })
            {
                foreach (bool mirror in new[] { false, true })
                {
                    Func<Point3D, Point3D> transform = point => Transform(
                        point,
                        (int)quarterTurns,
                        mirror,
                        500000.0,
                        -700000.0);
                    DoorGeometryRecognitionResult result =
                        DoorGeometryRecognizer.Recognize(
                            new[]
                            {
                                new LineSegment3D(
                                    transform(sourceLine.Start),
                                    transform(sourceLine.End))
                            },
                            new[]
                            {
                                new ArcSegment3D(
                                    transform(sourceArc.Center),
                                    transform(sourceArc.Start),
                                    transform(sourceArc.End),
                                    sourceArc.Radius)
                            });

                    Assert.IsTrue(
                        result.IsHigh,
                        string.Format(
                            "Expected High for rotation {0}, mirror {1}.",
                            quarterTurns,
                            mirror));
                    AssertPoint(
                        transform(sourceArc.Center),
                        result.FirstOpeningPoint);
                    AssertPoint(
                        transform(sourceArc.End),
                        result.SecondOpeningPoint);
                }
            }
        }

        [TestMethod]
        public void Recognize_DuplicateGeometryForSameEndpoints_RemainsUnique()
        {
            var line = new LineSegment3D(
                new Point3D(0.0, 0.0),
                new Point3D(0.0, 1000.0));
            var arc = new ArcSegment3D(
                new Point3D(0.0, 0.0),
                new Point3D(0.0, 1000.0),
                new Point3D(1000.0, 0.0),
                1000.0);

            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[] { line, line },
                    new[] { arc, arc });

            Assert.IsTrue(result.IsHigh);
            Assert.AreEqual(1, result.DistinctCandidateCount);
        }

        [TestMethod]
        public void Recognize_TwoSingleSwingSignatures_IsAmbiguous()
        {
            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 1000.0)),
                        new LineSegment3D(
                            new Point3D(3000.0, 0.0),
                            new Point3D(3000.0, 1000.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 1000.0),
                            new Point3D(1000.0, 0.0),
                            1000.0),
                        new ArcSegment3D(
                            new Point3D(3000.0, 0.0),
                            new Point3D(3000.0, 1000.0),
                            new Point3D(4000.0, 0.0),
                            1000.0)
                    });

            Assert.AreEqual(
                DoorGeometryRecognitionStatus.Ambiguous,
                result.Status);
            Assert.AreEqual(
                DoorGeometryRejectionCode.MultipleDistinctSignatures,
                result.RejectionCode);
            Assert.AreEqual(2, result.DistinctCandidateCount);
        }

        [TestMethod]
        public void Recognize_NoArc_IsUnsupported()
        {
            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 1000.0))
                    },
                    new ArcSegment3D[0]);

            Assert.AreEqual(
                DoorGeometryRecognitionStatus.Unsupported,
                result.Status);
            Assert.AreEqual(
                DoorGeometryRejectionCode.NoCompleteSingleSwingSignature,
                result.RejectionCode);
        }

        [TestMethod]
        public void Recognize_LineAndArcWithoutSharedFreeEnd_IsUnsupported()
        {
            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 900.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(0.0, 0.0),
                            new Point3D(0.0, 1000.0),
                            new Point3D(1000.0, 0.0),
                            1000.0)
                    });

            Assert.AreEqual(
                DoorGeometryRecognitionStatus.Unsupported,
                result.Status);
        }

        [TestMethod]
        public void Recognize_NonFiniteGeometry_IsUnsupportedWithDedicatedCode()
        {
            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(double.NaN, 0.0),
                            new Point3D(0.0, 1000.0))
                    },
                    new ArcSegment3D[0]);

            Assert.AreEqual(
                DoorGeometryRejectionCode.NonFiniteGeometry,
                result.RejectionCode);
        }

        private static Point3D Transform(
            Point3D point,
            int quarterTurns,
            bool mirror,
            double offsetX,
            double offsetY)
        {
            double x = mirror ? -point.X : point.X;
            double y = point.Y;
            double rotatedX;
            double rotatedY;
            switch (quarterTurns)
            {
                case 0:
                    rotatedX = x;
                    rotatedY = y;
                    break;
                case 1:
                    rotatedX = -y;
                    rotatedY = x;
                    break;
                case 2:
                    rotatedX = -x;
                    rotatedY = -y;
                    break;
                default:
                    rotatedX = y;
                    rotatedY = -x;
                    break;
            }

            return new Point3D(
                rotatedX + offsetX,
                rotatedY + offsetY,
                point.Z);
        }

        private static void AssertPoint(
            double expectedX,
            double expectedY,
            Point3D actual)
        {
            Assert.AreEqual(expectedX, actual.X, GeometryTolerance.Coordinate);
            Assert.AreEqual(expectedY, actual.Y, GeometryTolerance.Coordinate);
        }

        private static void AssertPoint(Point3D expected, Point3D actual)
        {
            AssertPoint(expected.X, expected.Y, actual);
            Assert.AreEqual(
                expected.Z,
                actual.Z,
                GeometryTolerance.Coordinate);
        }
    }
}
