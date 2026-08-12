using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    public enum OrthogonalDecisionGuideStep
    {
        Room = 1,
        Project = 2,
        Intent = 3,
        Geometry = 4,
        Candidates = 5,
        Summary = 6
    }

    public enum OrthogonalDecisionGuideAction
    {
        SelectRoom,
        SelectControlRegion,
        SelectControlDoor,
        SelectMainRegion,
        SelectSecondaryRegion,
        SelectConnectionEdge
    }

    public enum OrthogonalDecisionPreviewState
    {
        None,
        DisplayRequested,
        Visible,
        RefreshRequired,
        Clearing
    }

    public enum OrthogonalDecisionPreviewAction
    {
        Show,
        Refresh,
        Clear
    }

    public enum GuidedCandidateGroup
    {
        AutomaticRecommendation,
        ManualConfirmation,
        ProjectRuleMissing,
        Unavailable
    }

    public enum GuidedEliminatedGroup
    {
        BelowProjectAbsoluteMinimum,
        ParetoDominated,
        SearchTruncated,
        SmallBoundaryCutNeedsOppositeFullOrSeam,
        UnjustifiedLargeBoundaryCut,
        OtherHardFailure
    }

    public enum GuidedCornerAlignmentGroup
    {
        ExactGridIntersection,
        ExactSingleSeam,
        NoExactAlignment,
        NotApplicable
    }

    public sealed class GuidedRequirementPresentation
    {
        internal GuidedRequirementPresentation(
            DecisionRequirement requirement,
            string message,
            string nextAction,
            string engineeringDetails)
        {
            Requirement = requirement;
            Message = message;
            NextAction = nextAction;
            EngineeringDetails = engineeringDetails;
        }

        public DecisionRequirement Requirement { get; }

        public string Message { get; }

        public string NextAction { get; }

        public string EngineeringDetails { get; }
    }

    public sealed class GuidedCandidatePresentation
    {
        internal GuidedCandidatePresentation(
            EvaluatedLayoutCandidate candidate,
            int originalIndex,
            GuidedCandidateGroup group,
            GuidedEliminatedGroup? eliminatedGroup,
            GuidedCornerAlignmentGroup cornerAlignmentGroup,
            string title,
            string status,
            bool isRecommended = false,
            int recommendationRank = 0)
        {
            Candidate = candidate;
            OriginalIndex = originalIndex;
            Group = group;
            EliminatedGroup = eliminatedGroup;
            CornerAlignmentGroup = cornerAlignmentGroup;
            Title = title;
            Status = status;
            IsRecommended = isRecommended;
            RecommendationRank = recommendationRank;
        }

        public EvaluatedLayoutCandidate Candidate { get; }

        public int OriginalIndex { get; }

        public GuidedCandidateGroup Group { get; }

        public GuidedEliminatedGroup? EliminatedGroup { get; }

        public GuidedCornerAlignmentGroup CornerAlignmentGroup { get; }

        public string Title { get; }

        public string Status { get; }

        public bool IsRecommended { get; }

        public int RecommendationRank { get; }
    }

    public sealed class OrthogonalDecisionGuidedWorkflow
    {
        public const int EliminatedCandidatePageSize = 50;
        public const string OrdinaryPolicyVersion = "项目规则-1";

        private readonly OrthogonalDecisionInputSession input =
            new OrthogonalDecisionInputSession();
        private readonly OrthogonalDecisionPaletteSession palette =
            new OrthogonalDecisionPaletteSession();
        private AxisAlignedRectangle mainRegionDraft;
        private AxisAlignedRectangle secondaryRegionDraft;
        private string selectedCandidateId;
        private LayoutDrawingPlan previewPlan;
        private bool formalWritebackAcknowledged;
        private string formalWritebackCandidateId;
        private bool formalWritebackCompleted;
        private int formalWritebackLineCount;
        private bool automaticDimensioningEnabled = true;
        private bool roomFeatureDimensioningEnabled;
        private LayoutDrawingDimensionPlacement dimensionPlacement =
            LayoutDrawingDimensionPlacement.InsideRoom;
        private LayoutDrawingColorSettings colorSettings =
            LayoutDrawingColorSettings.Default;
        private readonly List<GuidedCandidatePresentation> candidates =
            new List<GuidedCandidatePresentation>();
        private IReadOnlyList<GuidedCandidatePresentation> candidateView;
        private IReadOnlyList<GuidedRequirementPresentation> requirementView =
            new ReadOnlyCollection<GuidedRequirementPresentation>(
                new List<GuidedRequirementPresentation>());
        private IReadOnlyList<GuidedCandidatePresentation>
            ruleSatisfiedCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    new List<GuidedCandidatePresentation>());
        private IReadOnlyList<GuidedCandidatePresentation> reviewCandidateView =
            new ReadOnlyCollection<GuidedCandidatePresentation>(
                new List<GuidedCandidatePresentation>());
        private IReadOnlyList<GuidedCandidatePresentation>
            missingRuleCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    new List<GuidedCandidatePresentation>());
        private IReadOnlyList<GuidedCandidatePresentation>
            filteredEliminatedCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    new List<GuidedCandidatePresentation>());
        private IReadOnlyList<GuidedCandidatePresentation>
            eliminatedCandidatePageView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    new List<GuidedCandidatePresentation>());
        private int eliminatedCandidateCount;
        private int filteredEliminatedCandidateCount;
        private int eliminatedPageCount = 1;
        private bool ordinarySummaryRendered;
        private string ordinarySummaryView;
        private EngineeringOrthogonalDecisionResult ordinarySummaryResult;
        private LayoutPolicyProfile ordinarySummaryPolicy;
        private DecisionRecord ordinarySummaryRecord;
        private DoorOpening ordinarySummaryDoor;
        private LayoutDecisionMode ordinarySummaryMode;
        private double ordinarySummaryTileWidth;
        private double ordinarySummaryTileHeight;
        private bool ordinarySummaryHasRoom;
        private bool ordinarySummaryRecommendedMinimumConfirmed;
        private bool ordinarySummaryFormalWritebackCompleted;
        private bool ordinarySummaryFormalWritebackAcknowledged;
        private int ordinarySummaryFormalWritebackLineCount;
        private bool ordinarySummaryAutomaticDimensioningEnabled;
        private bool ordinarySummaryRoomFeatureDimensioningEnabled;
        private LayoutDrawingDimensionPlacement ordinarySummaryDimensionPlacement;
        private LayoutDrawingColorSettings ordinarySummaryColorSettings;
        private GuidedEliminatedGroup? eliminatedFilter;
        private int eliminatedPageIndex;
        private string selectedDiagnosticTileId;
        private bool showAllAssessedBoundaryTiles;
        private bool showNeutralRegions;
        private bool showWallCornerDiagnostics;
        private bool recommendedMinimumConfirmed;

        public OrthogonalDecisionGuidedWorkflow()
        {
            candidateView = new ReadOnlyCollection<GuidedCandidatePresentation>(
                candidates);
            ActiveStep = OrthogonalDecisionGuideStep.Room;
            Notice = "请先填写砖规格，然后点击“在图中选择房间边界”。";
        }

        public OrthogonalDecisionInputSession Input => input;

        public OrthogonalDecisionPaletteSession Palette => palette;

        public OrthogonalDecisionGuideStep ActiveStep { get; private set; }

        public OrthogonalDecisionGuideAction? PendingAction { get; private set; }

        public bool IsCompleted { get; private set; }

        public string Notice { get; private set; }

        public string InvalidationNotice { get; private set; }

        public AxisAlignedRectangle MainRegionDraft => mainRegionDraft;

        public AxisAlignedRectangle SecondaryRegionDraft => secondaryRegionDraft;

        public bool ShowsMainSecondaryControls =>
            input.LayoutIntent == RoomLayoutIntent.MainSecondary;

        public bool HasWriteAuthorization
        {
            get
            {
                IReadOnlyList<LayoutDrawingLine> lines;
                string rejectionReason;
                return TryGetAuthorizedFormalLines(
                    out lines,
                    out rejectionReason);
            }
        }

        public bool RecommendedMinimumConfirmed => recommendedMinimumConfirmed;

        public LayoutDrawingPlan PreviewPlan => previewPlan;

        public string SelectedDiagnosticTileId => selectedDiagnosticTileId;

        public bool ShowAllAssessedBoundaryTiles =>
            showAllAssessedBoundaryTiles;

        public bool ShowNeutralRegions => showNeutralRegions;

        public bool ShowWallCornerDiagnostics => showWallCornerDiagnostics;

        public bool AutomaticDimensioningEnabled => automaticDimensioningEnabled;

        public bool RoomFeatureDimensioningEnabled =>
            roomFeatureDimensioningEnabled;

        public LayoutDrawingDimensionPlacement DimensionPlacement =>
            dimensionPlacement;

        public LayoutDrawingColorSettings ColorSettings => colorSettings;

        public bool PreferWallCornerAlignment =>
            input.PreferWallCornerAlignment;

        public string BoundaryNormalizationNotice
        {
            get
            {
                if (input.BoundaryWasNormalized
                    && input.BoundaryNormalization != null)
                {
                    return input.BoundaryNormalization.Message;
                }

                if (input.BoundaryNormalization != null
                    && input.BoundaryNormalization.Status
                        == OrthogonalBoundaryNormalizationStatus.Rejected
                    && input.BoundaryNormalization.LineDiagnostics.Count > 0)
                {
                    return input.BoundaryNormalization.Message;
                }

                return string.Empty;
            }
        }

        public GuidedEliminatedGroup? EliminatedFilter => eliminatedFilter;

        public int EliminatedPageIndex => eliminatedPageIndex;

        public TimeSpan LastCandidatePresentationBuildDuration
        {
            get;
            private set;
        }

        public OrthogonalDecisionPreviewState PreviewState { get; private set; }

        public bool CanRefreshPreview => previewPlan != null
            && (PreviewState == OrthogonalDecisionPreviewState.Visible
                || PreviewState == OrthogonalDecisionPreviewState.RefreshRequired);

        public bool FormalWritebackCompleted => formalWritebackCompleted;

        public bool FormalWritebackAwaitingCompletion =>
            formalWritebackAcknowledged;

        public int FormalWritebackLineCount => formalWritebackLineCount;

        public bool CanRequestFormalWriteback
        {
            get
            {
                string disabledReason;
                return CanRequestFormalWritebackInternal(out disabledReason);
            }
        }

        public IReadOnlyList<GuidedRequirementPresentation> Requirements =>
            requirementView;

        public IReadOnlyList<GuidedCandidatePresentation> Candidates =>
            candidateView;

        public IReadOnlyList<GuidedCandidatePresentation> RuleSatisfiedCandidates =>
            ruleSatisfiedCandidateView;

        public IReadOnlyList<GuidedCandidatePresentation> ReviewCandidates =>
            reviewCandidateView;

        public IReadOnlyList<GuidedCandidatePresentation> MissingRuleCandidates =>
            missingRuleCandidateView;

        public int EliminatedCandidateCount => eliminatedCandidateCount;

        public int FilteredEliminatedCandidateCount =>
            filteredEliminatedCandidateCount;

        public int EliminatedPageCount => eliminatedPageCount;

        public IReadOnlyList<GuidedCandidatePresentation> EliminatedCandidatePage =>
            eliminatedCandidatePageView;

        public bool AllCandidatesUnavailable =>
            Candidates.Count > 0
            && Candidates.All(item =>
                item.Group == GuidedCandidateGroup.Unavailable);

        public bool DoorSelectionCoversEntireBoundarySegment()
        {
            if (input.Room == null || input.ControlDoor == null)
            {
                return false;
            }

            IReadOnlyList<Point3D> vertices = input.Room.Vertices;
            for (int index = 0; index < vertices.Count; index++)
            {
                Point3D start = vertices[index];
                Point3D end = vertices[(index + 1) % vertices.Count];
                double rangeStart;
                double rangeEnd;
                if (!TryGetDoorWallSegmentRange(
                    input.Room,
                    input.ControlDoor.Wall,
                    start,
                    end,
                    out rangeStart,
                    out rangeEnd))
                {
                    continue;
                }

                if (Math.Abs(input.ControlDoor.AlongWallStart - rangeStart)
                        <= GeometryTolerance.Coordinate
                    && Math.Abs(input.ControlDoor.AlongWallEnd - rangeEnd)
                        <= GeometryTolerance.Coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ConnectionSelectionDoesNotMatchValidatedBoundary()
        {
            return input.Result != null
                && input.Result.Requirements.Any(
                    OrthogonalDecisionGuidedText.IsConnectionEdgeMismatch);
        }

        public string DescribeExpectedConnectionEdge()
        {
            if (input.MainSecondary == null)
            {
                return "主要铺贴区与相邻铺贴区实际相接的整段边";
            }

            AxisAlignedRectangle main = input.MainSecondary.MainRegion;
            AxisAlignedRectangle secondary = input.MainSecondary.SecondaryRegion;
            bool horizontalOverlap = Math.Min(main.East, secondary.East)
                    - Math.Max(main.West, secondary.West)
                > GeometryTolerance.Coordinate;
            bool verticalOverlap = Math.Min(main.North, secondary.North)
                    - Math.Max(main.South, secondary.South)
                > GeometryTolerance.Coordinate;
            if (horizontalOverlap
                && GeometryTolerance.NearlyEqual(main.South, secondary.North))
            {
                return "主要铺贴区下边与相邻铺贴区上边重合的整段水平边";
            }

            if (horizontalOverlap
                && GeometryTolerance.NearlyEqual(main.North, secondary.South))
            {
                return "主要铺贴区上边与相邻铺贴区下边重合的整段水平边";
            }

            if (verticalOverlap
                && GeometryTolerance.NearlyEqual(main.West, secondary.East))
            {
                return "主要铺贴区左边与相邻铺贴区右边重合的整段竖直边";
            }

            if (verticalOverlap
                && GeometryTolerance.NearlyEqual(main.East, secondary.West))
            {
                return "主要铺贴区右边与相邻铺贴区左边重合的整段竖直边";
            }

            return "主要铺贴区与相邻铺贴区实际相接的整段边";
        }

        public string GetCurrentGuidance()
        {
            if (!input.HasRoom)
            {
                return "当前待办：在图中选择房间边界。";
            }

            if (ConnectionSelectionDoesNotMatchValidatedBoundary())
            {
                return "• 所选接合边不在两个铺贴区的共同边界上。下一步："
                    + "返回“在图中标明重点”，重选"
                    + DescribeExpectedConnectionEdge()
                    + "；不要选择房间外轮廓上的短折边。";
            }

            if (Requirements.Count > 0)
            {
                return string.Join(
                    Environment.NewLine,
                    Requirements.Select(item =>
                        "• " + item.Message + " 下一步：" + item.NextAction));
            }

            if (AllCandidatesUnavailable)
            {
                GuidedCandidatePresentation first = Candidates[0];
                string doorGuidance = DoorSelectionCoversEntireBoundarySegment()
                    ? "当前门洞两点正好是整段墙线的两个端点；"
                        + "程序会把整段墙理解成门洞。请返回“在图中标明重点”，"
                        + "重选实际门洞两侧边缘。"
                    : string.Empty;
                return doorGuidance
                    + "当前输入信息已完整，但所有方案均不可使用。"
                    + OrthogonalDecisionGuidedText.FormatCandidateReason(
                        first.Candidate,
                        input.TileWidth,
                        input.TileHeight,
                        input.Policy == null
                            ? EngineeringLayoutRules.GuidedDefaultMinimumCutRatio
                            : input.Policy.DefaultMinimumCutRatio);
            }

            if (Candidates.Count == 0)
            {
                return "当前输入信息已完整，但尚未生成方案。"
                    + "请返回“在图中标明重点”检查门洞影响范围和门洞，或修改铺贴方式。";
            }

            return "当前没有未处理的缺失项。请查看方案或汇总。";
        }

        public OrthogonalRoomValidationResult LoadBoundary(
            IReadOnlyCollection<LineSegment3D> boundaryLines,
            double tileWidth,
            double tileHeight,
            double groutWidthMm = 0.0,
            double plasterThicknessMm = 0.0)
        {
            InvalidatePreview("房间边界已修改，旧的图面预览已失效。" );
            OrthogonalRoomValidationResult result = input.LoadBoundary(
                boundaryLines,
                tileWidth,
                tileHeight,
                groutWidthMm,
                plasterThicknessMm);
            mainRegionDraft = null;
            secondaryRegionDraft = null;
            selectedCandidateId = null;
            selectedDiagnosticTileId = null;
            recommendedMinimumConfirmed = false;
            IsCompleted = false;
            InvalidationNotice = string.Empty;
            SyncPalette();
            if (result.IsValid)
            {
                ActiveStep = OrthogonalDecisionGuideStep.Project;
                Notice = input.BoundaryWasNormalized
                    ? input.BoundaryNormalization.Message
                        + " 下一步请确认项目铺贴规则和使用方式。"
                    : "房间边界已通过验证。下一步请确认项目铺贴规则和使用方式。";
            }
            else
            {
                ActiveStep = OrthogonalDecisionGuideStep.Room;
                if (result.Error
                    == OrthogonalRoomValidationError.InvalidFinishedFace)
                {
                    Notice = "抹灰完成面无效；旧预览已失效，未生成候选，也未写回任何对象。"
                        + Environment.NewLine
                        + result.ErrorMessage;
                }
                else
                {
                    Notice = string.IsNullOrWhiteSpace(BoundaryNormalizationNotice)
                        ? "所选边界无效，请按提示修改后重新在图中选择。"
                        : BoundaryNormalizationNotice
                            + " 请按提示修改后重新在图中选择。";
                }
            }

            return result;
        }

        public void ApplyLayoutSettings(
            double tileWidth,
            double tileHeight,
            double groutWidthMm,
            double plasterThicknessMm)
        {
            bool invalidated = input.DecisionRecord != null;
            input.SetTileDimensions(tileWidth, tileHeight);
            input.SetGroutWidth(groutWidthMm);
            input.SetPlasterThickness(plasterThicknessMm);
            if (input.MainSecondary != null)
            {
                mainRegionDraft = input.MainSecondary.MainRegion;
                secondaryRegionDraft = input.MainSecondary.SecondaryRegion;
            }
            else
            {
                mainRegionDraft = null;
                secondaryRegionDraft = null;
            }

            AfterDecisionInputChanged(
                invalidated,
                "灰缝或抹灰完成面设置已修改，旧的预览和人工确认记录已失效。",
                "请重新检查门洞、分区和候选方案，然后重新显示预览。 ");
            if (!input.HasRoom
                && !string.IsNullOrWhiteSpace(
                    input.FinishedFaceErrorMessage))
            {
                ActiveStep = OrthogonalDecisionGuideStep.Room;
                Notice = "抹灰完成面无效；旧预览已失效，未生成候选，也未写回任何对象。"
                    + Environment.NewLine
                    + input.FinishedFaceErrorMessage;
            }
        }

        public void ApplyProjectSettings(
            LayoutDecisionMode mode,
            string policyVersion,
            double? secondAbsoluteMinimum,
            bool recommendedMinimumIsConfirmed = true,
            ProjectAbsoluteMinimumMode projectAbsoluteMinimumMode =
                ProjectAbsoluteMinimumMode.NotDecided,
            double recommendedMinimumCutRatio =
                EngineeringLayoutRules.DefaultMinimumCutRatio,
            double? projectAbsoluteMinimumRatio = null)
        {
            if (!recommendedMinimumIsConfirmed)
            {
                throw new ArgumentException(
                    "The recommended minimum ratio must be confirmed before project rules are applied.",
                    nameof(recommendedMinimumIsConfirmed));
            }

            if (string.IsNullOrWhiteSpace(policyVersion))
            {
                throw new ArgumentException(
                    "A project policy version is required.",
                    nameof(policyVersion));
            }

            EngineeringLayoutRules.ValidateMinimumCutRatio(
                recommendedMinimumCutRatio,
                nameof(recommendedMinimumCutRatio));

            if (projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.VisualConfirmation
                && (secondAbsoluteMinimum.HasValue
                    || projectAbsoluteMinimumRatio.HasValue))
            {
                throw new ArgumentException(
                    "Visual confirmation mode cannot carry a numeric project absolute minimum.",
                    nameof(secondAbsoluteMinimum));
            }

            if (projectAbsoluteMinimumMode == ProjectAbsoluteMinimumMode.Numeric
                && (!secondAbsoluteMinimum.HasValue
                    || projectAbsoluteMinimumRatio.HasValue))
            {
                throw new ArgumentException(
                    "Numeric project absolute minimum mode requires a value.",
                    nameof(secondAbsoluteMinimum));
            }

            if (projectAbsoluteMinimumMode
                    == ProjectAbsoluteMinimumMode.NumericRatio
                && (!projectAbsoluteMinimumRatio.HasValue
                    || secondAbsoluteMinimum.HasValue))
            {
                throw new ArgumentException(
                    "Numeric-ratio project absolute minimum mode requires a ratio value.",
                    nameof(projectAbsoluteMinimumRatio));
            }

            if (secondAbsoluteMinimum.HasValue
                && projectAbsoluteMinimumRatio.HasValue)
            {
                throw new ArgumentException(
                    "Set either a millimetre minimum or a ratio minimum, not both.",
                    nameof(projectAbsoluteMinimumRatio));
            }

            if (secondAbsoluteMinimum.HasValue && input.HasRoom)
            {
                double smallestRecommended = Math.Min(
                    input.TileWidth * recommendedMinimumCutRatio,
                    input.TileHeight * recommendedMinimumCutRatio);
                if (secondAbsoluteMinimum.Value
                    > smallestRecommended + GeometryTolerance.Coordinate)
                {
                    throw new ArgumentException(
                        "The project absolute minimum cannot exceed the recommended minimum for either tile axis.",
                        nameof(secondAbsoluteMinimum));
                }
            }

            if (projectAbsoluteMinimumRatio.HasValue)
            {
                EngineeringLayoutRules.ValidateMinimumCutRatio(
                    projectAbsoluteMinimumRatio.Value,
                    nameof(projectAbsoluteMinimumRatio));
                if (projectAbsoluteMinimumRatio.Value
                    > recommendedMinimumCutRatio + GeometryTolerance.Coordinate)
                {
                    throw new ArgumentException(
                        "The project absolute minimum ratio cannot exceed the recommended minimum ratio.",
                        nameof(projectAbsoluteMinimumRatio));
                }
            }

            bool invalidated = input.DecisionRecord != null;
            LayoutPolicyProfile policy = new LayoutPolicyProfile(
                policyVersion.Trim(),
                secondAbsoluteMinimum,
                projectAbsoluteMinimumMode,
                recommendedMinimumCutRatio,
                projectAbsoluteMinimumRatio);
            input.SetPolicy(policy, mode);
            recommendedMinimumConfirmed = true;
            AfterDecisionInputChanged(
                invalidated,
                "项目使用方式或规则已修改，旧的人工确认记录已失效。",
                "项目规则已保存。下一步请选择铺贴方式。");
            ActiveStep = OrthogonalDecisionGuideStep.Intent;
        }

        public void ApplyOrdinaryProjectRules(
            double? projectAbsoluteMinimum,
            bool recommendedMinimumIsConfirmed = true,
            ProjectAbsoluteMinimumMode projectAbsoluteMinimumMode =
                ProjectAbsoluteMinimumMode.NotDecided,
            double recommendedMinimumCutRatio =
                EngineeringLayoutRules.DefaultMinimumCutRatio,
            double? projectAbsoluteMinimumRatio = null)
        {
            ApplyProjectSettings(
                LayoutDecisionMode.ControlledProduction,
                OrdinaryPolicyVersion,
                projectAbsoluteMinimum,
                recommendedMinimumIsConfirmed,
                projectAbsoluteMinimumMode,
                recommendedMinimumCutRatio,
                projectAbsoluteMinimumRatio);
            SetLayoutIntent(RoomLayoutIntent.WholeRoomSinglePhase);
            Notice = "项目规则已保存。程序将自动分解房间、识别门洞邻接区域，"
                + "并默认保持全房连续相位；下一步只需在图中选择门洞。";
        }

        public void SetLayoutIntent(RoomLayoutIntent intent)
        {
            if (intent != RoomLayoutIntent.WholeRoomSinglePhase
                && intent != RoomLayoutIntent.MainSecondary)
            {
                throw new ArgumentOutOfRangeException(nameof(intent));
            }

            bool invalidated = input.DecisionRecord != null;
            input.SetLayoutIntent(intent);
            if (intent != RoomLayoutIntent.MainSecondary)
            {
                mainRegionDraft = null;
                secondaryRegionDraft = null;
            }

            AfterDecisionInputChanged(
                invalidated,
                "铺贴方式已修改，旧的人工确认记录已失效。",
                intent == RoomLayoutIntent.MainSecondary
                    ? "已选择分区铺贴。请在图中标明门洞影响范围、门洞、主要铺贴区、相邻铺贴区和两区接合边。"
                    : "已选择整房连续铺贴。请在图中标明门洞影响范围和门洞。");
            ActiveStep = OrthogonalDecisionGuideStep.Geometry;
        }

        public void SetWallCornerAlignmentPreference(bool enabled)
        {
            if (input.PreferWallCornerAlignment == enabled)
            {
                return;
            }

            bool invalidated = input.DecisionRecord != null;
            input.SetWallCornerAlignmentPreference(enabled);
            AfterDecisionInputChanged(
                invalidated,
                "可选质量优先设置已修改，旧的图面预览和人工确认记录已失效。",
                enabled
                    ? "已启用：在满足硬规则的前提下，优先比较入口观感和房间转角处的砖缝。"
                    : "已关闭：按基础排版顺序比较方案；转角尺寸仍可在工程详情中查看。" );
        }

        public void SetAutomaticDimensioning(bool enabled)
        {
            if (automaticDimensioningEnabled == enabled)
            {
                return;
            }

            automaticDimensioningEnabled = enabled;
            InvalidatePreview(
                "自动尺寸标注设置已修改，旧的图面预览已失效。" );
            Notice = enabled
                ? "已开启自动尺寸标注；请重新显示预览查看建筑样式尺寸链。"
                : "已关闭自动尺寸标注；请重新显示预览确认图面。";
        }

        public void SetRoomFeatureDimensioning(bool enabled)
        {
            if (roomFeatureDimensioningEnabled == enabled)
            {
                return;
            }

            roomFeatureDimensioningEnabled = enabled;
            InvalidatePreview(
                "房间凹凸台阶标注设置已修改，旧的图面预览已失效。" );
            Notice = enabled
                ? "已开启房间凹边、凸边和转角台阶尺寸标注；请重新预览。"
                : "已关闭房间凹边、凸边和转角台阶尺寸标注；特殊切砖仍按规则单独标注。";
        }

        public void SetDimensionPlacement(
            LayoutDrawingDimensionPlacement placement)
        {
            if (!Enum.IsDefined(
                typeof(LayoutDrawingDimensionPlacement),
                placement))
            {
                throw new ArgumentOutOfRangeException(nameof(placement));
            }

            if (dimensionPlacement == placement)
            {
                return;
            }

            dimensionPlacement = placement;
            InvalidatePreview(
                "尺寸标注位置已修改，旧的图面预览已失效。" );
            Notice = placement
                == LayoutDrawingDimensionPlacement.InsideRoom
                ? "已设置为房间内标注；尺寸线将沿砖边显示，请重新预览。"
                : "已设置为房间外标注；尺寸线将使用外置尺寸链，请重新预览。";
        }

        public void SetColorSettings(LayoutDrawingColorSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (colorSettings.IsEquivalentTo(settings))
            {
                return;
            }

            colorSettings = settings;
            InvalidatePreview(
                "图面颜色设置已修改，旧的图面预览已失效。" );
            Notice = "图面颜色已更新；请重新预览确认颜色和尺寸标注。";
        }

        public void SetControlRegion(AxisAlignedRectangle region)
        {
            bool invalidated = input.DecisionRecord != null;
            input.SetControlRegion(region);
            if (input.MainSecondary == null && mainRegionDraft != null)
            {
                mainRegionDraft = null;
                secondaryRegionDraft = null;
            }
            AfterDecisionInputChanged(
                invalidated,
                "门洞影响范围已修改，旧的人工确认记录已失效。",
                "门洞影响范围已保存。请继续在图中选择门洞。");
        }

        public void SetControlDoor(DoorOpening door)
        {
            bool invalidated = input.DecisionRecord != null;
            input.SetControlDoor(door);
            AfterDecisionInputChanged(
                invalidated,
                "门洞已修改，旧的人工确认记录已失效。",
                "门洞已保存。" + NextGeometryInstruction());
        }

        public void SetAutomaticallyLocatedDoor(
            AxisAlignedRectangle region,
            DoorOpening door)
        {
            bool invalidated = input.DecisionRecord != null;
            input.SetAutomaticDoorContext(region, door);
            mainRegionDraft = null;
            secondaryRegionDraft = null;
            AfterDecisionInputChanged(
                invalidated,
                "门洞或自动邻接区域已修改，旧的人工确认记录已失效。",
                "门洞已保存；程序已自动识别其邻接区域，并保持全房连续相位。"
                    + "请查看可保留方案。");
            ActiveStep = OrthogonalDecisionGuideStep.Candidates;
        }

        public void SetMainRegionDraft(AxisAlignedRectangle region)
        {
            InvalidatePreview("主要铺贴区已修改，旧的图面预览已失效。" );
            mainRegionDraft = region;
            CommitMainSecondaryIfComplete();
            Notice = secondaryRegionDraft == null
                ? "主要铺贴区已暂存。请继续选择相邻铺贴区；两项齐全后才会保存。"
                : Notice;
        }

        public void SetSecondaryRegionDraft(AxisAlignedRectangle region)
        {
            InvalidatePreview("相邻铺贴区已修改，旧的图面预览已失效。" );
            secondaryRegionDraft = region;
            CommitMainSecondaryIfComplete();
            Notice = mainRegionDraft == null
                ? "相邻铺贴区已暂存。请继续选择主要铺贴区；两项齐全后才会保存。"
                : Notice;
        }

        public void SetConnectionEdge(LineSegment3D edge)
        {
            bool invalidated = input.DecisionRecord != null;
            input.SetConnectionEdge(edge);
            AfterDecisionInputChanged(
                invalidated,
                "两区接合边已修改，旧的人工确认记录已失效。",
                "两区接合边已保存。请查看方案比较和当前待办。" );
            ActiveStep = OrthogonalDecisionGuideStep.Candidates;
        }

        public bool TrySelectCandidate(string candidateId)
        {
            if (previewPlan != null
                && !string.Equals(
                    previewPlan.CandidateId,
                    candidateId,
                    StringComparison.Ordinal))
            {
                InvalidatePreview(
                    "正在查看的方案已改变，旧的图面预览已失效。" );
            }

            if (!palette.TrySelectCandidate(candidateId))
            {
                return false;
            }

            selectedCandidateId = candidateId;
            selectedDiagnosticTileId = null;
            switch (palette.SelectedCandidate.State)
            {
                case LayoutCandidateState.AutomaticUsable:
                    Notice = "已选中满足规则的方案；无需填写项目复核原因。"
                        + "程序会自动显示同源临时预览；如还有其他可保留方案，可直接切换比较，再明确采用。";
                    break;
                case LayoutCandidateState.RequiresUserDecision:
                    Notice = "已选中需要人工确认的方案；程序会自动显示同源临时预览，"
                        + "图中会持续显示提醒；正式写回前还会再次要求明确确认。";
                    break;
                case LayoutCandidateState.RequiresProjectPolicy:
                    Notice = input.Policy != null
                        && input.Policy.AllowsVisualConfirmation
                        ? "已选中按图面确认方案；程序会自动显示同源临时预览，"
                            + "请根据实际边砖尺寸和位置判断，正式写回前仍需最终确认。"
                        : "已选中项目规则未确认的方案；可以查看窄砖诊断，"
                            + "但补齐项目绝对下限前不能确认采用。";
                    break;
                default:
                    Notice = "已选中不可使用的方案，仅供查看说明，不能进入预览。";
                    break;
            }
            return true;
        }

        public bool TryApplyDecisionRecord(string reason, out string error)
        {
            error = string.Empty;
            if (input.Policy == null)
            {
                error = "请先保存项目铺贴规则。";
                return false;
            }

            EvaluatedLayoutCandidate selected = palette.Candidates.FirstOrDefault(
                candidate => candidate.Id == selectedCandidateId);
            if (selected != null
                && selected.State == LayoutCandidateState.RequiresProjectPolicy)
            {
                error = "该方案仍缺少项目规则，请先处理面板顶部的项目规则待办。";
                return false;
            }

            if (selected == null
                || !selected.HasRawCandidate
                || (selected.State != LayoutCandidateState.AutomaticUsable
                    && selected.State != LayoutCandidateState.RequiresUserDecision))
            {
                error = "请先在“需要人工确认”中选择一个可记录方案。";
                return false;
            }

            bool requiresReason = selected.State
                == LayoutCandidateState.RequiresUserDecision;
            if (requiresReason && string.IsNullOrWhiteSpace(reason))
            {
                error = "请填写本次人工选择的可审计原因。";
                return false;
            }

            InvalidatePreview("人工确认记录已更新，旧的图面预览已失效。" );
            input.ApplyDecisionRecord(
                new DecisionRecord(
                    selected.Id,
                    input.Policy.Version,
                    string.IsNullOrWhiteSpace(reason)
                        ? string.Empty
                        : reason.Trim(),
                    requiresReason));
            SyncPalette();
            selectedCandidateId = selected.Id;
            InvalidationNotice = string.Empty;
            Notice = requiresReason
                ? "项目复核记录已保存：该方案不代表自动合规，也不代表自动满足推荐下限。"
                : "已采用满足规则的方案；无需项目复核原因。";
            ActiveStep = OrthogonalDecisionGuideStep.Summary;
            return true;
        }

        public bool TryRequestPreview(out LayoutCandidate candidate)
        {
            bool requested = palette.TryRequestPreview(out candidate);
            if (requested)
            {
                EnsurePreviewPlan(candidate.Id);
                PreviewState = OrthogonalDecisionPreviewState.DisplayRequested;
            }

            Notice = requested
                ? "正在所属图纸中显示临时铺贴图；不会创建图层、实体或写事务。"
                : OrthogonalDecisionGuidedText.PreviewDisabledReason(palette);
            return requested;
        }

        public bool TryRequestComparisonPreview(out LayoutCandidate candidate)
        {
            bool requested = palette.TryRequestComparisonPreview(out candidate);
            if (requested)
            {
                EnsurePreviewPlan(candidate.Id);
                PreviewState = OrthogonalDecisionPreviewState.DisplayRequested;
            }

            Notice = requested
                ? "正在图中临时查看所选方案；仅用于比较，不代表确认采用或自动合规，"
                    + "不会创建图层、实体或写事务。"
                : "所选方案不能临时看图。请选择自动推荐、需要人工确认或已明确按图面确认的可用方案；"
                    + "不可使用的方案只能查看文字说明。";
            return requested;
        }

        public bool SelectDiagnosticTile(string tileId)
        {
            if (previewPlan == null || string.IsNullOrWhiteSpace(tileId)
                || !previewPlan.Tiles.Any(tile => tile.Id == tileId
                    && (tile.IsBelowRecommended
                        || showAllAssessedBoundaryTiles
                            && tile.HasApplicableBoundaryCut)))
            {
                return false;
            }

            selectedDiagnosticTileId = tileId;
            Notice = "已在图中加强显示 " + tileId
                + "；尺寸、适用轴、边界侧、砖型和原因见窄砖诊断清单。";
            return true;
        }

        public void SetDiagnosticDisplayOptions(
            bool showAllAssessed,
            bool showNeutralRegionReference,
            bool showWallCorners = false)
        {
            showAllAssessedBoundaryTiles = showAllAssessed;
            showNeutralRegions = showNeutralRegionReference;
            showWallCornerDiagnostics = showWallCorners;
            if (!showAllAssessedBoundaryTiles
                && previewPlan != null
                && selectedDiagnosticTileId != null)
            {
                LayoutDrawingTile selected = previewPlan.Tiles.FirstOrDefault(
                    tile => tile.Id == selectedDiagnosticTileId);
                if (selected != null && !selected.IsBelowRecommended)
                {
                    selectedDiagnosticTileId = null;
                }
            }
        }

        public void SetEliminatedFilter(GuidedEliminatedGroup? filter)
        {
            eliminatedFilter = filter;
            eliminatedPageIndex = 0;
            RebuildEliminatedPresentationViews();
        }

        public bool MoveEliminatedPage(int direction)
        {
            int next = eliminatedPageIndex + Math.Sign(direction);
            if (next < 0 || next >= EliminatedPageCount)
            {
                return false;
            }

            eliminatedPageIndex = next;
            RebuildEliminatedCandidatePage();
            return true;
        }

        public bool TryRequestPreviewRefresh(out string disabledReason)
        {
            disabledReason = string.Empty;
            if (!CanRefreshPreview)
            {
                disabledReason = previewPlan == null
                    ? "当前没有可刷新的图面预览，请先点击“在图中预览”。"
                    : "预览正在处理，请稍候。";
                Notice = disabledReason;
                return false;
            }

            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            PreviewState = OrthogonalDecisionPreviewState.DisplayRequested;
            Notice = "正在用同一份铺贴图刷新临时预览；不会重新计算方案。";
            return true;
        }

        public bool BeginClearPreview()
        {
            if (previewPlan == null
                && PreviewState == OrthogonalDecisionPreviewState.None)
            {
                Notice = "当前没有需要清除的图面预览。";
                return false;
            }

            PreviewState = OrthogonalDecisionPreviewState.Clearing;
            Notice = "正在清除图中的临时铺贴线。";
            return true;
        }

        public void MarkPreviewVisible()
        {
            if (previewPlan == null)
            {
                throw new InvalidOperationException(
                    "A visible preview requires a drawing plan.");
            }

            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            PreviewState = OrthogonalDecisionPreviewState.Visible;
            Notice = "临时铺贴图已显示：实际分格线使用 ACI "
                + colorSettings.DivisionLineColorIndex
                + "，"
                + (automaticDimensioningEnabled
                    ? "尺寸标注按当前 ACI 颜色和位置设置显示，"
                    : string.Empty)
                + (previewPlan.StartPoint != null
                    ? "起铺点箭头已按实际起铺方向显示，"
                    : "当前方案未找到整砖/半砖起铺位置，未显示起铺点（"
                        + previewPlan.StartPointUnavailableReason
                        + "），")
                + "黄色/橙色为窄砖诊断，中性区域仅在勾选后显示；图纸仍未写入。";
        }

        public void MarkPreviewRefreshRequired(string reason)
        {
            if (previewPlan == null)
            {
                return;
            }

            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            PreviewState = OrthogonalDecisionPreviewState.RefreshRequired;
            Notice = string.IsNullOrWhiteSpace(reason)
                ? "临时预览需要刷新。"
                : reason;
        }

        public void MarkPreviewCleared(string reason)
        {
            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            palette.CancelPreview();
            previewPlan = null;
            PreviewState = OrthogonalDecisionPreviewState.None;
            Notice = string.IsNullOrWhiteSpace(reason)
                ? "图中的临时铺贴线已清除；图纸没有变化。"
                : reason;
        }

        public void MarkPreviewDisplayFailed(string reason)
        {
            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            palette.CancelPreview();
            previewPlan = null;
            PreviewState = OrthogonalDecisionPreviewState.None;
            Notice = "临时预览显示失败：" + reason
                + "。方案仍保留，可返回修改后重试；图纸没有写入。";
        }

        public string GetFormalWritebackDisabledReason()
        {
            string disabledReason;
            return CanRequestFormalWritebackInternal(out disabledReason)
                ? string.Empty
                : disabledReason;
        }

        public string GetFormalWritebackConfirmationMessage()
        {
            if (!CanRequestFormalWriteback)
            {
                return GetFormalWritebackDisabledReason();
            }

            int lineCount = previewPlan.DivisionLines.Count
                + previewPlan.Connections.Count;
            int dimensionCount = previewPlan.Dimensions.Count;
            var builder = new System.Text.StringBuilder();
            builder.Append("即将把当前同源预览中的 ");
            builder.Append(lineCount);
            builder.Append(" 条正式分格线写入图层 ");
            builder.Append(OrthogonalLayoutWritebackPolicy.ConfirmedLayerName);
            if (dimensionCount > 0)
            {
                builder.Append("，并把 ");
                builder.Append(dimensionCount);
                builder.Append(" 个尺寸标注写入图层 ");
                builder.Append(OrthogonalLayoutWritebackPolicy.DimensionLayerName);
                builder.Append("；");
                builder.Append(FormatDimensionSettings());
            }

            if (previewPlan.StartPoint != null)
            {
                builder.Append("，并把起铺点标志写入图层 ");
                builder.Append(OrthogonalLayoutWritebackPolicy.StartPointLayerName);
            }
            else
            {
                builder.Append("；当前方案未找到整砖/半砖起铺位置，不写入起铺点标志");
            }

            builder.Append("。本次只写实际分格线、必要连接边、已勾选的尺寸标注和起铺点标志，"
                + "不写中性连接线、墙角诊断、窄砖标记或其他预览标记。\r\n\r\n");
            if (previewPlan.CandidateState
                == LayoutCandidateState.RequiresProjectPolicy)
            {
                builder.Append(GetVisualConfirmationWarning());
                builder.Append("\r\n\r\n");
            }
            if (previewPlan.CandidateState
                == LayoutCandidateState.RequiresUserDecision)
            {
                builder.Append("提醒：当前候选达到项目绝对下限，但仍有需要人工复核的边界情况；这不代表自动合规。\r\n\r\n");
            }

            builder.Append("确认后将一次性写回；失败会自动回滚，且不会自动保存 DWG。是否继续？");
            return builder.ToString();
        }

        public bool TryAcknowledgeFormalWriteback(out string error)
        {
            error = GetFormalWritebackDisabledReason();
            if (!string.IsNullOrWhiteSpace(error))
            {
                Notice = error;
                return false;
            }

            formalWritebackAcknowledged = true;
            formalWritebackCandidateId = previewPlan.CandidateId;
            Notice = "正式写回最终确认已记录；正在等待 AutoCAD 原子事务执行。";
            return true;
        }

        public bool TryGetAuthorizedFormalLines(
            out IReadOnlyList<LayoutDrawingLine> lines,
            out string rejectionReason)
        {
            bool acknowledged = formalWritebackAcknowledged
                && string.Equals(
                    formalWritebackCandidateId,
                    previewPlan == null ? null : previewPlan.CandidateId,
                    StringComparison.Ordinal);
            return OrthogonalLayoutWritebackPolicy.TryGetFormalLines(
                previewPlan,
                PreviewState == OrthogonalDecisionPreviewState.Visible,
                acknowledged,
                input.Policy != null && input.Policy.AllowsVisualConfirmation,
                out lines,
                out rejectionReason);
        }

        public bool TryGetAuthorizedFormalDimensions(
            out IReadOnlyList<LayoutDrawingDimension> dimensions,
            out string rejectionReason)
        {
            bool acknowledged = formalWritebackAcknowledged
                && string.Equals(
                    formalWritebackCandidateId,
                    previewPlan == null ? null : previewPlan.CandidateId,
                    StringComparison.Ordinal);
            return OrthogonalLayoutWritebackPolicy.TryGetFormalDimensions(
                previewPlan,
                PreviewState == OrthogonalDecisionPreviewState.Visible,
                acknowledged,
                input.Policy != null && input.Policy.AllowsVisualConfirmation,
                out dimensions,
                out rejectionReason);
        }

        public bool TryGetAuthorizedFormalStartPoint(
            out LayoutDrawingStartPoint startPoint,
            out string rejectionReason)
        {
            bool acknowledged = formalWritebackAcknowledged
                && string.Equals(
                    formalWritebackCandidateId,
                    previewPlan == null ? null : previewPlan.CandidateId,
                    StringComparison.Ordinal);
            return OrthogonalLayoutWritebackPolicy.TryGetFormalStartPoint(
                previewPlan,
                PreviewState == OrthogonalDecisionPreviewState.Visible,
                acknowledged,
                input.Policy != null && input.Policy.AllowsVisualConfirmation,
                out startPoint,
                out rejectionReason);
        }

        public void MarkFormalWritebackFailed(string reason)
        {
            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            if (previewPlan != null)
            {
                PreviewState = OrthogonalDecisionPreviewState.Visible;
            }

            Notice = "正式写回失败：" + (string.IsNullOrWhiteSpace(reason)
                ? "未知错误。"
                : reason + "。")
                + "当前预览仍保留；请重新点击“确认并写入图纸”。";
        }

        public void MarkFormalWritebackSucceeded(int lineCount)
        {
            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            formalWritebackCompleted = true;
            formalWritebackLineCount = lineCount;
            palette.CancelPreview();
            previewPlan = null;
            selectedDiagnosticTileId = null;
            PreviewState = OrthogonalDecisionPreviewState.None;
            Notice = string.Format(
                CultureInfo.CurrentCulture,
                "已正式写回 {0} 个对象；预览已清除，图纸未自动保存。需要撤销时可使用一次 U 或 UNDO。",
                lineCount);
        }

        public bool BeginHostAction(
            OrthogonalDecisionGuideAction action,
            out string disabledReason)
        {
            disabledReason = GetActionDisabledReason(action);
            if (!string.IsNullOrEmpty(disabledReason))
            {
                Notice = disabledReason;
                return false;
            }

            PendingAction = action;
            IsCompleted = false;
            Notice = "正在回到图中选择；按 Esc 只取消本次选择。";
            return true;
        }

        public void EndHostAction(string resultNotice)
        {
            PendingAction = null;
            if (!string.IsNullOrWhiteSpace(resultNotice))
            {
                Notice = resultNotice;
            }
        }

        public string GetActionDisabledReason(
            OrthogonalDecisionGuideAction action)
        {
            if (PendingAction.HasValue)
            {
                return "已有图面选择正在进行，请先完成或按 Esc 取消。";
            }

            switch (action)
            {
                case OrthogonalDecisionGuideAction.SelectRoom:
                    return string.Empty;
                case OrthogonalDecisionGuideAction.SelectControlRegion:
                    return input.HasRoom
                        ? string.Empty
                        : "请先在图中选择并验证房间边界。";
                case OrthogonalDecisionGuideAction.SelectControlDoor:
                    return input.HasRoom && input.Policy != null
                        ? string.Empty
                        : "请先选择房间并确认项目规则。";
                case OrthogonalDecisionGuideAction.SelectMainRegion:
                case OrthogonalDecisionGuideAction.SelectSecondaryRegion:
                    return ShowsMainSecondaryControls && input.HasRoom
                        ? string.Empty
                        : "只有选择分区铺贴并载入房间后才能选取。";
                case OrthogonalDecisionGuideAction.SelectConnectionEdge:
                    return input.MainSecondary != null
                        ? string.Empty
                        : "请先在图中选全主要铺贴区和相邻铺贴区。";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        public bool MovePrevious()
        {
            if (ActiveStep == OrthogonalDecisionGuideStep.Room)
            {
                Notice = "当前已经是第一步。";
                return false;
            }

            ActiveStep--;
            Notice = "已返回上一步；已确认内容没有改变。";
            return true;
        }

        public void ViewStep(OrthogonalDecisionGuideStep step)
        {
            if (!Enum.IsDefined(typeof(OrthogonalDecisionGuideStep), step))
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }

            ActiveStep = step;
            Notice = "正在查看本步；已确认内容没有改变。";
        }

        public bool MoveNext(out string disabledReason)
        {
            disabledReason = GetNextDisabledReason();
            if (!string.IsNullOrEmpty(disabledReason))
            {
                Notice = disabledReason;
                return false;
            }

            if (ActiveStep == OrthogonalDecisionGuideStep.Summary)
            {
                return false;
            }

            ActiveStep++;
            Notice = "请按本步提示继续。";
            return true;
        }

        public void BeginModify(OrthogonalDecisionGuideStep step)
        {
            if (!Enum.IsDefined(typeof(OrthogonalDecisionGuideStep), step))
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }

            ActiveStep = step;
            IsCompleted = false;
            Notice = "正在修改本步。只有应用新值后才会使旧决定失效。";
        }

        public void BeginRoomReselection()
        {
            InvalidatePreview("重选房间，旧的图面预览已失效。" );
            input.ClearRoom();
            mainRegionDraft = null;
            secondaryRegionDraft = null;
            selectedCandidateId = null;
            palette.Reset();
            ActiveStep = OrthogonalDecisionGuideStep.Room;
            IsCompleted = false;
            InvalidationNotice = "已清除旧房间、图面重点、排版方案和人工确认记录；项目规则与使用方式已保留。";
            Notice = "请重新在图中选择房间边界；取消或无效时不会恢复旧房间。";
        }

        public bool Complete(out string disabledReason)
        {
            disabledReason = string.Empty;
            if (PendingAction.HasValue)
            {
                disabledReason = "请先完成或取消正在进行的图面选择。";
                Notice = disabledReason;
                return false;
            }

            if (!input.HasRoom || input.Result == null)
            {
                disabledReason = "至少需要先载入一个合法房间，才能完成只读汇总。";
                Notice = disabledReason;
                return false;
            }

            ActiveStep = OrthogonalDecisionGuideStep.Summary;
            IsCompleted = true;
            InvalidatePreview("本次预览已结束，临时铺贴线需要清除。" );
            Notice = "本轮已标记为只读结束；没有正式生成排版，也没有写入图纸。";
            return true;
        }

        public void CancelAll()
        {
            bool retainedFormalWriteback = formalWritebackCompleted;
            int retainedFormalLineCount = formalWritebackLineCount;
            if (!retainedFormalWriteback)
            {
                InvalidatePreview("取消整个任务，旧的图面预览已失效。" );
            }
            input.Cancel();
            palette.Reset();
            mainRegionDraft = null;
            secondaryRegionDraft = null;
            selectedCandidateId = null;
            selectedDiagnosticTileId = null;
            showAllAssessedBoundaryTiles = false;
            showNeutralRegions = false;
            showWallCornerDiagnostics = false;
            eliminatedFilter = null;
            eliminatedPageIndex = 0;
            recommendedMinimumConfirmed = false;
            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            formalWritebackCompleted = false;
            formalWritebackLineCount = 0;
            automaticDimensioningEnabled = true;
            roomFeatureDimensioningEnabled = false;
            dimensionPlacement = LayoutDrawingDimensionPlacement.InsideRoom;
            colorSettings = LayoutDrawingColorSettings.Default;
            PendingAction = null;
            ActiveStep = OrthogonalDecisionGuideStep.Room;
            IsCompleted = false;
            InvalidationNotice = string.Empty;
            Notice = retainedFormalWriteback
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "本次向导已取消；此前已正式写回的 {0} 个对象仍保留在图纸中，"
                        + "本次取消不会撤销或删除它们。现在可以继续排版其他房间。",
                    retainedFormalLineCount)
                : "本次向导已取消并清空；未创建或修改任何图层、实体或事务。";
        }

        public EngineeringOrthogonalDecisionResult Recalculate()
        {
            InvalidatePreview("重新计算后，旧的图面预览已失效。" );
            EngineeringOrthogonalDecisionResult result = input.Recalculate();
            SyncPalette();
            Notice = "已按当前输入重新计算；排版方案顺序、诊断和指标均保持原始结果。";
            return result;
        }

        public string GetNextDisabledReason()
        {
            switch (ActiveStep)
            {
                case OrthogonalDecisionGuideStep.Room:
                    return input.HasRoom
                        ? string.Empty
                        : "请先点击“在图中选择房间边界”。";
                case OrthogonalDecisionGuideStep.Project:
                    return input.Policy != null && recommendedMinimumConfirmed
                        ? string.Empty
                        : "请填写建议下限比例，并选择项目最低尺寸的处理方式后保存。";
                case OrthogonalDecisionGuideStep.Intent:
                    return input.LayoutIntent.HasValue
                        ? string.Empty
                        : "请选择整房连续铺贴或分区铺贴。";
                case OrthogonalDecisionGuideStep.Geometry:
                    return GeometryCompletedReason();
                case OrthogonalDecisionGuideStep.Candidates:
                    if (palette.State
                            == OrthogonalDecisionPaletteState.AutomaticPreviewReady
                        || palette.State
                            == OrthogonalDecisionPaletteState.ManualReviewPreviewReady
                        || palette.State
                            == OrthogonalDecisionPaletteState.VisualConfirmationPreviewReady
                        || palette.State
                            == OrthogonalDecisionPaletteState.RecordedDecisionPreviewReady
                        || palette.State
                            == OrthogonalDecisionPaletteState.PreviewRequested)
                    {
                        return string.Empty;
                    }

                    if (AllCandidatesUnavailable)
                    {
                        if (ConnectionSelectionDoesNotMatchValidatedBoundary())
                        {
                            return "所选接合边不是两个铺贴区实际相接的整段边。"
                                + "请返回“在图中标明重点”重选"
                                + DescribeExpectedConnectionEdge()
                                + "；"
                                + "不要选择房间外轮廓上的短折边。";
                        }

                        if (DoorSelectionCoversEntireBoundarySegment())
                        {
                            return "当前门洞两点覆盖了整段墙线。请返回“在图中标明重点”，"
                                + "重选实际门洞两侧边缘，不要使用墙线两端代替门洞。";
                        }

                            return "当前所有方案均不可使用。请返回“图面重点”"
                                + "检查门洞影响范围和门洞所在墙，或返回项目铺贴规则调整设置。";
                    }

                    if (palette.SelectedCandidate != null
                        && palette.SelectedCandidate.State
                            == LayoutCandidateState.RequiresUserDecision)
                    {
                        return "程序已选中唯一需要人工确认的方案；同源临时预览会自动显示。"
                            + "图中提醒会保留，正式写回前再明确确认。";
                    }

                    return "请先处理当前缺失项，或记录需要人工确认的方案。";
                case OrthogonalDecisionGuideStep.Summary:
                    return "当前已经是最后一步。";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string BuildSummary()
        {
            var lines = new List<string>
            {
                input.HasRoom
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "房间：已验证；砖规格 {0:0.###} × {1:0.###} mm。",
                        input.TileWidth,
                        input.TileHeight)
                    : "房间：尚未选择。",
                input.Policy == null
                    ? "项目规则：版本尚未确认。"
                    : "项目规则：" + input.Policy.Version + "；使用方式="
                        + OrthogonalDecisionGuidedText.FormatMode(input.Mode)
                        + "；建议下限="
                        + (recommendedMinimumConfirmed
                            ? input.Policy.DefaultMinimumCutRatio
                                .ToString("0.###", CultureInfo.InvariantCulture) + "T"
                            : "待确认")
                        + "；项目绝对下限="
                        + FormatProjectAbsoluteMinimumSetting() + "。",
                "铺贴方式：" + OrthogonalDecisionGuidedText.FormatIntent(
                    input.LayoutIntent),
                "门洞影响范围：" + (input.ControlRegion == null ? "未选择" : "已选择")
                    + "；门洞：" + (input.ControlDoor == null ? "未选择" : "已选择") + "。",
                ShowsMainSecondaryControls
                    ? "主要区与相邻区：" + (input.MainSecondary == null ? "未选全" : "已确认")
                        + "；两区接合边："
                        + (input.SelectedConnectionEdge.HasValue ? "已选择" : "未选择") + "。"
                    : "主要区与相邻区：当前铺贴方式不需要。",
                "排版方案：" + palette.Candidates.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个；状态=" + OrthogonalDecisionGuidedText.FormatPaletteState(
                        palette.State) + "。",
                input.Result != null && input.Result.AppliedRecord != null
                    ? OrthogonalDecisionGuidedText.FormatDecisionRecordForUser(
                        input.Result.AppliedRecord,
                        input.Mode)
                    : "人工确认记录：无。"
            };

            return string.Join(Environment.NewLine, lines);
        }

        public string BuildOrdinarySummary()
        {
            EngineeringOrthogonalDecisionResult result = input.Result;
            DecisionRecord record = result == null
                ? null
                : result.AppliedRecord;
            if (ordinarySummaryRendered
                && ReferenceEquals(ordinarySummaryResult, result)
                && ReferenceEquals(ordinarySummaryPolicy, input.Policy)
                && ReferenceEquals(ordinarySummaryRecord, record)
                && ReferenceEquals(ordinarySummaryDoor, input.ControlDoor)
                && ordinarySummaryMode == input.Mode
                && ordinarySummaryTileWidth == input.TileWidth
                && ordinarySummaryTileHeight == input.TileHeight
                && ordinarySummaryHasRoom == input.HasRoom
                && ordinarySummaryRecommendedMinimumConfirmed
                    == recommendedMinimumConfirmed
                && ordinarySummaryFormalWritebackCompleted
                    == formalWritebackCompleted
                && ordinarySummaryFormalWritebackAcknowledged
                    == formalWritebackAcknowledged
                && ordinarySummaryFormalWritebackLineCount
                    == formalWritebackLineCount
                && ordinarySummaryAutomaticDimensioningEnabled
                    == automaticDimensioningEnabled
                && ordinarySummaryRoomFeatureDimensioningEnabled
                    == roomFeatureDimensioningEnabled
                && ordinarySummaryDimensionPlacement == dimensionPlacement
                && ordinarySummaryColorSettings != null
                && ordinarySummaryColorSettings.IsEquivalentTo(colorSettings))
            {
                return ordinarySummaryView;
            }

            var lines = new List<string>
            {
                input.HasRoom
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "房间：已验证；砖规格 {0:0.###} × {1:0.###} mm。",
                        input.TileWidth,
                        input.TileHeight)
                    : "房间：尚未选择。",
                input.Policy == null
                    ? "项目规则：尚未确认。"
                    : "项目规则：建议下限 "
                        + (recommendedMinimumConfirmed
                            ? input.Policy.DefaultMinimumCutRatio
                                .ToString("0.###", CultureInfo.InvariantCulture) + "T"
                            : "待确认")
                        + "；项目绝对下限="
                        + FormatProjectAbsoluteMinimumSetting() + "。",
                input.ControlDoor == null
                    ? "门洞：尚未选择。"
                    : "门洞：已验证；邻接区域由程序自动识别，全房保持连续相位。",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "方案：满足规则 {0}、待项目复核 {1}、规则缺失 {2}、硬淘汰 {3}。",
                    RuleSatisfiedCandidates.Count,
                    ReviewCandidates.Count,
                    MissingRuleCandidates.Count,
                    EliminatedCandidateCount),
                input.Result != null && input.Result.AppliedRecord != null
                    ? input.Result.AppliedRecord.AcceptsException
                        ? "方案确认：已保存项目复核记录；不代表自动合规。"
                        : "方案确认：已采用一个满足规则的方案。"
                    : "方案确认：尚未保存。",
                "自动尺寸标注："
                    + (automaticDimensioningEnabled ? "已开启" : "已关闭")
                    + "；"
                    + FormatDimensionSettings()
                    + "。",
                formalWritebackCompleted
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        "图面状态：已正式写回 {0} 个对象；DWG 未自动保存，可用一次 U 或 UNDO 撤销。",
                        formalWritebackLineCount)
                    : formalWritebackAcknowledged
                        ? "图面状态：已完成最后确认，正在等待一次原子写回；尚未写入 DWG。"
                        : "图面状态：仅临时矢量预览，DWG 零写入。"
            };

            ordinarySummaryResult = result;
            ordinarySummaryPolicy = input.Policy;
            ordinarySummaryRecord = record;
            ordinarySummaryDoor = input.ControlDoor;
            ordinarySummaryMode = input.Mode;
            ordinarySummaryTileWidth = input.TileWidth;
            ordinarySummaryTileHeight = input.TileHeight;
            ordinarySummaryHasRoom = input.HasRoom;
            ordinarySummaryRecommendedMinimumConfirmed =
                recommendedMinimumConfirmed;
            ordinarySummaryFormalWritebackCompleted =
                formalWritebackCompleted;
            ordinarySummaryFormalWritebackAcknowledged =
                formalWritebackAcknowledged;
            ordinarySummaryFormalWritebackLineCount = formalWritebackLineCount;
            ordinarySummaryAutomaticDimensioningEnabled =
                automaticDimensioningEnabled;
            ordinarySummaryRoomFeatureDimensioningEnabled =
                roomFeatureDimensioningEnabled;
            ordinarySummaryDimensionPlacement = dimensionPlacement;
            ordinarySummaryColorSettings = colorSettings;
            ordinarySummaryView = string.Join(Environment.NewLine, lines);
            ordinarySummaryRendered = true;
            return ordinarySummaryView;
        }

        public string GetVisualConfirmationWarning()
        {
            return previewPlan == null
                || input.Policy == null
                || !input.Policy.AllowsVisualConfirmation
                || previewPlan.CandidateState
                    != LayoutCandidateState.RequiresProjectPolicy
                ? string.Empty
                : FormatVisualConfirmationWarning(previewPlan);
        }

        private string FormatProjectAbsoluteMinimumSetting()
        {
            if (input.Policy == null)
            {
                return "尚未决定";
            }

            if (input.Policy.HasProjectAbsoluteMinimum)
            {
                if (input.Policy.ProjectAbsoluteMinimumRatio.HasValue)
                {
                    return "比例 "
                        + input.Policy.ProjectAbsoluteMinimumRatio.Value.ToString(
                            "0.###", CultureInfo.InvariantCulture)
                        + "（按 X/Y 对应砖尺寸换算）";
                }

                return input.Policy.ProjectAbsoluteMinimumCut.Value.ToString(
                    "0.###", CultureInfo.InvariantCulture) + " mm";
            }

            return input.Policy.AllowsVisualConfirmation
                ? "按图面确认（不设置数值）"
                : "尚未决定";
        }

        private static string FormatVisualConfirmationWarning(
            LayoutDrawingPlan plan)
        {
            var measurements = plan.Tiles
                .SelectMany(tile => tile.CutMeasurements.Select(measurement =>
                    new
                    {
                        Tile = tile,
                        Measurement = measurement
                    }))
                .Where(item => item.Measurement.Status
                    == ProjectCutStatus.RequiresProjectPolicy)
                .OrderBy(item => item.Measurement.ActualValue)
                .ThenBy(item => item.Tile.Id, StringComparer.Ordinal)
                .ToList();
            if (measurements.Count == 0)
            {
                return "按图面确认提醒：本方案未设置数值绝对下限；请结合图面整体效果确认，不代表自动合规结论。";
            }

            double minimum = measurements[0].Measurement.ActualValue;
            var affectedTiles = measurements
                .Select(item => item.Tile)
                .Distinct()
                .ToList();
            string locations = string.Join(
                "；",
                affectedTiles
                    .Take(3)
                    .Select(FormatVisualTileLocation));
            return string.Format(
                CultureInfo.InvariantCulture,
                "按图面确认提醒：本方案未设置数值绝对下限；实际最小边界砖尺寸为 {0:0.###} mm（{1} 方向），涉及 {2} 块边界砖。示例位置：{3}。请结合图面整体效果和现场要求确认；这不是自动合规结论。",
                minimum,
                measurements[0].Measurement.Axis,
                affectedTiles.Count,
                locations);
        }

        private static string FormatVisualTileLocation(LayoutDrawingTile tile)
        {
            if (tile.Outline.Count == 0)
            {
                return tile.Id;
            }

            double west = tile.Outline.Min(point => point.X);
            double south = tile.Outline.Min(point => point.Y);
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} @ ({1:0.###}, {2:0.###})，边界侧={3}",
                tile.Id,
                west,
                south,
                string.Join("/", tile.BoundarySides));
        }

        private void CommitMainSecondaryIfComplete()
        {
            if (mainRegionDraft == null || secondaryRegionDraft == null)
            {
                return;
            }

            bool hadDoor = input.ControlDoor != null;
            bool invalidated = input.DecisionRecord != null;
            input.SetMainSecondary(
                new MainSecondaryRegionDefinition(
                    mainRegionDraft,
                    secondaryRegionDraft));
            AfterDecisionInputChanged(
                invalidated,
                "主要铺贴区或相邻铺贴区已修改，旧的人工确认记录已失效。",
                hadDoor && input.ControlDoor == null
                    ? "两个铺贴区已保存；主要铺贴区改变了门洞影响范围，请重新选择门洞。"
                    : "两个铺贴区已保存。请继续选择两区接合边。");
        }

        private static bool TryGetDoorWallSegmentRange(
            AxisAlignedOrthogonalPolygon room,
            RoomSide wall,
            Point3D start,
            Point3D end,
            out double rangeStart,
            out double rangeEnd)
        {
            rangeStart = 0.0;
            rangeEnd = 0.0;
            bool matches;
            switch (wall)
            {
                case RoomSide.North:
                    matches = Math.Abs(start.Y - room.North)
                            <= GeometryTolerance.Coordinate
                        && Math.Abs(end.Y - room.North)
                            <= GeometryTolerance.Coordinate;
                    rangeStart = Math.Min(start.X, end.X);
                    rangeEnd = Math.Max(start.X, end.X);
                    return matches;
                case RoomSide.South:
                    matches = Math.Abs(start.Y - room.South)
                            <= GeometryTolerance.Coordinate
                        && Math.Abs(end.Y - room.South)
                            <= GeometryTolerance.Coordinate;
                    rangeStart = Math.Min(start.X, end.X);
                    rangeEnd = Math.Max(start.X, end.X);
                    return matches;
                case RoomSide.West:
                    matches = Math.Abs(start.X - room.West)
                            <= GeometryTolerance.Coordinate
                        && Math.Abs(end.X - room.West)
                            <= GeometryTolerance.Coordinate;
                    rangeStart = Math.Min(start.Y, end.Y);
                    rangeEnd = Math.Max(start.Y, end.Y);
                    return matches;
                case RoomSide.East:
                    matches = Math.Abs(start.X - room.East)
                            <= GeometryTolerance.Coordinate
                        && Math.Abs(end.X - room.East)
                            <= GeometryTolerance.Coordinate;
                    rangeStart = Math.Min(start.Y, end.Y);
                    rangeEnd = Math.Max(start.Y, end.Y);
                    return matches;
                default:
                    throw new ArgumentOutOfRangeException(nameof(wall));
            }
        }

        private void AfterDecisionInputChanged(
            bool invalidated,
            string invalidationMessage,
            string notice)
        {
            InvalidatePreview(
                "影响方案的输入已修改，旧的图面预览已失效。" );
            SyncPalette();
            IsCompleted = false;
            InvalidationNotice = invalidated
                ? invalidationMessage
                : string.Empty;
            bool selectedRequiresUserDecision =
                palette.SelectedCandidate != null
                && palette.SelectedCandidate.State
                    == LayoutCandidateState.RequiresUserDecision;
            bool selectedVisualConfirmation =
                palette.SelectedCandidate != null
                && palette.SelectedCandidate.State
                    == LayoutCandidateState.RequiresProjectPolicy
                && input.Policy != null
                && input.Policy.AllowsVisualConfirmation;
            Notice = selectedRequiresUserDecision
                ? notice + " 已自动选中唯一待确认方案；同源临时预览会自动显示，"
                    + "看图后在正式写回前确认即可。"
                : selectedVisualConfirmation
                    ? notice + " 已自动选中唯一按图面确认方案；同源临时预览会自动显示，"
                        + "请查看边砖尺寸和位置后再进行最终确认。"
                    : notice;
        }

        private void EnsurePreviewPlan(string candidateId)
        {
            if (previewPlan == null
                || !string.Equals(
                    previewPlan.CandidateId,
                    candidateId,
                    StringComparison.Ordinal))
            {
                previewPlan = LayoutDrawingPlanBuilder.Build(
                    input.Result,
                    candidateId,
                    automaticDimensioningEnabled,
                    dimensionPlacement,
                    colorSettings,
                    roomFeatureDimensioningEnabled);
            }
        }

        private string FormatDimensionSettings()
        {
            string placement = dimensionPlacement
                == LayoutDrawingDimensionPlacement.InsideRoom
                ? "房间内"
                : "房间外";
            return "位置=" + placement
                + "；分割线 ACI "
                + colorSettings.DivisionLineColorIndex
                + "；砖尺寸标注 ACI "
                + colorSettings.TileDimensionColorIndex
                + "；凹凸/特殊标注 ACI "
                + colorSettings.BoundaryFeatureDimensionColorIndex
                + "；抹灰边界 ACI "
                + colorSettings.PlasterBoundaryColorIndex
                + "；房间凹凸台阶 "
                + (roomFeatureDimensioningEnabled ? "开启" : "关闭");
        }

        private void SyncPalette()
        {
            palette.SetResult(input.Result, input.Mode);
            RebuildRequirementPresentations();
            RebuildCandidatePresentations();
            selectedCandidateId = palette.SelectedCandidate == null
                ? null
                : palette.SelectedCandidate.Id;
        }

        private bool CanRequestFormalWritebackInternal(out string reason)
        {
            reason = string.Empty;
            if (formalWritebackAcknowledged)
            {
                reason = "正式写回请求正在等待 AutoCAD 执行，请稍候。";
                return false;
            }

            if (previewPlan == null)
            {
                reason = "请先显示当前候选的临时预览。";
                return false;
            }

            if (PreviewState != OrthogonalDecisionPreviewState.Visible)
            {
                reason = PreviewState
                    == OrthogonalDecisionPreviewState.RefreshRequired
                    ? "当前预览已失效，请先点击“刷新预览”，再确认正式写回。"
                    : "当前预览尚未显示完成，请等待显示完成后再确认正式写回。";
                return false;
            }

            if (!string.Equals(
                selectedCandidateId,
                previewPlan.CandidateId,
                StringComparison.Ordinal))
            {
                reason = "当前选中的候选已不是预览对应的候选；请重新预览后再确认。";
                return false;
            }

            if (!OrthogonalLayoutWritebackPolicy.IsCandidateEligible(
                previewPlan.CandidateState,
                input.Policy != null && input.Policy.AllowsVisualConfirmation))
            {
                reason = "当前候选不属于允许正式写回的状态；项目规则缺失、淘汰或未完成复核的候选不能写回。";
                return false;
            }

            return true;
        }

        private void InvalidatePreview(string message)
        {
            if (previewPlan == null
                && PreviewState == OrthogonalDecisionPreviewState.None)
            {
                return;
            }

            formalWritebackAcknowledged = false;
            formalWritebackCandidateId = null;
            previewPlan = null;
            selectedDiagnosticTileId = null;
            PreviewState = OrthogonalDecisionPreviewState.None;
            palette.CancelPreview();
            if (!string.IsNullOrWhiteSpace(message))
            {
                InvalidationNotice = message;
            }
        }

        private string NextGeometryInstruction()
        {
            if (!ShowsMainSecondaryControls)
            {
                ActiveStep = OrthogonalDecisionGuideStep.Candidates;
                return " 请查看方案比较和当前待办。";
            }

            if (input.MainSecondary == null)
            {
                return " 请继续在图中选择主要铺贴区和相邻铺贴区。";
            }

            if (!input.SelectedConnectionEdge.HasValue)
            {
                return " 请继续在图中选择两区接合边。";
            }

            ActiveStep = OrthogonalDecisionGuideStep.Candidates;
            return " 请查看方案比较和当前待办。";
        }

        private string GeometryCompletedReason()
        {
            if (ShowsMainSecondaryControls && input.ControlRegion == null)
            {
                return "请先在图中选择门洞影响范围。";
            }

            if (input.ControlDoor == null)
            {
                return "请先在图中选择门洞。";
            }

            if (input.ControlRegion == null)
            {
                return "门洞邻接区域尚未由程序自动识别，请重新选择门洞两侧边缘。";
            }

            if (ShowsMainSecondaryControls && input.MainSecondary == null)
            {
                return "请在图中选全主要铺贴区和相邻铺贴区。";
            }

            if (ShowsMainSecondaryControls
                && !input.SelectedConnectionEdge.HasValue)
            {
                return "请在图中选择两个铺贴区共同的接合边。";
            }

            return string.Empty;
        }

        private void RebuildCandidatePresentations()
        {
            Stopwatch timer = Stopwatch.StartNew();
            candidates.Clear();
            bool uniqueAutomatic = input.Result != null
                && input.Result.CanProceedAutomatically;
            string recommendedId = input.PreferWallCornerAlignment
                ? palette.Candidates.FirstOrDefault(candidate =>
                    candidate.HasRawCandidate
                    && (candidate.State == LayoutCandidateState.AutomaticUsable
                        || candidate.State == LayoutCandidateState.RequiresUserDecision
                        || input.Policy != null
                            && input.Policy.AllowsVisualConfirmation
                            && candidate.State
                                == LayoutCandidateState.RequiresProjectPolicy))?.Id
                : null;
            int recommendationRank = 0;
            for (int index = 0; index < palette.Candidates.Count; index++)
            {
                EvaluatedLayoutCandidate evaluated = palette.Candidates[index];
                GuidedCandidateGroup group =
                    OrthogonalDecisionGuidedText.MapCandidateGroup(
                        evaluated.State);
                bool isRecommended = !string.IsNullOrEmpty(recommendedId)
                    && string.Equals(
                        evaluated.Id,
                        recommendedId,
                        StringComparison.Ordinal);
                if (input.PreferWallCornerAlignment
                    && evaluated.HasRawCandidate
                    && group != GuidedCandidateGroup.Unavailable)
                {
                    recommendationRank++;
                }
                candidates.Add(new GuidedCandidatePresentation(
                    evaluated,
                    evaluated.OriginalIndex > 0
                        ? evaluated.OriginalIndex
                        : index + 1,
                    group,
                    group == GuidedCandidateGroup.Unavailable
                        ? OrthogonalDecisionGuidedText.MapEliminatedGroup(
                            evaluated)
                        : (GuidedEliminatedGroup?)null,
                    OrthogonalDecisionGuidedText.MapCornerAlignmentGroup(
                        evaluated),
                    "方案 " + (index + 1).ToString(
                        CultureInfo.InvariantCulture),
                    OrthogonalDecisionGuidedText.FormatCandidateStatus(
                        evaluated.State,
                        uniqueAutomatic,
                        isRecommended)
                        + (input.PreferWallCornerAlignment
                            ? "；"
                                + OrthogonalDecisionGuidedText
                                    .FormatCornerAlignment(evaluated)
                            : string.Empty),
                    isRecommended,
                    isRecommended ? recommendationRank : 0));
            }

            candidateView = new ReadOnlyCollection<GuidedCandidatePresentation>(
                candidates);
            ruleSatisfiedCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    candidates.Where(item => item.Group
                        == GuidedCandidateGroup.AutomaticRecommendation)
                        .ToList());
            reviewCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    candidates.Where(item => item.Group
                        == GuidedCandidateGroup.ManualConfirmation)
                        .ToList());
            missingRuleCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    candidates.Where(item => item.Group
                        == GuidedCandidateGroup.ProjectRuleMissing)
                        .ToList());
            eliminatedCandidateCount = candidates.Count(item =>
                item.Group == GuidedCandidateGroup.Unavailable);
            RebuildEliminatedPresentationViews();
            timer.Stop();
            LastCandidatePresentationBuildDuration = timer.Elapsed;
        }

        private void RebuildRequirementPresentations()
        {
            requirementView =
                new ReadOnlyCollection<GuidedRequirementPresentation>(
                    (input.Result == null
                        ? Enumerable.Empty<DecisionRequirement>()
                        : input.Result.Requirements)
                    .Select(OrthogonalDecisionGuidedText.PresentRequirement)
                    .ToList());
        }

        private void RebuildEliminatedPresentationViews()
        {
            filteredEliminatedCandidateView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    candidates.Where(item =>
                            item.Group == GuidedCandidateGroup.Unavailable
                            && (!eliminatedFilter.HasValue
                                || item.EliminatedGroup
                                    == eliminatedFilter.Value))
                        .ToList());
            filteredEliminatedCandidateCount =
                filteredEliminatedCandidateView.Count;
            eliminatedPageCount = Math.Max(
                1,
                (filteredEliminatedCandidateCount
                    + EliminatedCandidatePageSize - 1)
                    / EliminatedCandidatePageSize);
            if (eliminatedPageIndex >= eliminatedPageCount)
            {
                eliminatedPageIndex = Math.Max(0, eliminatedPageCount - 1);
            }

            RebuildEliminatedCandidatePage();
        }

        private void RebuildEliminatedCandidatePage()
        {
            eliminatedCandidatePageView =
                new ReadOnlyCollection<GuidedCandidatePresentation>(
                    filteredEliminatedCandidateView
                        .Skip(eliminatedPageIndex * EliminatedCandidatePageSize)
                        .Take(EliminatedCandidatePageSize)
                        .ToList());
        }
    }
}
