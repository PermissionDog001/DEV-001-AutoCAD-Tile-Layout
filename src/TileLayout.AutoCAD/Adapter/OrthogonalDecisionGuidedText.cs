using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TileLayout.Core;

namespace TileLayout.AutoCAD.Adapter
{
    public static class OrthogonalDecisionGuidedText
    {
        private const string ConnectionEdgeMismatchReason =
            "The selected connection edge does not match the validated main-secondary connection.";

        public static bool IsConnectionEdgeMismatch(
            DecisionRequirement requirement)
        {
            return requirement != null
                && requirement.Code == DecisionRequirementCode.InputUntrusted
                && string.Equals(
                    requirement.Reason,
                    ConnectionEdgeMismatchReason,
                    StringComparison.Ordinal);
        }

        public static GuidedRequirementPresentation PresentRequirement(
            DecisionRequirement requirement)
        {
            if (requirement == null)
            {
                throw new ArgumentNullException(nameof(requirement));
            }

            string message;
            string nextAction;
            switch (requirement.Code)
            {
                case DecisionRequirementCode.ProjectSecondAbsoluteMinimum:
                    message = "项目铺贴规则中还没有确认允许的最小边砖宽度。";
                    nextAction = "返回项目铺贴规则，填写最低尺寸或选择“按图面确认”。";
                    break;
                case DecisionRequirementCode.RoomControlRegion:
                    message = "还没有标明这个门洞影响哪一块铺贴范围。";
                    nextAction = "点击“在图中选择门洞影响范围”。";
                    break;
                case DecisionRequirementCode.RoomControlDoor:
                    message = "还没有指定控制本房间排版的门洞。";
                    nextAction = "先选择门洞影响范围，再点击“在图中选择门洞”。";
                    break;
                case DecisionRequirementCode.RoomLayoutIntent:
                    message = "还没有选择整房连续铺贴或分区铺贴。";
                    nextAction = "返回项目铺贴规则明确选择。";
                    break;
                case DecisionRequirementCode.RoomMainSecondaryDefinition:
                    message = "主要铺贴区和相邻铺贴区尚未选全，程序不会按凹角或面积猜测。";
                    nextAction = "依次点击“在图中选择主要铺贴区”和“在图中选择相邻铺贴区”。";
                    break;
                case DecisionRequirementCode.RoomConnectionEdge:
                    message = "两个铺贴区实际相接的整段边尚未指定。";
                    nextAction = "点击“在图中选择两区接合边”。";
                    break;
                case DecisionRequirementCode.CandidateSelection:
                    message = "存在多个可保留方案，需要人工比较并选择。";
                    nextAction = "在“需要人工确认”中选择方案并填写原因。";
                    break;
                case DecisionRequirementCode.CandidateExceptionAcceptance:
                    message = "所选方案包含需要留痕的例外。";
                    nextAction = "填写原因并点击“保存人工确认记录”。";
                    break;
                case DecisionRequirementCode.InputUntrusted:
                    if (IsConnectionEdgeMismatch(requirement))
                    {
                        message = "所选接合边不在两个铺贴区的共同边界上。";
                        nextAction = "返回“在图中标明重点”，重选两区实际相接的整段边；"
                            + "不要选择房间外轮廓上的短折边。";
                    }
                    else
                    {
                        message = "当前房间、区域或连接关系不能被可靠验证。";
                        nextAction = "展开工程详情，按原始原因修改或重选。";
                    }
                    break;
                case DecisionRequirementCode.CapabilityUnsupported:
                    message = "当前输入超出本阶段可处理范围。";
                    nextAction = "修改输入或取消；当前不能请求预览。";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(requirement));
            }

            var details = new StringBuilder();
            details.Append("代码=");
            details.Append(requirement.Code);
            details.Append("；层级=");
            details.Append(requirement.Level);
            details.Append("；原始原因=");
            details.Append(requirement.Reason);
            if (!string.IsNullOrWhiteSpace(requirement.RequiredInput))
            {
                details.Append("；RequiredInput=");
                details.Append(requirement.RequiredInput);
            }

            if (requirement.Options.Count > 0)
            {
                details.Append("；Options=");
                details.Append(string.Join(", ", requirement.Options));
            }

            if (requirement.AffectedCandidateIds.Count > 0)
            {
                details.Append("；候选=");
                details.Append(string.Join(", ", requirement.AffectedCandidateIds));
            }

            return new GuidedRequirementPresentation(
                requirement,
                message,
                nextAction,
                details.ToString());
        }

        public static GuidedCandidateGroup MapCandidateGroup(
            LayoutCandidateState state)
        {
            switch (state)
            {
                case LayoutCandidateState.AutomaticUsable:
                    return GuidedCandidateGroup.AutomaticRecommendation;
                case LayoutCandidateState.RequiresUserDecision:
                    return GuidedCandidateGroup.ManualConfirmation;
                case LayoutCandidateState.RequiresProjectPolicy:
                    return GuidedCandidateGroup.ProjectRuleMissing;
                case LayoutCandidateState.InputUntrusted:
                case LayoutCandidateState.Eliminated:
                case LayoutCandidateState.CapabilityUnsupported:
                    return GuidedCandidateGroup.Unavailable;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        public static GuidedEliminatedGroup MapEliminatedGroup(
            EvaluatedLayoutCandidate evaluated)
        {
            if (evaluated == null || !evaluated.HasRawCandidate)
            {
                return GuidedEliminatedGroup.OtherHardFailure;
            }

            LayoutCandidate candidate = evaluated.Candidate;
            if (candidate.TileAssessments.Any(assessment =>
                    assessment.Status
                        == ProjectCutStatus.BelowProjectAbsoluteMinimum)
                || candidate.Diagnostics.Any(diagnostic => diagnostic.Code
                    == CandidateDiagnosticCode.ProjectAbsoluteMinimumNotMet))
            {
                return GuidedEliminatedGroup.BelowProjectAbsoluteMinimum;
            }

            if (candidate.Diagnostics.Any(diagnostic => diagnostic.Code
                == CandidateDiagnosticCode
                    .SmallBoundaryCutWithoutOppositeFullOrSeam))
            {
                return GuidedEliminatedGroup
                    .SmallBoundaryCutNeedsOppositeFullOrSeam;
            }

            if (candidate.Diagnostics.Any(diagnostic => diagnostic.Code
                == CandidateDiagnosticCode
                    .LargeBoundaryCutWithoutCornerOrSavingBand))
            {
                return GuidedEliminatedGroup.UnjustifiedLargeBoundaryCut;
            }

            if (candidate.Diagnostics.Any(diagnostic => diagnostic.Code
                == CandidateDiagnosticCode.DominatedByCandidate))
            {
                return GuidedEliminatedGroup.ParetoDominated;
            }

            if (candidate.Diagnostics.Any(diagnostic => diagnostic.Code
                == CandidateDiagnosticCode.CandidateSearchTruncated))
            {
                return GuidedEliminatedGroup.SearchTruncated;
            }

            return GuidedEliminatedGroup.OtherHardFailure;
        }

        public static GuidedCornerAlignmentGroup MapCornerAlignmentGroup(
            EvaluatedLayoutCandidate evaluated)
        {
            if (evaluated == null
                || !evaluated.HasRawCandidate
                || evaluated.Candidate.Metrics.OptimizationTargetCornerCount == 0)
            {
                return GuidedCornerAlignmentGroup.NotApplicable;
            }

            if (evaluated.Candidate.Metrics.ExactGridIntersectionCornerCount > 0)
            {
                return GuidedCornerAlignmentGroup.ExactGridIntersection;
            }

            return evaluated.Candidate.Metrics.ExactSeamAlignedCornerCount > 0
                ? GuidedCornerAlignmentGroup.ExactSingleSeam
                : GuidedCornerAlignmentGroup.NoExactAlignment;
        }

        public static string FormatCornerAlignment(
            EvaluatedLayoutCandidate evaluated)
        {
            switch (MapCornerAlignmentGroup(evaluated))
            {
                case GuidedCornerAlignmentGroup.ExactGridIntersection:
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "墙角对缝：{0} 个双向交点，{1} 个至少单缝命中",
                        evaluated.Candidate.Metrics
                            .ExactGridIntersectionCornerCount,
                        evaluated.Candidate.Metrics
                            .ExactSeamAlignedCornerCount);
                case GuidedCornerAlignmentGroup.ExactSingleSeam:
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "墙角对缝：{0} 个单缝准确命中",
                        evaluated.Candidate.Metrics
                            .ExactSeamAlignedCornerCount);
                case GuidedCornerAlignmentGroup.NoExactAlignment:
                    return "墙角对缝：没有准确命中";
                default:
                    return "墙角对缝：当前房间无优化目标";
            }
        }

