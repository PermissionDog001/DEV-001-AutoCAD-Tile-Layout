using System;
using System.Globalization;
using System.Security;
using System.Text;
using TileLayout.Core.Models;

namespace TileLayout.Core
{
    public static class LayoutDrawingPlanSvgRenderer
    {
        private const double CanvasWidth = 960.0;
        private const double CanvasHeight = 720.0;
        private const double Margin = 48.0;
        private const double LegendWidth = 230.0;

        public static string Render(LayoutDrawingPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (plan.Width <= GeometryTolerance.Coordinate
                || plan.Height <= GeometryTolerance.Coordinate)
            {
                throw new ArgumentException(
                    "The drawing plan must have positive bounds.",
                    nameof(plan));
            }

            double drawingWidth = CanvasWidth - (2.0 * Margin) - LegendWidth;
            double drawingHeight = CanvasHeight - (2.0 * Margin);
            double scale = Math.Min(
                drawingWidth / plan.Width,
                drawingHeight / plan.Height);
            var svg = new StringBuilder();
            svg.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"960\" height=\"720\" viewBox=\"0 0 960 720\" data-candidate=\"")
                .Append(Escape(plan.CandidateId))
                .AppendLine("\">");
            svg.AppendLine("  <rect width=\"960\" height=\"720\" fill=\"#ffffff\"/>");
            svg.AppendLine("  <g id=\"tiles\" stroke=\"#c7cdd4\" stroke-width=\"1\">");
            foreach (LayoutDrawingTile tile in plan.Tiles)
            {
                string fill = tile.IsContinuousIrregular
                    ? "#ffe7bf"
                    : tile.IsFullTile ? "#f7fafc" : "#edf4fb";
                svg.Append("    <polygon id=\"")
                    .Append(tile.Id)
                    .Append("\" points=\"")
                    .Append(Points(tile.Outline, plan, scale))
                    .Append("\" fill=\"")
                    .Append(fill)
                    .AppendLine("\"/>");
            }

            svg.AppendLine("  </g>");
            svg.AppendLine("  <g id=\"regions\" fill=\"none\" stroke=\"#9a86c8\" stroke-width=\"1\" stroke-dasharray=\"6 4\">");
            foreach (LayoutDrawingRegion region in plan.Regions)
            {
                svg.Append("    <rect id=\"region-")
                    .Append(Escape(region.Id))
                    .Append("\" x=\"")
                    .Append(F(X(region.Bounds.West, plan, scale)))
                    .Append("\" y=\"")
                    .Append(F(Y(region.Bounds.North, plan, scale)))
                    .Append("\" width=\"")
                    .Append(F(region.Bounds.Width * scale))
                    .Append("\" height=\"")
                    .Append(F(region.Bounds.Height * scale))
                    .AppendLine("\"/>");
            }

            svg.AppendLine("  </g>");
            svg.Append("  <polygon id=\"room-boundary\" points=\"")
                .Append(Points(plan.RoomOutline, plan, scale))
                .AppendLine("\" fill=\"none\" stroke=\"#374151\" stroke-width=\"3\"/>");
            svg.AppendLine("  <g id=\"division-lines\" stroke=\"#1677c8\" stroke-width=\"2\">");
            foreach (LayoutDrawingLine line in plan.DivisionLines)
            {
                if (line.Semantic == LayoutDrawingLineSemantic.FinishedFaceOutline)
                {
                    continue;
                }

                AppendLine(svg, line, plan, scale);
            }

            svg.AppendLine("  </g>");
            svg.AppendLine("  <g id=\"connections\" stroke=\"#d97706\" stroke-width=\"4\">");
            foreach (LayoutDrawingLine line in plan.Connections)
            {
                AppendLine(svg, line, plan, scale);
            }

            svg.AppendLine("  </g>");
            svg.AppendLine("  <g id=\"wall-corners\" fill=\"none\" stroke-width=\"3\">");
            foreach (LayoutDrawingWallCorner corner in plan.WallCorners)
            {
                AppendWallCorner(svg, corner, plan, scale);
            }

            svg.AppendLine("  </g>");
            double legendX = CanvasWidth - LegendWidth + 18.0;
            svg.AppendLine("  <g id=\"legend\" font-family=\"Microsoft YaHei, sans-serif\" font-size=\"15\" fill=\"#1f2937\">");
            svg.Append("    <text x=\"").Append(F(legendX)).AppendLine("\" y=\"64\" font-size=\"19\" font-weight=\"bold\">铺贴图预览</text>");
            svg.Append("    <text x=\"").Append(F(legendX)).Append("\" y=\"94\">状态：")
                .Append(FormatState(plan.CandidateState)).AppendLine("</text>");
            svg.Append("    <text x=\"").Append(F(legendX)).Append("\" y=\"122\">地砖：")
                .Append(plan.Tiles.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(" 块</text>");
            svg.Append("    <text x=\"").Append(F(legendX)).Append("\" y=\"150\">分格线：")
                .Append(plan.DivisionLines.Count.ToString(CultureInfo.InvariantCulture)).AppendLine(" 条</text>");
            svg.Append("    <line x1=\"").Append(F(legendX)).Append("\" y1=\"184\" x2=\"")
                .Append(F(legendX + 38)).AppendLine("\" y2=\"184\" stroke=\"#1677c8\" stroke-width=\"2\"/>");
            svg.Append("    <text x=\"").Append(F(legendX + 48)).AppendLine("\" y=\"190\">实际分格线</text>");
            svg.Append("    <line x1=\"").Append(F(legendX)).Append("\" y1=\"218\" x2=\"")
                .Append(F(legendX + 38)).AppendLine("\" y2=\"218\" stroke=\"#d97706\" stroke-width=\"4\"/>");
            svg.Append("    <text x=\"").Append(F(legendX + 48)).AppendLine("\" y=\"224\">两区接合边</text>");
            svg.Append("    <rect x=\"").Append(F(legendX)).AppendLine("\" y=\"246\" width=\"38\" height=\"20\" fill=\"#ffe7bf\" stroke=\"#c7cdd4\"/>");
            svg.Append("    <text x=\"").Append(F(legendX + 48)).AppendLine("\" y=\"262\">连续异形砖</text>");
            svg.Append("    <line x1=\"").Append(F(legendX + 8)).Append("\" y1=\"288\" x2=\"")
                .Append(F(legendX + 30)).AppendLine("\" y2=\"310\" stroke=\"#2563eb\" stroke-width=\"3\"/>");
            svg.Append("    <line x1=\"").Append(F(legendX + 30)).Append("\" y1=\"288\" x2=\"")
                .Append(F(legendX + 8)).AppendLine("\" y2=\"310\" stroke=\"#2563eb\" stroke-width=\"3\"/>");
            svg.Append("    <text x=\"").Append(F(legendX + 48)).AppendLine("\" y=\"305\">墙角—地砖缝诊断</text>");
            svg.AppendLine("  </g>");
            svg.AppendLine("</svg>");
            return svg.ToString().Replace("\r\n", "\n");
        }

