using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    internal sealed class OrthogonalLayoutRoomRange
    {
        internal OrthogonalLayoutRoomRange(
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

        internal double West { get; }

        internal double East { get; }

        internal double South { get; }

        internal double North { get; }

        internal double Elevation { get; }
    }

    internal static class OrthogonalLayoutWritebackPolicy
    {
        internal const string ConfirmedLayerName =
            "TILE_LAYOUT_ORTHO_CONFIRMED";
        internal const short ConfirmedLayerColorIndex = 3;
        internal const string ConfirmedLayerLinetypeName = "Continuous";
        internal const string RoomRangeMetadataApplicationName =
            "TILE_ORTHO_ROOM";
        internal const string RoomRangeMetadataVersion = "ROOM_RANGE_V1";

        internal static bool IsSameRoomRange(
            LayoutDrawingPlan plan,
            double west,
            double east,
            double south,
            double north,
            double elevation)
        {
            if (plan == null)
            {
                return false;
            }

            return NearlyEqual(plan.SourceWest, west)
                && NearlyEqual(plan.SourceEast, east)
                && NearlyEqual(plan.SourceSouth, south)
                && NearlyEqual(plan.SourceNorth, north)
                && NearlyEqual(plan.Elevation, elevation);
        }

        internal static bool NearlyEqual(double first, double second)
        {
            return Math.Abs(first - second) <= GeometryTolerance.Coordinate;
        }

        internal static bool IsCandidateEligible(LayoutCandidateState state)
        {
            return IsCandidateEligible(state, false);
        }

        internal static bool IsCandidateEligible(
            LayoutCandidateState state,
            bool allowsVisualConfirmation)
        {
            return state == LayoutCandidateState.AutomaticUsable
                || state == LayoutCandidateState.RequiresUserDecision
                || allowsVisualConfirmation
                    && state == LayoutCandidateState.RequiresProjectPolicy;
        }

        internal static bool TryGetFormalLines(
            LayoutDrawingPlan plan,
            bool previewIsVisible,
            bool confirmationAcknowledged,
            out IReadOnlyList<LayoutDrawingLine> lines,
            out string rejectionReason)
        {
            return TryGetFormalLines(
                plan,
                previewIsVisible,
                confirmationAcknowledged,
                false,
                out lines,
                out rejectionReason);
        }

        internal static bool TryGetFormalLines(
            LayoutDrawingPlan plan,
            bool previewIsVisible,
            bool confirmationAcknowledged,
            bool allowsVisualConfirmation,
            out IReadOnlyList<LayoutDrawingLine> lines,
            out string rejectionReason)
        {
            lines = null;
            rejectionReason = string.Empty;
            if (plan == null)
            {
                rejectionReason = "当前没有可写回的同源绘图计划。";
                return false;
            }

            if (!IsCandidateEligible(
                plan.CandidateState,
                allowsVisualConfirmation))
            {
                rejectionReason = allowsVisualConfirmation
                    ? "只有 AutomaticUsable、已明确提醒后确认的 RequiresUserDecision，或按图面确认模式下最终确认的 RequiresProjectPolicy 候选可以写回。"
                    : "只有 AutomaticUsable 或已明确提醒后确认的 RequiresUserDecision 候选可以写回。";
                return false;
            }

            if (!previewIsVisible)
            {
                rejectionReason = "必须先显示当前候选的临时预览，才能正式写回。";
                return false;
            }

            if (!confirmationAcknowledged)
            {
                rejectionReason = "正式写回尚未经过最后确认。";
                return false;
            }

            var formalLines = new List<LayoutDrawingLine>(
                plan.DivisionLines.Count + plan.Connections.Count);
            formalLines.AddRange(plan.DivisionLines);
            formalLines.AddRange(plan.Connections);
            if (formalLines.Count == 0)
            {
                rejectionReason = "当前同源绘图计划没有正式分格线或连接边，无需写回。";
                return false;
            }

            lines = new ReadOnlyCollection<LayoutDrawingLine>(formalLines);
            return true;
        }
    }
}
