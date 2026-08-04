using System;
using System.Globalization;
using System.Text;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    public static class OrthogonalDecisionPaletteText
    {
        public static string FormatRequirement(DecisionRequirement requirement)
        {
            if (requirement == null)
            {
                throw new ArgumentNullException(nameof(requirement));
            }

            var builder = new StringBuilder();
            builder.Append(requirement.Code);
            builder.Append("：");
            builder.Append(requirement.Reason);
            if (!string.IsNullOrWhiteSpace(requirement.RequiredInput))
            {
                builder.Append(" 所需输入：");
                builder.Append(requirement.RequiredInput);
            }

            if (requirement.Options.Count > 0)
            {
                builder.Append(" 可选项：");
                builder.Append(string.Join("、", requirement.Options));
            }

            return builder.ToString();
        }

        public static string FormatCandidate(EvaluatedLayoutCandidate evaluated)
        {
            if (evaluated == null)
            {
                throw new ArgumentNullException(nameof(evaluated));
            }

            var builder = new StringBuilder();
            builder.Append("候选：");
            builder.Append(evaluated.Id);
            builder.AppendLine();
            builder.Append("状态：");
            builder.Append(evaluated.State);
            builder.AppendLine();
            builder.Append("状态原因：");
            builder.Append(evaluated.StateReason);

            LayoutCandidate candidate = evaluated.Candidate;
            if (candidate == null)
            {
                return builder.ToString();
            }

            builder.AppendLine();
            builder.Append("DOR2 原始理由：");
            builder.Append(candidate.SelectionReason);
            builder.AppendLine();
            builder.Append("结构：");
            builder.Append(candidate.Structure.Kind);
            builder.Append("；分格片段=");
            builder.Append(candidate.DivisionLines.Count);
            builder.Append("；实际砖块=");
            builder.Append(candidate.Tiles.Count);
            AppendMetrics(builder, candidate.Metrics);
            builder.AppendLine();
            builder.Append("原始诊断：");
            if (candidate.Diagnostics.Count == 0)
            {
                builder.Append("无");
            }
            else
            {
                foreach (CandidateDiagnostic diagnostic in candidate.Diagnostics)
                {
                    builder.AppendLine();
                    builder.Append("- ");
                    builder.Append(diagnostic.Code);
                    builder.Append(" [");
                    builder.Append(diagnostic.Severity);
                    builder.Append("] ");
                    builder.Append(diagnostic.Message);
                }
            }

            return builder.ToString();
        }

        public static string FormatPreviewStatus(
            OrthogonalDecisionPaletteSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            switch (session.State)
            {
                case OrthogonalDecisionPaletteState.Empty:
                    return "尚未载入工程决策结果；本 PaletteSet 不猜测房间语义，也不会写入图纸。";
                case OrthogonalDecisionPaletteState.NeedsProjectPolicy:
                    return "项目策略尚未齐备，不能进入预览。";
                case OrthogonalDecisionPaletteState.NeedsRoomSemantics:
                    return "房间语义尚未齐备，不能进入预览。";
                case OrthogonalDecisionPaletteState.NeedsCandidateSelection:
                    return "尚未选定可保留方案；人工复核候选无需填写原因，正式写回前还会最终确认。";
                case OrthogonalDecisionPaletteState.AutomaticPreviewReady:
                    return "唯一自动候选已就绪；可直接请求零写入预览。";
                case OrthogonalDecisionPaletteState.ManualReviewPreviewReady:
                    return "已选中需要人工复核的方案；可先请求零写入预览，正式写回仍需最终确认。";
                case OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady:
                    return "已明确选择按图面确认；可先请求同源预览，正式写回前仍需最终确认，且不代表自动合规。";
                case OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady:
                    return session.Mode == LayoutDecisionMode.Research
                        ? "已保留研究 DecisionRecord；可请求预览，但为非自动合规。"
                        : "已保留受控生产 DecisionRecord；可请求预览，但为非自动合规。";
                case OrthogonalDecisionPaletteState.PreviewRequested:
                    return "已发出预览请求；本向导不创建图层、实体或写事务。";
                case OrthogonalDecisionPaletteState.Blocked:
                    return "当前结果不可预览；请先处理显示的问题或能力边界。";
                default:
                    throw new ArgumentOutOfRangeException(nameof(session));
            }
        }

        public static string FormatDecisionRecord(
            DecisionRecord record,
            LayoutDecisionMode mode)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "DecisionRecord：候选={0}；策略版本={1}；模式={2}；"
                    + "例外接受={3}；原因={4}；非自动合规。",
                record.CandidateId,
                record.PolicyVersion,
                mode,
                record.AcceptsException ? "是" : "否",
                record.Reason);
        }

        private static void AppendMetrics(
            StringBuilder builder,
            LayoutCandidateMetrics metrics)
        {
            if (metrics == null)
            {
                return;
            }

            builder.AppendLine();
            builder.Append("原始指标：内部非整砖=");
            builder.Append(metrics.InteriorNonFullTileCount);
            builder.Append("，内部非整砖面积=");
            builder.Append(FormatNumber(metrics.InteriorNonFullTileArea));
            builder.Append("，过渡缝长=");
            builder.Append(FormatNumber(metrics.InternalTransitionSeamLength));
            builder.Append("，边界非整砖=");
            builder.Append(metrics.BoundaryNonFullTileCount);
            builder.Append("，低于默认下限=");
            builder.Append(metrics.BelowDefaultMinimumBoundaryTileCount);
            builder.Append("，最小边界带=");
            builder.Append(FormatNumber(metrics.MinimumBoundaryBandWidth));
            builder.Append("，相位重置=");
            builder.Append(metrics.PhaseResetCount);
            builder.Append("，连续异形砖=");
            builder.Append(metrics.ContinuousIrregularTileCount);
            builder.Append("，首视线代价=");
            builder.Append(metrics.FirstSightlinePenalty);
            builder.Append("，关键对缝=");
            builder.Append(metrics.KeyAlignmentCount);
            builder.Append("，墙角目标=");
            builder.Append(metrics.OptimizationTargetCornerCount);
            builder.Append("，双向墙角交点=");
            builder.Append(metrics.ExactGridIntersectionCornerCount);
            builder.Append("，至少单缝命中墙角=");
            builder.Append(metrics.ExactSeamAlignedCornerCount);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
