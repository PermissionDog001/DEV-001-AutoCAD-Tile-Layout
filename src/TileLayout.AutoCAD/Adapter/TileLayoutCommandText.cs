using System;
using System.Globalization;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    public static class TileLayoutCommandText
    {
        public static string FormatValidationFailure(RectangleValidationResult validation)
        {
            if (validation == null)
            {
                throw new ArgumentNullException(nameof(validation));
            }

            if (validation.IsValid)
            {
                throw new ArgumentException(
                    "A successful validation result cannot be formatted as a failure.",
                    nameof(validation));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "矩形验证失败：{0} 未生成任何对象。",
                validation.ErrorMessage);
        }

        public static string FormatSuccess(TileLayoutResult layout, string layerName)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("Layer name is required.", nameof(layerName));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "排版完成：房间宽={0} mm，高={1} mm，完整列数={2}，完整行数={3}，"
                    + "东侧余量={4} mm，北侧余量={5} mm；已在图层 {6} 生成 {7} 条内部分格线。",
                FormatNumber(layout.Room.Width),
                FormatNumber(layout.Room.Height),
                layout.FullColumnCount,
                layout.FullRowCount,
                FormatNumber(layout.EastRemainder),
                FormatNumber(layout.NorthRemainder),
                layerName,
                layout.DivisionLines.Count);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
