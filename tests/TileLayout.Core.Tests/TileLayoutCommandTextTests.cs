using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core.Models;

namespace TileLayout.Core.Tests
{
    [TestClass]
    public sealed class TileLayoutCommandTextTests
    {
        [TestMethod]
        public void FormatValidationFailure_NonAxisAlignedBoundary_ReturnsReadableMessage()
        {
            var lines = new[]
            {
                new LineSegment3D(new Point3D(0.0, 0.0), new Point3D(3600.0, 10.0)),
                new LineSegment3D(new Point3D(3600.0, 10.0), new Point3D(3600.0, 3000.0)),
                new LineSegment3D(new Point3D(3600.0, 3000.0), new Point3D(0.0, 3000.0)),
                new LineSegment3D(new Point3D(0.0, 3000.0), new Point3D(0.0, 0.0))
            };

            RectangleValidationResult validation = RectangleValidator.Validate(lines);

            Assert.AreEqual(
                "矩形验证失败：四条线必须与 WCS X/Y 轴平行。 未生成任何对象。",
                TileLayoutCommandText.FormatValidationFailure(validation));
        }

        [TestMethod]
        public void FormatSuccess_DoubleRemainderLayout_ReportsAllRequiredValues()
        {
            RectangleValidationResult validation = RectangleValidator.Validate(
                TestGeometry.RectangleLines(0.0, 4250.0, 0.0, 3100.0));
            TileLayoutResult layout = TileGridCalculator.Calculate(validation.Rectangle);

            string message = TileLayoutCommandText.FormatSuccess(
                layout,
                "TILE_LAYOUT_600");

            Assert.AreEqual(
                "排版完成：房间宽=4250 mm，高=3100 mm，完整列数=7，完整行数=5，"
                    + "东侧余量=50 mm，北侧余量=100 mm；已在图层 TILE_LAYOUT_600 生成 12 条内部分格线。",
                message);
        }

        [TestMethod]
        public void FormatSuccess_SmallRoom_ReportsZeroLinesAndFullSpans()
        {
            RectangleValidationResult validation = RectangleValidator.Validate(
                TestGeometry.RectangleLines(0.0, 500.0, 0.0, 500.0));
            TileLayoutResult layout = TileGridCalculator.Calculate(validation.Rectangle);

            string message = TileLayoutCommandText.FormatSuccess(
                layout,
                "TILE_LAYOUT_600");

            StringAssert.Contains(message, "完整列数=0，完整行数=0");
            StringAssert.Contains(message, "东侧余量=500 mm，北侧余量=500 mm");
            StringAssert.Contains(message, "生成 0 条内部分格线");
        }

        [TestMethod]
        public void FormatOrthogonalValidationFailure_TJunction_ReturnsReadableMessage()
        {
            var lines = OrthogonalRoomValidatorTests.LinesFromVertices(
                new Point3D(0.0, 0.0),
                new Point3D(1000.0, 0.0),
                new Point3D(1000.0, 800.0),
                new Point3D(0.0, 800.0))
                .Concat(new[]
                {
                    new LineSegment3D(
                        new Point3D(500.0, 0.0),
                        new Point3D(500.0, 400.0))
                })
                .ToArray();
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(lines);

            string message =
                TileLayoutCommandText.FormatOrthogonalValidationFailure(validation);

            StringAssert.StartsWith(message, "正交房间验证失败：");
            StringAssert.Contains(message, "自交、非相邻接触或端点落在另一条线内部");
            StringAssert.EndsWith(message, "未生成任何对象。");
        }

        [TestMethod]
        public void FormatParameterizedSuccess_RectangularTile_ReportsAllRequiredValues()
        {
            RectangleValidationResult validation = RectangleValidator.Validate(
                TestGeometry.RectangleLines(0.0, 4250.0, 0.0, 3100.0));
            TileLayoutResult layout = TileGridCalculator.Calculate(
                validation.Rectangle,
                new TileLayoutParameters(600.0, 1200.0));

            string message = TileLayoutCommandText.FormatParameterizedSuccess(
                layout,
                "TILE_LAYOUT");

            Assert.AreEqual(
                "排版完成：砖宽=600 mm，砖高=1200 mm，起铺角=西南，起排方向=西→东/南→北，"
                    + "房间宽=4250 mm，高=3100 mm，完整列数=7，完整行数=2，"
                    + "东侧余量=50 mm，北侧余量=700 mm；"
                    + "已在图层 TILE_LAYOUT 生成 9 条内部分格线。",
                message);
        }