        private static void AppendLine(
            StringBuilder svg,
            LayoutDrawingLine line,
            LayoutDrawingPlan plan,
            double scale)
        {
            svg.Append("    <line id=\"").Append(line.Id)
                .Append("\" x1=\"").Append(F(X(line.Geometry.Start.X, plan, scale)))
                .Append("\" y1=\"").Append(F(Y(line.Geometry.Start.Y, plan, scale)))
                .Append("\" x2=\"").Append(F(X(line.Geometry.End.X, plan, scale)))
                .Append("\" y2=\"").Append(F(Y(line.Geometry.End.Y, plan, scale)))
                .AppendLine("\"/>");
        }

        private static void AppendWallCorner(
            StringBuilder svg,
            LayoutDrawingWallCorner corner,
            LayoutDrawingPlan plan,
            double scale)
        {
            double x = X(corner.Position.X, plan, scale);
            double y = Y(corner.Position.Y, plan, scale);
            const double markerRadius = 6.0;
            string color = !corner.IsOptimizationTarget
                ? "#6b7280"
                : corner.IsExactGridIntersection
                    ? "#2563eb"
                    : corner.HasAnyExactSeam ? "#0891b2" : "#d97706";
            svg.Append("    <g id=\"")
                .Append(Escape(corner.Id))
                .Append("\" stroke=\"")
                .Append(color)
                .Append("\" data-geometry=\"")
                .Append(corner.GeometryType)
                .Append("\" data-target=\"")
                .Append(corner.IsOptimizationTarget ? "true" : "false")
                .Append("\" data-vertical-hit=\"")
                .Append(corner.HasVerticalSeam ? "true" : "false")
                .Append("\" data-horizontal-hit=\"")
                .Append(corner.HasHorizontalSeam ? "true" : "false")
                .Append("\" data-nearest-vertical=\"")
                .Append(NullableDistance(corner.NearestVerticalSeamDistance))
                .Append("\" data-nearest-horizontal=\"")
                .Append(NullableDistance(corner.NearestHorizontalSeamDistance))
                .AppendLine("\">");
            svg.Append("      <line x1=\"").Append(F(x - markerRadius))
                .Append("\" y1=\"").Append(F(y - markerRadius))
                .Append("\" x2=\"").Append(F(x + markerRadius))
                .Append("\" y2=\"").Append(F(y + markerRadius))
                .AppendLine("\"/>");
            svg.Append("      <line x1=\"").Append(F(x + markerRadius))
                .Append("\" y1=\"").Append(F(y - markerRadius))
                .Append("\" x2=\"").Append(F(x - markerRadius))
                .Append("\" y2=\"").Append(F(y + markerRadius))
                .AppendLine("\"/>");
            svg.AppendLine("    </g>");
        }

        private static string NullableDistance(double? value)
        {
            return value.HasValue ? F(value.Value) : string.Empty;
        }

        private static string Points(
            System.Collections.Generic.IReadOnlyList<Point3D> points,
            LayoutDrawingPlan plan,
            double scale)
        {
            var value = new StringBuilder();
            for (int index = 0; index < points.Count; index++)
            {
                if (index > 0)
                {
                    value.Append(' ');
                }

                value.Append(F(X(points[index].X, plan, scale)))
                    .Append(',')
                    .Append(F(Y(points[index].Y, plan, scale)));
            }

            return value.ToString();
        }

        private static double X(double x, LayoutDrawingPlan plan, double scale)
        {
            return Margin + ((x - plan.West) * scale);
        }

        private static double Y(double y, LayoutDrawingPlan plan, double scale)
        {
            return Margin + ((plan.North - y) * scale);
        }

        private static string F(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return SecurityElement.Escape(value) ?? string.Empty;
        }

        private static string FormatState(LayoutCandidateState state)
        {
            switch (state)
            {
                case LayoutCandidateState.AutomaticUsable:
                    return "自动推荐";
                case LayoutCandidateState.RequiresUserDecision:
                    return "需要人工确认";
                case LayoutCandidateState.RequiresProjectPolicy:
                    return "需要补充项目规则";
                default:
                    return "当前不可使用";
            }
        }
    }
}
