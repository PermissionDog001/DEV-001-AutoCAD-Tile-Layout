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
