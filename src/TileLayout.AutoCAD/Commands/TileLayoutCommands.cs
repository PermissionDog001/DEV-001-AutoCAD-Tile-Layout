using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
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
        private const string DoorRectangularLayoutLayerName =
            "TILE_LAYOUT_DOOR_RECT";

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

        [CommandMethod("TILEORTHOUI", CommandFlags.Modal)]
        public void ShowOrthogonalDecisionPalette()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            OrthogonalDecisionPaletteHost.Show(document);
            document.Editor.WriteMessage(
                "\n自动排砖插件对话框已打开。请按四页顺序操作；"
                    + "无需输入动作字母或 WCS 坐标，本阶段不会写入图纸。" );
        }

        [CommandMethod("TILEUI", CommandFlags.Modal)]
        public void ShowOrthogonalDecisionPaletteShort()
        {
            ShowOrthogonalDecisionPalette();
        }

        [CommandMethod("TILEORTHOUIROOM", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void SelectOrthogonalDecisionRoomFromPalette()
        {
            ExecuteGuidedRoomSelection();
        }

        [CommandMethod("TILEORTHOUICONTROL", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void SelectOrthogonalDecisionControlRegionFromPalette()
        {
            ExecuteGuidedRectangleSelection(
                OrthogonalDecisionGuideAction.SelectControlRegion,
                "门洞影响范围",
                (control, rectangle) => control.ApplyControlRegion(rectangle));
        }

        [CommandMethod("TILEORTHOUIDOOR", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void SelectOrthogonalDecisionDoorFromPalette()
        {
            Document document;
            OrthogonalDecisionPaletteControl control;
            if (!TryGetGuidedActionContext(
                OrthogonalDecisionGuideAction.SelectControlDoor,
                out document,
                out control))
            {
                return;
            }

            Editor editor = document.Editor;
            LayoutDrawingPlan suspendedPreview = SuspendGuidedPreview(
                document,
                control);
            try
            {
                TileLayout.Core.Models.AxisAlignedOrthogonalPolygon room =
                    control.Workflow.Input.Room;
                if (room == null)
                {
                    RestoreGuidedPreview(document, control, suspendedPreview);
                    EndGuidedAction(
                        control,
                        editor,
                        "请先在“房间与规则”页选择并验证房间。" );
                    return;
                }

                OrthogonalDoorOpeningProjectionResult projection;
                if (!TryPromptOrthogonalDoorOpening(
                    editor,
                    room,
                    control.Workflow.Input.OriginalRoom,
                    Math.Max(
                        control.Workflow.Input.BoundaryPointMatchTolerance,
                        GeometryTolerance.NearOrthogonalEndpointJoinTolerance),
                    out projection,
                    true))
                {
                    RestoreGuidedPreview(document, control, suspendedPreview);
                    EndGuidedAction(
                        control,
                        editor,
                        "已取消本次门洞选择；此前确认的门洞（如有）保持不变。" );
                    return;
                }

                if (!projection.IsValid)
                {
                    RestoreGuidedPreview(document, control, suspendedPreview);
                    EndGuidedAction(
                        control,
                        editor,
                        TileLayoutCommandText.FormatDoorProjectionFailure(
                            projection.Projection));
                    return;
                }

                control.ApplyAutomaticallyLocatedDoor(
                    projection.ControlRegion,
                    projection.Opening);
                RestoreGuidedPreview(document, control, suspendedPreview);
                EndGuidedAction(
                    control,
                    editor,
                    "门洞已通过完整房间外边界验证，邻接区域已自动识别；"
                        + "默认保持全房连续相位，未创建或修改任何对象。" );
            }
            catch (System.Exception exception)
            {
                RestoreGuidedPreview(document, control, suspendedPreview);
                EndGuidedAction(
                    control,
                    editor,
                    "门洞选择失败：" + exception.Message);
            }
        }

        [CommandMethod("TILEORTHOUIMAIN", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void SelectOrthogonalDecisionMainRegionFromPalette()
        {
            ExecuteGuidedRectangleSelection(
                OrthogonalDecisionGuideAction.SelectMainRegion,
                "主要铺贴区",
                (control, rectangle) => control.ApplyMainRegion(rectangle));
        }

        [CommandMethod("TILEORTHOUISECONDARY", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void SelectOrthogonalDecisionSecondaryRegionFromPalette()
        {
            ExecuteGuidedRectangleSelection(
                OrthogonalDecisionGuideAction.SelectSecondaryRegion,
                "相邻铺贴区",
                (control, rectangle) => control.ApplySecondaryRegion(rectangle));
        }

        [CommandMethod("TILEORTHOUIEDGE", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void SelectOrthogonalDecisionConnectionEdgeFromPalette()
        {
            Document document;
            OrthogonalDecisionPaletteControl control;
            if (!TryGetGuidedActionContext(
                OrthogonalDecisionGuideAction.SelectConnectionEdge,
                out document,
                out control))
            {
                return;
            }

            Editor editor = document.Editor;
            LayoutDrawingPlan suspendedPreview = SuspendGuidedPreview(
                document,
                control);
            try
            {
                CoreLineSegment3D edge;
                if (!TryPromptOrthogonalConnectionEdge(
                    editor,
                    control.Workflow.Input.Room.Elevation,
                    control.Workflow.DescribeExpectedConnectionEdge(),
                    out edge))
                {
                    RestoreGuidedPreview(document, control, suspendedPreview);
                    EndGuidedAction(
                        control,
                        editor,
                        "已取消本次接合边选择；此前确认的接合边（如有）保持不变。" );
                    return;
                }

                control.ApplyConnectionEdge(edge);
                EndGuidedAction(
                    control,
                    editor,
                    "两区接合边已从图面选择；排版方案已刷新，图纸没有变化。" );
            }
            catch (System.Exception exception)
            {
                RestoreGuidedPreview(document, control, suspendedPreview);
                EndGuidedAction(
                    control,
                    editor,
                    "两区接合边选择失败：" + exception.Message);
            }
        }

        [CommandMethod("TILEORTHOUIPREVIEW", CommandFlags.Modal | CommandFlags.NoHistory)]
        public void UpdateOrthogonalDecisionPreviewFromPalette()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            OrthogonalDecisionPreviewAction action;
            OrthogonalDecisionPaletteControl control;
            if (!OrthogonalDecisionPaletteHost.TryTakePendingPreviewAction(
                document,
                out action,
                out control))
            {
                if (document != null)
                {
                    document.Editor.WriteMessage(
                        "\n该内部命令只能由当前复杂房向导的预览按钮调用。" );
                }

                return;
            }

            try
            {
                if (action == OrthogonalDecisionPreviewAction.Clear)
                {
                    OrthogonalLayoutTransientPreview.Clear(document);
                    control.MarkPreviewCleared(
                        "图中的临时铺贴线已清除；当前方案和人工确认记录仍保留。" );
                    document.Editor.WriteMessage(
                        "\n临时铺贴图已清除；未创建或修改任何图层、实体或事务。" );
                    return;
                }

                LayoutDrawingPlan plan = control.Workflow.PreviewPlan;
                if (plan == null)
                {
                    control.MarkPreviewDisplayFailed("当前方案没有可显示的同源绘图计划");
                    return;
                }

                OrthogonalLayoutTransientPreview.Show(
                    document,
                    plan,
                    control.Workflow.ShowAllAssessedBoundaryTiles,
                    control.Workflow.ShowNeutralRegions,
                    control.Workflow.ShowWallCornerDiagnostics,
                    control.Workflow.SelectedDiagnosticTileId);
                control.MarkPreviewVisible();
                document.Editor.WriteMessage(
                    action == OrthogonalDecisionPreviewAction.Refresh
                        ? "\n临时铺贴图已按同一绘图计划刷新；DWG 零写入。"
                        : "\n临时铺贴图已显示；绿色为实际分格线，黄色为两区接合边，DWG 零写入。" );
            }
            catch (System.Exception exception)
            {
                try
                {
                    OrthogonalLayoutTransientPreview.Clear(document);
                }
                catch (System.Exception)
                {
                }

                control.MarkPreviewDisplayFailed(exception.Message);
                document.Editor.WriteMessage(
                    "\n临时预览失败：{0}；未写入图纸。",
                    exception.Message);
            }
        }

        [CommandMethod("TILEORTHOUIWRITE", CommandFlags.Modal)]
        public void WriteConfirmedOrthogonalLayoutFromPalette()
        {
            ExecutePendingFormalWriteback();
        }

        internal static void ExecutePendingFormalWriteback()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            OrthogonalDecisionPaletteControl control;
            if (!OrthogonalDecisionPaletteHost.TryTakePendingFormalWriteback(
                document,
                out control))
            {
                if (document != null)
                {
                    document.Editor.WriteMessage(
                        "\n该内部命令只能由当前复杂房向导的正式写回按钮调用。" );
                }

                return;
            }

            try
            {
                WriteConfirmedOrthogonalLayout(document, control);
            }
            finally
            {
                OrthogonalDecisionPaletteHost.FinishPendingFormalWriteback(
                    document,
                    control);
            }
        }

        [CommandMethod("TILEDOORRECT", CommandFlags.Modal)]
        public void CreateDoorControlledRectangularLayout()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            ExecuteDoorControlledRectangularLayout(document);
        }

        private static void ExecuteGuidedRoomSelection()
        {
            Document document;
            OrthogonalDecisionPaletteControl control;
            if (!TryGetGuidedActionContext(
                OrthogonalDecisionGuideAction.SelectRoom,
                out document,
                out control))
            {
                return;
            }

            Database database = document.Database;
            Editor editor = document.Editor;
            LayoutDrawingPlan suspendedPreview = SuspendGuidedPreview(
                document,
                control);
            try
            {
                if (!database.TileMode)
                {
                    RestoreGuidedPreview(document, control, suspendedPreview);
                    EndGuidedAction(
                        control,
                        editor,
                        "请切换到模型空间后再选择房间；未读取房间，未生成任何对象。" );
                    return;
                }

                if (database.Insunits != UnitsValue.Millimeters)
                {
                    EndGuidedAction(
                        control,
                        editor,
                        "当前图纸单位不是毫米，请先确认 INSUNITS；未读取房间。" );
                    return;
                }

                PromptSelectionResult selection = SelectGuidedBoundarySources(
                    editor);
                if (selection.Status != PromptStatus.OK)
                {
                    EndGuidedAction(
                        control,
                        editor,
                        "已取消本次房间选择；未创建或修改任何对象。" );
                    return;
                }

                ObjectId[] selectedIds = selection.Value.GetObjectIds();
                if (selectedIds.Length == 0)
                {
                    EndGuidedAction(
                        control,
                        editor,
                        "必须选择至少四条 LINE；本次选择不足，房间未载入。" );
                    return;
                }

                IReadOnlyCollection<CoreLineSegment3D> boundaryLines;
                using (Transaction transaction =
                    database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)transaction.GetObject(
                        database.BlockTableId,
                        OpenMode.ForRead);
                    boundaryLines = ReadGuidedBoundarySnapshots(
                        transaction,
                        selectedIds,
                        blockTable[BlockTableRecord.ModelSpace],
                        editor);
                }

                if (boundaryLines == null)
                {
                    EndGuidedAction(
                        control,
                        editor,
                        "边界读取失败；请只选择当前模型空间中的 LINE、直线型 LWPOLYLINE、二维或 3D POLYLINE；原始实体未修改。" );
                    return;
                }

                OrthogonalRoomValidationResult validation =
                    control.ApplyRoomBoundary(boundaryLines);
                if (!validation.IsValid)
                {
                    string normalizationNotice = control.Workflow
                        .BoundaryNormalizationNotice;
                    EndGuidedAction(
                        control,
                        editor,
                        string.IsNullOrWhiteSpace(normalizationNotice)
                            ? TileLayoutCommandText
                                .FormatOrthogonalValidationFailure(validation)
                            : normalizationNotice);
                    return;
                }

                EndGuidedAction(
                    control,
                    editor,
                    "房间边界已从图面载入并通过验证；请返回对话框确认项目规则。" );
            }
            catch (System.Exception exception)
            {
                EndGuidedAction(
                    control,
                    editor,
                    "房间选择失败：" + exception.Message);
            }
        }

        private static void ExecuteGuidedRectangleSelection(
            OrthogonalDecisionGuideAction action,
            string semanticName,
            Action<OrthogonalDecisionPaletteControl,
                TileLayout.Core.Models.AxisAlignedRectangle> apply)
        {
            Document document;
            OrthogonalDecisionPaletteControl control;
            if (!TryGetGuidedActionContext(
                action,
                out document,
                out control))
            {
                return;
            }

            Editor editor = document.Editor;
            LayoutDrawingPlan suspendedPreview = SuspendGuidedPreview(
                document,
                control);
            try
            {
                TileLayout.Core.Models.AxisAlignedRectangle rectangle;
                if (!TryPromptAxisAlignedRectangle(
                    editor,
                    semanticName,
                    control.Workflow.Input.Room.Elevation,
                    out rectangle))
                {
                    RestoreGuidedPreview(document, control, suspendedPreview);
                    EndGuidedAction(
                        control,
                        editor,
                        "已取消本次" + semanticName
                            + "选择；此前确认的值（如有）保持不变。" );
                    return;
                }

                apply(control, rectangle);
                EndGuidedAction(
                    control,
                    editor,
                    semanticName + "已从图面选择；排版方案已刷新，图纸没有变化。" );
            }
            catch (System.Exception exception)
            {
                RestoreGuidedPreview(document, control, suspendedPreview);
                EndGuidedAction(
                    control,
                    editor,
                    semanticName + "选择失败：" + exception.Message);
            }
        }

        private static bool TryGetGuidedActionContext(
            OrthogonalDecisionGuideAction expected,
            out Document document,
            out OrthogonalDecisionPaletteControl control)
        {
            document = Application.DocumentManager.MdiActiveDocument;
            if (!OrthogonalDecisionPaletteHost.TryGetPendingAction(
                document,
                expected,
                out control))
            {
                OrthogonalDecisionPaletteHost.RestoreAfterGuideAction();
                if (document != null)
                {
                    document.Editor.WriteMessage(
                        "\n该内部命令只能由当前复杂房向导按钮调用；"
                    + "请执行 TILEORTHOUI 并从浮动对话框操作。" );
                }

                return false;
            }

            return true;
        }

        private static void EndGuidedAction(
            OrthogonalDecisionPaletteControl control,
            Editor editor,
            string message)
        {
            control.EndAction(message);
            OrthogonalDecisionPaletteHost.RestoreAfterGuideAction();
            editor.WriteMessage("\n{0}", message);
        }

        private static LayoutDrawingPlan SuspendGuidedPreview(
            Document document,
            OrthogonalDecisionPaletteControl control)
        {
            if (!OrthogonalLayoutTransientPreview.IsVisible(document))
            {
                return null;
            }

            LayoutDrawingPlan plan = control.Workflow.PreviewPlan;
            OrthogonalLayoutTransientPreview.Clear(document);
            return plan;
        }

        private static void RestoreGuidedPreview(
            Document document,
            OrthogonalDecisionPaletteControl control,
            LayoutDrawingPlan suspendedPlan)
        {
            if (suspendedPlan == null
                || !ReferenceEquals(
                    suspendedPlan,
                    control.Workflow.PreviewPlan))
            {
                return;
            }

            OrthogonalLayoutTransientPreview.Show(
                document,
                suspendedPlan,
                control.Workflow.ShowAllAssessedBoundaryTiles,
                control.Workflow.ShowNeutralRegions,
                control.Workflow.ShowWallCornerDiagnostics,
                control.Workflow.SelectedDiagnosticTileId);
            control.MarkPreviewVisible();
        }

        private static void ExecuteDoorControlledRectangularLayout(
            Document document)
        {
            Database database = document.Database;
            Editor editor = document.Editor;

            if (!database.TileMode)
            {
                editor.WriteMessage(
                    "\n请切换到模型空间后再执行 TILEDOORRECT。未生成任何对象。");
                return;
            }

            if (database.Insunits != UnitsValue.Millimeters)
            {
                editor.WriteMessage(
                    "\n当前图纸单位不是毫米（INSUNITS={0}），"
                        + "TILEDOORRECT 已停止。未生成任何对象。",
                    database.Insunits);
                return;
            }

            PromptSelectionResult selectionResult = SelectBoundaryLines(editor);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage(
                    "\n未完成四条矩形房间 LINE 的选择，未生成任何对象。");
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

            TileLayout.Core.Models.AxisAlignedRectangle room;
            if (!TryReadValidatedRectangle(
                database,
                selectedIds,
                editor,
                out room))
            {
                return;
            }

            double tileWidth;
            double tileHeight;
            if (!TryPromptEngineeringTileDimensions(
                editor,
                out tileWidth,
                out tileHeight))
            {
                editor.WriteMessage(
                    "\nTILEDOORRECT 已取消；原边界未修改，未生成任何对象。");
                return;
            }

            while (true)
            {
                DoorOpeningProjectionResult projection;
                if (!TryPromptDoorOpening(
                    database,
                    editor,
                    room,
                    out projection))
                {
                    ClearDoorLayoutPreview(editor);
                    editor.WriteMessage(
                        "\nTILEDOORRECT 已取消；原边界和门洞输入未修改，"
                            + "未生成任何对象。");
                    return;
                }

                if (!projection.IsValid)
                {
                    editor.WriteMessage(
                        "\n{0}",
                        TileLayoutCommandText.FormatDoorProjectionFailure(
                            projection));
                    continue;
                }

                EngineeringRectangularLayoutResult layout;
                try
                {
                    layout = EngineeringRectangularLayoutCalculator.Calculate(
                        room,
                        new EngineeringRectangularLayoutParameters(
                            tileWidth,
                            tileHeight,
                            projection.Opening));
                }
                catch (TileLayoutLimitExceededException exception)
                {
                    editor.WriteMessage(
                        "\n{0}",
                        TileLayoutCommandText.FormatLimitExceeded(
                            exception,
                            "TILEDOORRECT"));
                    return;
                }
                catch (ArgumentException exception)
                {
                    editor.WriteMessage(
                        "\n门洞控制计算失败：{0} 未生成任何对象。",
                        exception.Message);
                    return;
                }

                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatDoorOpeningSummary(
                        room,
                        projection.Opening));

                if (!layout.IsSuccessful)
                {
                    editor.WriteMessage(
                        "\n{0}",
                        TileLayoutCommandText.FormatEngineeringFailure(layout));
                    if (PromptFailedCandidateAction(editor)
                        == DoorLayoutInteractionAction.Reselect)
                    {
                        continue;
                    }

                    editor.WriteMessage(
                        "\nTILEDOORRECT 已取消，未生成任何对象。");
                    return;
                }

                var previewSession = new DoorLayoutPreviewSession(layout);
                bool reselect = false;
                while (previewSession.State
                    == DoorLayoutInteractionState.Previewing)
                {
                    editor.WriteMessage(
                        "\n{0}",
                        TileLayoutCommandText.FormatEngineeringCandidateSummary(
                            previewSession.SelectedCandidate));
                    DrawDoorLayoutPreview(
                        editor,
                        previewSession.SelectedCandidate,
                        projection);

                    DoorLayoutInteractionAction action;
                    if (!TryPromptDoorPreviewAction(editor, out action))
                    {
                        action = DoorLayoutInteractionAction.Cancel;
                    }

                    ClearDoorLayoutPreview(editor);
                    if (action == DoorLayoutInteractionAction.Flip
                        && !previewSession.CanFlip)
                    {
                        editor.WriteMessage(
                            "\n当前结果没有 DR2 提供的居中等价候选，"
                                + "只能对居中门洞的等价版式执行翻转。");
                        continue;
                    }

                    previewSession.Apply(action);
                    if (previewSession.State
                        == DoorLayoutInteractionState.ReselectRequested)
                    {
                        reselect = true;
                    }
                }

                if (reselect)
                {
                    editor.WriteMessage(
                        "\n请重新指定门洞：可直接点取两点或显式选择对象(O)；"
                            + "房间边界和砖规格保持不变。");
                    continue;
                }

                if (previewSession.State
                    == DoorLayoutInteractionState.Cancelled)
                {
                    editor.WriteMessage(
                        "\nTILEDOORRECT 已取消；接受前未创建或修改正式图层及实体。");
                    return;
                }

                if (!previewSession.IsWriteAuthorized)
                {
                    throw new InvalidOperationException(
                        "Door layout write-back requires an accepted preview.");
                }

                WriteAcceptedDoorLayout(
                    document,
                    previewSession.SelectedCandidate);
                return;
            }
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

        private static bool TryReadValidatedRectangle(
            Database database,
            ObjectId[] selectedIds,
            Editor editor,
            out TileLayout.Core.Models.AxisAlignedRectangle room)
        {
            room = null;
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
                    return false;
                }

                RectangleValidationResult validation =
                    RectangleValidator.Validate(boundaryLines);
                if (!validation.IsValid)
                {
                    editor.WriteMessage(
                        "\n{0}",
                        TileLayoutCommandText.FormatValidationFailure(validation));
                    return false;
                }

                room = validation.Rectangle;
                return true;
            }
        }

        private static bool TryPromptEngineeringTileDimensions(
            Editor editor,
            out double tileWidth,
            out double tileHeight)
        {
            tileWidth = 0.0;
            tileHeight = 0.0;
            if (!TryPromptTileDimension(
                editor,
                "砖宽",
                "沿 WCS X",
                TileLayoutRules.TileWidth,
                out tileWidth))
            {
                return false;
            }

            return TryPromptTileDimension(
                editor,
                "砖高",
                "沿 WCS Y",
                TileLayoutRules.TileHeight,
                out tileHeight);
        }

        private static bool TryPromptAxisAlignedRectangle(
            Editor editor,
            string semanticName,
            double elevation,
            out TileLayout.Core.Models.AxisAlignedRectangle rectangle)
        {
            rectangle = null;
            var firstOptions = new PromptPointOptions(
                "\n请在图中捕捉" + semanticName + "矩形的第一个对角点：")
            {
                AllowNone = false
            };
            PromptPointResult firstResult = editor.GetPoint(firstOptions);
            if (firstResult.Status != PromptStatus.OK)
            {
                return false;
            }

            var secondOptions = new PromptPointOptions(
                "\n请在图中捕捉" + semanticName + "矩形的另一个对角点：")
            {
                AllowNone = false,
                BasePoint = firstResult.Value,
                UseBasePoint = true,
                UseDashedLine = true
            };
            PromptPointResult secondResult = editor.GetPoint(secondOptions);
            if (secondResult.Status != PromptStatus.OK)
            {
                return false;
            }

            Point3d first = firstResult.Value;
            Point3d second = secondResult.Value;
            if (Math.Abs(first.Z - elevation) > GeometryTolerance.Coordinate
                || Math.Abs(second.Z - elevation)
                    > GeometryTolerance.Coordinate)
            {
                editor.WriteMessage(
                    "\n{0}必须与房间边界位于同一 WCS 高程。",
                    semanticName);
                return false;
            }

            double west = Math.Min(first.X, second.X);
            double east = Math.Max(first.X, second.X);
            double south = Math.Min(first.Y, second.Y);
            double north = Math.Max(first.Y, second.Y);
            try
            {
                rectangle = new TileLayout.Core.Models.AxisAlignedRectangle(
                    west,
                    east,
                    south,
                    north,
                    elevation);
                return true;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                editor.WriteMessage(
                    "\n{0}无效：{1}",
                    semanticName,
                    exception.Message);
                return false;
            }
        }

        private static bool TryPromptOrthogonalConnectionEdge(
            Editor editor,
            double elevation,
            string expectedEdgeDescription,
            out CoreLineSegment3D edge)
        {
            edge = default(CoreLineSegment3D);
            var firstOptions = new PromptPointOptions(
                "\n请捕捉主要铺贴区与相邻铺贴区共同接触边的第一个端点"
                    + "（应选择" + expectedEdgeDescription
                    + "，不要选择房间外轮廓上的短折边）：")
            {
                AllowNone = false
            };
            PromptPointResult firstResult = editor.GetPoint(firstOptions);
            if (firstResult.Status != PromptStatus.OK)
            {
                return false;
            }

            var secondOptions = new PromptPointOptions(
                "\n请捕捉主要铺贴区与相邻铺贴区共同接触边的第二个端点"
                    + "（需要点取两区实际相接的整段边）：")
            {
                AllowNone = false,
                BasePoint = firstResult.Value,
                UseBasePoint = true,
                UseDashedLine = true
            };
            PromptPointResult secondResult = editor.GetPoint(secondOptions);
            if (secondResult.Status != PromptStatus.OK)
            {
                return false;
            }

            Point3d first = firstResult.Value;
            Point3d second = secondResult.Value;
            bool sameElevation = Math.Abs(first.Z - elevation)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(second.Z - elevation)
                    <= GeometryTolerance.Coordinate;
            bool vertical = Math.Abs(first.X - second.X)
                <= GeometryTolerance.Coordinate;
            bool horizontal = Math.Abs(first.Y - second.Y)
                <= GeometryTolerance.Coordinate;
            if (!sameElevation || vertical == horizontal)
            {
                editor.WriteMessage(
                    "\n两区接合边必须是与房间同高程的非退化 WCS 水平或竖直线段。");
                return false;
            }

            edge = new CoreLineSegment3D(
                new CorePoint3D(first.X, first.Y, elevation),
                new CorePoint3D(second.X, second.Y, elevation));
            return true;
        }

        private static bool TryPromptDoorOpening(
            Database database,
            Editor editor,
            TileLayout.Core.Models.AxisAlignedRectangle room,
            out DoorOpeningProjectionResult projection,
            bool guidedPalette = false)
        {
            projection = null;
            var inputSession = new DoorOpeningInputSession();
            while (inputSession.State
                == DoorOpeningInputState.AwaitingFirstPoint)
            {
                var firstOptions = new PromptPointOptions(
                    guidedPalette
                        ? "\n请在图中捕捉门洞一侧的边缘点："
                        : "\n请选择门洞第一个边缘点或 [对象(O)]"
                            + "（默认两点，WCS，需在矩形墙段公差内）：")
                {
                    AllowNone = false
                };
                if (!guidedPalette)
                {
                    firstOptions.Keywords.Add("O");
                }
                PromptPointResult firstResult = editor.GetPoint(firstOptions);
                if (firstResult.Status == PromptStatus.Keyword
                    && string.Equals(
                        firstResult.StringResult,
                        "O",
                        StringComparison.OrdinalIgnoreCase))
                {
                    DoorObjectRecognitionResult recognition;
                    if (!TryRecognizeDoorObject(
                        database,
                        editor,
                        room,
                        out recognition))
                    {
                        inputSession.Cancel();
                        return false;
                    }

                    if (!recognition.IsHigh)
                    {
                        inputSession.RejectRecognizedObject();
                        editor.WriteMessage(
                            "\n{0}",
                            TileLayoutCommandText
                                .FormatDoorObjectRecognitionFailure(
                                    recognition));
                        continue;
                    }

                    inputSession.AcceptRecognizedObject();
                    projection = recognition.Projection;
                    editor.WriteMessage(
                        "\n{0}",
                        TileLayoutCommandText
                            .FormatDoorObjectRecognitionSuccess(recognition));
                    return true;
                }

                if (firstResult.Status != PromptStatus.OK)
                {
                    inputSession.Cancel();
                    return false;
                }

                inputSession.AcceptFirstPoint();
                var secondOptions = new PromptPointOptions(
                    guidedPalette
                        ? "\n请在图中捕捉门洞另一侧的边缘点（需位于同一面墙）："
                        : "\n请选择门洞第二个边缘点（必须与第一点位于同一面墙）：")
                {
                    AllowNone = false,
                    BasePoint = firstResult.Value,
                    UseBasePoint = true,
                    UseDashedLine = true
                };
                PromptPointResult secondResult = editor.GetPoint(secondOptions);
                if (secondResult.Status != PromptStatus.OK)
                {
                    inputSession.Cancel();
                    return false;
                }

                inputSession.AcceptSecondPoint();
                Point3d first = firstResult.Value;
                Point3d second = secondResult.Value;
                projection = DoorOpeningPointAdapter.ProjectToRoomWall(
                    room,
                    new CorePoint3D(first.X, first.Y, first.Z),
                    new CorePoint3D(second.X, second.Y, second.Z));
                return true;
            }

            return false;
        }

        private static bool TryPromptOrthogonalDoorOpening(
            Editor editor,
            TileLayout.Core.Models.AxisAlignedOrthogonalPolygon room,
            TileLayout.Core.Models.AxisAlignedOrthogonalPolygon sourceRoom,
            double boundaryPointMatchTolerance,
            out OrthogonalDoorOpeningProjectionResult projection,
            bool guidedDialog)
        {
            projection = null;
            var firstOptions = new PromptPointOptions(
                guidedDialog
                    ? "\n请在完成面外边界上捕捉门洞一侧的边缘点（已设置抹灰时也可捕捉原始外墙）："
                    : "\n请选择门洞第一个边缘点：")
            {
                AllowNone = false
            };
            PromptPointResult firstResult = editor.GetPoint(firstOptions);
            if (firstResult.Status != PromptStatus.OK)
            {
                return false;
            }

            var secondOptions = new PromptPointOptions(
                guidedDialog
                    ? "\n请捕捉同一段外墙上的门洞另一侧边缘点："
                    : "\n请选择同一段外墙上的门洞第二个边缘点：")
            {
                AllowNone = false,
                BasePoint = firstResult.Value,
                UseBasePoint = true,
                UseDashedLine = true
            };
            PromptPointResult secondResult = editor.GetPoint(secondOptions);
            if (secondResult.Status != PromptStatus.OK)
            {
                return false;
            }

            Point3d first = firstResult.Value;
            Point3d second = secondResult.Value;
            projection = DoorOpeningPointAdapter.ProjectToOrthogonalRoomWall(
                room,
                sourceRoom,
                new CorePoint3D(first.X, first.Y, first.Z),
                new CorePoint3D(second.X, second.Y, second.Z),
                boundaryPointMatchTolerance);
            return true;
        }

        private static bool TryRecognizeDoorObject(
            Database database,
            Editor editor,
            TileLayout.Core.Models.AxisAlignedRectangle room,
            out DoorObjectRecognitionResult recognition)
        {
            recognition = null;
            var options = new PromptEntityOptions(
                TileLayoutCommandText.DoorObjectSelectionPrompt);
            PromptEntityResult selection = editor.GetEntity(options);
            if (selection.Status != PromptStatus.OK)
            {
                return false;
            }

            DoorBlockGeometryReadResult read =
                DoorBlockGeometryReader.Read(
                    database,
                    selection.ObjectId);
            if (!read.IsSuccessful)
            {
                recognition = read.Rejection;
                return true;
            }

            recognition = DoorObjectRecognitionCoordinator.Recognize(
                room,
                read.Lines,
                read.Arcs,
                read.Route.Value);
            return true;
        }

        private static bool TryPromptDoorPreviewAction(
            Editor editor,
            out DoorLayoutInteractionAction action)
        {
            var options = new PromptKeywordOptions(
                "\n预览操作 [接受(A)/翻转(F)/重选(R)/取消(C)] <接受>：")
            {
                AllowNone = true
            };
            options.Keywords.Add("A");
            options.Keywords.Add("F");
            options.Keywords.Add("R");
            options.Keywords.Add("C");
            options.Keywords.Default = "A";

            PromptResult result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK
                && result.Status != PromptStatus.None)
            {
                action = DoorLayoutInteractionAction.Cancel;
                return false;
            }

            string keyword = result.Status == PromptStatus.None
                ? "A"
                : result.StringResult;
            switch (keyword.ToUpperInvariant())
            {
                case "A":
                    action = DoorLayoutInteractionAction.Accept;
                    return true;
                case "F":
                    action = DoorLayoutInteractionAction.Flip;
                    return true;
                case "R":
                    action = DoorLayoutInteractionAction.Reselect;
                    return true;
                case "C":
                    action = DoorLayoutInteractionAction.Cancel;
                    return true;
                default:
                    throw new InvalidOperationException(
                        "AutoCAD returned an unsupported door-preview keyword.");
            }
        }

        private static DoorLayoutInteractionAction PromptFailedCandidateAction(
            Editor editor)
        {
            var options = new PromptKeywordOptions(
                "\n当前门洞没有可预览候选 [重选(R)/取消(C)] <重选>：")
            {
                AllowNone = true
            };
            options.Keywords.Add("R");
            options.Keywords.Add("C");
            options.Keywords.Default = "R";

            PromptResult result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK
                && result.Status != PromptStatus.None)
            {
                return DoorLayoutInteractionAction.Cancel;
            }

            string keyword = result.Status == PromptStatus.None
                ? "R"
                : result.StringResult;
            return string.Equals(
                keyword,
                "R",
                StringComparison.OrdinalIgnoreCase)
                ? DoorLayoutInteractionAction.Reselect
                : DoorLayoutInteractionAction.Cancel;
        }

        private static void DrawDoorLayoutPreview(
            Editor editor,
            LayoutCandidate candidate,
            DoorOpeningProjectionResult projection)
        {
            ClearDoorLayoutPreview(editor);
            foreach (CoreLineSegment3D divisionLine in candidate.DivisionLines)
            {
                editor.DrawVector(
                    new Point3d(
                        divisionLine.Start.X,
                        divisionLine.Start.Y,
                        divisionLine.Start.Z),
                    new Point3d(
                        divisionLine.End.X,
                        divisionLine.End.Y,
                        divisionLine.End.Z),
                    3,
                    false);
            }

            editor.DrawVector(
                new Point3d(
                    projection.FirstProjectedPoint.X,
                    projection.FirstProjectedPoint.Y,
                    projection.FirstProjectedPoint.Z),
                new Point3d(
                    projection.SecondProjectedPoint.X,
                    projection.SecondProjectedPoint.Y,
                    projection.SecondProjectedPoint.Z),
                2,
                true);
        }

        private static void ClearDoorLayoutPreview(Editor editor)
        {
            editor.Regen();
        }

        private static void WriteAcceptedDoorLayout(
            Document document,
            LayoutCandidate candidate)
        {
            Editor editor = document.Editor;
            Database database = document.Database;
            int maximum = TileLayoutRules.MaximumParameterizedDivisionLineCount;
            if (candidate.DivisionLines.Count > maximum)
            {
                editor.WriteMessage(
                    "\n接受的候选包含 {0} 条内部分格线，超过 TILEDOORRECT "
                        + "单次上限 {1} 条；未创建或修改输出图层。",
                    candidate.DivisionLines.Count,
                    maximum);
                return;
            }

            try
            {
                using (Transaction transaction =
                    database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable =
                        (BlockTable)transaction.GetObject(
                            database.BlockTableId,
                            OpenMode.ForRead);
                    ObjectId modelSpaceId =
                        blockTable[BlockTableRecord.ModelSpace];
                    ObjectId layoutLayerId = EnsureLayoutLayer(
                        transaction,
                        database,
                        DoorRectangularLayoutLayerName);
                    WriteDivisionLines(
                        transaction,
                        modelSpaceId,
                        layoutLayerId,
                        candidate.DivisionLines);
                    transaction.Commit();
                }

                editor.WriteMessage(
                    "\n{0}",
                    TileLayoutCommandText.FormatEngineeringWriteSuccess(
                        candidate,
                        DoorRectangularLayoutLayerName));
                editor.WriteMessage(
                    "\n原四条边界 LINE 未修改，插件未保存图纸；"
                        + "可用一次 U 或 UNDO 撤销本次接受后新增。");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                editor.WriteMessage(
                    "\n生成失败（AutoCAD 状态：{0}），"
                        + "单个写事务已回滚，未保留部分分格线。",
                    exception.ErrorStatus);
            }
            catch (System.Exception)
            {
                editor.WriteMessage(
                    "\n生成失败，单个写事务已回滚，未保留部分分格线。"
                        + "请保留测试副本并记录操作步骤。");
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

        private static PromptSelectionResult SelectGuidedBoundarySources(
            Editor editor)
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding =
                    "\n请选择一间房的边界：四条以上 LINE，或一个闭合（或首尾误差不超过 "
                    + GeometryTolerance.NearOrthogonalEndpointJoinTolerance
                    + " mm）的直线型 LWPOLYLINE/二维/3D POLYLINE；禁止混合和多环：",
                RejectObjectsFromNonCurrentSpace = true
            };
            var filter = new SelectionFilter(
                new[]
                {
                    new TypedValue(
                        (int)DxfCode.Start,
                        "LINE,LWPOLYLINE,POLYLINE")
                });
            return editor.GetSelection(options, filter);
        }

        private static IReadOnlyCollection<CoreLineSegment3D>
            ReadGuidedBoundarySnapshots(
                Transaction transaction,
                ObjectId[] selectedIds,
                ObjectId modelSpaceId,
                Editor editor)
        {
            var lineSnapshots = new List<CoreLineSegment3D>();
            List<Point3d> polylineVertices = null;
            bool sawPolyline = false;
            bool allowNearEndpointClosure = false;

            foreach (ObjectId selectedId in selectedIds)
            {
                Entity entity = transaction.GetObject(
                    selectedId,
                    OpenMode.ForRead,
                    false) as Entity;
                if (entity == null)
                {
                    editor.WriteMessage(
                        "\n边界实体读取失败；未读取任何对象。 ");
                    return null;
                }

                if (entity.OwnerId != modelSpaceId)
                {
                    editor.WriteMessage(
                        "\n边界实体必须位于当前模型空间；请不要选择块内、外部参照或布局空间中的多段线。 ");
                    return null;
                }

                Polyline lightweight = entity as Polyline;
                Polyline2d legacy = entity as Polyline2d;
                Polyline3d spatial = entity as Polyline3d;
                if (lightweight != null
                    || legacy != null
                    || spatial != null)
                {
                    if (sawPolyline || lineSnapshots.Count > 0
                        || selectedIds.Length != 1)
                    {
                        editor.WriteMessage(
                            "\n一间房只能选择一个闭合多段线，不能与 LINE 混合或选择多个环。 ");
                        return null;
                    }

                    sawPolyline = true;
                    if (!TryReadPolylineVertices(
                        transaction,
                        lightweight,
                        legacy,
                        spatial,
                        out polylineVertices,
                        out allowNearEndpointClosure,
                        editor))
                    {
                        return null;
                    }

                    continue;
                }

                Line line = entity as Line;
                if (line == null || sawPolyline)
                {
                    editor.WriteMessage(
                        "\n只接受 LINE、闭合或首尾误差不超过 "
                        + GeometryTolerance.NearOrthogonalEndpointJoinTolerance
                        + " mm 的直线型 LWPOLYLINE/二维或 3D POLYLINE；"
                        + "禁止混合输入。 ");
                    return null;
                }

                Point3d start = line.StartPoint;
                Point3d end = line.EndPoint;
                lineSnapshots.Add(
                    new CoreLineSegment3D(
                        new CorePoint3D(start.X, start.Y, start.Z),
                        new CorePoint3D(end.X, end.Y, end.Z)));
            }

            if (sawPolyline)
            {
                return BuildPolylineSegments(
                    polylineVertices,
                    allowNearEndpointClosure);
            }

            return lineSnapshots.Count >= 4 ? lineSnapshots : null;
        }

        private static bool TryReadPolylineVertices(
            Transaction transaction,
            Polyline lightweight,
            Polyline2d legacy,
            Polyline3d spatial,
            out List<Point3d> vertices,
            out bool allowNearEndpointClosure,
            Editor editor)
        {
            vertices = new List<Point3d>();
            allowNearEndpointClosure = false;
            if (lightweight != null)
            {
                for (int index = 0;
                    index < lightweight.NumberOfVertices;
                    index++)
                {
                    if (Math.Abs(lightweight.GetBulgeAt(index))
                        > GeometryTolerance.Coordinate)
                    {
                        editor.WriteMessage(
                            "\n首期不接受带 bulge 或圆弧的多段线；原始实体未修改。 ");
                        return false;
                    }

                    vertices.Add(lightweight.GetPoint3dAt(index));
                }

                if (!lightweight.Closed)
                {
                    if (!HasPermittedEndpointClosure(vertices))
                    {
                        editor.WriteMessage(
                            "\nLWPOLYLINE 必须闭合，或首尾顶点间距不超过 "
                            + GeometryTolerance.NearOrthogonalEndpointJoinTolerance
                            + " mm；当前首尾间距约 "
                            + EndpointGap(vertices).ToString("0.###")
                            + " mm。请使用 PL 的“闭合(C)”或修正首尾间隙，原始实体未修改。 ");
                        return false;
                    }

                    allowNearEndpointClosure = !SamePoint(
                        vertices[0],
                        vertices[vertices.Count - 1]);
                }
            }
            else if (legacy != null)
            {
                foreach (ObjectId vertexId in legacy)
                {
                    Vertex2d vertex = transaction.GetObject(
                        vertexId,
                        OpenMode.ForRead,
                        false) as Vertex2d;
                    if (vertex == null)
                    {
                        editor.WriteMessage(
                            "\n二维 POLYLINE 顶点读取失败；原始实体未修改。 ");
                        return false;
                    }

                    if (Math.Abs(vertex.Bulge)
                        > GeometryTolerance.Coordinate)
                    {
                        editor.WriteMessage(
                            "\n首期不接受带 bulge 或圆弧的多段线；原始实体未修改。 ");
                        return false;
                    }

                    vertices.Add(vertex.Position);
                }

                if (!legacy.Closed)
                {
                    if (!HasPermittedEndpointClosure(vertices))
                    {
                        editor.WriteMessage(
                            "\n二维 POLYLINE 必须闭合，或首尾顶点间距不超过 "
                            + GeometryTolerance.NearOrthogonalEndpointJoinTolerance
                            + " mm；当前首尾间距约 "
                            + EndpointGap(vertices).ToString("0.###")
                            + " mm。请使用 PEDIT 的“闭合”或修正首尾间隙，原始实体未修改。 ");
                        return false;
                    }

                    allowNearEndpointClosure = !SamePoint(
                        vertices[0],
                        vertices[vertices.Count - 1]);
                }
            }
            else if (spatial != null)
            {
                if (spatial.PolyType != Poly3dType.SimplePoly)
                {
                    editor.WriteMessage(
                        "\n3D POLYLINE 必须是直线型，当前为 {0}；"
                        + "请先转换为直线型多段线或 LINE，原始实体未修改。 ",
                        spatial.PolyType);
                    return false;
                }

                foreach (ObjectId vertexId in spatial)
                {
                    PolylineVertex3d vertex = transaction.GetObject(
                        vertexId,
                        OpenMode.ForRead,
                        false) as PolylineVertex3d;
                    if (vertex == null)
                    {
                        editor.WriteMessage(
                            "\n3D POLYLINE 顶点读取失败；原始实体未修改。 ");
                        return false;
                    }

                    vertices.Add(vertex.Position);
                }

                if (!spatial.Closed)
                {
                    if (!HasPermittedEndpointClosure(vertices))
                    {
                        editor.WriteMessage(
                            "\n3D POLYLINE 必须闭合，或首尾顶点间距不超过 "
                            + GeometryTolerance.NearOrthogonalEndpointJoinTolerance
                            + " mm；当前首尾间距约 "
                            + EndpointGap(vertices).ToString("0.###")
                            + " mm。请修正首尾间隙，原始实体未修改。 ");
                        return false;
                    }

                    allowNearEndpointClosure = !SamePoint(
                        vertices[0],
                        vertices[vertices.Count - 1]);
                }
            }

            RemoveDeterministicDuplicateVertices(vertices);
            if (vertices.Count < 4)
            {
                editor.WriteMessage(
                    "\n闭合多段线至少需要四个确定性合并后的顶点；原始实体未修改。 ");
                return false;
            }

            return true;
        }

        private static bool HasPermittedEndpointClosure(
            IList<Point3d> vertices)
        {
            return vertices != null
                && vertices.Count > 1
                && (SamePoint(vertices[0], vertices[vertices.Count - 1])
                    || NearEndpoint(
                        vertices[0],
                        vertices[vertices.Count - 1]));
        }

        private static double EndpointGap(IList<Point3d> vertices)
        {
            if (vertices == null || vertices.Count < 2)
            {
                return double.NaN;
            }

            double deltaX = vertices[0].X
                - vertices[vertices.Count - 1].X;
            double deltaY = vertices[0].Y
                - vertices[vertices.Count - 1].Y;
            double deltaZ = vertices[0].Z
                - vertices[vertices.Count - 1].Z;
            return Math.Sqrt(
                (deltaX * deltaX)
                + (deltaY * deltaY)
                + (deltaZ * deltaZ));
        }

        private static IReadOnlyCollection<CoreLineSegment3D>
            BuildPolylineSegments(
                IList<Point3d> vertices,
                bool allowNearEndpointClosure)
        {
            var coreVertices = new List<CorePoint3D>(vertices.Count);
            for (int index = 0; index < vertices.Count; index++)
            {
                Point3d point = vertices[index];
                coreVertices.Add(
                    new CorePoint3D(point.X, point.Y, point.Z));
            }

            return GuidedBoundaryPolylineConverter.BuildSegments(
                coreVertices,
                allowNearEndpointClosure);
        }

        private static void RemoveDeterministicDuplicateVertices(
            IList<Point3d> vertices)
        {
            if (vertices.Count < 2)
            {
                return;
            }

            for (int index = vertices.Count - 1; index > 0; index--)
            {
                if (SamePoint(
                    vertices[index - 1],
                    vertices[index]))
                {
                    vertices.RemoveAt(index);
                }
            }

            while (vertices.Count > 1
                && SamePoint(vertices[0], vertices[vertices.Count - 1]))
            {
                vertices.RemoveAt(vertices.Count - 1);
            }
        }

        private static bool SamePoint(Point3d first, Point3d second)
        {
            return Math.Abs(first.X - second.X)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Y - second.Y)
                    <= GeometryTolerance.Coordinate
                && Math.Abs(first.Z - second.Z)
                    <= GeometryTolerance.Coordinate;
        }

        private static bool NearEndpoint(Point3d first, Point3d second)
        {
            if (Math.Abs(first.Z - second.Z)
                > GeometryTolerance.Coordinate)
            {
                return false;
            }

            double deltaX = first.X - second.X;
            double deltaY = first.Y - second.Y;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY))
                <= GeometryTolerance.NearOrthogonalEndpointJoinTolerance;
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

        private static void WriteConfirmedOrthogonalLayout(
            Document document,
            OrthogonalDecisionPaletteControl control)
        {
            Editor editor = document.Editor;
            Database database = document.Database;
            IReadOnlyList<LayoutDrawingLine> formalLines;
            string rejectionReason;
            if (!control.Workflow.TryGetAuthorizedFormalLines(
                out formalLines,
                out rejectionReason))
            {
                control.MarkFormalWritebackFailed(rejectionReason);
                editor.WriteMessage(
                    "\n正式写回已拒绝：{0}；当前预览仍保留，需重新点击确认。",
                    rejectionReason);
                return;
            }

            IReadOnlyList<LayoutDrawingDimension> formalDimensions;
            if (!control.Workflow.TryGetAuthorizedFormalDimensions(
                out formalDimensions,
                out rejectionReason))
            {
                control.MarkFormalWritebackFailed(rejectionReason);
                editor.WriteMessage(
                    "\n正式尺寸标注写回已拒绝：{0}；当前预览仍保留，需重新点击确认。",
                    rejectionReason);
                return;
            }

            LayoutDrawingStartPoint formalStartPoint;
            if (!control.Workflow.TryGetAuthorizedFormalStartPoint(
                out formalStartPoint,
                out rejectionReason))
            {
                control.MarkFormalWritebackFailed(rejectionReason);
                editor.WriteMessage(
                    "\n正式起铺点写回已拒绝：{0}；当前预览仍保留，需重新点击确认。",
                    rejectionReason);
                return;
            }

            if (!database.TileMode)
            {
                const string message = "请切换到模型空间后再正式写回；图纸没有变化。";
                control.MarkFormalWritebackFailed(message);
                editor.WriteMessage("\n正式写回已拒绝：{0}", message);
                return;
            }

            try
            {
                using (Transaction transaction =
                    database.TransactionManager.StartTransaction())
                {
                    BlockTable blockTable = (BlockTable)transaction.GetObject(
                        database.BlockTableId,
                        OpenMode.ForRead);
                    ObjectId modelSpaceId =
                        blockTable[BlockTableRecord.ModelSpace];
                    ObjectId layoutLayerId = EnsureConfirmedLayoutLayer(
                        transaction,
                        database);
                    ObjectId dimensionLayerId = ObjectId.Null;
                    if (formalDimensions.Count > 0)
                    {
                        dimensionLayerId = EnsureDimensionLayer(
                            transaction,
                            database);
                    }
                    ObjectId startPointLayerId = ObjectId.Null;
                    if (formalStartPoint != null)
                    {
                        startPointLayerId = EnsureStartPointLayer(
                            transaction,
                            database);
                    }
                    EnsureRoomRangeIsNotAlreadyWrittenInModelSpace(
                        transaction,
                        modelSpaceId,
                        layoutLayerId,
                        control.Workflow.PreviewPlan,
                        formalLines);
                    if (formalDimensions.Count > 0)
                    {
                        EnsureRoomRangeIsNotAlreadyWrittenInModelSpace(
                            transaction,
                            modelSpaceId,
                            dimensionLayerId,
                            control.Workflow.PreviewPlan,
                            new List<LayoutDrawingLine>());
                    }
                    if (formalStartPoint != null)
                    {
                        EnsureRoomRangeIsNotAlreadyWrittenInModelSpace(
                            transaction,
                            modelSpaceId,
                            startPointLayerId,
                            control.Workflow.PreviewPlan,
                            new List<LayoutDrawingLine>());
                    }
                    ObjectId dimensionStyleId = ObjectId.Null;
                    if (formalDimensions.Count > 0)
                    {
                        dimensionStyleId = OrthogonalLayoutDimensionStyle.Ensure(
                            database,
                            transaction);
                    }
                    EnsureRoomRangeMetadataApplication(
                        transaction,
                        database);
                    int writtenLineCount = WriteFormalDrawingLines(
                        transaction,
                        database,
                        modelSpaceId,
                        layoutLayerId,
                        control.Workflow.PreviewPlan,
                        formalLines);
                    int writtenDimensionCount = WriteFormalDimensions(
                        transaction,
                        database,
                        modelSpaceId,
                        dimensionLayerId,
                        control.Workflow.PreviewPlan,
                        formalDimensions,
                        dimensionStyleId);
                    int writtenStartPointCount = WriteFormalStartPoint(
                        transaction,
                        database,
                        modelSpaceId,
                        startPointLayerId,
                        control.Workflow.PreviewPlan,
                        formalStartPoint);
                    if (writtenLineCount != formalLines.Count
                        || writtenDimensionCount != formalDimensions.Count
                        || writtenStartPointCount
                            != (formalStartPoint == null
                                ? 0
                                : OrthogonalLayoutStartPointEntityFactory
                                    .ExpectedEntityCount))
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                System.Globalization.CultureInfo.CurrentCulture,
                                "正式写回事务已创建 {0} 条线、{1} 个尺寸标注和 {2} 个起铺点对象，预期 {3} 条线、{4} 个尺寸标注和 {5} 个起铺点对象。",
                                writtenLineCount,
                                writtenDimensionCount,
                                writtenStartPointCount,
                                formalLines.Count,
                                formalDimensions.Count,
                                formalStartPoint == null
                                    ? 0
                                    : OrthogonalLayoutStartPointEntityFactory
                                        .ExpectedEntityCount));
                    }
                    transaction.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception exception)
            {
                string message = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    "AutoCAD 状态 {0}。事务已回滚，未保留部分写回对象。",
                    exception.ErrorStatus);
                control.MarkFormalWritebackFailed(message);
                editor.WriteMessage("\n正式写回失败：{0}", message);
                return;
            }
            catch (System.Exception exception)
            {
                string message = string.IsNullOrWhiteSpace(exception.Message)
                    ? "未知错误。事务已回滚，未保留部分写回对象。"
                    : exception.Message
                        + "。事务已回滚，未保留部分写回对象。";
                control.MarkFormalWritebackFailed(message);
                editor.WriteMessage("\n正式写回失败：{0}", message);
                return;
            }
            try
            {
                OrthogonalLayoutTransientPreview.Clear(document);
                editor.Regen();
            }
            catch (System.Exception exception)
            {
                editor.WriteMessage(
                    "\n正式对象已经写回，但清除临时预览时出现提示：{0}",
                    exception.Message);
            }

            control.MarkFormalWritebackSucceeded(
                formalLines.Count
                    + formalDimensions.Count
                    + (formalStartPoint == null
                        ? 0
                        : OrthogonalLayoutStartPointEntityFactory
                            .ExpectedEntityCount));
            editor.WriteMessage(
                "\n已正式追加并核验 {0} 条分格线、{1} 个尺寸标注和 {2} 个起铺点对象；"
                    + "分格线图层为 {3}，标注图层为 {4}，起铺点图层为 {5}；按房间范围完成重复保护。",
                formalLines.Count,
                formalDimensions.Count,
                formalStartPoint == null
                    ? 0
                    : OrthogonalLayoutStartPointEntityFactory
                        .ExpectedEntityCount,
                OrthogonalLayoutWritebackPolicy.ConfirmedLayerName,
                OrthogonalLayoutWritebackPolicy.DimensionLayerName,
                OrthogonalLayoutWritebackPolicy.StartPointLayerName);
            editor.WriteMessage(
                "\n既有墙线和对象未修改，插件未自动保存 DWG；可用一次 U 或 UNDO 撤销本次全部写回。" );
        }

        private static ObjectId EnsureConfirmedLayoutLayer(
            Transaction transaction,
            Database database)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId,
                OpenMode.ForRead);
            ObjectId layerId;
            LayerTableRecord layer;
            if (layerTable.Has(OrthogonalLayoutWritebackPolicy.ConfirmedLayerName))
            {
                layerId = layerTable[
                    OrthogonalLayoutWritebackPolicy.ConfirmedLayerName];
                layer = (LayerTableRecord)transaction.GetObject(
                    layerId,
                    OpenMode.ForRead);
                if (layer.IsLocked)
                {
                    throw new InvalidOperationException(
                        "目标图层已锁定，无法安全写回；图纸没有变化。" );
                }

                ObjectId continuousLinetypeId = FindContinuousLinetype(
                    transaction,
                    database);
                if (layer.Color == null
                    || layer.Color.ColorIndex
                        != OrthogonalLayoutWritebackPolicy.ConfirmedLayerColorIndex
                    || layer.LinetypeObjectId != continuousLinetypeId)
                {
                    throw new InvalidOperationException(
                        "目标图层已存在但属性不是 ACI 3/Continuous；为保护既有图层，已拒绝写回。" );
                }

                return layerId;
            }
            else
            {
                layerTable.UpgradeOpen();
                layer = new LayerTableRecord
                {
                    Name = OrthogonalLayoutWritebackPolicy.ConfirmedLayerName,
                    Color = Color.FromColorIndex(
                        ColorMethod.ByAci,
                        OrthogonalLayoutWritebackPolicy.ConfirmedLayerColorIndex)
                };
                ObjectId continuousLinetypeId = FindContinuousLinetype(
                    transaction,
                    database);
                layer.LinetypeObjectId = continuousLinetypeId;
                layerId = layerTable.Add(layer);
                transaction.AddNewlyCreatedDBObject(layer, true);
                return layerId;
            }
        }

        private static ObjectId EnsureDimensionLayer(
            Transaction transaction,
            Database database)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId,
                OpenMode.ForRead);
            ObjectId layerId;
            LayerTableRecord layer;
            if (layerTable.Has(OrthogonalLayoutWritebackPolicy.DimensionLayerName))
            {
                layerId = layerTable[
                    OrthogonalLayoutWritebackPolicy.DimensionLayerName];
                layer = (LayerTableRecord)transaction.GetObject(
                    layerId,
                    OpenMode.ForRead);
                if (layer.IsLocked)
                {
                    throw new InvalidOperationException(
                        "尺寸标注图层已锁定，无法安全写回；图纸没有变化。" );
                }

                ObjectId continuousLinetypeId = FindContinuousLinetype(
                    transaction,
                    database);
                if (layer.Color == null
                    || layer.Color.ColorIndex
                        != OrthogonalLayoutWritebackPolicy.DimensionLayerColorIndex
                    || layer.LinetypeObjectId != continuousLinetypeId)
                {
                    throw new InvalidOperationException(
                        "尺寸标注图层已存在但属性不是 ACI 2/Continuous；"
                            + "为保护既有图层，已拒绝写回。" );
                }

                return layerId;
            }

            layerTable.UpgradeOpen();
            layer = new LayerTableRecord
            {
                Name = OrthogonalLayoutWritebackPolicy.DimensionLayerName,
                Color = Color.FromColorIndex(
                    ColorMethod.ByAci,
                    OrthogonalLayoutWritebackPolicy.DimensionLayerColorIndex)
            };
            ObjectId continuousId = FindContinuousLinetype(
                transaction,
                database);
            layer.LinetypeObjectId = continuousId;
            layerId = layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
            return layerId;
        }

        private static ObjectId EnsureStartPointLayer(
            Transaction transaction,
            Database database)
        {
            LayerTable layerTable = (LayerTable)transaction.GetObject(
                database.LayerTableId,
                OpenMode.ForRead);
            ObjectId layerId;
            LayerTableRecord layer;
            if (layerTable.Has(
                OrthogonalLayoutWritebackPolicy.StartPointLayerName))
            {
                layerId = layerTable[
                    OrthogonalLayoutWritebackPolicy.StartPointLayerName];
                layer = (LayerTableRecord)transaction.GetObject(
                    layerId,
                    OpenMode.ForRead);
                if (layer.IsLocked)
                {
                    throw new InvalidOperationException(
                        "起铺点图层已锁定，无法安全写回；图纸没有变化。" );
                }

                ObjectId continuousLinetypeId = FindContinuousLinetype(
                    transaction,
                    database);
                if (layer.Color == null
                    || layer.Color.ColorIndex
                        != OrthogonalLayoutWritebackPolicy
                            .StartPointLayerColorIndex
                    || layer.LinetypeObjectId != continuousLinetypeId)
                {
                    throw new InvalidOperationException(
                        "起铺点图层已存在但属性不是 ACI 3/Continuous；"
                            + "为保护既有图层，已拒绝写回。" );
                }

                return layerId;
            }

            layerTable.UpgradeOpen();
            layer = new LayerTableRecord
            {
                Name = OrthogonalLayoutWritebackPolicy.StartPointLayerName,
                Color = Color.FromColorIndex(
                    ColorMethod.ByAci,
                    OrthogonalLayoutWritebackPolicy
                        .StartPointLayerColorIndex)
            };
            ObjectId continuousId = FindContinuousLinetype(
                transaction,
                database);
            layer.LinetypeObjectId = continuousId;
            layerId = layerTable.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
            return layerId;
        }

        private static ObjectId FindContinuousLinetype(
            Transaction transaction,
            Database database)
        {
            LinetypeTable linetypeTable = (LinetypeTable)transaction.GetObject(
                database.LinetypeTableId,
                OpenMode.ForRead);
            if (!linetypeTable.Has(
                OrthogonalLayoutWritebackPolicy.ConfirmedLayerLinetypeName))
            {
                throw new InvalidOperationException(
                    "AutoCAD 当前图纸没有 Continuous 线型，正式写回已拒绝。" );
            }

            return linetypeTable[
                OrthogonalLayoutWritebackPolicy.ConfirmedLayerLinetypeName];
        }

        private static void EnsureRoomRangeIsNotAlreadyWrittenInModelSpace(
            Transaction transaction,
            ObjectId modelSpaceId,
            ObjectId layerId,
            LayoutDrawingPlan plan,
            IReadOnlyList<LayoutDrawingLine> formalLines)
        {
            if (plan == null)
            {
                throw new InvalidOperationException(
                    "当前没有可用于重复判定的房间范围。" );
            }

            // DOR8 writes formal lines only to model space. Limit ownership
            // and count checks to that space so unrelated block definitions
            // cannot make every room writeback traverse the whole database.
            var legacyLines = new List<Line>();
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                modelSpaceId,
                OpenMode.ForRead);
            foreach (ObjectId objectId in modelSpace)
            {
                Entity entity = transaction.GetObject(
                    objectId,
                    OpenMode.ForRead,
                    false) as Entity;
                if (entity == null || entity.LayerId != layerId)
                {
                    continue;
                }

                bool metadataPresent;
                OrthogonalLayoutRoomRange range =
                    ReadRoomRangeMetadata(entity, out metadataPresent);
                if (metadataPresent)
                {
                    if (OrthogonalLayoutWritebackPolicy.IsSameRoomRange(
                        plan,
                        range.West,
                        range.East,
                        range.South,
                        range.North,
                        range.Elevation))
                    {
                        throw new InvalidOperationException(
                            "当前房间范围已经在目标图层正式写回，已拒绝重复写回；"
                                + "没有删除、覆盖或追加任何对象。" );
                    }

                    continue;
                }

                Line legacyLine = entity as Line;
                if (legacyLine != null)
                {
                    legacyLines.Add(legacyLine);
                }
            }

            if (HasSameFormalLineGeometry(legacyLines, formalLines))
            {
                throw new InvalidOperationException(
                    "目标图层已有与当前房间相同的旧版正式分格线，已拒绝重复写回；"
                        + "本次没有删除、覆盖或追加任何对象。" );
            }

        }

        private static OrthogonalLayoutRoomRange ReadRoomRangeMetadata(
            Entity entity,
            out bool metadataPresent)
        {
            metadataPresent = false;
            using (ResultBuffer buffer = entity.GetXDataForApplication(
                OrthogonalLayoutWritebackPolicy.RoomRangeMetadataApplicationName))
            {
                if (buffer == null)
                {
                    return null;
                }

                metadataPresent = true;
                TypedValue[] values = buffer.AsArray();
                string version = null;
                var coordinates = new List<double>();
                foreach (TypedValue value in values)
                {
                    if (value.TypeCode
                        == (int)DxfCode.ExtendedDataAsciiString
                        && version == null)
                    {
                        version = value.Value as string;
                    }
                    else if (value.TypeCode
                        == (int)DxfCode.ExtendedDataReal)
                    {
                        if (!(value.Value is double))
                        {
                            throw new InvalidOperationException(
                                "目标图层存在无法识别的房间范围归属标记；"
                                    + "为保护既有对象，已拒绝写回。" );
                        }

                        coordinates.Add((double)value.Value);
                    }
                }

                if (!string.Equals(
                    version,
                    OrthogonalLayoutWritebackPolicy.RoomRangeMetadataVersion,
                    StringComparison.Ordinal)
                    || coordinates.Count != 5)
                {
                    throw new InvalidOperationException(
                        "目标图层存在无法识别的房间范围归属标记；"
                            + "为保护既有对象，已拒绝写回。" );
                }

                return new OrthogonalLayoutRoomRange(
                    coordinates[0],
                    coordinates[1],
                    coordinates[2],
                    coordinates[3],
                    coordinates[4]);
            }
        }

        private static bool HasSameFormalLineGeometry(
            IReadOnlyList<Line> existingLines,
            IReadOnlyList<LayoutDrawingLine> formalLines)
        {
            if (existingLines == null
                || formalLines == null
                || formalLines.Count == 0
                || existingLines.Count < formalLines.Count)
            {
                return false;
            }

            var used = new bool[existingLines.Count];
            foreach (LayoutDrawingLine formalLine in formalLines)
            {
                bool matched = false;
                for (int index = 0; index < existingLines.Count; index++)
                {
                    if (used[index]
                        || !SameLineGeometry(
                            existingLines[index],
                            formalLine.Geometry))
                    {
                        continue;
                    }

                    used[index] = true;
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameLineGeometry(
            Line existingLine,
            CoreLineSegment3D expectedLine)
        {
            Point3d existingStart = existingLine.StartPoint;
            Point3d existingEnd = existingLine.EndPoint;
            bool forward = SamePoint(
                existingStart,
                expectedLine.Start)
                && SamePoint(existingEnd, expectedLine.End);
            bool reverse = SamePoint(
                existingStart,
                expectedLine.End)
                && SamePoint(existingEnd, expectedLine.Start);
            return forward || reverse;
        }

        private static bool SamePoint(
            Point3d actual,
            CorePoint3D expected)
        {
            return OrthogonalLayoutWritebackPolicy.NearlyEqual(
                actual.X,
                expected.X)
                && OrthogonalLayoutWritebackPolicy.NearlyEqual(
                    actual.Y,
                    expected.Y)
                && OrthogonalLayoutWritebackPolicy.NearlyEqual(
                    actual.Z,
                    expected.Z);
        }

        private static void EnsureRoomRangeMetadataApplication(
            Transaction transaction,
            Database database)
        {
            RegAppTable table = (RegAppTable)transaction.GetObject(
                database.RegAppTableId,
                OpenMode.ForRead);
            if (table.Has(
                OrthogonalLayoutWritebackPolicy.RoomRangeMetadataApplicationName))
            {
                return;
            }

            table.UpgradeOpen();
            var record = new RegAppTableRecord
            {
                Name = OrthogonalLayoutWritebackPolicy
                    .RoomRangeMetadataApplicationName
            };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        private static int WriteFormalDrawingLines(
            Transaction transaction,
            Database database,
            ObjectId modelSpaceId,
            ObjectId layoutLayerId,
            LayoutDrawingPlan plan,
            IReadOnlyList<LayoutDrawingLine> formalLines)
        {
            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                modelSpaceId,
                OpenMode.ForWrite);
            int writtenCount = 0;
            foreach (LayoutDrawingLine formalLine in formalLines)
            {
                CoreLineSegment3D geometry = formalLine.Geometry;
                var line = new Line(
                    new Point3d(
                        geometry.Start.X,
                        geometry.Start.Y,
                        geometry.Start.Z),
                    new Point3d(
                        geometry.End.X,
                        geometry.End.Y,
                        geometry.End.Z));
                line.SetDatabaseDefaults(database);
                line.LayerId = layoutLayerId;
                line.Color = Color.FromColorIndex(
                    ColorMethod.ByAci,
                    formalLine.Semantic
                        == LayoutDrawingLineSemantic.FinishedFaceOutline
                        ? plan.ColorSettings.PlasterBoundaryColorIndex
                        : plan.ColorSettings.DivisionLineColorIndex);
                line.Linetype = "ByLayer";
                using (var roomRangeData = new ResultBuffer(
                    new TypedValue(
                        (int)DxfCode.ExtendedDataRegAppName,
                        OrthogonalLayoutWritebackPolicy
                            .RoomRangeMetadataApplicationName),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataAsciiString,
                        OrthogonalLayoutWritebackPolicy
                            .RoomRangeMetadataVersion),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceWest),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceEast),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceSouth),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceNorth),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.Elevation)))
                {
                    line.XData = roomRangeData;
                }
                modelSpace.AppendEntity(line);
                transaction.AddNewlyCreatedDBObject(line, true);
                writtenCount++;
            }

            return writtenCount;
        }

        private static int WriteFormalDimensions(
            Transaction transaction,
            Database database,
            ObjectId modelSpaceId,
            ObjectId dimensionLayerId,
            LayoutDrawingPlan plan,
            IReadOnlyList<LayoutDrawingDimension> dimensions,
            ObjectId dimensionStyleId)
        {
            if (dimensions == null || dimensions.Count == 0)
            {
                return 0;
            }

            if (dimensionLayerId.IsNull)
            {
                throw new InvalidOperationException(
                    "尺寸标注图层尚未准备好，正式写回已停止。" );
            }

            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                modelSpaceId,
                OpenMode.ForWrite);
            int writtenCount = 0;
            foreach (LayoutDrawingDimension dimension in dimensions)
            {
                RotatedDimension entity =
                    OrthogonalLayoutDimensionEntityFactory.Create(
                        database,
                        dimension,
                        plan.ColorSettings,
                        dimensionStyleId);
                entity.LayerId = dimensionLayerId;
                entity.Linetype = "ByLayer";
                using (var roomRangeData = new ResultBuffer(
                    new TypedValue(
                        (int)DxfCode.ExtendedDataRegAppName,
                        OrthogonalLayoutWritebackPolicy
                            .RoomRangeMetadataApplicationName),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataAsciiString,
                        OrthogonalLayoutWritebackPolicy
                            .RoomRangeMetadataVersion),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceWest),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceEast),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceSouth),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceNorth),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.Elevation)))
                {
                    entity.XData = roomRangeData;
                }

                modelSpace.AppendEntity(entity);
                transaction.AddNewlyCreatedDBObject(entity, true);
                writtenCount++;
            }

            return writtenCount;
        }

        private static int WriteFormalStartPoint(
            Transaction transaction,
            Database database,
            ObjectId modelSpaceId,
            ObjectId startPointLayerId,
            LayoutDrawingPlan plan,
            LayoutDrawingStartPoint startPoint)
        {
            if (startPoint == null)
            {
                return 0;
            }

            if (startPointLayerId.IsNull)
            {
                throw new InvalidOperationException(
                    "起铺点图层尚未准备好，正式写回已停止。" );
            }

            BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(
                modelSpaceId,
                OpenMode.ForWrite);
            IReadOnlyList<Entity> entities =
                OrthogonalLayoutStartPointEntityFactory.Create(
                    database,
                    plan);
            int writtenCount = 0;
            foreach (Entity entity in entities)
            {
                entity.LayerId = startPointLayerId;
                entity.Linetype = "ByLayer";
                using (var roomRangeData = new ResultBuffer(
                    new TypedValue(
                        (int)DxfCode.ExtendedDataRegAppName,
                        OrthogonalLayoutWritebackPolicy
                            .RoomRangeMetadataApplicationName),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataAsciiString,
                        OrthogonalLayoutWritebackPolicy
                            .RoomRangeMetadataVersion),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceWest),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceEast),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceSouth),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.SourceNorth),
                    new TypedValue(
                        (int)DxfCode.ExtendedDataReal,
                        plan.Elevation)))
                {
                    entity.XData = roomRangeData;
                }

                modelSpace.AppendEntity(entity);
                transaction.AddNewlyCreatedDBObject(entity, true);
                writtenCount++;
            }

            return writtenCount;
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
