using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using TileLayout.AutoCAD.Adapter;
using TileLayout.Core;
using CoreLineSegment3D = TileLayout.Core.Models.LineSegment3D;
using CorePoint3D = TileLayout.Core.Models.Point3D;

namespace TileLayout.AutoCAD
{
    public sealed class TileLayoutCommands
    {
        private const string FixedLayoutLayerName = "TILE_LAYOUT_600";
        private const string ParameterizedLayoutLayerName = "TILE_LAYOUT";
        private const string OrthogonalLayoutLayerName = "TILE_LAYOUT_ORTHO";

        [CommandMethod("TILE600", CommandFlags.Modal)]
        public void CreateTileLayout()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            ExecuteLayout(
                document,
                "TILE600",
                FixedLayoutLayerName,
                null);
        }

        [CommandMethod("TILELAYOUT", CommandFlags.Modal)]
        public void CreateParameterizedTileLayout()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            TileLayoutParameters parameters;
            if (!TryPromptTileLayoutParameters(document.Editor, out parameters))
            {
                document.Editor.WriteMessage(
                    "\nTILELAYOUT 已取消，未进入边界选择，未生成任何对象。");
                return;
            }

            ExecuteLayout(
                document,
                "TILELAYOUT",
                ParameterizedLayoutLayerName,
                parameters);
        }

        [CommandMethod("TILEORTHO", CommandFlags.Modal)]
        public void CreateOrthogonalTileLayout()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            TileLayoutParameters parameters;
            if (!TryPromptOrthogonalTileLayoutParameters(
                document.Editor,
                out parameters))
            {
                document.Editor.WriteMessage(
                    "\nTILEORTHO 已取消，未进入边界选择，未生成任何对象。");
                return;
            }

            ExecuteOrthogonalLayout(document, parameters);
        }

        private static void ExecuteOrthogonalLayout(
            Document document,
            TileLayoutParameters parameters)
        {
            Database database = document.Database;
            Editor editor = document.Editor;

            if (!database.TileMode)
            {
                editor.WriteMessage(
                    "\n请切换到模型空间后再执行 TILEORTHO。未生成任何对象。");
                return;
            }

            if (database.Insunits != UnitsValue.Millimeters)
            {
                editor.WriteMessage(
                    "\n当前图纸单位不是毫米（INSUNITS={0}），TILEORTHO 已停止。未生成任何对象。",
                    database.Insunits);
                return;
            }

            PromptSelectionResult selectionResult =
                SelectOrthogonalBoundaryLines(editor);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage(
                    "\n未完成正交房间边界 LINE 的选择，未生成任何对象。");
                return;
            }

            ObjectId[] selectedIds = selectionResult.Value.GetObjectIds();
            if (selectedIds.Length < 4)
            {
                editor.WriteMessage(
                    "\n必须选择至少四条 LINE；本次选择了 {0} 条。未生成任何对象。",
                    selectedIds.Length);
                return;
            }

            try
            {
                OrthogonalTileLayoutResult layout;
                using (Transaction transaction =
                    database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)transaction.GetObject(
                        database.BlockTableId,
                        OpenMode.ForRead);
                    ObjectId modelSpaceId = blockTable[BlockTableRecord.ModelSpace];
                    IReadOnlyCollection<CoreLineSegment3D> boundaryLines =
                        ReadBoundarySnapshots(
                            transaction,
                            selectedIds,
                            modelSpaceId,
                            editor);
                    if (boundaryLines == null)
                    {
                        return;
                    }

                    OrthogonalRoomValidationResult validation =
                        OrthogonalRoomValidator.Validate(boundaryLines);
                    if (!validation.IsValid)
                    {
                        editor.WriteMessage(
                            "\n{0}",
                            TileLayoutCommandText.FormatOrthogonalValidationFailure(
                                validation));
                        return;
                    }

                    layout = OrthogonalTileGridCalculator.Calculate(
                        validation.Room,
                        parameters);
                    ObjectId layoutLayerId = EnsureLayoutLayer(
                        transaction,
                        database,
                        OrthogonalLayoutLayerName);
                    WriteDivisionLines(
                        transaction,
                        modelSpaceId,
                        layoutLayerId,
                        layout.DivisionLines);
                    transaction.Commit();
                }

                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatOrthogonalSuccess(
                        layout,
                        OrthogonalLayoutLayerName));
                editor.WriteMessage(
                    "\n原边界 LINE 未修改，插件未保存图纸；可用一次 U 或 UNDO 撤销本次新增。");
            }
            catch (TileLayoutLimitExceededException exception)
            {
                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatLimitExceeded(
                        exception,
                        "TILEORTHO"));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                editor.WriteMessage(
                    "\n生成失败（AutoCAD 状态：{0}），事务已回滚，未保留部分分格线。",
                    exception.ErrorStatus);
            }
            catch (System.Exception)
            {
                editor.WriteMessage(
                    "\n生成失败，事务已回滚，未保留部分分格线。请保留测试图并记录操作步骤。"
                );
            }
        }

        private static void ExecuteLayout(
            Document document,
            string commandName,
            string layoutLayerName,
            TileLayoutParameters parameters)
        {
            Database database = document.Database;
            Editor editor = document.Editor;

            if (!database.TileMode)
            {
                editor.WriteMessage(
                    "\n请切换到模型空间后再执行 {0}。未生成任何对象。",
                    commandName);
                return;
            }

            if (database.Insunits != UnitsValue.Millimeters)
            {
                editor.WriteMessage(
                    "\n当前图纸单位不是毫米（INSUNITS={0}），{1} 已停止。未生成任何对象。",
                    database.Insunits,
                    commandName);
                return;
            }

            PromptSelectionResult selectionResult = SelectBoundaryLines(editor);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n未完成四条 LINE 的选择，未生成任何对象。");
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

            try
            {
                TileLayoutResult layout;
                using (Transaction transaction =
                    database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)transaction.GetObject(
                        database.BlockTableId,
                        OpenMode.ForRead);
                    ObjectId modelSpaceId = blockTable[BlockTableRecord.ModelSpace];

                    IReadOnlyCollection<CoreLineSegment3D> boundaryLines =
                        ReadBoundarySnapshots(
                            transaction,
                            selectedIds,
                            modelSpaceId,
                            editor);
                    if (boundaryLines == null)
                    {
                        return;
                    }

                    RectangleValidationResult validation =
                        RectangleValidator.Validate(boundaryLines);
                    if (!validation.IsValid)
                    {
                        editor.WriteMessage(
                            "\n{0}",
                            TileLayoutCommandText.FormatValidationFailure(validation));
                        return;
                    }

                    layout = parameters == null
                        ? TileGridCalculator.Calculate(validation.Rectangle)
                        : TileGridCalculator.Calculate(validation.Rectangle, parameters);
                    ObjectId layoutLayerId = EnsureLayoutLayer(
                        transaction,
                        database,
                        layoutLayerName);
                    WriteDivisionLines(
                        transaction,
                        modelSpaceId,
                        layoutLayerId,
                        layout.DivisionLines);

                    transaction.Commit();
                }

                editor.WriteMessage(
                    "\n{0}",
                    parameters == null
                        ? TileLayoutCommandText.FormatSuccess(
                            layout,
                            layoutLayerName)
                        : TileLayoutCommandText.FormatParameterizedSuccess(
                            layout,
                            layoutLayerName));
                editor.WriteMessage(
                    "\n原四条墙线未修改，插件未保存图纸；可用一次 U 或 UNDO 撤销本次新增。");
            }
            catch (TileLayoutLimitExceededException exception)
            {
                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatLimitExceeded(exception));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                editor.WriteMessage(
                    "\n生成失败（AutoCAD 状态：{0}），事务已回滚，未保留部分分格线。",
                    exception.ErrorStatus);
            }
            catch (System.Exception)
            {
                editor.WriteMessage(
                    "\n生成失败，事务已回滚，未保留部分分格线。请保留测试图并记录操作步骤。"
                );
            }
        }

        private static bool TryPromptTileLayoutParameters(
            Editor editor,
            out TileLayoutParameters parameters)
        {
            parameters = null;

            double tileWidth;
            if (!TryPromptTileDimension(
                editor,
                "砖宽",
                "沿 WCS X",
                TileLayoutRules.TileWidth,
                out tileWidth))
            {
                return false;
            }

            double tileHeight;
            if (!TryPromptTileDimension(
                editor,
                "砖高",
                "沿 WCS Y",
                TileLayoutRules.TileHeight,
                out tileHeight))
            {
                return false;
            }

            TileLayoutStartCorner startCorner;
            if (!TryPromptStartCorner(editor, out startCorner))
            {
                return false;
            }

            parameters = new TileLayoutParameters(
                tileWidth,
                tileHeight,
                startCorner);
            return true;
        }

        private static bool TryPromptOrthogonalTileLayoutParameters(
            Editor editor,
            out TileLayoutParameters parameters)
        {
            parameters = null;

            double tileWidth;
            if (!TryPromptTileDimension(
                editor,
                "砖宽",
                "沿 WCS X",
                TileLayoutRules.TileWidth,
                out tileWidth))
            {
                return false;
            }

            double tileHeight;
            if (!TryPromptTileDimension(
                editor,
                "砖高",
                "沿 WCS Y",
                TileLayoutRules.TileHeight,
                out tileHeight))
            {
                return false;
            }

            TileLayoutStartCorner anchor;
            if (!TryPromptBoundingBoxAnchor(editor, out anchor))
            {
                return false;
            }

            parameters = new TileLayoutParameters(tileWidth, tileHeight, anchor);
            return true;
        }

        private static bool TryPromptStartCorner(
            Editor editor,
            out TileLayoutStartCorner startCorner)
        {
            var options = new PromptKeywordOptions(
                "\n请选择起铺角（WCS：SW=西→东/南→北，SE=东→西/南→北，"
                    + "NW=西→东/北→南，NE=东→西/北→南）[SW/SE/NW/NE] <SW>：")
            {
                AllowNone = true
            };
            options.Keywords.Add("SW");
            options.Keywords.Add("SE");
            options.Keywords.Add("NW");
            options.Keywords.Add("NE");
            options.Keywords.Default = "SW";

            PromptResult result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK
                && result.Status != PromptStatus.None)
            {
                startCorner = TileLayoutStartCorner.SouthWest;
                return false;
            }

            string keyword = result.Status == PromptStatus.None
                ? "SW"
                : result.StringResult;
            switch (keyword.ToUpperInvariant())
            {
                case "SW":
                    startCorner = TileLayoutStartCorner.SouthWest;
                    return true;
                case "SE":
                    startCorner = TileLayoutStartCorner.SouthEast;
                    return true;
                case "NW":
                    startCorner = TileLayoutStartCorner.NorthWest;
                    return true;
                case "NE":
                    startCorner = TileLayoutStartCorner.NorthEast;
                    return true;
                default:
                    throw new InvalidOperationException(
                        "AutoCAD returned an unsupported start-corner keyword.");
            }
        }

        private static bool TryPromptBoundingBoxAnchor(
            Editor editor,
            out TileLayoutStartCorner anchor)
        {
            var options = new PromptKeywordOptions(
                "\n请选择 WCS 包围盒网格锚点（锚点可在房间外，只决定网格相位）"
                    + "[SW/SE/NW/NE] <SW>：")
            {
                AllowNone = true
            };
            options.Keywords.Add("SW");
            options.Keywords.Add("SE");
            options.Keywords.Add("NW");
            options.Keywords.Add("NE");
            options.Keywords.Default = "SW";

            PromptResult result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK
                && result.Status != PromptStatus.None)
            {
                anchor = TileLayoutStartCorner.SouthWest;
                return false;
            }

            string keyword = result.Status == PromptStatus.None
                ? "SW"
                : result.StringResult;
            switch (keyword.ToUpperInvariant())
            {
                case "SW":
                    anchor = TileLayoutStartCorner.SouthWest;
                    return true;
                case "SE":
                    anchor = TileLayoutStartCorner.SouthEast;
                    return true;
                case "NW":
                    anchor = TileLayoutStartCorner.NorthWest;
                    return true;
                case "NE":
                    anchor = TileLayoutStartCorner.NorthEast;
                    return true;
                default:
                    throw new InvalidOperationException(
                        "AutoCAD returned an unsupported bounding-box anchor keyword.");
            }
        }

        private static bool TryPromptTileDimension(
            Editor editor,
            string dimensionName,
            string direction,
            double defaultValue,
            out double value)
        {
            var options = new PromptDoubleOptions(
                string.Format(
                    "\n请输入{0}（{1}，mm） <600>：",
                    dimensionName,
                    direction))
            {
                AllowNegative = true,
                AllowNone = true,
                AllowZero = true,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };

            while (true)
            {
                PromptDoubleResult result = editor.GetDouble(options);
                if (result.Status != PromptStatus.OK
                    && result.Status != PromptStatus.None)
                {
                    value = 0.0;
                    return false;
                }

                value = result.Status == PromptStatus.None
                    ? defaultValue
                    : result.Value;
                if (IsValidTileDimension(value))
                {
                    return true;
                }

                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatParameterError(dimensionName));
            }
        }

        private static bool IsValidTileDimension(double value)
        {
            try
            {
                new TileLayoutParameters(value, value);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static PromptSelectionResult SelectBoundaryLines(Editor editor)
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择组成轴对齐矩形房间的四条模型空间 LINE：",
                RejectObjectsFromNonCurrentSpace = true
            };
            var filter = new SelectionFilter(
                new[] { new TypedValue((int)DxfCode.Start, "LINE") });

            return editor.GetSelection(options, filter);
        }

        private static PromptSelectionResult SelectOrthogonalBoundaryLines(
            Editor editor)
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding =
                    "\n请选择组成单一 WCS 正交简单闭环的 4 条及以上模型空间 LINE：",
                RejectObjectsFromNonCurrentSpace = true
            };
            var filter = new SelectionFilter(
                new[] { new TypedValue((int)DxfCode.Start, "LINE") });

            return editor.GetSelection(options, filter);
        }

        private static IReadOnlyCollection<CoreLineSegment3D> ReadBoundarySnapshots(
            Transaction transaction,
            IEnumerable<ObjectId> selectedIds,
            ObjectId modelSpaceId,
            Editor editor)
        {
            var snapshots = new List<CoreLineSegment3D>();
            foreach (ObjectId selectedId in selectedIds)
            {
                Line line = transaction.GetObject(selectedId, OpenMode.ForRead) as Line;
                if (line == null || line.OwnerId != modelSpaceId)
                {
                    editor.WriteMessage(
                        "\n选择中包含非模型空间 LINE，未生成任何对象。"
                    );
                    return null;
                }

                Point3d start = line.StartPoint;
                Point3d end = line.EndPoint;
                snapshots.Add(
                    new CoreLineSegment3D(
                        new CorePoint3D(start.X, start.Y, start.Z),
                        new CorePoint3D(end.X, end.Y, end.Z)));
            }

            return snapshots;
        }

        private static ObjectId EnsureLayoutLayer(
            Transaction transaction,
            Database database,
            string layerName)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId,
                OpenMode.ForRead);
            if (layerTable.Has(layerName))
            {
                return layerTable[layerName];
            }

            layerTable.UpgradeOpen();
            var layer = new LayerTableRecord { Name = layerName };
            ObjectId layerId = layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
            return layerId;
        }

        private static void WriteDivisionLines(
            Transaction transaction,
            ObjectId modelSpaceId,
            ObjectId layoutLayerId,
            IEnumerable<CoreLineSegment3D> divisionLines)
        {
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                modelSpaceId,
                OpenMode.ForWrite);

            foreach (CoreLineSegment3D divisionLine in divisionLines)
            {
                var line = new Line(
                    new Point3d(
                        divisionLine.Start.X,
                        divisionLine.Start.Y,
                        divisionLine.Start.Z),
                    new Point3d(
                        divisionLine.End.X,
                        divisionLine.End.Y,
                        divisionLine.End.Z))
                {
                    LayerId = layoutLayerId
                };

                modelSpace.AppendEntity(line);
                transaction.AddNewlyCreatedDBObject(line, true);
            }
        }
    }
}
