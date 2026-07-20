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
    }
}
