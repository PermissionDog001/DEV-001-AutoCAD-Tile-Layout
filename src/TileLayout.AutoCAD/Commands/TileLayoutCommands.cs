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
        private const string LayoutLayerName = "TILE_LAYOUT_600";

        [CommandMethod("TILE600", CommandFlags.Modal)]
        public void CreateTileLayout()
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
                editor.WriteMessage("\n请切换到模型空间后再执行 TILE600。未生成任何对象。");
                return;
            }

            if (database.Insunits != UnitsValue.Millimeters)
            {
                editor.WriteMessage(
                    "\n当前图纸单位不是毫米（INSUNITS={0}），TILE600 已停止。未生成任何对象。",
                    database.Insunits);
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

                    layout = TileGridCalculator.Calculate(validation.Rectangle);
                    ObjectId layoutLayerId = EnsureLayoutLayer(transaction, database);
                    WriteDivisionLines(
                        transaction,
                        modelSpaceId,
                        layoutLayerId,
                        layout.DivisionLines);

                    transaction.Commit();
                }

                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatSuccess(layout, LayoutLayerName));
                editor.WriteMessage(
                    "\n原四条墙线未修改，插件未保存图纸；可用一次 U 或 UNDO 撤销本次新增。");
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

        private static IReadOnlyCollection<CoreLineSegment3D> ReadBoundarySnapshots(
            Transaction transaction,
            IEnumerable<ObjectId> selectedIds,
            ObjectId modelSpaceId,
            Editor editor)
        {
            var snapshots = new List<CoreLineSegment3D>(4);
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
            Database database)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId,
                OpenMode.ForRead);
            if (layerTable.Has(LayoutLayerName))
            {
                return layerTable[LayoutLayerName];
            }

            layerTable.UpgradeOpen();
            var layer = new LayerTableRecord { Name = LayoutLayerName };
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
