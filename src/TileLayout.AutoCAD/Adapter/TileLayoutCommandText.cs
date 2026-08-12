using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    public static class TileLayoutCommandText
    {
        public const string DoorObjectSelectionPrompt =
            "\n请选择一个模型空间顶层门块（动态块或受支持静态块；Esc 取消命令）：";

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

        public static string FormatDoorProjectionFailure(
            DoorOpeningProjectionResult projection)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            if (projection.IsValid)
            {
                throw new ArgumentException(
                    "A successful door projection cannot be formatted as a failure.",
                    nameof(projection));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "门洞两点无效：{0} 请重新选择；未生成任何对象。",
                projection.ErrorMessage);
        }

        public static string FormatDoorObjectRecognitionSuccess(
            DoorObjectRecognitionResult recognition)
        {
            if (recognition == null)
            {
                throw new ArgumentNullException(nameof(recognition));
            }

            if (!recognition.IsHigh)
            {
                throw new ArgumentException(
                    "Only a High door-object result can be formatted as success.",
                    nameof(recognition));
            }

            string source = recognition.Route
                == DoorBlockRecognitionRoute.FrozenStaticSignature
                ? "静态块冻结双线门几何签名"
                : "动态块单扇平开门线弧签名";
            return "对象辅助识别：High；已由唯一" + source
                + "取得门洞端点，并通过现有两点适配验证。";
        }

        public static string FormatDoorObjectRecognitionFailure(
            DoorObjectRecognitionResult recognition)
        {
            if (recognition == null)
            {
                throw new ArgumentNullException(nameof(recognition));
            }

            if (recognition.IsHigh)
            {
                throw new ArgumentException(
                    "A High door-object result cannot be formatted as failure.",
                    nameof(recognition));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "对象辅助识别未采用：{0}（{1}）。{2} 已回退到现有门洞两点输入；"
                    + "房间边界、砖规格和所选对象均未修改。",
                recognition.Status,
                recognition.RejectionCode,
                recognition.Reason);
        }

        public static string FormatDoorOpeningSummary(
            AxisAlignedRectangle room,
            DoorOpening opening)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            if (opening == null)
            {
                throw new ArgumentNullException(nameof(opening));
            }

            double lowDistance = opening.GetDistanceToLowWallEnd(room);
            double highDistance = opening.GetDistanceToHighWallEnd(room);
            string lowDirection;
            string highDirection;
            string entryDirection;
            GetDoorDirections(
                opening.Wall,
                out lowDirection,
                out highDirection,
                out entryDirection);
            string bias = GeometryTolerance.NearlyEqual(
                lowDistance,
                highDistance)
                ? "居中"
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "偏{0}",
                    lowDistance < highDistance
                        ? lowDirection
                        : highDirection);

            return string.Format(
                CultureInfo.InvariantCulture,
                "门洞识别：{0}，洞宽={1} mm，{2}，进门方向={3}，"
                    + "到{4}/{5}端净距={6}/{7} mm。",
                FormatRoomSide(opening.Wall),
                FormatNumber(opening.Width),
                bias,
                entryDirection,
                lowDirection,
                highDirection,
                FormatNumber(lowDistance),
                FormatNumber(highDistance));
        }

        public static string FormatEngineeringCandidateSummary(
            LayoutCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate.IsRejected)
            {
                throw new ArgumentException(
                    "A rejected candidate cannot be formatted as a preview.",
                    nameof(candidate));
            }

            BoundaryBandPlan xPlan =
                candidate.GetAxisPlan(TileLayoutAxis.X);
            BoundaryBandPlan yPlan =
                candidate.GetAxisPlan(TileLayoutAxis.Y);
            return string.Format(
                CultureInfo.InvariantCulture,
                "候选摘要：{0}；X向={1}，Y向={2}；"
                    + "西/东/南/北边砖={3}/{4}/{5}/{6} mm；"
                    + "施工起铺方向=X {7}、Y {8}；诊断：{9}。",
                candidate.IsFlippedAlternative ? "居中等价翻转" : "默认候选",
                FormatAxisPlan(xPlan),
                FormatAxisPlan(yPlan),
                FormatNumber(xPlan.GetBoundary(RoomSide.West).Width),
                FormatNumber(xPlan.GetBoundary(RoomSide.East).Width),
                FormatNumber(yPlan.GetBoundary(RoomSide.South).Width),
                FormatNumber(yPlan.GetBoundary(RoomSide.North).Width),
                FormatConstructionDirection(xPlan),
                FormatConstructionDirection(yPlan),
                FormatDiagnostics(candidate.Diagnostics));
        }

        public static string FormatEngineeringFailure(
            EngineeringRectangularLayoutResult layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (layout.IsSuccessful)
            {
                throw new ArgumentException(
                    "A successful engineering layout cannot be formatted as a failure.",
                    nameof(layout));
            }

            var diagnostics = new List<CandidateDiagnostic>();
            foreach (LayoutCandidate candidate in layout.EliminatedCandidates)
            {
                foreach (CandidateDiagnostic diagnostic in candidate.Diagnostics)
                {
                    diagnostics.Add(diagnostic);
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "没有可接受的门洞控制候选：{0}。未生成任何对象。",
                FormatDiagnostics(diagnostics));
        }

        public static string FormatEngineeringWriteSuccess(
            LayoutCandidate candidate,
            string layerName)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate.IsRejected)
            {
                throw new ArgumentException(
                    "A rejected candidate cannot be written.",
                    nameof(candidate));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException(
                    "Layer name is required.",
                    nameof(layerName));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "门洞控制排版已接受：{0}；已在图层 {1} 生成 {2} 条内部分格线。",
                candidate.IsFlippedAlternative ? "居中等价翻转候选" : "默认候选",
                layerName,
                candidate.DivisionLines.Count);
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

        private static string FormatAxisPlan(BoundaryBandPlan plan)
        {
            if (plan.UsesRedistribution)
            {
                return "半砖/过渡砖重分配";
            }

            return plan.NaturalRemainder <= GeometryTolerance.Coordinate
                ? "整除"
                : "整砖+合法自然余量";
        }

        private static string FormatConstructionDirection(
            BoundaryBandPlan plan)
        {
            if (plan.Axis == TileLayoutAxis.X)
            {
                return plan.ConstructionStartSide == RoomSide.East
                    ? "东→西"
                    : "西→东";
            }

            return plan.ConstructionStartSide == RoomSide.North
                ? "北→南"
                : "南→北";
        }

        private static string FormatDiagnostics(
            IEnumerable<CandidateDiagnostic> diagnostics)
        {
            var builder = new StringBuilder();
            foreach (CandidateDiagnostic diagnostic in diagnostics)
            {
                if (builder.Length > 0)
                {
                    builder.Append("；");
                }

                builder.Append(FormatDiagnostic(diagnostic));
            }

            return builder.Length == 0 ? "无" : builder.ToString();
        }

        private static string FormatDiagnostic(CandidateDiagnostic diagnostic)
        {
            string axis = diagnostic.Axis.HasValue
                ? FormatAxis(diagnostic.Axis.Value) + "向"
                : string.Empty;
            switch (diagnostic.Code)
            {
                case CandidateDiagnosticCode.ExactTileMultiple:
                    return axis + "尺寸整除";
                case CandidateDiagnosticCode.NaturalRemainderAccepted:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}自然余量 {1} mm 不小于下限 {2} mm",
                        axis,
                        FormatOptionalNumber(diagnostic.ActualValue),
                        FormatOptionalNumber(diagnostic.Threshold));
                case CandidateDiagnosticCode.NarrowRemainderRedistributed:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}自然窄余量 {1} mm 已按半砖/过渡砖重分配"
                            + "（当前下限 {2} mm）",
                        axis,
                        FormatOptionalNumber(diagnostic.ActualValue),
                        FormatOptionalNumber(diagnostic.Threshold));
                case CandidateDiagnosticCode.OrthogonalClipRedistributed:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}异形裁切后边界带 {1} mm 小于下限 {2} mm，"
                            + "已按半砖/过渡砖重新分配并复核完整房间",
                        axis,
                        FormatOptionalNumber(diagnostic.ActualValue),
                        FormatOptionalNumber(diagnostic.Threshold));
                case CandidateDiagnosticCode.OrthogonalClipAlongWallFlipped:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}沿墙分配经异形裁切后形成 {1} mm 窄带，"
                            + "已切换到另一控制侧并复核完整房间（下限 {2} mm）",
                        axis,
                        FormatOptionalNumber(diagnostic.ActualValue),
                        FormatOptionalNumber(diagnostic.Threshold));
                case CandidateDiagnosticCode.CenteredDoorDefaultApplied:
                    return "门洞居中，使用固定 WCS 优先候选";
                case CandidateDiagnosticCode.CenteredDoorFlipped:
                    return "门洞居中，已翻转等价沿墙分配";
                case CandidateDiagnosticCode.MinimumCutNotMet:
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}边界砖 {1} mm 小于当前下限 {2} mm",
                        axis,
                        FormatOptionalNumber(diagnostic.ActualValue),
                        FormatOptionalNumber(diagnostic.Threshold));
                case CandidateDiagnosticCode.InsufficientFullTileForRedistribution:
                    return axis + "没有可用于半砖重分配的整砖";
                case CandidateDiagnosticCode.PolicyConstraintNotSatisfied:
                    return string.IsNullOrWhiteSpace(diagnostic.Message)
                        ? axis + "未满足候选策略约束"
                        : diagnostic.Message;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(diagnostic.Code));
            }
        }

        private static string FormatOptionalNumber(double? value)
        {
            return value.HasValue ? FormatNumber(value.Value) : "未知";
        }

        private static string FormatAxis(TileLayoutAxis axis)
        {
            return axis == TileLayoutAxis.X ? "X" : "Y";
        }

        private static string FormatRoomSide(RoomSide side)
        {
            switch (side)
            {
                case RoomSide.West:
                    return "西墙";
                case RoomSide.East:
                    return "东墙";
                case RoomSide.South:
                    return "南墙";
                case RoomSide.North:
                    return "北墙";
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
            }
        }

        private static void GetDoorDirections(
            RoomSide side,
            out string lowDirection,
            out string highDirection,
            out string entryDirection)
        {
            switch (side)
            {
                case RoomSide.West:
                    lowDirection = "南";
                    highDirection = "北";
                    entryDirection = "西→东";
                    return;
                case RoomSide.East:
                    lowDirection = "南";
                    highDirection = "北";
                    entryDirection = "东→西";
                    return;
                case RoomSide.South:
                    lowDirection = "西";
                    highDirection = "东";
                    entryDirection = "南→北";
                    return;
                case RoomSide.North:
                    lowDirection = "西";
                    highDirection = "东";
                    entryDirection = "北→南";
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
            }
        }
    }
}
