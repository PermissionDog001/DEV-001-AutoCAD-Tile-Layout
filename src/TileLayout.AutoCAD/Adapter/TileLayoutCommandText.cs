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
            ValidateSuccessArguments(layout, layerName);

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

        public static string FormatOrthogonalValidationFailure(
            OrthogonalRoomValidationResult validation)
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
                "正交房间验证失败：{0} 未生成任何对象。",
                validation.ErrorMessage);
        }

        public static string FormatParameterizedSuccess(
            TileLayoutResult layout,
            string layerName)
        {
            ValidateSuccessArguments(layout, layerName);

            return string.Format(
                CultureInfo.InvariantCulture,
                "排版完成：砖宽={0} mm，砖高={1} mm，起铺角={2}，起排方向={3}/{4}，"
                    + "房间宽={5} mm，高={6} mm，完整列数={7}，完整行数={8}，"
                    + "{9}侧余量={10} mm，{11}侧余量={12} mm；"
                    + "已在图层 {13} 生成 {14} 条内部分格线。",
                FormatParameterNumber(layout.Parameters.TileWidth),
                FormatParameterNumber(layout.Parameters.TileHeight),
                FormatStartCorner(layout.Parameters.StartCorner),
                layout.Parameters.StartsFromEast ? "东→西" : "西→东",
                layout.Parameters.StartsFromNorth ? "北→南" : "南→北",
                FormatNumber(layout.Room.Width),
                FormatNumber(layout.Room.Height),
                layout.FullColumnCount,
                layout.FullRowCount,
                layout.Parameters.StartsFromEast ? "西" : "东",
                FormatNumber(layout.HorizontalRemainder),
                layout.Parameters.StartsFromNorth ? "南" : "北",
                FormatNumber(layout.VerticalRemainder),
                layerName,
                layout.DivisionLines.Count);
        }

        public static string FormatOrthogonalSuccess(
            OrthogonalTileLayoutResult layout,
            string layerName)
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
                "正交房间排版完成：砖宽={0} mm，砖高={1} mm，包围盒网格锚点={2}，"
                    + "网格方向={3}/{4}，包围盒宽={5} mm，高={6} mm，"
                    + "X/Y 完整模数={7}/{8}，包围盒{9}侧余量={10} mm，"
                    + "{11}侧余量={12} mm；已在图层 {13} 生成 {14} 条最终室内分格片段。",
                FormatParameterNumber(layout.Parameters.TileWidth),
                FormatParameterNumber(layout.Parameters.TileHeight),
                FormatStartCorner(layout.Parameters.StartCorner),
                layout.Parameters.StartsFromEast ? "东→西" : "西→东",
                layout.Parameters.StartsFromNorth ? "北→南" : "南→北",
                FormatNumber(layout.Room.Width),
                FormatNumber(layout.Room.Height),
                layout.FullColumnCount,
                layout.FullRowCount,
                layout.Parameters.StartsFromEast ? "西" : "东",
                FormatNumber(layout.HorizontalRemainder),
                layout.Parameters.StartsFromNorth ? "南" : "北",
                FormatNumber(layout.VerticalRemainder),
                layerName,
                layout.DivisionLines.Count);
        }

        public static string FormatParameterError(string dimensionName)
        {
            if (string.IsNullOrWhiteSpace(dimensionName))
            {
                throw new ArgumentException(
                    "Dimension name is required.",
                    nameof(dimensionName));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}必须是有限数值且严格大于 {1} mm，请重新输入或按 Esc 取消。",
                dimensionName,
                GeometryTolerance.Coordinate.ToString(
                    "0.######",
                    CultureInfo.InvariantCulture));
        }

        public static string FormatLimitExceeded(
            TileLayoutLimitExceededException exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "本次预计生成 {0} 条内部分格线，超过 TILELAYOUT 单次上限 {1} 条。"
                    + "请增大砖规格或缩小房间范围；未生成任何对象。",
                exception.EstimatedDivisionLineCount.ToString(
                    "G17",
                    CultureInfo.InvariantCulture),
                exception.MaximumDivisionLineCount);
        }

        public static string FormatLimitExceeded(
            TileLayoutLimitExceededException exception,
            string commandName)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (string.IsNullOrWhiteSpace(commandName))
            {
                throw new ArgumentException(
                    "Command name is required.",
                    nameof(commandName));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "本次预计生成 {0} 条内部分格线或室内片段，超过 {1} 单次上限 {2} 条。"
                    + "请增大砖规格或缩小房间范围；未生成任何对象。",
                exception.EstimatedDivisionLineCount.ToString(
                    "G17",
                    CultureInfo.InvariantCulture),
                commandName,
                exception.MaximumDivisionLineCount);
        }

        private static void ValidateSuccessArguments(
            TileLayoutResult layout,
            string layerName)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("Layer name is required.", nameof(layerName));
            }
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatParameterNumber(double value)
        {
            return value.ToString("0.###############", CultureInfo.InvariantCulture);
        }

        private static string FormatStartCorner(TileLayoutStartCorner startCorner)
        {
            switch (startCorner)
            {
                case TileLayoutStartCorner.SouthWest:
                    return "西南";
                case TileLayoutStartCorner.SouthEast:
                    return "东南";
                case TileLayoutStartCorner.NorthWest:
                    return "西北";
                case TileLayoutStartCorner.NorthEast:
                    return "东北";
                default:
                    throw new ArgumentOutOfRangeException(nameof(startCorner));
            }
        }
    }
}
