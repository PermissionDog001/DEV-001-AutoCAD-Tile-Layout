using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using AcadLine = Autodesk.AutoCAD.DatabaseServices.Line;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    internal static class OrthogonalLayoutTransientPreview
    {
        private static Document ownerDocument;
        private static LayoutDrawingPlan visiblePlan;
        private static readonly List<Entity> visibleTransients =
            new List<Entity>();
        private static TransientManager transientManager;
        private static IntegerCollection viewportNumbers;
        private static int transientSubDrawingMode;

        internal static bool IsVisible(Document document)
        {
            return document != null
                && ReferenceEquals(document, ownerDocument)
                && visiblePlan != null;
        }

        internal static void Show(Document document, LayoutDrawingPlan plan)
        {
            Show(document, plan, false, false, false, null);
        }

        internal static void Show(
            Document document,
            LayoutDrawingPlan plan,
            bool showAllAssessedBoundaryTiles,
            bool showNeutralRegions,
            bool showWallCornerDiagnostics,
            string selectedDiagnosticTileId)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (ownerDocument != null
                && !ReferenceEquals(ownerDocument, document))
            {
                ClearAny();
            }

            if (ReferenceEquals(ownerDocument, document)
                && (visiblePlan != null || visibleTransients.Count > 0))
            {
                bool regenRequired = ClearTransientGraphics();
                if (regenRequired)
                {
                    document.Editor.Regen();
                }

                ownerDocument = null;
                visiblePlan = null;
            }

            ownerDocument = document;
            visiblePlan = plan;
            try
            {
                Editor editor = document.Editor;
                DrawRoomOutline(editor, plan);
                foreach (LayoutDrawingLine line in plan.DivisionLines)
                {
                    if (line.Semantic
                        == LayoutDrawingLineSemantic.FinishedFaceOutline)
                    {
                        continue;
                    }

                    Draw(
                        editor,
                        line.Geometry,
                        plan.ColorSettings.DivisionLineColorIndex,
                        false);
                }

                foreach (LayoutDrawingLine line in plan.Connections)
                {
                    Draw(
                        editor,
                        line.Geometry,
                        plan.ColorSettings.DivisionLineColorIndex,
                        true);
                }

                DrawDimensions(document.Database, plan);
                DrawStartPoint(document.Database, plan);

                DrawCutDiagnostics(
                    editor,
                    plan,
                    showAllAssessedBoundaryTiles,
                    selectedDiagnosticTileId);
                if (showNeutralRegions)
                {
                    DrawNeutralRegionReference(editor, plan);
                }
                if (showWallCornerDiagnostics)
                {
                    DrawWallCornerDiagnostics(editor, plan);
                }

                editor.UpdateScreen();
            }
            catch
            {
                Clear(document);
                throw;
            }
        }

        internal static void Clear(Document document)
        {
            if (!ReferenceEquals(document, ownerDocument)
                || (visiblePlan == null && visibleTransients.Count == 0))
            {
                return;
            }

            try
            {
                bool regenRequired = ClearTransientGraphics();
                if (regenRequired)
                {
                    document.Editor.Regen();
                }

                document.Editor.UpdateScreen();
            }
            finally
            {
                ownerDocument = null;
                visiblePlan = null;
            }
        }

        internal static void ClearAny()
        {
            Document document = ownerDocument;
            if (document == null
                && visibleTransients.Count == 0)
            {
                ownerDocument = null;
                visiblePlan = null;
                return;
            }

            try
            {
                bool regenRequired = ClearTransientGraphics();
                if (document != null)
                {
                    if (regenRequired)
                    {
                        document.Editor.Regen();
                    }

                    document.Editor.UpdateScreen();
                }
            }
            finally
            {
                ownerDocument = null;
                visiblePlan = null;
            }
        }

        private static void DrawRoomOutline(
            Editor editor,
            LayoutDrawingPlan plan)
        {
            int colorIndex = plan.PlasterThicknessMm
                > GeometryTolerance.Coordinate
                ? plan.ColorSettings.PlasterBoundaryColorIndex
                : 8;
            for (int index = 0; index < plan.RoomOutline.Count; index++)
            {
                Draw(
                    editor,
                    new LineSegment3D(
                        plan.RoomOutline[index],
                        plan.RoomOutline[(index + 1) % plan.RoomOutline.Count]),
                    colorIndex,
                    false);
            }
        }

        private static void DrawCutDiagnostics(
            Editor editor,
            LayoutDrawingPlan plan,
            bool showAllAssessedBoundaryTiles,
            string selectedDiagnosticTileId)
        {
            foreach (LayoutDrawingTile tile in plan.Tiles)
            {
                if (!tile.IsBelowRecommended
                    && !(showAllAssessedBoundaryTiles
                        && tile.HasApplicableBoundaryCut))
                {
                    continue;
                }

                bool selected = string.Equals(
                    tile.Id,
                    selectedDiagnosticTileId,
                    StringComparison.Ordinal);
                int color = selected
                    ? 1
                    : tile.AssessmentStatus
                        == ProjectCutStatus.RequiresProjectPolicy
                            ? 2
                            : tile.AssessmentStatus
                                == ProjectCutStatus.RequiresUserReview
                                ? 30
                                : 4;
                DrawClosedOutline(editor, tile.Outline, color, selected);
            }
        }

        private static void DrawDimensions(
            Database database,
            LayoutDrawingPlan plan)
        {
            ObjectId architecturalTickBlockId =
                OrthogonalLayoutDimensionStyle
                    .GetTransientArchitecturalTickBlockId(database);
            foreach (LayoutDrawingDimension dimension in plan.Dimensions)
            {
                Entity transient = OrthogonalLayoutDimensionEntityFactory
                    .CreateTransient(
                    database,
                    dimension,
                    plan.ColorSettings,
                    architecturalTickBlockId);
                try
                {
                    AddTransient(transient);
                }
                catch
                {
                    transient.Dispose();
                    throw;
                }
            }
        }

        private static void DrawStartPoint(
            Database database,
            LayoutDrawingPlan plan)
        {
            foreach (Entity transient
                in OrthogonalLayoutStartPointEntityFactory.CreateTransient(
                    database,
                    plan))
            {
                try
                {
                    AddTransient(transient);
                }
                catch
                {
                    transient.Dispose();
                    throw;
                }
            }
        }

        private static void DrawNeutralRegionReference(
            Editor editor,
            LayoutDrawingPlan plan)
        {
            foreach (LayoutDrawingNeutralRegion region in plan.NeutralRegions)
            {
                var outline = new[]
                {
                    new Point3D(region.Bounds.West, region.Bounds.South,
                        region.Bounds.Elevation),
                    new Point3D(region.Bounds.East, region.Bounds.South,
                        region.Bounds.Elevation),
                    new Point3D(region.Bounds.East, region.Bounds.North,
                        region.Bounds.Elevation),
                    new Point3D(region.Bounds.West, region.Bounds.North,
                        region.Bounds.Elevation)
                };
                DrawClosedOutline(editor, outline, 4, false);
            }

            foreach (LayoutDrawingLine connection in plan.NeutralConnections)
            {
                Draw(editor, connection.Geometry, 6, true);
            }
        }

        private static void DrawWallCornerDiagnostics(
            Editor editor,
            LayoutDrawingPlan plan)
        {
            double markerSize = Math.Max(
                5.0,
                Math.Min(100.0, Math.Min(plan.Width, plan.Height) * 0.01));
            foreach (LayoutDrawingWallCorner corner in plan.WallCorners)
            {
                int color = !corner.IsOptimizationTarget
                    ? 8
                    : corner.IsExactGridIntersection
                    ? 5
                    : corner.HasAnyExactSeam
                    ? 4
                    : 2;
                Point3D point = corner.Position;
                Draw(editor,
                    new LineSegment3D(
                        new Point3D(point.X - markerSize,
                            point.Y - markerSize, point.Z),
                        new Point3D(point.X + markerSize,
                            point.Y + markerSize, point.Z)),
                    color,
                    corner.IsOptimizationTarget);
                Draw(editor,
                    new LineSegment3D(
                        new Point3D(point.X - markerSize,
                            point.Y + markerSize, point.Z),
                        new Point3D(point.X + markerSize,
                            point.Y - markerSize, point.Z)),
                    color,
                    corner.IsOptimizationTarget);
            }
        }

        private static void DrawClosedOutline(
            Editor editor,
            System.Collections.Generic.IReadOnlyList<Point3D> outline,
            int colorIndex,
            bool highlight)
        {
            for (int index = 0; index < outline.Count; index++)
            {
                Draw(
                    editor,
                    new LineSegment3D(
                        outline[index],
                        outline[(index + 1) % outline.Count]),
                    colorIndex,
                    highlight);
            }
        }

        private static void Draw(
            Editor editor,
            LineSegment3D line,
            int colorIndex,
            bool highlight)
        {
            EnsureTransientDrawingContext();
            var transient = new AcadLine(
                new Point3d(
                    line.Start.X,
                    line.Start.Y,
                    line.Start.Z),
                new Point3d(
                    line.End.X,
                    line.End.Y,
                    line.End.Z));
            transient.Color = Color.FromColorIndex(
                ColorMethod.ByAci,
                (short)colorIndex);
            try
            {
                AddTransient(transient);
            }
            catch
            {
                transient.Dispose();
                throw;
            }
        }

        private static void AddTransient(Entity transient)
        {
            if (!transientManager.AddTransient(
                transient,
                TransientDrawingMode.Main,
                transientSubDrawingMode,
                viewportNumbers))
            {
                throw new InvalidOperationException(
                    "AutoCAD 无法注册临时预览对象。" );
            }

            visibleTransients.Add(transient);
        }

        private static void EnsureTransientDrawingContext()
        {
            if (transientManager != null && viewportNumbers != null)
            {
                return;
            }

            transientManager = TransientManager.CurrentTransientManager;
            if (transientManager == null)
            {
                throw new InvalidOperationException(
                    "AutoCAD 当前没有可用的临时图形管理器。" );
            }

            viewportNumbers = new IntegerCollection();
            transientSubDrawingMode = 0;
            int drawOrderResult = transientManager.GetFreeSubDrawingMode(
                TransientDrawingMode.Main,
                viewportNumbers,
                ref transientSubDrawingMode);
            if (drawOrderResult == 0 || transientSubDrawingMode == 0)
            {
                transientManager = null;
                viewportNumbers = null;
                throw new InvalidOperationException(
                    "AutoCAD 当前没有可用的临时预览绘制顺序。" );
            }
        }

        private static bool ClearTransientGraphics()
        {
            TransientManager manager = transientManager;
            IntegerCollection viewports = viewportNumbers;
            bool regenRequired = false;
            foreach (Entity transient in visibleTransients)
            {
                try
                {
                    if (manager != null && viewports != null)
                    {
                        manager.EraseTransient(transient, viewports);
                    }
                }
                catch (System.Exception)
                {
                    // A full regen is only needed when AutoCAD rejects an
                    // individual transient erase.
                    regenRequired = true;
                }

                try
                {
                    transient.Dispose();
                }
                catch (System.Exception)
                {
                }
            }

            visibleTransients.Clear();
            transientManager = null;
            viewportNumbers = null;
            transientSubDrawingMode = 0;
            return regenRequired;
        }
    }
}