        public static string FormatBoundaryNormalization(
            OrthogonalBoundaryNormalizationResult normalization)
        {
            if (normalization == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            switch (normalization.Status)
            {
                case OrthogonalBoundaryNormalizationStatus.ExactWcs:
                    builder.Append("边界方向诊断：全部边界线已与 WCS X/Y 轴平行；未建立归一化副本。");
                    break;
                case OrthogonalBoundaryNormalizationStatus.NearOrthogonal:
                    builder.AppendFormat(
                        CultureInfo.CurrentCulture,
                        "边界方向诊断：已建立只读正交计算副本；最大角度偏差 {0:0.######}°，最大端点修正 {1:0.###} mm。原始 LINE 未修改。",
                        normalization.MaximumAngleDeviationDegrees,
                        normalization.MaximumEndpointCorrection);
                    break;
                default:
                    builder.Append("边界方向诊断：未建立正交计算副本；当前边界未满足近似正交限制。");
                    if (!string.IsNullOrWhiteSpace(normalization.Message))
                    {
                        builder.Append(" ");
                        builder.Append(normalization.Message);
                    }

                    break;
            }

            builder.AppendFormat(
                CultureInfo.CurrentCulture,
                " 固定阈值：角度 ≤ {0:0.######}°，端点修正 ≤ {1:0.###} mm，端点连接容差 ≤ {2:0.###} mm。",
                GeometryTolerance.NearOrthogonalAngleDegrees,
                GeometryTolerance.NearOrthogonalMaximumEndpointCorrection,
                GeometryTolerance.NearOrthogonalEndpointJoinTolerance);

            foreach (OrthogonalBoundaryLineDiagnostic diagnostic
                in normalization.LineDiagnostics)
            {
                builder.AppendLine();
                builder.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "第 {0} 条：{1} 轴，方向偏差 {2:0.######}°，最大端点修正 {3:0.###} mm；方向{4}，修正{5}。",
                    diagnostic.LineNumber,
                    diagnostic.NearestAxis == OrthogonalBoundaryLineAxis.X
                        ? "X"
                        : diagnostic.NearestAxis == OrthogonalBoundaryLineAxis.Y
                            ? "Y"
                            : "未知",
                    diagnostic.AngleDeviationDegrees,
                    diagnostic.MaximumEndpointCorrection,
                    diagnostic.WithinDirectionTolerance ? "通过" : "超限",
                    diagnostic.WithinCorrectionTolerance ? "通过" : "超限");
            }

            return builder.ToString();
        }

        public static string FormatCandidateStatus(
            LayoutCandidateState state,
            bool uniqueAutomatic = false,
            bool isRecommended = false)
        {
            string result;
            switch (state)
            {
                case LayoutCandidateState.AutomaticUsable:
                    result = uniqueAutomatic
                        ? "满足规则（唯一可自动采用）"
                        : "满足已确认规则（需比较选择）";
                    break;
                case LayoutCandidateState.RequiresUserDecision:
                    result = "低于推荐值（待项目复核）";
                    break;
                case LayoutCandidateState.RequiresProjectPolicy:
                    result = "项目最低尺寸待处理";
                    break;
                case LayoutCandidateState.InputUntrusted:
                    result = "不可使用（输入不能可靠验证）";
                    break;
                case LayoutCandidateState.Eliminated:
                    result = "不可使用（已淘汰）";
                    break;
                case LayoutCandidateState.CapabilityUnsupported:
                    result = "不可使用（超出当前能力）";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }

            return isRecommended
                ? "推荐首选；" + result
                : result;
        }

