using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class OrthogonalBoundaryNormalizerTests
    {
        [TestMethod]
        public void Analyze_ExactWcsBoundary_RemainsExact()
        {
            OrthogonalBoundaryNormalizationResult result =
                OrthogonalBoundaryNormalizer.Analyze(
                    RectangleLines(0.0));

            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.ExactWcs,
                result.Status);
            Assert.IsTrue(result.IsAccepted);
            Assert.IsFalse(result.WasNormalized);
            Assert.AreEqual(GeometryTolerance.Coordinate, result.PointMatchTolerance);
        }

        [TestMethod]
        public void PolylineConverter_MergesOnlyDeterministicDuplicateVertices()
        {
            IReadOnlyCollection<LineSegment3D> segments =
                GuidedBoundaryPolylineConverter.BuildSegments(
                    new List<Point3D>
                    {
                        new Point3D(0.0, 0.0),
                        new Point3D(1000.0, 0.0),
                        new Point3D(1000.0, 1000.0),
                        new Point3D(1000.0, 1000.0),
                        new Point3D(0.0, 1000.0),
                        new Point3D(0.0, 0.0),
                        new Point3D(0.0, 0.0)
                    });

            Assert.AreEqual(4, segments.Count);
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(segments);
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            Assert.AreEqual(1000.0, validation.Room.Width,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(1000.0, validation.Room.Height,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void PolylineConverter_ClosesSmallOpenEndpointGapLikeLineInput()
        {
            IReadOnlyCollection<LineSegment3D> segments =
                GuidedBoundaryPolylineConverter.BuildSegments(
                    new List<Point3D>
                    {
                        new Point3D(
                            3905.6685588740365,
                            5704.0967723746417),
                        new Point3D(
                            10256.184531607534,
                            5704.0967761893389),
                        new Point3D(
                            10256.544422724153,
                            2554.0982305426714),
                        new Point3D(
                            4455.3381981634157,
                            2554.0982305426714),
                        new Point3D(
                            4455.3381981634157,
                            2654.0998832602618),
                        new Point3D(
                            3905.7765197538392,
                            2654.0804302115557),
                        new Point3D(
                            3905.7765185021417,
                            5704.0967723746417)
                    },
                    true);

            Assert.AreEqual(6, segments.Count);
            OrthogonalBoundaryNormalizationResult normalized =
                OrthogonalBoundaryNormalizer.Analyze(segments);
            Assert.IsTrue(normalized.IsAccepted, normalized.Message);

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(normalized.Lines);
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            Assert.AreEqual(6, validation.Room.Vertices.Count);
        }

        [TestMethod]
        public void PolylineConverter_PreservesAxisAlignedConcaveTopology()
        {
            IReadOnlyCollection<LineSegment3D> segments =
                GuidedBoundaryPolylineConverter.BuildSegments(
                    new List<Point3D>
                    {
                        new Point3D(0.0, 0.0),
                        new Point3D(1800.0, 0.0),
                        new Point3D(1800.0, 600.0),
                        new Point3D(600.0, 600.0),
                        new Point3D(600.0, 1800.0),
                        new Point3D(0.0, 1800.0)
                    });

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(segments);
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            Assert.AreEqual(6, validation.Room.Vertices.Count);
        }

        [TestMethod]
        public void Analyze_SmallCommonRotation_BuildsNearOrthogonalCopy()
        {
            OrthogonalBoundaryNormalizationResult result =
                OrthogonalBoundaryNormalizer.Analyze(
                    RectangleLines(0.02));

            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.NearOrthogonal,
                result.Status,
                result.Message);
            Assert.IsTrue(result.WasNormalized);
            Assert.IsTrue(
                result.MaximumAngleDeviationDegrees
                    <= GeometryTolerance.NearOrthogonalAngleDegrees);
            Assert.IsTrue(
                result.MaximumEndpointCorrection
                    <= GeometryTolerance.NearOrthogonalMaximumEndpointCorrection
                        + GeometryTolerance.Coordinate);

            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(result.Lines);
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            Assert.AreEqual(6000.0, validation.Room.Width, 0.01);
            Assert.AreEqual(3000.0, validation.Room.Height, 0.01);
        }

        [TestMethod]
        public void Analyze_SmallCommonRotation_PreservesComplexOrthogonalTopology()
        {
            IList<Point3D> source = ComplexOrthogonalBoundaryFixture.Vertices();
            double radians = ToRadians(0.02);
            var rotated = new List<LineSegment3D>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                rotated.Add(new LineSegment3D(
                    Rotate(source[index], radians),
                    Rotate(source[(index + 1) % source.Count], radians)));
            }

            OrthogonalBoundaryNormalizationResult result =
                OrthogonalBoundaryNormalizer.Analyze(rotated);

            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.NearOrthogonal,
                result.Status,
                result.Message);
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(result.Lines);
            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            Assert.AreEqual(source.Count, validation.Room.Vertices.Count);
        }

        [TestMethod]
        public void Analyze_RotationBeyondAngleLimit_IsRejectedWithLineDiagnostic()
        {
            OrthogonalBoundaryNormalizationResult result =
                OrthogonalBoundaryNormalizer.Analyze(
                    RectangleLines(0.2));

            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.Rejected,
                result.Status);
            Assert.IsFalse(result.IsAccepted);
            Assert.IsTrue(result.LineDiagnostics.Count > 0);
            Assert.IsTrue(result.MaximumAngleDeviationDegrees
                > GeometryTolerance.NearOrthogonalAngleDegrees);
            StringAssert.Contains(result.Message, "第");
            StringAssert.Contains(result.Message, "超过近似正交阈值");
        }

        [TestMethod]
        public void Analyze_LongLineCorrectionBeyondLimit_IsRejected()
        {
            var lines = new[]
            {
                new LineSegment3D(
                    new Point3D(0.0, 0.0),
                    new Point3D(
                        10000.0 * Math.Cos(ToRadians(0.04)),
                        10000.0 * Math.Sin(ToRadians(0.04))))
            };

            OrthogonalBoundaryNormalizationResult result =
                OrthogonalBoundaryNormalizer.Analyze(lines);

            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.Rejected,
                result.Status);
            StringAssert.Contains(result.Message, "端点修正");
            Assert.IsTrue(result.MaximumEndpointCorrection
                > GeometryTolerance.NearOrthogonalMaximumEndpointCorrection);
        }

        [TestMethod]
        public void InputSession_UsesNormalizedCopyOnlyForNearOrthogonalRoom()
        {
            var input = new OrthogonalDecisionInputSession();

            OrthogonalRoomValidationResult validation = input.LoadBoundary(
                RectangleLines(0.02),
                600.0,
                600.0);

            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            Assert.IsTrue(input.BoundaryWasNormalized);
            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.NearOrthogonal,
                input.BoundaryNormalization.Status);
            Assert.IsTrue(
                input.BoundaryPointMatchTolerance
                    > GeometryTolerance.Coordinate);
            Assert.AreEqual(6000.0, input.Room.Width, 0.01);
            Assert.AreEqual(3000.0, input.Room.Height, 0.01);
        }

        [TestMethod]
        public void InputSession_DoesNotAcceptIntentionalSlopedRoom()
        {
            var input = new OrthogonalDecisionInputSession();

            OrthogonalRoomValidationResult validation = input.LoadBoundary(
                RectangleLines(0.2),
                600.0,
                600.0);

            Assert.IsFalse(validation.IsValid);
            Assert.IsNull(input.Room);
            Assert.IsFalse(input.BoundaryWasNormalized);
            Assert.AreEqual(
                OrthogonalBoundaryNormalizationStatus.Rejected,
                input.BoundaryNormalization.Status);
        }

        [TestMethod]
        public void GuidedWorkflow_ReportsReadOnlyNormalizationWithoutChangingSourceLines()
        {
            var workflow = new OrthogonalDecisionGuidedWorkflow();

            OrthogonalRoomValidationResult validation = workflow.LoadBoundary(
                RectangleLines(0.02),
                600.0,
                600.0);

            Assert.IsTrue(validation.IsValid, validation.ErrorMessage);
            StringAssert.Contains(workflow.Notice, "只读正交计算副本");
            Assert.IsTrue(workflow.Input.BoundaryWasNormalized);
            Assert.IsFalse(workflow.HasWriteAuthorization);
        }

        [TestMethod]
        public void FormatBoundaryNormalization_ListsFixedLimitsAndEachLine()
        {
            OrthogonalBoundaryNormalizationResult result =
                OrthogonalBoundaryNormalizer.Analyze(
                    RectangleLines(0.02));

            string text = OrthogonalDecisionGuidedText
                .FormatBoundaryNormalization(result);

            StringAssert.Contains(text, "只读正交计算副本");
            StringAssert.Contains(text, "角度");
            StringAssert.Contains(text, "端点修正");
            StringAssert.Contains(text, "端点连接容差");
            StringAssert.Contains(text, "第 1 条");
            StringAssert.Contains(text, "第 4 条");
        }

        private static IReadOnlyCollection<LineSegment3D> RectangleLines(
            double rotationDegrees)
        {
            double radians = ToRadians(rotationDegrees);
            Point3D[] vertices =
            {
                Rotate(new Point3D(0.0, 0.0), radians),
                Rotate(new Point3D(6000.0, 0.0), radians),
                Rotate(new Point3D(6000.0, 3000.0), radians),
                Rotate(new Point3D(0.0, 3000.0), radians)
            };
            var lines = new List<LineSegment3D>(vertices.Length);
            for (int index = 0; index < vertices.Length; index++)
            {
                lines.Add(new LineSegment3D(
                    vertices[index],
                    vertices[(index + 1) % vertices.Length]));
            }

            return lines;
        }

        private static Point3D Rotate(Point3D point, double radians)
        {
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new Point3D(
                (point.X * cosine) - (point.Y * sine),
                (point.X * sine) + (point.Y * cosine),
                point.Z);
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
