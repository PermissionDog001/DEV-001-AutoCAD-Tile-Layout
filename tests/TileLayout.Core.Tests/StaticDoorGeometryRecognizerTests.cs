using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class StaticDoorGeometryRecognizerTests
    {
        [TestMethod]
        public void Recognize_FrozenRealStaticDoorSnapshots_ReturnExpectedOpenings()
        {
            foreach (StaticDoorSnapshot snapshot in RealSnapshots())
            {
                DoorGeometryRecognitionResult result =
                    StaticDoorGeometryRecognizer.Recognize(
                        snapshot.Lines,
                        new[] { snapshot.Arc });

                Assert.IsTrue(result.IsHigh, snapshot.Id);
                AssertPoint(
                    snapshot.ExpectedHinge,
                    result.FirstOpeningPoint,
                    snapshot.Id + " hinge");
                AssertPoint(
                    snapshot.ExpectedJamb,
                    result.SecondOpeningPoint,
                    snapshot.Id + " jamb");
            }
        }

        [TestMethod]
        public void Recognize_RotationMirrorAndReversedInput_NormalizeSameOpening()
        {
            StaticDoorSnapshot source = CanonicalSnapshot(0.0, 0.0);
            foreach (int quarterTurns in new[] { 0, 1, 2, 3 })
            {
                foreach (bool mirror in new[] { false, true })
                {
                    Func<Point3D, Point3D> transform = point => Transform(
                        point,
                        quarterTurns,
                        mirror,
                        500000.0,
                        -700000.0);
                    LineSegment3D[] lines = source.Lines
                        .Reverse()
                        .Select(line => new LineSegment3D(
                            transform(line.End),
                            transform(line.Start)))
                        .ToArray();
                    var arc = new ArcSegment3D(
                        transform(source.Arc.Center),
                        transform(source.Arc.End),
                        transform(source.Arc.Start),
                        source.Arc.Radius);

                    DoorGeometryRecognitionResult result =
                        StaticDoorGeometryRecognizer.Recognize(
                            lines,
                            new[] { arc });

                    Assert.IsTrue(
                        result.IsHigh,
                        string.Format(
                            "rotation={0}, mirror={1}",
                            quarterTurns,
                            mirror));
                    AssertPoint(
                        transform(source.ExpectedHinge),
                        result.FirstOpeningPoint,
                        "transformed hinge");
                    AssertPoint(
                        transform(source.ExpectedJamb),
                        result.SecondOpeningPoint,
                        "transformed jamb");
                }
            }
        }

        [TestMethod]
        public void Recognize_DuplicateGeometryForSameOpening_RemainsUnique()
        {
            StaticDoorSnapshot snapshot = CanonicalSnapshot(0.0, 0.0);

            DoorGeometryRecognitionResult result =
                StaticDoorGeometryRecognizer.Recognize(
                    snapshot.Lines.Concat(snapshot.Lines),
                    new[] { snapshot.Arc, snapshot.Arc });

            Assert.IsTrue(result.IsHigh);
            Assert.AreEqual(1, result.DistinctCandidateCount);
        }

        [TestMethod]
        public void Recognize_TwoStaticDoorSignatures_IsAmbiguous()
        {
            StaticDoorSnapshot first = CanonicalSnapshot(0.0, 0.0);
            StaticDoorSnapshot second = CanonicalSnapshot(3000.0, 0.0);

            DoorGeometryRecognitionResult result =
                StaticDoorGeometryRecognizer.Recognize(
                    first.Lines.Concat(second.Lines),
                    new[] { first.Arc, second.Arc });

            Assert.AreEqual(
                DoorGeometryRecognitionStatus.Ambiguous,
                result.Status);
            Assert.AreEqual(
                DoorGeometryRejectionCode.MultipleDistinctSignatures,
                result.RejectionCode);
            Assert.AreEqual(2, result.DistinctCandidateCount);
        }

        [TestMethod]
        public void Recognize_MissingLeafEdgeOrArc_IsUnsupported()
        {
            StaticDoorSnapshot snapshot = CanonicalSnapshot(0.0, 0.0);

            DoorGeometryRecognitionResult missingEdge =
                StaticDoorGeometryRecognizer.Recognize(
                    snapshot.Lines.Skip(1),
                    new[] { snapshot.Arc });
            DoorGeometryRecognitionResult missingArc =
                StaticDoorGeometryRecognizer.Recognize(
                    snapshot.Lines,
                    new ArcSegment3D[0]);

            Assert.AreEqual(
                DoorGeometryRejectionCode.NoCompleteSingleSwingSignature,
                missingEdge.RejectionCode);
            Assert.AreEqual(
                DoorGeometryRejectionCode.NoCompleteSingleSwingSignature,
                missingArc.RejectionCode);
        }

        [TestMethod]
        public void Recognize_ArcCenterOrRadiusConflict_IsUnsupported()
        {
            StaticDoorSnapshot snapshot = CanonicalSnapshot(0.0, 0.0);
            var shiftedCenter = new ArcSegment3D(
                new Point3D(
                    snapshot.Arc.Center.X + 1.0,
                    snapshot.Arc.Center.Y,
                    snapshot.Arc.Center.Z),
                new Point3D(
                    snapshot.Arc.Start.X + 1.0,
                    snapshot.Arc.Start.Y,
                    snapshot.Arc.Start.Z),
                new Point3D(
                    snapshot.Arc.End.X + 1.0,
                    snapshot.Arc.End.Y,
                    snapshot.Arc.End.Z),
                snapshot.Arc.Radius);
            var wrongRadius = new ArcSegment3D(
                snapshot.Arc.Center,
                new Point3D(
                    snapshot.Arc.Start.X,
                    snapshot.Arc.Start.Y + 1.0,
                    snapshot.Arc.Start.Z),
                snapshot.Arc.End,
                snapshot.Arc.Radius + 1.0);

            Assert.IsFalse(
                StaticDoorGeometryRecognizer.Recognize(
                    snapshot.Lines,
                    new[] { shiftedCenter }).IsHigh);
            Assert.IsFalse(
                StaticDoorGeometryRecognizer.Recognize(
                    snapshot.Lines,
                    new[] { wrongRadius }).IsHigh);
        }

        [TestMethod]
        public void Recognize_NonFiniteGeometry_UsesExistingDedicatedCode()
        {
            StaticDoorSnapshot snapshot = CanonicalSnapshot(0.0, 0.0);
            var invalidLines = snapshot.Lines.ToArray();
            invalidLines[0] = new LineSegment3D(
                new Point3D(double.NaN, 0.0),
                invalidLines[0].End);

            DoorGeometryRecognitionResult result =
                StaticDoorGeometryRecognizer.Recognize(
                    invalidLines,
                    new[] { snapshot.Arc });

            Assert.AreEqual(
                DoorGeometryRejectionCode.NonFiniteGeometry,
                result.RejectionCode);
        }

        [TestMethod]
        public void Recognize_S2bStaticNonDoorSnapshot_IsNotAccepted()
        {
            var lines = new[]
            {
                Line(5553.7230761014689, 3634.6619844471452,
                    5599.10532301119, 3634.6619844471452),
                Line(5552.66419955633, 3615.9557809720018,
                    5560.5459241009994, 3755.1949998826194),
                Line(5592.7316590699666, 3745.5979041866767,
                    5560.0026743952367, 3745.5979041866767),
                Line(5600.16419955633, 3615.9557809720018,
                    5592.28247501166, 3755.1949998826194)
            };
            var arcs = new[]
            {
                Arc(5575.289589316646, 3641.6824552749486,
                    5576.4141995563359, 3648.421087506983,
                    5576.4141995563359, 3634.9438230429141,
                    6.831830834397624),
                Arc(5577.5388097960276, 3641.6824552749486,
                    5576.4141995563377, 3634.9438230429141,
                    5576.4141995563377, 3648.421087506983,
                    6.831830834397624)
            };

            DoorGeometryRecognitionResult result =
                StaticDoorGeometryRecognizer.Recognize(lines, arcs);

            Assert.AreEqual(
                DoorGeometryRecognitionStatus.Unsupported,
                result.Status);
            Assert.AreEqual(0, result.DistinctCandidateCount);
        }

        [TestMethod]
        public void ExistingSingleLineRecognizer_DoesNotGuessStaticDoubleLineDoor()
        {
            StaticDoorSnapshot snapshot = RealS1cP1();

            DoorGeometryRecognitionResult result =
                DoorGeometryRecognizer.Recognize(
                    snapshot.Lines,
                    new[] { snapshot.Arc });

            Assert.AreEqual(
                DoorGeometryRecognitionStatus.Unsupported,
                result.Status);
        }

        [TestMethod]
        public void Recognize_S1cOpening_PassesExistingPointAdapter()
        {
            StaticDoorSnapshot snapshot = RealS1cP1();
            DoorGeometryRecognitionResult geometry =
                StaticDoorGeometryRecognizer.Recognize(
                    snapshot.Lines,
                    new[] { snapshot.Arc });

            DoorOpeningProjectionResult projection =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    new AxisAlignedRectangle(
                        2479.4905222752059,
                        5179.4905222752059,
                        980.936970504822,
                        3651.2969705048217),
                    geometry.FirstOpeningPoint,
                    geometry.SecondOpeningPoint);

            Assert.IsTrue(projection.IsValid);
            Assert.AreEqual(RoomSide.East, projection.Opening.Wall);
            Assert.AreEqual(
                2346.8953915522834,
                projection.Opening.AlongWallStart,
                GeometryTolerance.Coordinate);
            Assert.AreEqual(
                3346.8953915522834,
                projection.Opening.AlongWallEnd,
                GeometryTolerance.Coordinate);
        }

        [TestMethod]
        public void Recognize_S2Openings_StayRejectedByExistingPointAdapter()
        {
            StaticDoorSnapshot northDoor = RealS2P1();
            StaticDoorSnapshot eastDoor = RealS2P2();
            DoorGeometryRecognitionResult northGeometry =
                StaticDoorGeometryRecognizer.Recognize(
                    northDoor.Lines,
                    new[] { northDoor.Arc });
            DoorGeometryRecognitionResult eastGeometry =
                StaticDoorGeometryRecognizer.Recognize(
                    eastDoor.Lines,
                    new[] { eastDoor.Arc });

            DoorOpeningProjectionResult northProjection =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    new AxisAlignedRectangle(
                        0.0,
                        10000.0,
                        0.0,
                        3615.9557815912658),
                    northGeometry.FirstOpeningPoint,
                    northGeometry.SecondOpeningPoint);
            DoorOpeningProjectionResult eastProjection =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    new AxisAlignedRectangle(
                        0.0,
                        6379.4030574115814,
                        0.0,
                        5000.0),
                    eastGeometry.FirstOpeningPoint,
                    eastGeometry.SecondOpeningPoint);

            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                northProjection.Error);
            Assert.AreEqual(
                DoorOpeningPointError.PointNotOnRoomWall,
                eastProjection.Error);
        }

        private static IEnumerable<StaticDoorSnapshot> RealSnapshots()
        {
            yield return RealS1bP1();
            yield return RealS1bP2();
            yield return RealS1cP1();
            yield return RealS2P1();
            yield return RealS2P2();
            yield return RealS3bP1();
            yield return RealS3bP2();
        }

        private static StaticDoorSnapshot RealS1bP1()
        {
            return Snapshot(
                "S1b-P1",
                new[]
                {
                    Line(4530.1028050654868, 2565.2619030800797,
                        4530.1028050654868, 2515.2619030800797),
                    Line(5505.1028050654868, 2515.2619030800797,
                        5505.1028050654868, 2565.2619030800797),
                    Line(5505.1028050654868, 2515.2619030800797,
                        4530.1028050654868, 2515.2619030800797),
                    Line(5505.1028050654868, 2565.2619030800797,
                        4530.1028050654868, 2565.2619030800797)
                },
                Arc(5505.1028050654868, 2540.2619030800797,
                    5505.1028050654868, 3515.2619030800797,
                    4530.4233705845909, 2565.2619030800797,
                    975.0),
                Point(5505.1028050654868, 2515.2619030800797),
                Point(5505.1028050654868, 3515.2619030800797));
        }

        private static StaticDoorSnapshot RealS1bP2()
        {
            return Snapshot(
                "S1b-P2",
                new[]
                {
                    Line(4168.5340019703945, 1107.4974858688092,
                        4218.5340019703945, 1107.4974858688092),
                    Line(4218.5340019703945, 2082.4974858688092,
                        4168.5340019703945, 2082.4974858688092),
                    Line(4218.5340019703945, 2082.4974858688092,
                        4218.5340019703945, 1107.4974858688092),
                    Line(4168.5340019703945, 2082.4974858688092,
                        4168.5340019703945, 1107.4974858688092)
                },
                Arc(4193.5340019703945, 2082.4974858688092,
                    3218.5340019703945, 2082.4974858688092,
                    4168.5340019703945, 1107.8180513879129,
                    975.0),
                Point(4218.5340019703945, 2082.4974858688092),
                Point(3218.5340019703945, 2082.4974858688092));
        }

        private static StaticDoorSnapshot RealS1cP1()
        {
            return Snapshot(
                "S1c-P1",
                new[]
                {
                    Line(4204.4905222752059, 2396.8953915522834,
                        4204.4905222752059, 2346.8953915522834),
                    Line(5179.4905222752059, 2346.8953915522834,
                        5179.4905222752059, 2396.8953915522834),
                    Line(5179.4905222752059, 2346.8953915522834,
                        4204.4905222752059, 2346.8953915522834),
                    Line(5179.4905222752059, 2396.8953915522834,
                        4204.4905222752059, 2396.8953915522834)
                },
                Arc(5179.4905222752059, 2371.8953915522834,
                    5179.4905222752059, 3346.8953915522834,
                    4204.8110877943091, 2396.8953915522834,
                    975.0),
                Point(5179.4905222752059, 2346.8953915522834),
                Point(5179.4905222752059, 3346.8953915522834));
        }

        private static StaticDoorSnapshot RealS2P1()
        {
            return Snapshot(
                "S2-P1",
                new[]
                {
                    Line(3529.4030712956132, 2740.9557817213122,
                        3479.4030712956128, 2740.9557817357618),
                    Line(3479.4030715773806, 3715.9557817357618,
                        3529.4030715773806, 3715.9557817213122),
                    Line(3479.4030715773806, 3715.9557817357618,
                        3479.4030712956132, 2740.9557817357618),
                    Line(3529.4030715773806, 3715.9557817213122,
                        3529.4030712956132, 2740.9557817213122)
                },
                Arc(3504.4030715773806, 3715.9557817285372,
                    4479.4030715773806, 3715.95578144677,
                    3529.4030712957065, 2741.2763472404158,
                    975.0),
                Point(3479.4030715773806, 3715.9557817357618),
                Point(4479.4030715773806, 3715.95578144677));
        }

        private static StaticDoorSnapshot RealS2P2()
        {
            return Snapshot(
                "S2-P2",
                new[]
                {
                    Line(7454.4030572815345, 1647.6301695661164,
                        7454.4030572670854, 1597.6301695661159),
                    Line(6479.4030572670854, 1597.6301698478837,
                        6479.4030572815345, 1647.6301698478837),
                    Line(6479.4030572670854, 1597.6301698478837,
                        7454.4030572670854, 1597.6301695661164),
                    Line(6479.4030572815345, 1647.6301698478837,
                        7454.4030572815345, 1647.6301695661164)
                },
                Arc(6479.40305727431, 1622.6301698478837,
                    6479.4030575560782, 2597.6301698478837,
                    7454.0824917624313, 1647.6301695662089,
                    974.99999999999977),
                Point(6479.4030572670854, 1597.6301698478837),
                Point(6479.4030575560782, 2597.6301698478837));
        }

        private static StaticDoorSnapshot RealS3bP1()
        {
            return Snapshot(
                "S3b-P1",
                new[]
                {
                    Line(4765.6685637819292, 4127.5182931386844,
                        4715.6685637819282, 4127.5182931386844),
                    Line(4715.6685637819282, 3152.5182931386848,
                        4765.6685637819282, 3152.5182931386848),
                    Line(4715.6685637819282, 3152.5182931386848,
                        4715.6685637819292, 4127.5182931386844),
                    Line(4765.6685637819282, 3152.5182931386848,
                        4765.6685637819292, 4127.5182931386844)
                },
                Arc(4740.6685637819282, 3152.5182931386848,
                    5715.6685637819282, 3152.5182931386839,
                    4765.6685637819282, 4127.1977276195812,
                    975.0),
                Point(4715.6685637819282, 3152.5182931386848),
                Point(5715.6685637819282, 3152.5182931386839));
        }

        private static StaticDoorSnapshot RealS3bP2()
        {
            return Snapshot(
                "S3b-P2",
                new[]
                {
                    Line(8865.6685763510468, 2398.7920951903252,
                        8865.6685763510468, 2448.7920951903257),
                    Line(7890.6685763510459, 2448.7920951903261,
                        7890.6685763510459, 2398.7920951903261),
                    Line(7890.6685763510459, 2448.7920951903261,
                        8865.6685763510468, 2448.7920951903252),
                    Line(7890.6685763510459, 2398.7920951903261,
                        8865.6685763510468, 2398.7920951903252)
                },
                Arc(7890.6685763510459, 2423.7920951903261,
                    7890.668576351045, 1448.7920951903261,
                    8865.3480108319418, 2398.7920951903252,
                    975.0),
                Point(7890.6685763510459, 2448.7920951903261),
                Point(7890.668576351045, 1448.7920951903261));
        }

        private static StaticDoorSnapshot CanonicalSnapshot(
            double offsetX,
            double offsetY)
        {
            double freeArcX = Math.Sqrt((975.0 * 975.0) - (25.0 * 25.0));
            return Snapshot(
                "canonical",
                new[]
                {
                    Line(offsetX + 975.0, offsetY,
                        offsetX + 975.0, offsetY + 50.0),
                    Line(offsetX, offsetY, offsetX, offsetY + 50.0),
                    Line(offsetX, offsetY, offsetX + 975.0, offsetY),
                    Line(offsetX, offsetY + 50.0,
                        offsetX + 975.0, offsetY + 50.0)
                },
                Arc(offsetX, offsetY + 25.0,
                    offsetX, offsetY + 1000.0,
                    offsetX + freeArcX, offsetY + 50.0,
                    975.0),
                Point(offsetX, offsetY),
                Point(offsetX, offsetY + 1000.0));
        }

        private static StaticDoorSnapshot Snapshot(
            string id,
            LineSegment3D[] lines,
            ArcSegment3D arc,
            Point3D expectedHinge,
            Point3D expectedJamb)
        {
            return new StaticDoorSnapshot(
                id,
                lines,
                arc,
                expectedHinge,
                expectedJamb);
        }

        private static LineSegment3D Line(
            double startX,
            double startY,
            double endX,
            double endY)
        {
            return new LineSegment3D(
                Point(startX, startY),
                Point(endX, endY));
        }

        private static ArcSegment3D Arc(
            double centerX,
            double centerY,
            double startX,
            double startY,
            double endX,
            double endY,
            double radius)
        {
            return new ArcSegment3D(
                Point(centerX, centerY),
                Point(startX, startY),
                Point(endX, endY),
                radius);
        }

        private static Point3D Point(double x, double y)
        {
            return new Point3D(x, y, 0.0);
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
            Point3D expected,
            Point3D actual,
            string message)
        {
            Assert.AreEqual(
                expected.X,
                actual.X,
                GeometryTolerance.Coordinate,
                message + " X");
            Assert.AreEqual(
                expected.Y,
                actual.Y,
                GeometryTolerance.Coordinate,
                message + " Y");
            Assert.AreEqual(
                expected.Z,
                actual.Z,
                GeometryTolerance.Coordinate,
                message + " Z");
        }

        private sealed class StaticDoorSnapshot
        {
            public StaticDoorSnapshot(
                string id,
                LineSegment3D[] lines,
                ArcSegment3D arc,
                Point3D expectedHinge,
                Point3D expectedJamb)
            {
                Id = id;
                Lines = lines;
                Arc = arc;
                ExpectedHinge = expectedHinge;
                ExpectedJamb = expectedJamb;
            }

            public string Id { get; }

            public LineSegment3D[] Lines { get; }

            public ArcSegment3D Arc { get; }

            public Point3D ExpectedHinge { get; }

            public Point3D ExpectedJamb { get; }
        }
    }
}