        public static string FormatCandidateReason(LayoutCandidateState state)
        {
            switch (state)
            {
                case LayoutCandidateState.AutomaticUsable:
                    return "满足当前已确认规则，并且是唯一可自动采用的方案。";
                case LayoutCandidateState.RequiresUserDecision:
                    return "存在多个可保留方案或需要留痕的例外，必须人工比较。";
                case LayoutCandidateState.RequiresProjectPolicy:
                    return "还缺少项目级规则，补齐前不能确认本方案。";
                case LayoutCandidateState.InputUntrusted:
                    return "当前房间、区域或连接关系不能被可靠验证。";
                case LayoutCandidateState.Eliminated:
                    return "本方案没有满足已经冻结的硬性规则。";
                case LayoutCandidateState.CapabilityUnsupported:
                    return "本方案超出当前阶段能够可靠处理的范围。";
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        public static string FormatCandidateReason(
            EvaluatedLayoutCandidate evaluated,
            double tileWidth = double.NaN,
            double tileHeight = double.NaN,
            double recommendedMinimumCutRatio =
                EngineeringLayoutRules.GuidedDefaultMinimumCutRatio)
        {
            if (evaluated == null)
            {
                throw new ArgumentNullException(nameof(evaluated));
            }

            if (evaluated.State == LayoutCandidateState.InputUntrusted
                && string.Equals(
                    evaluated.StateReason,
                    ConnectionEdgeMismatchReason,
                    StringComparison.Ordinal))
            {
                return "所选接合边不在两个铺贴区的共同边界上。"
                    + "请返回“在图中标明重点”，重选两区实际相接的整段边；"
                    + "不要选择房间外轮廓上的短折边。";
            }

            if (evaluated.HasRawCandidate
                && evaluated.State != LayoutCandidateState.Eliminated)
            {
                CandidateDiagnostic smallBoundaryCut = evaluated.Candidate
                    .Diagnostics.Where(item =>
                        item.Code == CandidateDiagnosticCode
                            .SmallBoundaryCutWithoutOppositeFullOrSeam
                        && item.ActualValue.HasValue)
                    .OrderByDescending(item => item.ActualValue.Value)
                    .FirstOrDefault();
                if (smallBoundaryCut != null)
                {
                    return FormatSmallBoundaryCutReason(
                        evaluated.Candidate,
                        smallBoundaryCut);
                }
            }

            if (evaluated.State == LayoutCandidateState.Eliminated
                && evaluated.HasRawCandidate)
            {
                CandidateDiagnostic groutTileBody = evaluated.Candidate
                    .RejectionReasons.FirstOrDefault(item =>
                        item.Code == CandidateDiagnosticCode
                            .GroutTileBodyUnavailable);
                if (groutTileBody != null)
                {
                    return "按当前灰缝宽度，某个排版位置的边砖没有足够空间保留实体砖体，因此本方案已淘汰；请比较其它方案，或返回“铺贴方式”调整排版相位。";
                }

                CandidateDiagnostic clippedPattern = evaluated.Candidate
                    .RejectionReasons.Where(item =>
                        item.Code == CandidateDiagnosticCode
                            .DoorControlledBoundaryPatternClippedBelowAbsoluteMinimum
                        && item.ActualValue.HasValue)
                    .OrderBy(item => item.ActualValue.Value)
                    .FirstOrDefault();
                if (clippedPattern != null)
                {
                    string clippedLocation = FormatMinimumLocation(
                        evaluated.Candidate,
                        clippedPattern.ActualValue.Value,
                        clippedPattern.Side);
                    string clippedThreshold = clippedPattern.Threshold.HasValue
                        ? string.Format(
                            CultureInfo.CurrentCulture,
                            "{0:0.###} mm",
                            clippedPattern.Threshold.Value)
                        : "当前项目绝对下限";
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "门洞控制的半砖/整砖—过渡砖相位已生成，但完整房间轮廓裁切后在{0}形成 {1:0.###} mm 的独立边界切砖，低于项目绝对下限 {2}，因此该模式候选已硬淘汰；请比较保留下来的安全方案。",
                        clippedLocation,
                        clippedPattern.ActualValue.Value,
                        clippedThreshold);
                }

                CandidateDiagnostic smallBoundaryCut = evaluated.Candidate
                    .Diagnostics.Where(item =>
                        item.Code == CandidateDiagnosticCode
                            .SmallBoundaryCutWithoutOppositeFullOrSeam
                        && item.ActualValue.HasValue)
                    .OrderByDescending(item => item.ActualValue.Value)
                    .FirstOrDefault();
                if (smallBoundaryCut != null)
                {
                    string location = FormatMinimumLocation(
                        evaluated.Candidate,
                        smallBoundaryCut.ActualValue.Value,
                        smallBoundaryCut.Side);
                    string axis = smallBoundaryCut.Axis == TileLayoutAxis.X
                        ? "东西方向"
                        : "南北方向";
                    string opposite = FormatOppositeBoundarySide(
                        smallBoundaryCut.Side,
                        axis);
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "复杂房间的{0}存在 {1:0.###} mm 的节材边砖（{2}，达到推荐下限且小于半砖），但对面{3}没有完整整砖或同轴准确墙角对缝；因此该候选不列入满足规则。",
                        axis,
                        smallBoundaryCut.ActualValue.Value,
                        location,
                        opposite);
                }

                CandidateDiagnostic largeBoundaryCut = evaluated.Candidate
                    .RejectionReasons.Where(item =>
                        item.Code == CandidateDiagnosticCode
                            .LargeBoundaryCutWithoutCornerOrSavingBand
                        && item.ActualValue.HasValue)
                    .OrderByDescending(item => item.ActualValue.Value)
                    .FirstOrDefault();
                if (largeBoundaryCut != null)
                {
                    string location = FormatMinimumLocation(
                        evaluated.Candidate,
                        largeBoundaryCut.ActualValue.Value,
                        largeBoundaryCut.Side);
                    string axis = largeBoundaryCut.Axis == TileLayoutAxis.X
                        ? "东西方向"
                        : "南北方向";
                    string half = largeBoundaryCut.Threshold.HasValue
                        ? largeBoundaryCut.Threshold.Value.ToString(
                            "0.###",
                            CultureInfo.CurrentCulture) + " mm"
                        : "半砖宽度";
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "复杂房间的{0}存在 {1:0.###} mm 的大于半砖非整边砖（{2}，半砖为 {3}），"
                            + "同轴没有准确墙角对缝、明确的过渡分配，也没有半砖或达到推荐下限且小于半砖的节材边砖；"
                            + "因此该候选不列入满足规则。",
                        axis,
                        largeBoundaryCut.ActualValue.Value,
                        location,
                        half);
                }

                CandidateDiagnostic minimumCut = evaluated.Candidate
                    .RejectionReasons.Where(item =>
                        item.Code == CandidateDiagnosticCode.MinimumCutNotMet
                        && item.ActualValue.HasValue)
                    .OrderBy(item => item.ActualValue.Value)
                    .FirstOrDefault();
                if (minimumCut != null)
                {
                    string location = FormatMinimumLocation(
                        evaluated.Candidate,
                        minimumCut.ActualValue.Value,
                        minimumCut.Side);
                    double? threshold = minimumCut.Threshold;
                    if (!threshold.HasValue
                        && !double.IsNaN(tileWidth)
                        && !double.IsInfinity(tileWidth)
                        && !double.IsNaN(tileHeight)
                        && !double.IsInfinity(tileHeight)
                        && Math.Abs(tileWidth - tileHeight)
                            <= GeometryTolerance.Coordinate)
                    {
                        threshold = tileWidth
                            * recommendedMinimumCutRatio;
                    }

                    if (!threshold.HasValue)
                    {
                        return string.Format(
                            CultureInfo.CurrentCulture,
                            "最窄处为{0}，低于当前建议下限 {1:0.###}T，"
                                + "因此本方案不能使用。"
                                + "请返回“在图中标明重点”检查门洞影响范围和门洞所在墙，"
                                + "或返回“铺贴方式”调整方案。",
                            location,
                            recommendedMinimumCutRatio);
                    }

                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "最窄处为{0}，小于当前项目下限 "
                            + "{1:0.###} mm，因此本方案不能使用。"
                            + "请返回“在图中标明重点”检查门洞影响范围和门洞所在墙，"
                            + "或返回“铺贴方式”调整方案。",
                        location,
                        threshold.Value);
                }
            }

            return FormatCandidateReason(evaluated.State);
        }

        public static string FormatCandidateOverview(
            EvaluatedLayoutCandidate evaluated)
        {
            return FormatCandidateOverview(evaluated, true);
        }

        public static string FormatCandidateOverview(
            EvaluatedLayoutCandidate evaluated,
            bool includeCornerQualityFacts)
        {
            if (evaluated == null || !evaluated.HasRawCandidate)
            {
                return "当前方案没有可展示的铺贴数据。";
            }

            LayoutCandidate candidate = evaluated.Candidate;
            string layoutKind;
            switch (candidate.Structure.Kind)
            {
                case OrthogonalCandidateKind.WholeRoomSinglePhase:
                    layoutKind = "整房连续铺贴";
                    break;
                case OrthogonalCandidateKind.MainSecondary:
                    layoutKind = "分区铺贴";
                    break;
                default:
                    layoutKind = "门洞控制铺贴";
                    break;
            }

            string bandTreatment = string.Join(
                "、",
                candidate.Structure.Connections
                    .Select(item => item.ProtrusionTreatment)
                    .Where(item => item != ProtrusionBandTreatment.None)
                    .Distinct()
                    .Select(item => item == ProtrusionBandTreatment.Absorbed
                        ? "突出窄带并入相邻砖"
                        : "突出窄带单独铺贴"));
            if (string.IsNullOrEmpty(bandTreatment))
            {
                bandTreatment = "没有突出窄带处理";
            }

            string boundarySummary = candidate.Metrics.BoundaryNonFullTileCount == 0
                ? "没有边界非整砖"
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "最窄边砖 {0:0.###} mm；边界非整砖 {1} 块",
                    candidate.Metrics.MinimumBoundaryBandWidth,
                    candidate.Metrics.BoundaryNonFullTileCount);

            BoundaryBandPlan xPlan = null;
            BoundaryBandPlan yPlan = null;
            string sideBands;
            if (candidate.TryGetAxisPlan(TileLayoutAxis.X, out xPlan)
                && candidate.TryGetAxisPlan(TileLayoutAxis.Y, out yPlan))
            {
                string actualBoundaryBands =
                    FormatActualRoomBoundaryBands(candidate);
                string phaseReferenceBands = string.Format(
                    CultureInfo.CurrentCulture,
                    "排版相位参考带（仅用于生成相位，不代表完整房间墙段）："
                        + "西侧 {0:0.###} mm、东侧 {1:0.###} mm、"
                        + "南侧 {2:0.###} mm、北侧 {3:0.###} mm。",
                    xPlan.GetBoundary(RoomSide.West).Width,
                    xPlan.GetBoundary(RoomSide.East).Width,
                    yPlan.GetBoundary(RoomSide.South).Width,
                    yPlan.GetBoundary(RoomSide.North).Width);
                string phaseReferenceKinds = string.Format(
                    CultureInfo.CurrentCulture,
                    "相位参考带类型：西侧 {0}、东侧 {1}、南侧 {2}、北侧 {3}；"
                        + "实际边砖若与参考带不同，是完整房间轮廓裁切后的实测结果。",
                    FormatBoundaryBandKind(
                        xPlan.GetBoundary(RoomSide.West).Kind),
                    FormatBoundaryBandKind(
                        xPlan.GetBoundary(RoomSide.East).Kind),
                    FormatBoundaryBandKind(
                        yPlan.GetBoundary(RoomSide.South).Kind),
                    FormatBoundaryBandKind(
                        yPlan.GetBoundary(RoomSide.North).Kind));
                sideBands = actualBoundaryBands + " " + phaseReferenceBands
                    + " " + phaseReferenceKinds;
            }
            else
            {
                sideBands = "当前方案没有完整的四侧轴带摘要；"
                    + "请以实际砖块和完整裁切诊断为准。";
            }
            string redistributionSummary = includeCornerQualityFacts
                ? FormatRedistributionSummary(candidate, xPlan, yPlan)
                : string.Empty;
            string minimumLocation = candidate.Metrics.BoundaryNonFullTileCount == 0
                ? string.Empty
                : " 完整图形裁切后的最窄位置："
                    + FormatMinimumLocation(
                        candidate,
                        candidate.Metrics.MinimumBoundaryBandWidth,
                        null)
                    + "。";

            string cornerQuality = includeCornerQualityFacts
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    " 墙角对缝：目标 {0} 个，双向交点 {1} 个，至少单缝命中 {2} 个。"
                        + "；2/3 安全对缝：双向 {3} 个、单缝 {4} 个；"
                        + "入口视觉范围内低于推荐下限 {5} 块，其中盲区 {6} 块",
                    candidate.Metrics.OptimizationTargetCornerCount,
                    candidate.Metrics.ExactGridIntersectionCornerCount,
                    candidate.Metrics.ExactSeamAlignedCornerCount,
                    candidate.Metrics.SafeDoubleWallCornerAlignmentCount,
                    candidate.Metrics.SafeSingleWallCornerAlignmentCount,
                    candidate.Metrics.EntranceVisualBelowRecommendedBoundaryTileCount,
                    candidate.Metrics.EntranceBlindBelowRecommendedBoundaryTileCount)
                : string.Empty;

            return string.Format(
                CultureInfo.CurrentCulture,
                "铺贴概况：{0}；地砖 {1} 块；{2}；连续异形砖 {3} 块；{4}。"
                    + " {5}{6}{7}{8}",
                layoutKind,
                candidate.Tiles.Count,
                boundarySummary,
                candidate.Metrics.ContinuousIrregularTileCount,
                bandTreatment,
                sideBands,
                minimumLocation,
                cornerQuality,
                redistributionSummary);
        }

        private static string FormatRedistributionSummary(
            LayoutCandidate candidate,
            BoundaryBandPlan xPlan,
            BoundaryBandPlan yPlan)
        {
            var axes = new List<string>();
            bool xHasDoorControlledSource = xPlan != null
                && (xPlan.UsesRedistribution
                    || HasDoorControlledRedistributionSource(
                        candidate,
                        TileLayoutAxis.X));
            bool yHasDoorControlledSource = yPlan != null
                && (yPlan.UsesRedistribution
                    || HasDoorControlledRedistributionSource(
                        candidate,
                        TileLayoutAxis.Y));
            if (xPlan != null && xPlan.UsesRedistribution)
            {
                axes.Add(FormatRedistributionAxis("东西轴", xPlan));
            }

            if (yPlan != null && yPlan.UsesRedistribution)
            {
                axes.Add(FormatRedistributionAxis("南北轴", yPlan));
            }

            if (candidate != null)
            {
                if (xPlan != null
                    && !xPlan.UsesRedistribution
                    && xHasDoorControlledSource)
                {
                    axes.Add(FormatDoorControlledPhaseAxis(
                        candidate,
                        "东西轴",
                        xPlan,
                        TileLayoutAxis.X));
                }

                if (yPlan != null
                    && !yPlan.UsesRedistribution
                    && yHasDoorControlledSource)
                {
                    axes.Add(FormatDoorControlledPhaseAxis(
                        candidate,
                        "南北轴",
                        yPlan,
                        TileLayoutAxis.Y));
                }

                // Wall-corner alternatives are intentionally retained when
                // the optional preference is enabled.  If an axis has no
                // redistribution source, make the frozen threshold rule
                // explicit instead of letting the preview look like it
                // silently ignored the door-controlled allocation.
                if (xPlan != null
                    && !xHasDoorControlledSource
                    && HasTargetCornerSource(candidate, TileLayoutAxis.X))
                {
                    axes.Add(FormatCornerAlternativeAxis(
                        candidate,
                        "东西轴",
                        TileLayoutAxis.X));
                }

                if (yPlan != null
                    && !yHasDoorControlledSource
                    && HasTargetCornerSource(candidate, TileLayoutAxis.Y))
                {
                    axes.Add(FormatCornerAlternativeAxis(
                        candidate,
                        "南北轴",
                        TileLayoutAxis.Y));
                }
            }

            if (axes.Count == 0)
            {
                return string.Empty;
            }

            bool hasDoorControlledSource = xHasDoorControlledSource
                || yHasDoorControlledSource;
            string prefix = hasDoorControlledSource
                ? " 门洞边界调整（与房间转角优先开关独立；转角质量仅在开关开启时参与排序）："
                : " 相位规则说明：";
            return prefix + string.Join("；", axes) + "。";
        }

        private static bool HasDoorControlledRedistributionSource(
            LayoutCandidate candidate,
            TileLayoutAxis axis)
        {
            return candidate != null
                && candidate.PhaseSources.Any(source =>
                    source.Axis == axis
                    && (source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryRedistribution
                        || source.Kind == GridPhaseSourceKind
                            .DoorControlledBoundaryPattern));
        }

        private static bool HasDoorControlledBoundaryPatternSource(
            LayoutCandidate candidate,
            TileLayoutAxis axis)
        {
            return candidate != null
                && candidate.PhaseSources.Any(source =>
                    source.Axis == axis
                    && source.Kind == GridPhaseSourceKind
                        .DoorControlledBoundaryPattern);
        }

        private static string FormatDoorControlledPhaseAxis(
            LayoutCandidate candidate,
            string axisName,
            BoundaryBandPlan plan,
            TileLayoutAxis axis)
        {
            if (!HasDoorControlledBoundaryPatternSource(candidate, axis))
            {
                return axisName
                    + "：已采用门洞边界的半砖—整砖—过渡砖相位（边砖带见上）";
            }

            bool mirroredFallback = candidate.PhaseSources.Any(source =>
                source.Axis == axis
                && source.Kind == GridPhaseSourceKind
                    .DoorControlledBoundaryPattern
                && source.Reason.IndexOf(
                    "mirrored",
                    StringComparison.OrdinalIgnoreCase) >= 0);
            string orientation = mirroredFallback
                ? "；本轴为镜像备选方向，需与门洞对向优先方向比较"
                : string.Empty;
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}：门洞边界调整模式；{1} {2:0.###} mm（{3}）、{4} {5:0.###} mm（{6}）{7}",
                axisName,
                FormatSide(plan.LowBoundary.Side),
                plan.LowBoundary.Width,
                FormatBoundaryBandKind(plan.LowBoundary.Kind),
                FormatSide(plan.HighBoundary.Side),
                plan.HighBoundary.Width,
                FormatBoundaryBandKind(plan.HighBoundary.Kind),
                orientation);
        }

        private static string FormatBoundaryBandKind(
            BoundaryBandKind kind)
        {
            switch (kind)
            {
                case BoundaryBandKind.FullTile:
                    return "整砖";
                case BoundaryBandKind.HalfTile:
                    return "半砖";
                case BoundaryBandKind.Transition:
                    return "过渡砖";
                case BoundaryBandKind.NaturalRemainder:
                    return "自然余量";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static bool HasTargetCornerSource(
            LayoutCandidate candidate,
            TileLayoutAxis axis)
        {
            return candidate != null
                && candidate.PhaseSources.Any(source =>
                    source.Axis == axis
                    && source.IsTargetCornerAnchor);
        }

        private static string FormatCornerAlternativeAxis(
            LayoutCandidate candidate,
            string axisName,
            TileLayoutAxis axis)
        {
            bool meetsRecommended = candidate != null
                && candidate.TileAssessments
                    .SelectMany(assessment => assessment.Measurements)
                    .Where(measurement => measurement.Axis == axis)
                    .All(measurement => measurement.Status
                        == ProjectCutStatus.MeetsRecommendedMinimum);
            return meetsRecommended
                ? axisName
                    + "：墙角锚定替代相位；自然余量达到推荐下限，未触发半砖—过渡砖重分配"
                : axisName
                    + "：墙角锚定替代相位；该轴仍有低于推荐下限的实际边界切砖，未采用半砖—过渡砖重分配";
        }

        private static string FormatRedistributionAxis(
            string axisName,
            BoundaryBandPlan plan)
        {
            string half = plan.HalfTileSide.HasValue
                ? FormatSide(plan.HalfTileSide.Value)
                : "未标注边";
            string transition = plan.TransitionTileSide.HasValue
                ? FormatSide(plan.TransitionTileSide.Value)
                : "未标注边";
            double halfWidth = plan.HalfTileSide.HasValue
                ? plan.GetBoundary(plan.HalfTileSide.Value).Width
                : 0.0;
            double transitionWidth = plan.TransitionTileSide.HasValue
                ? plan.GetBoundary(plan.TransitionTileSide.Value).Width
                : 0.0;
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}：半砖在{1} {2:0.###} mm、中央整砖 {3} 块、过渡砖在{4} {5:0.###} mm",
                axisName,
                half,
                halfWidth,
                plan.InteriorFullTileCount,
                transition,
                transitionWidth);
        }

        public static string FormatCandidateGenerationReport(
            CandidateGenerationReport report)
        {
            return FormatCandidateGenerationReport(report, true);
        }

        public static string FormatCandidateGenerationReport(
            CandidateGenerationReport report,
            bool includeCornerQualityFacts)
        {
            if (report == null)
            {
                report = CandidateGenerationReport.Empty;
            }

            if (!report.PhaseSearchEnabled)
            {
                return "候选搜索：当前沿用基础候选生成与顺序；"
                    + "本次未运行有上限的 X/Y 相位组合搜索；"
                    + (includeCornerQualityFacts
                        ? "墙角命中仅作只读诊断，不参与相位生成或候选排序。"
                        : "可选质量诊断未参与相位生成或候选排序。");
            }

            if (!report.WallCornerSearchEnabled)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "候选搜索：基础相位搜索已运行，{0}；"
                        + "X 相位 {1}、Y 相位 {2}、组合 {3}、生成替代 {4}、"
                        + "相位去重 {5}、支配淘汰 {6}、保留 {7}。"
                        + "{8}",
                    includeCornerQualityFacts
                        ? "墙角锚定优先未启用"
                        : "可选质量优先未启用",
                    report.XPhaseCount,
                    report.YPhaseCount,
                    report.PhaseCombinationCount,
                    report.GeneratedAlternativeCount,
                    report.DuplicatePhaseCount,
                    report.DominatedCandidateCount,
                    report.RetainedCandidateCount,
                    includeCornerQualityFacts
                        ? "如需墙角锚定相位及推荐排序，请勾选“优先考虑房间转角处的砖缝”。"
                        : "候选按基础方案原始顺序展示；原始指标仍保留在工程详情中。");
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "候选搜索：X 相位 {0}、Y 相位 {1}、组合 {2}、生成替代 {3}、"
                    + "相位去重 {4}、支配淘汰 {5}、保留 {6}。"
                    + "墙角锚定：目标 {7}、X/Y 锚定相位 {8}/{9}、"
                    + "双向/单轴组合 {10}/{11}、合并来源 {12}。"
                    + "上限状态：X={13}、Y={14}、组合={15}、墙角组合={16}、"
                    + "保留={17}；总体{18}截断。",
                report.XPhaseCount,
                report.YPhaseCount,
                report.PhaseCombinationCount,
                report.GeneratedAlternativeCount,
                report.DuplicatePhaseCount,
                report.DominatedCandidateCount,
                report.RetainedCandidateCount,
                report.OptimizationTargetCornerCount,
                report.XTargetAnchorPhaseCount,
                report.YTargetAnchorPhaseCount,
                report.DoubleAnchorCombinationCount,
                report.SingleAnchorCombinationCount,
                report.MergedPhaseSourceCount,
                report.XPhaseLimitReached ? "已触发" : "未触发",
                report.YPhaseLimitReached ? "已触发" : "未触发",
                report.CombinationLimitReached ? "已触发" : "未触发",
                report.AnchorCombinationLimitReached ? "已触发" : "未触发",
                report.RetentionLimitReached ? "已触发" : "未触发",
                report.IsTruncated ? "已" : "未");
        }

        public static string FormatDiagnosticTile(LayoutDrawingTile tile)
        {
            if (tile == null)
            {
                throw new ArgumentNullException(nameof(tile));
            }

            string sides = tile.BoundarySides.Count == 0
                ? "无外墙侧"
                : string.Join("/", tile.BoundarySides.Select(FormatSide));
            string type = tile.IsContinuousIrregular
                ? "连续异形砖（按整体轮廓测量）"
                : "独立边界砖";
            string measurements = tile.CutMeasurements.Count == 0
                ? "无适用边界切割测量"
                : string.Join("；", tile.CutMeasurements.Select(measurement =>
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0}轴实际 {1:0.###} mm / 推荐 {2:0.###} mm / 项目 {3} / {4}",
                        measurement.Axis,
                        measurement.ActualValue,
                        measurement.RecommendedMinimum,
                        measurement.ProjectAbsoluteMinimum.HasValue
                            ? measurement.ProjectAbsoluteMinimum.Value.ToString(
                                "0.###", CultureInfo.CurrentCulture) + " mm"
                            : "未确认",
                        FormatCutStatus(measurement.Status))));
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}：名义尺寸 {1:0.###} × {2:0.###} mm；边界侧={3}；"
                    + "类型={4}；{5}；原因={6}",
                tile.Id,
                tile.NominalWidth,
                tile.NominalHeight,
                sides,
                type,
                measurements,
                string.IsNullOrWhiteSpace(tile.AssessmentReason)
                    ? "无"
                    : tile.AssessmentReason);
        }

        public static string FormatNeutralRegionReference(
            NeutralOrthogonalRegionPartition partition)
        {
            if (partition == null)
            {
                return "房间结构参考：尚未生成。";
            }

            var lines = new List<string>
            {
                string.Format(
                    CultureInfo.CurrentCulture,
                    "房间结构参考：中性矩形区域 {0} 个，共享边 {1} 条。"
                        + "本信息不代表主区、次区、重要区或相位重置。",
                    partition.Regions.Count,
                    partition.Connections.Count)
            };
            foreach (NeutralOrthogonalRegion region in partition.Regions)
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}：X {1:0.###}～{2:0.###}，Y {3:0.###}～{4:0.###}，"
                        + "面积 {5:0.###} mm²",
                    region.Id,
                    region.Bounds.West,
                    region.Bounds.East,
                    region.Bounds.South,
                    region.Bounds.North,
                    region.Area));
            }

            foreach (NeutralRegionConnection connection in partition.Connections)
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "共享边：{0} ↔ {1}；({2:0.###},{3:0.###})～({4:0.###},{5:0.###})",
                    connection.FirstRegionId,
                    connection.SecondRegionId,
                    connection.SharedEdge.Start.X,
                    connection.SharedEdge.Start.Y,
                    connection.SharedEdge.End.X,
                    connection.SharedEdge.End.Y));
            }

            return string.Join(Environment.NewLine, lines);
        }

        public static string FormatWallCornerDiagnostics(
            LayoutDrawingPlan plan)
        {
            if (plan == null)
            {
                return "墙角—地砖缝诊断：请先临时查看一个可保留方案。";
            }

            var lines = new List<string>
            {
                string.Format(
                    CultureInfo.CurrentCulture,
                    "墙角—地砖缝诊断：目标反射角 {0} 个，双向交点 {1} 个，"
                        + "至少单缝准确命中 {2} 个。准确公差沿用 Core {3} mm；"
                        + "90°角只读，不参与择优。",
                    plan.WallCorners.Count(corner =>
                        corner.IsOptimizationTarget),
                    plan.WallCorners.Count(corner =>
                        corner.IsOptimizationTarget
                        && corner.IsExactGridIntersection),
                    plan.WallCorners.Count(corner =>
                        corner.IsOptimizationTarget
                        && corner.HasAnyExactSeam),
                    GeometryTolerance.Coordinate)
            };
            foreach (LayoutDrawingWallCorner corner in plan.WallCorners)
            {
                string status = corner.IsExactGridIntersection
                    ? "双向网格交点准确命中"
                    : corner.HasAnyExactSeam
                    ? "单条地砖缝准确命中"
                    : "未准确命中";
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}：({1:0.###}, {2:0.###})；{3}；{4}；"
                        + "最近竖缝 {5}，最近横缝 {6}。",
                    corner.Id,
                    corner.Position.X,
                    corner.Position.Y,
                    corner.IsOptimizationTarget
                        ? "270°目标转角"
                        : "90°只读转角",
                    status,
                    FormatOptionalDistance(
                        corner.NearestVerticalSeamDistance),
                    FormatOptionalDistance(
                        corner.NearestHorizontalSeamDistance)));
                if (corner.IsOptimizationTarget)
                {
                    lines.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "\u5899\u89d2\u5b89\u5168\u68c0\u67e5\uff1a\u7ad6\u7f1d\u4e24\u4fa7 {0}/{1} mm\uff0c\u6a2a\u7f1d\u4e24\u4fa7 {2}/{3} mm\uff1b\u5b89\u5168\u5bf9\u7f1d\uff1a\u53cc\u5411={4}\uff0c\u5355\u7f1d={5}\uff1b\u9608\u503c=2/3T\uff0c\u7b49\u53f7\u89c6\u4e3a\u6ee1\u8db3\u3002",
                        FormatOptionalSpan(corner.VerticalAdjacentSpanA),
                        FormatOptionalSpan(corner.VerticalAdjacentSpanB),
                        FormatOptionalSpan(corner.HorizontalAdjacentSpanA),
                        FormatOptionalSpan(corner.HorizontalAdjacentSpanB),
                        corner.IsSafeDoubleAlignment ? "\u662f" : "\u5426",
                        corner.IsSafeSingleAlignment ? "\u662f" : "\u5426"));
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatOptionalDistance(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
                    + " mm"
                : "无实际分格线";
        }

        private static string FormatOptionalSpan(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
                : "—";
        }

        public static string FormatEliminatedGroup(GuidedEliminatedGroup group)
        {
            switch (group)
            {
                case GuidedEliminatedGroup.BelowProjectAbsoluteMinimum:
                    return "低于项目绝对下限";
                case GuidedEliminatedGroup.ParetoDominated:
                    return "被其他候选客观支配";
                case GuidedEliminatedGroup.SearchTruncated:
                    return "搜索或保留上限截断";
                case GuidedEliminatedGroup.SmallBoundaryCutNeedsOppositeFullOrSeam:
                    return "小于半砖节材边砖缺少对侧完整整砖/对缝";
                case GuidedEliminatedGroup.UnjustifiedLargeBoundaryCut:
                    return "大于半砖且无对缝/节材用途";
                case GuidedEliminatedGroup.OtherHardFailure:
                    return "其他硬失败";
                default:
                    throw new ArgumentOutOfRangeException(nameof(group));
            }
        }

        private static string FormatCutStatus(ProjectCutStatus status)
        {
            switch (status)
            {
                case ProjectCutStatus.MeetsRecommendedMinimum:
                    return "自动满足";
                case ProjectCutStatus.RequiresProjectPolicy:
                    return "项目规则缺失";
                case ProjectCutStatus.RequiresUserReview:
                    return "待项目复核";
                case ProjectCutStatus.BelowProjectAbsoluteMinimum:
                    return "低于项目绝对下限";
                case ProjectCutStatus.NotApplicableFullTile:
                    return "整砖不适用";
                case ProjectCutStatus.InteriorNonFullDiagnostic:
                    return "内部非整砖诊断";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static string FormatSmallBoundaryCutReason(
            LayoutCandidate candidate,
            CandidateDiagnostic diagnostic)
        {
            string location = FormatMinimumLocation(
                candidate,
                diagnostic.ActualValue.Value,
                diagnostic.Side);
            string axis = diagnostic.Axis == TileLayoutAxis.X
                ? "东西方向"
                : "南北方向";
            string opposite = FormatOppositeBoundarySide(
                diagnostic.Side,
                axis);
            return string.Format(
                CultureInfo.CurrentCulture,
                "复杂房间的{0}存在 {1:0.###} mm 的节材边砖（{2}，达到推荐下限且小于半砖），但对面{3}没有整砖或同轴准确墙角对缝；因此该候选只能待项目复核，不能列入满足规则。",
                axis,
                diagnostic.ActualValue.Value,
                location,
                opposite);
        }

        private static string FormatMinimumLocation(
            LayoutCandidate candidate,
            double width,
            RoomSide? knownSide)
        {
            if (knownSide.HasValue)
            {
                return FormatSide(knownSide.Value) + " "
                    + width.ToString("0.###", CultureInfo.CurrentCulture)
                    + " mm";
            }

            var matchingSides = new List<string>();
            foreach (TileFootprint tile in candidate.Tiles)
            {
                if (tile.BoundarySides.Count == 0)
                {
                    continue;
                }

                bool matchesX = tile.BoundarySides.Any(side =>
                    (side == RoomSide.West || side == RoomSide.East)
                    && Math.Abs(tile.NominalWidth - width)
                        <= GeometryTolerance.Coordinate);
                bool matchesY = tile.BoundarySides.Any(side =>
                    (side == RoomSide.South || side == RoomSide.North)
                    && Math.Abs(tile.NominalHeight - width)
                        <= GeometryTolerance.Coordinate);
                if (!matchesX && !matchesY)
                {
                    continue;
                }

                foreach (RoomSide side in tile.BoundarySides)
                {
                    bool isMatchingSide =
                        (matchesX
                            && (side == RoomSide.West
                                || side == RoomSide.East))
                        || (matchesY
                            && (side == RoomSide.South
                                || side == RoomSide.North));
                    if (isMatchingSide
                        && !matchingSides.Contains(FormatSide(side)))
                    {
                        matchingSides.Add(FormatSide(side));
                    }
                }
            }

            string place = matchingSides.Count == 0
                ? "异形转角或边界裁切处"
                : string.Join("、", matchingSides);
            return place + " "
                + width.ToString("0.###", CultureInfo.CurrentCulture)
                + " mm";
        }

        private static string FormatActualRoomBoundaryBands(
            LayoutCandidate candidate)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "完整房间最外包络实际边砖：西墙 {0}、东墙 {1}、"
                    + "南墙 {2}、北墙 {3}。",
                FormatActualBoundaryWidths(candidate, RoomSide.West, true),
                FormatActualBoundaryWidths(candidate, RoomSide.East, true),
                FormatActualBoundaryWidths(candidate, RoomSide.South, false),
                FormatActualBoundaryWidths(candidate, RoomSide.North, false));
        }

        private static string FormatActualBoundaryWidths(
            LayoutCandidate candidate,
            RoomSide side,
            bool verticalBoundary)
        {
            var widths = new List<double>();
            foreach (TileFootprint tile in candidate.Tiles)
            {
                if (!tile.BoundarySides.Contains(side))
                {
                    continue;
                }

                double width = verticalBoundary
                    ? tile.NominalWidth
                    : tile.NominalHeight;
                if (width <= GeometryTolerance.Coordinate
                    || widths.Any(existing =>
                        Math.Abs(existing - width)
                            <= GeometryTolerance.Coordinate))
                {
                    continue;
                }

                widths.Add(width);
            }

            if (widths.Count == 0)
            {
                return "未从实际砖块确认";
            }

            widths.Sort();
            string formatted = string.Join(
                " / ",
                widths.Select(width => width.ToString(
                    "0.###",
                    CultureInfo.CurrentCulture)));
            return formatted + " mm";
        }

        private static string FormatSide(RoomSide side)
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

        private static string FormatOppositeBoundarySide(
            RoomSide? side,
            string axis)
        {
            if (side.HasValue)
            {
                return FormatSide(Opposite(side.Value));
            }

            return axis == "东西方向"
                ? "对侧东西墙"
                : "对侧南北墙";
        }

        private static RoomSide Opposite(RoomSide side)
        {
            switch (side)
            {
                case RoomSide.West:
                    return RoomSide.East;
                case RoomSide.East:
                    return RoomSide.West;
                case RoomSide.South:
                    return RoomSide.North;
                case RoomSide.North:
                    return RoomSide.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side));
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
                CultureInfo.CurrentCulture,
                "人工决定记录：方案={0}；策略版本={1}；模式={2}；"
                    + "例外接受={3}；原因={4}；非自动合规。",
                record.CandidateId,
                record.PolicyVersion,
                FormatMode(mode),
                record.AcceptsException ? "是" : "否",
                record.Reason);
        }

        public static string FormatDecisionRecordForUser(
            DecisionRecord record,
            LayoutDecisionMode mode)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "人工确认记录：已保存；项目规则版本={0}；使用方式={1}；"
                    + "原因={2}；人工确认，不代表自动合规。",
                record.PolicyVersion,
                FormatMode(mode),
                record.Reason);
        }

        public static string PreviewDisabledReason(
            OrthogonalDecisionPaletteSession palette)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            if (palette.State
                    == OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady
                && !palette.CanRequestPreview)
            {
                return "当前查看的不是已记录方案；请重新选择已记录方案后再请求预览。";
            }

            switch (palette.State)
            {
                case OrthogonalDecisionPaletteState.Empty:
                    return "请先在图中选择房间边界。";
                case OrthogonalDecisionPaletteState.NeedsProjectPolicy:
                    return "请先补齐面板显示的项目规则。";
                case OrthogonalDecisionPaletteState.NeedsRoomSemantics:
                    return "请先完成面板显示的房间和门洞信息。";
                case OrthogonalDecisionPaletteState.NeedsCandidateSelection:
                    return "请选择一个可保留方案；人工复核候选无需填写原因，正式写回前还会最终确认。";
                case OrthogonalDecisionPaletteState.Blocked:
                    return "当前输入不可预览，请按面板提示修改或重选。";
                case OrthogonalDecisionPaletteState.PreviewRequested:
                    return "临时铺贴图正在显示或等待刷新。";
                case OrthogonalDecisionPaletteState.AutomaticPreviewReady:
                case OrthogonalDecisionPaletteState.ManualReviewPreviewReady:
                case OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady:
                case OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady:
                    return string.Empty;
                default:
                    throw new ArgumentOutOfRangeException(nameof(palette));
            }
        }

        public static string FormatMode(LayoutDecisionMode mode)
        {
            return mode == LayoutDecisionMode.ControlledProduction
                ? "项目执行"
                : "方案研究";
        }

        public static string FormatIntent(RoomLayoutIntent? intent)
        {
            if (!intent.HasValue)
            {
                return "尚未选择";
            }

            switch (intent.Value)
            {
                case RoomLayoutIntent.WholeRoomSinglePhase:
                    return "整房连续铺贴";
                case RoomLayoutIntent.MainSecondary:
                    return "分区铺贴（主要区 + 相邻区）";
                case RoomLayoutIntent.Unsupported:
                    return "当前不支持";
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }

        public static string FormatPaletteState(
            OrthogonalDecisionPaletteState state)
        {
            switch (state)
            {
                case OrthogonalDecisionPaletteState.Empty:
                    return "尚未载入";
                case OrthogonalDecisionPaletteState.NeedsProjectPolicy:
                    return "需要补充项目规则";
                case OrthogonalDecisionPaletteState.NeedsRoomSemantics:
                    return "需要补充图面信息";
                case OrthogonalDecisionPaletteState.NeedsCandidateSelection:
                    return "需要人工选择";
                case OrthogonalDecisionPaletteState.AutomaticPreviewReady:
                    return "自动推荐已就绪";
                case OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady:
                    return "按图面确认已就绪（不代表自动合规）";
                case OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady:
                    return "人工确认记录已保存（不代表自动合规）";
                case OrthogonalDecisionPaletteState.PreviewRequested:
                    return "临时铺贴图已请求";
                case OrthogonalDecisionPaletteState.Blocked:
                    return "当前不可用";
                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        public static bool TryParsePositiveMillimeters(
            string text,
            out double value)
        {
            return double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value > GeometryTolerance.Coordinate;
        }

        public static bool TryParseNonNegativeMillimeters(
            string text,
            out double value)
        {
            return double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0.0;
        }
    }
}
