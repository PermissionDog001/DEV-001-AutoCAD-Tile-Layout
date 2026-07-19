using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace TileLayout.AutoCAD.Probe
{
    public sealed class TileProbeCommands
    {
        private const double CoordinateTolerance = 1e-6;
        private const string ProbeLayerName = "TILE_LAYOUT_PROBE";

        [CommandMethod("TILE600PROBE", CommandFlags.Modal)]
        public void RunProbe()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Database database = document.Database;
            Editor editor = document.Editor;

            if (!database.TileMode)
            {
                editor.WriteMessage("\n请切换到模型空间后再执行 TILE600PROBE。");
                return;
            }

            if (database.Insunits != UnitsValue.Millimeters)
            {
                editor.WriteMessage(
                    "\n当前图纸单位不是毫米（INSUNITS={0}），技术探针已停止。",
                    database.Insunits);
                return;
            }

            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择组成轴对齐矩形房间的四条 LINE："
            };
            SelectionFilter filter = new SelectionFilter(
                new[] { new TypedValue((int)DxfCode.Start, "LINE") });

            PromptSelectionResult selectionResult = editor.GetSelection(options, filter);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n未完成选择，未生成任何对象。");
                return;
            }

            ObjectId[] selectedIds = selectionResult.Value.GetObjectIds();
            if (selectedIds.Length != 4)
            {
                editor.WriteMessage(
                    "\n必须且只能选择四条 LINE；本次选择了 {0} 条。未生成任何对象。",
                    selectedIds.Length);
                return;
            }

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable =
                    (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                ObjectId modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
                var lines = new List<LineSnapshot>(4);

                for (int index = 0; index < selectedIds.Length; index++)
                {
                    Line line = transaction.GetObject(selectedIds[index], OpenMode.ForRead) as Line;
                    if (line == null || line.OwnerId != modelSpaceId)
                    {
                        editor.WriteMessage("\n选择中包含非模型空间 LINE，未生成任何对象。");
                        return;
                    }

                    lines.Add(new LineSnapshot(line.StartPoint, line.EndPoint));
                    editor.WriteMessage(
                        "\nLINE {0}: 起点 {1}，终点 {2}",
                        index + 1,
                        FormatPoint(line.StartPoint),
                        FormatPoint(line.EndPoint));
                }

                RectangleProbeResult rectangle;
                string validationError;
                if (!TryCreateRectangle(lines, out rectangle, out validationError))
                {
                    editor.WriteMessage("\n矩形验证失败：{0} 未生成任何对象。", validationError);
                    return;
                }

                EnsureProbeLayer(transaction, database);

                BlockTableRecord modelSpace =
                    (BlockTableRecord)transaction.GetObject(modelSpaceId, OpenMode.ForWrite);
                double probeOffset = Math.Min(600.0, Math.Min(rectangle.Width, rectangle.Height));
                var probeLine = new Line(
                    new Point3d(rectangle.West, rectangle.South, rectangle.Elevation),
                    new Point3d(
                        rectangle.West + probeOffset,
                        rectangle.South + probeOffset,
                        rectangle.Elevation))
                {
                    Layer = ProbeLayerName
                };

                modelSpace.AppendEntity(probeLine);
                transaction.AddNewlyCreatedDBObject(probeLine, true);
                transaction.Commit();

                editor.WriteMessage(
                    "\n矩形验证通过：宽={0} mm，高={1} mm，西南角={2}。",
                    FormatNumber(rectangle.Width),
                    FormatNumber(rectangle.Height),
                    FormatPoint(new Point3d(rectangle.West, rectangle.South, rectangle.Elevation)));
                editor.WriteMessage(
                    "\n已在图层 {0} 创建一条测试线。请输入 UNDO 并撤销 1 步进行验证。",
                    ProbeLayerName);
            }
        }

        private static void EnsureProbeLayer(Transaction transaction, Database database)
        {
            LayerTable layerTable =
                (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(ProbeLayerName))
            {
                return;
            }

            layerTable.UpgradeOpen();
            var layer = new LayerTableRecord { Name = ProbeLayerName };
            layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
        }

        private static bool TryCreateRectangle(
            IReadOnlyCollection<LineSnapshot> lines,
            out RectangleProbeResult result,
            out string error)
        {
            result = null;
            error = null;

            Point3d[] points = lines.SelectMany(line => new[] { line.Start, line.End }).ToArray();
            double elevation = points[0].Z;
            if (points.Any(point => !NearlyEqual(point.Z, elevation)))
            {
                error = "四条线不共面或 Z 坐标不一致。";
                return false;
            }

            double west = points.Min(point => point.X);
            double east = points.Max(point => point.X);
            double south = points.Min(point => point.Y);
            double north = points.Max(point => point.Y);

            if (east - west <= CoordinateTolerance || north - south <= CoordinateTolerance)
            {
                error = "矩形宽度或高度小于等于零。";
                return false;
            }

            var sides = new bool[4]; // south, east, north, west
            foreach (LineSnapshot line in lines)
            {
                double deltaX = Math.Abs(line.End.X - line.Start.X);
                double deltaY = Math.Abs(line.End.Y - line.Start.Y);
                bool horizontal = deltaX > CoordinateTolerance && deltaY <= CoordinateTolerance;
                bool vertical = deltaY > CoordinateTolerance && deltaX <= CoordinateTolerance;

                int side;
                if (horizontal && Spans(line.Start.X, line.End.X, west, east))
                {
                    if (NearlyEqual(line.Start.Y, south) && NearlyEqual(line.End.Y, south))
                    {
                        side = 0;
                    }
                    else if (NearlyEqual(line.Start.Y, north) && NearlyEqual(line.End.Y, north))
                    {
                        side = 2;
                    }
                    else
                    {
                        error = "存在不位于矩形南侧或北侧边界的水平线。";
                        return false;
                    }
                }
                else if (vertical && Spans(line.Start.Y, line.End.Y, south, north))
                {
                    if (NearlyEqual(line.Start.X, east) && NearlyEqual(line.End.X, east))
                    {
                        side = 1;
                    }
                    else if (NearlyEqual(line.Start.X, west) && NearlyEqual(line.End.X, west))
                    {
                        side = 3;
                    }
                    else
                    {
                        error = "存在不位于矩形东侧或西侧边界的竖直线。";
                        return false;
                    }
                }
                else
                {
                    error = "四条线必须与 WCS X/Y 轴平行，并完整连接矩形四角。";
                    return false;
                }

                if (sides[side])
                {
                    error = "矩形边界存在重复边或缺失边。";
                    return false;
                }

                sides[side] = true;
            }

            if (sides.Any(side => !side))
            {
                error = "四条线没有形成完整闭合矩形。";
                return false;
            }

            result = new RectangleProbeResult(west, east, south, north, elevation);
            return true;
        }

        private static bool Spans(double first, double second, double minimum, double maximum)
        {
            return NearlyEqual(Math.Min(first, second), minimum)
                && NearlyEqual(Math.Max(first, second), maximum);
        }

        private static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) <= CoordinateTolerance;
        }

        private static string FormatPoint(Point3d point)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0}, {1}, {2})",
                FormatNumber(point.X),
                FormatNumber(point.Y),
                FormatNumber(point.Z));
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private sealed class LineSnapshot
        {
            public LineSnapshot(Point3d start, Point3d end)
            {
                Start = start;
                End = end;
            }

            public Point3d Start { get; }

            public Point3d End { get; }
        }

        private sealed class RectangleProbeResult
        {
            public RectangleProbeResult(
                double west,
                double east,
                double south,
                double north,
                double elevation)
            {
                West = west;
                East = east;
                South = south;
                North = north;
                Elevation = elevation;
            }

            public double West { get; }

            public double East { get; }

            public double South { get; }

            public double North { get; }

            public double Elevation { get; }

            public double Width => East - West;

            public double Height => North - South;
        }
    }
}
