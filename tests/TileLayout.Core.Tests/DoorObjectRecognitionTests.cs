using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class DoorObjectRecognitionTests
    {
        [TestMethod]
        public void Recognize_UniqueSignatureOnRoomWall_UsesExistingPointAdapter()
        {
            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 2000.0, 0.0, 2000.0),
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 500.0),
                            new Point3D(1000.0, 500.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(0.0, 500.0),
                            new Point3D(1000.0, 500.0),
                            new Point3D(0.0, 1500.0),
                            1000.0)
                    });

            Assert.AreEqual(DoorObjectRecognitionStatus.High, result.Status);
            Assert.IsTrue(result.Projection.IsValid);
            Assert.AreEqual(RoomSide.West, result.Projection.Opening.Wall);
            Assert.AreEqual(500.0, result.Projection.Opening.AlongWallStart);
            Assert.AreEqual(1500.0, result.Projection.Opening.AlongWallEnd);
        }

        [TestMethod]
        public void Recognize_SignatureAwayFromRoomWall_IsInvalidNotHigh()
        {
            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 2000.0, 0.0, 2000.0),
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(100.0, 500.0),
                            new Point3D(1100.0, 500.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(100.0, 500.0),
                            new Point3D(1100.0, 500.0),
                            new Point3D(100.0, 1500.0),
                            1000.0)
                    });

            Assert.AreEqual(DoorObjectRecognitionStatus.Invalid, result.Status);
            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.PointAdapterRejected,
                result.RejectionCode);
            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                result.Projection.Error);
        }

        [TestMethod]
        public void Recognize_MultipleSignatures_PreservesAmbiguousReason()
        {
            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 5000.0, 0.0, 5000.0),
                    new[]
                    {
                        new LineSegment3D(
                            new Point3D(0.0, 500.0),
                            new Point3D(1000.0, 500.0)),
                        new LineSegment3D(
                            new Point3D(0.0, 2500.0),
                            new Point3D(1000.0, 2500.0))
                    },
                    new[]
                    {
                        new ArcSegment3D(
                            new Point3D(0.0, 500.0),
                            new Point3D(1000.0, 500.0),
                            new Point3D(0.0, 1500.0),
                            1000.0),
                        new ArcSegment3D(
                            new Point3D(0.0, 2500.0),
                            new Point3D(1000.0, 2500.0),
                            new Point3D(0.0, 3500.0),
                            1000.0)
                    });

            Assert.AreEqual(
                DoorObjectRecognitionStatus.Ambiguous,
                result.Status);
            Assert.AreEqual(2, result.DistinctCandidateCount);
            StringAssert.Contains(result.Reason, "2 个不同门洞候选");
        }

        [TestMethod]
        public void Recognize_FrozenStaticDoubleLineSignature_UsesExistingPointAdapter()
        {
            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 2000.0, 0.0, 2000.0),
                    StaticDoorLines(0.0),
                    new[] { StaticDoorArc(0.0) },
                    DoorBlockRecognitionRoute.FrozenStaticSignature);

            Assert.AreEqual(DoorObjectRecognitionStatus.High, result.Status);
            Assert.AreEqual(
                DoorBlockRecognitionRoute.FrozenStaticSignature,
                result.Route);
            Assert.AreEqual(RoomSide.West, result.Projection.Opening.Wall);
            Assert.AreEqual(0.0, result.Projection.Opening.AlongWallStart);
            Assert.AreEqual(1000.0, result.Projection.Opening.AlongWallEnd);
        }

        [TestMethod]
        public void Recognize_FrozenStaticIncompleteSignature_IsNotAccepted()
        {
            LineSegment3D[] complete = StaticDoorLines(0.0);
            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 2000.0, 0.0, 2000.0),
                    new[] { complete[0], complete[1], complete[2] },
                    new[] { StaticDoorArc(0.0) },
                    DoorBlockRecognitionRoute.FrozenStaticSignature);

            Assert.AreEqual(
                DoorObjectRecognitionStatus.Unsupported,
                result.Status);
            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode
                    .NoCompleteSingleSwingSignature,
                result.RejectionCode);
            Assert.IsNull(result.Projection);
        }

        [TestMethod]
        public void Recognize_FrozenStaticSignatureAwayFromWall_UsesOriginalAdapterRejection()
        {
            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(
                        -100.0,
                        2000.0,
                        0.0,
                        2000.0),
                    StaticDoorLines(0.0),
                    new[] { StaticDoorArc(0.0) },
                    DoorBlockRecognitionRoute.FrozenStaticSignature);

            Assert.AreEqual(DoorObjectRecognitionStatus.Invalid, result.Status);
            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.PointAdapterRejected,
                result.RejectionCode);
            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                result.Projection.Error);
        }

        [TestMethod]
        public void Recognize_FrozenStaticMultipleDoors_IsAmbiguousNotAccepted()
        {
            LineSegment3D[] first = StaticDoorLines(0.0);
            LineSegment3D[] second = StaticDoorLines(2500.0);
            var lines = new LineSegment3D[first.Length + second.Length];
            first.CopyTo(lines, 0);
            second.CopyTo(lines, first.Length);

            DoorObjectRecognitionResult result =
                DoorObjectRecognitionCoordinator.Recognize(
                    new AxisAlignedRectangle(0.0, 5000.0, 0.0, 5000.0),
                    lines,
                    new[] { StaticDoorArc(0.0), StaticDoorArc(2500.0) },
                    DoorBlockRecognitionRoute.FrozenStaticSignature);

            Assert.AreEqual(DoorObjectRecognitionStatus.Ambiguous, result.Status);
            Assert.AreEqual(
                DoorObjectRecognitionRejectionCode.MultipleDistinctSignatures,
                result.RejectionCode);
            Assert.AreEqual(2, result.DistinctCandidateCount);
            Assert.IsNull(result.Projection);
        }

        [TestMethod]
        public void RejectedResult_AllFrozenRejectionCodesRemainExplicitAndFallbackSafe()
        {
            foreach (DoorObjectRecognitionRejectionCode code in
                Enum.GetValues(typeof(DoorObjectRecognitionRejectionCode)))
            {
                if (code == DoorObjectRecognitionRejectionCode.None)
                {
                    continue;
                }

                DoorObjectRecognitionStatus status = code
                    == DoorObjectRecognitionRejectionCode
                        .MultipleDistinctSignatures
                    ? DoorObjectRecognitionStatus.Ambiguous
                    : code == DoorObjectRecognitionRejectionCode
                        .PointAdapterRejected
                        ? DoorObjectRecognitionStatus.Invalid
                        : DoorObjectRecognitionStatus.Unsupported;
                DoorObjectRecognitionResult result =
                    DoorObjectRecognitionResult.Rejected(
                        status,
                        code,
                        "测试拒绝原因。");

                Assert.IsFalse(result.IsHigh);
                Assert.AreEqual(code, result.RejectionCode);
                string message = TileLayoutCommandText
                    .FormatDoorObjectRecognitionFailure(result);
                StringAssert.Contains(message, code.ToString());
                StringAssert.Contains(message, "已回退到现有门洞两点输入");
            }
        }

        private static LineSegment3D[] StaticDoorLines(double offsetY)
        {
            return new[]
            {
                Line(975.0, offsetY, 975.0, offsetY + 50.0),
                Line(0.0, offsetY, 0.0, offsetY + 50.0),
                Line(0.0, offsetY, 975.0, offsetY),
                Line(0.0, offsetY + 50.0, 975.0, offsetY + 50.0)
            };
        }

        private static ArcSegment3D StaticDoorArc(double offsetY)
        {
            double freeArcX = Math.Sqrt((975.0 * 975.0) - (25.0 * 25.0));
            return new ArcSegment3D(
                new Point3D(0.0, offsetY + 25.0),
                new Point3D(0.0, offsetY + 1000.0),
                new Point3D(freeArcX, offsetY + 50.0),
                975.0);
        }

        private static LineSegment3D Line(
            double startX,
            double startY,
            double endX,
            double endY)
        {
            return new LineSegment3D(
                new Point3D(startX, startY),
                new Point3D(endX, endY));
        }
    }
}