        [TestMethod]
        public void FormatParameterizedSuccess_NorthEast_ReportsDirectionsAndCutSides()
        {
            RectangleValidationResult validation = RectangleValidator.Validate(
                TestGeometry.RectangleLines(0.0, 4250.0, 0.0, 3100.0));
            TileLayoutResult layout = TileGridCalculator.Calculate(
                validation.Rectangle,
                new TileLayoutParameters(
                    600.0,
                    1200.0,
                    TileLayoutStartCorner.NorthEast));

            string message = TileLayoutCommandText.FormatParameterizedSuccess(
                layout,
                "TILE_LAYOUT");

            Assert.AreEqual(
                "排版完成：砖宽=600 mm，砖高=1200 mm，起铺角=东北，起排方向=东→西/北→南，"
                    + "房间宽=4250 mm，高=3100 mm，完整列数=7，完整行数=2，"
                    + "西侧余量=50 mm，南侧余量=700 mm；"
                    + "已在图层 TILE_LAYOUT 生成 9 条内部分格线。",
                message);
        }

        [TestMethod]
        public void FormatOrthogonalSuccess_LRoom_ReportsBoundingBoxAndFinalFragments()
        {
            OrthogonalRoomValidationResult validation =
                OrthogonalRoomValidator.Validate(
                    OrthogonalRoomValidatorTests.LinesFromVertices(
                        new Point3D(0.0, 0.0),
                        new Point3D(1800.0, 0.0),
                        new Point3D(1800.0, 600.0),
                        new Point3D(600.0, 600.0),
                        new Point3D(600.0, 1800.0),
                        new Point3D(0.0, 1800.0)));
            OrthogonalTileLayoutResult layout =
                OrthogonalTileGridCalculator.Calculate(
                    validation.Room,
                    new TileLayoutParameters(600.0, 600.0));

            string message = TileLayoutCommandText.FormatOrthogonalSuccess(
                layout,
                "TILE_LAYOUT_ORTHO");

            Assert.AreEqual(
                "正交房间排版完成：砖宽=600 mm，砖高=600 mm，包围盒网格锚点=西南，"
                    + "网格方向=西→东/南→北，包围盒宽=1800 mm，高=1800 mm，"
                    + "X/Y 完整模数=3/3，包围盒东侧余量=0 mm，北侧余量=0 mm；"
                    + "已在图层 TILE_LAYOUT_ORTHO 生成 4 条最终室内分格片段。",
                message);
        }

        [TestMethod]
        public void FormatDoorProjectionFailure_DifferentWalls_ExplainsRetryAndNoWrite()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);
            DoorOpeningProjectionResult projection =
                DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new Point3D(0.0, 400.0),
                    new Point3D(800.0, 1300.0));

            Assert.AreEqual(
                "门洞两点无效：两个门洞端点必须位于同一面房间墙。 "
                    + "请重新选择；未生成任何对象。",
                TileLayoutCommandText.FormatDoorProjectionFailure(projection));
        }

        [TestMethod]
        public void FormatDoorOpeningSummary_WestDoor_ReportsWallBiasAndEntryDirection()
        {
            var room = new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0);
            var opening = new DoorOpening(RoomSide.West, 800.0, 1100.0);

            Assert.AreEqual(
                "门洞识别：西墙，洞宽=300 mm，偏北，进门方向=西→东，"
                    + "到南/北端净距=800/200 mm。",
                TileLayoutCommandText.FormatDoorOpeningSummary(room, opening));
        }

        [TestMethod]
        public void FormatEngineeringCandidateSummary_Sample02_ReportsBandsDirectionsAndDiagnostics()
        {
            EngineeringRectangularLayoutResult layout =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 3126.0, 0.0, 3076.0),
                    new EngineeringRectangularLayoutParameters(
                        600.0,
                        600.0,
                        new DoorOpening(RoomSide.West, 200.0, 800.0)));

            string message =
                TileLayoutCommandText.FormatEngineeringCandidateSummary(
                    layout.DefaultCandidate);

            StringAssert.StartsWith(message, "候选摘要：默认候选");
            StringAssert.Contains(
                message,
                "西/东/南/北边砖=426/300/300/376 mm");
            StringAssert.Contains(
                message,
                "施工起铺方向=X 东→西、Y 南→北");
            StringAssert.Contains(
                message,
                "X向自然窄余量 126 mm 已按半砖/过渡砖重分配");
            StringAssert.Contains(
                message,
                "Y向自然窄余量 76 mm 已按半砖/过渡砖重分配");
        }

        [TestMethod]
        public void FormatEngineeringCandidateSummary_FlippedCenteredDoor_ReportsEquivalentFlip()
        {
            EngineeringRectangularLayoutResult layout =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0),
                    new EngineeringRectangularLayoutParameters(
                        600.0,
                        600.0,
                        new DoorOpening(RoomSide.West, 500.0, 800.0)));

            string message =
                TileLayoutCommandText.FormatEngineeringCandidateSummary(
                    layout.FlippedCandidate);

            StringAssert.Contains(message, "候选摘要：居中等价翻转");
            StringAssert.Contains(message, "门洞居中，已翻转等价沿墙分配");
        }

        [TestMethod]
        public void FormatEngineeringFailure_TooSmallRoom_ReportsRejectionDiagnostics()
        {
            EngineeringRectangularLayoutResult layout =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 200.0, 0.0, 600.0),
                    new EngineeringRectangularLayoutParameters(
                        600.0,
                        600.0,
                        new DoorOpening(RoomSide.West, 100.0, 500.0)));

            string message =
                TileLayoutCommandText.FormatEngineeringFailure(layout);

            StringAssert.StartsWith(message, "没有可接受的门洞控制候选：");
            StringAssert.Contains(message, "X向边界砖 200 mm 小于默认下限 252 mm");
            StringAssert.Contains(message, "X向没有可用于半砖重分配的整砖");
            StringAssert.EndsWith(message, "未生成任何对象。");
        }

        [TestMethod]
        public void FormatEngineeringWriteSuccess_ReportsCandidateLayerAndLineCount()
        {
            EngineeringRectangularLayoutResult layout =
                EngineeringRectangularLayoutCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 1300.0, 0.0, 1300.0),
                    new EngineeringRectangularLayoutParameters(
                        600.0,
                        600.0,
                        new DoorOpening(RoomSide.West, 100.0, 500.0)));

            Assert.AreEqual(
                "门洞控制排版已接受：默认候选；已在图层 "
                    + "TILE_LAYOUT_DOOR_RECT 生成 4 条内部分格线。",
                TileLayoutCommandText.FormatEngineeringWriteSuccess(
                    layout.DefaultCandidate,
                    "TILE_LAYOUT_DOOR_RECT"));
        }

        [TestMethod]
        public void FormatParameterError_Width_ReturnsRetryGuidance()
        {
            Assert.AreEqual(
                "砖宽必须是有限数值且严格大于 0.000001 mm，请重新输入或按 Esc 取消。",
                TileLayoutCommandText.FormatParameterError("砖宽"));
        }

        [TestMethod]
        public void FormatLimitExceeded_ReportsEstimateLimitAndRemediation()
        {
            TileLayoutLimitExceededException exception = null;
            try
            {
                TileGridCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 10002.0, 0.0, 0.5),
                    new TileLayoutParameters(1.0, 1.0));
            }
            catch (TileLayoutLimitExceededException caught)
            {
                exception = caught;
            }

            Assert.IsNotNull(exception);
            Assert.AreEqual(
                "本次预计生成 10001 条内部分格线，超过 TILELAYOUT 单次上限 10000 条。"
                    + "请增大砖规格或缩小房间范围；未生成任何对象。",
                TileLayoutCommandText.FormatLimitExceeded(exception));
        }

        [TestMethod]
        public void FormatLimitExceeded_OrthogonalCommand_ReportsFinalFragmentScope()
        {
            TileLayoutLimitExceededException exception = null;
            try
            {
                TileGridCalculator.Calculate(
                    new AxisAlignedRectangle(0.0, 10002.0, 0.0, 0.5),
                    new TileLayoutParameters(1.0, 1.0));
            }
            catch (TileLayoutLimitExceededException caught)
            {
                exception = caught;
            }

            Assert.AreEqual(
                "本次预计生成 10001 条内部分格线或室内片段，超过 TILEORTHO 单次上限 10000 条。"
                    + "请增大砖规格或缩小房间范围；未生成任何对象。",
                TileLayoutCommandText.FormatLimitExceeded(exception, "TILEORTHO"));
        }
    }
}
