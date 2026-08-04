using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using TileLayout.Core;
using TileLayout.Core.Models;

namespace TileLayout.AutoCAD.Adapter
{
    internal sealed class OrthogonalDecisionGuideActionEventArgs : EventArgs
    {
        public OrthogonalDecisionGuideActionEventArgs(
            OrthogonalDecisionGuideAction action)
        {
            Action = action;
        }

        public OrthogonalDecisionGuideAction Action { get; }
    }

    internal sealed class OrthogonalDecisionPreviewActionEventArgs : EventArgs
    {
        public OrthogonalDecisionPreviewActionEventArgs(
            OrthogonalDecisionPreviewAction action)
        {
            Action = action;
        }

        public OrthogonalDecisionPreviewAction Action { get; }
    }

    internal sealed class OrthogonalDecisionPaletteControl : UserControl
    {
        private static readonly string[] ProductStageTitles =
        {
            "房间与规则",
            "门洞",
            "选择方案",
            "图面核对"
        };

        private static readonly string[] InternalStepTitles =
        {
            "房间和地砖",
            "项目铺贴规则",
            "铺贴方式",
            "图面重点",
            "排版方案",
            "预览与结束"
        };

        // Keep the candidate page usable when the floating dialog is resized
        // below its preferred height.  The outer page scrolls instead of
        // allowing the list/detail rows to collapse to zero height.
        private const int MinimumCandidateListHeight = 128;
        private const int MinimumCandidateDetailHeight = 118;
        private const int MinimumCandidateControlWidth = 240;

        private readonly ToolTip toolTip = new ToolTip();
        private readonly Label progress = new Label();
        private readonly Label nextAction = new Label();
        private readonly Label notice = new Label();
        private readonly Label invalidation = new Label();
        private readonly TextBox requirements = CreateReadOnlyTextBox();
        private readonly CheckBox engineeringToggle = new CheckBox
        {
            Text = "显示工程详情",
            AutoSize = true
        };
        private readonly TextBox engineeringDetails = CreateReadOnlyTextBox();
        private readonly TabControl tabs = new TabControl();

        private readonly TextBox tileWidth = new TextBox { Text = "600" };
        private readonly TextBox tileHeight = new TextBox { Text = "600" };
        private readonly TextBox groutWidth = new TextBox { Text = "1.5" };
        private readonly TextBox plasterThickness = new TextBox { Text = "0" };
        private readonly Button applyLayoutSettings = new Button
        {
            Text = "保存砖、灰缝和完成面设置",
            AutoSize = true
        };
        private readonly Label layoutSettingsStatus = CreateWrapLabel();
        private readonly Button selectRoom = new Button
        {
            Text = "在图中选择房间边界",
            AutoSize = true
        };
        private readonly Label roomStatus = CreateWrapLabel();
        private readonly Label roomDisabledReason = CreateReasonLabel();

        private readonly ComboBox mode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        private readonly TextBox policyVersion = new TextBox { Text = "P-1" };
        private readonly TextBox secondMinimum = new TextBox();
        private readonly Label recommendedMinimum = CreateWrapLabel();
        private readonly CheckBox confirmRecommendedMinimum = new CheckBox
        {
            Text = "我已确认本项目采用固定推荐下限 0.42T",
            AutoSize = true
        };
        private readonly CheckBox absoluteMinimumUnconfirmed = new CheckBox
        {
            Text = "项目绝对下限尚未确认",
            AutoSize = true,
            Checked = true
        };
        private readonly CheckBox visualConfirmationMode = new CheckBox
        {
            Text = "按图面确认（不设置数值绝对下限）",
            AutoSize = true
        };
        private readonly CheckBox preferWallCornerAlignment = new CheckBox
        {
            Text = "墙角对缝优先（可选，默认关闭）",
            AutoSize = true
        };
        private readonly Button applyProject = new Button
        {
            Text = "保存项目铺贴规则",
            AutoSize = true
        };
        private readonly Label projectStatus = CreateWrapLabel();

        private readonly RadioButton wholeRoom = new RadioButton
        {
            Text = "整房连续铺贴",
            AutoSize = true
        };
        private readonly RadioButton mainSecondary = new RadioButton
        {
            Text = "分区铺贴（主要区 + 相邻区）",
            AutoSize = true
        };
        private readonly Button applyIntent = new Button
        {
            Text = "保存铺贴方式",
            AutoSize = true
        };
        private readonly Label intentStatus = CreateWrapLabel();

        private readonly Button selectControlRegion = CreateSelectionButton(
            "在图中选择门洞影响范围");
        private readonly Button selectDoor = CreateSelectionButton(
            "在图中选择门洞");
        private readonly Button selectMainRegion = CreateSelectionButton(
            "在图中选择主要铺贴区");
        private readonly Button selectSecondaryRegion = CreateSelectionButton(
            "在图中选择相邻铺贴区");
        private readonly Button selectConnectionEdge = CreateSelectionButton(
            "在图中选择两区接合边");
        private readonly Label controlRegionStatus = CreateWrapLabel();
        private readonly Label doorStatus = CreateWrapLabel();
        private readonly Label mainRegionStatus = CreateWrapLabel();
        private readonly Label secondaryRegionStatus = CreateWrapLabel();
        private readonly Label connectionStatus = CreateWrapLabel();
        private readonly Label controlRegionReason = CreateReasonLabel();
        private readonly Label doorReason = CreateReasonLabel();
        private readonly Label mainRegionReason = CreateReasonLabel();
        private readonly Label secondaryRegionReason = CreateReasonLabel();
        private readonly Label connectionReason = CreateReasonLabel();
        private readonly Panel mainSecondaryPanel = new Panel();

        private readonly ListBox automaticCandidates = new ListBox();
        private readonly ListBox manualCandidates = new ListBox();
        private readonly ListBox missingPolicyCandidates = new ListBox();
        private readonly ListBox unavailableCandidates = new ListBox();
        private readonly Label candidateSearchStatistics = CreateWrapLabel();
        private readonly CheckBox showEliminatedCandidates = new CheckBox
        {
            Text = "展开淘汰候选文字审计",
            AutoSize = true
        };
        private readonly ComboBox eliminatedFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220
        };
        private readonly Button previousEliminatedPage = new Button
        {
            Text = "上一页",
            AutoSize = true
        };
        private readonly Button nextEliminatedPage = new Button
        {
            Text = "下一页",
            AutoSize = true
        };
        private readonly Label eliminatedPageStatus = CreateWrapLabel();
        private readonly TextBox candidateDetail = CreateReadOnlyTextBox();
        private readonly Button inspectCandidate = new Button
        {
            Text = "查看所选方案",
            AutoSize = true
        };
        private readonly Label inspectionDisabledReason = CreateReasonLabel();
        private readonly Label reviewNotice = CreateReasonLabel();
        private readonly ListBox diagnosticTiles = new ListBox();
        private readonly TextBox diagnosticTileDetail = CreateReadOnlyTextBox();
        private readonly CheckBox showAllAssessedTiles = new CheckBox
        {
            Text = "在图中标出全部受检边砖（窄砖始终标出）",
            AutoSize = true
        };
        private readonly CheckBox showNeutralRegions = new CheckBox
        {
            Text = "在图中显示自动分区与共享边（只读）",
            AutoSize = true
        };
        private readonly CheckBox showWallCornerDiagnostics = new CheckBox
        {
            Text = "在图中显示角点分格诊断（只读）",
            AutoSize = true
        };
        private readonly CheckBox neutralRegionDetailsToggle = new CheckBox
        {
            Text = "展开房间结构参考（只读）",
            AutoSize = true
        };
        private readonly TextBox neutralRegionDetails = CreateReadOnlyTextBox();
        private readonly TextBox wallCornerDetails = CreateReadOnlyTextBox();

        private readonly TextBox summary = CreateReadOnlyTextBox();
        private readonly Button preview = new Button
        {
            Text = "在图中预览",
            AutoSize = true
        };
        private readonly Button refreshPreview = new Button
        {
            Text = "刷新预览",
            AutoSize = true
        };
        private readonly Button cancelPreview = new Button
        {
            Text = "清除预览",
            AutoSize = true
        };
        private readonly Button focusDrawing = new Button
        {
            Text = "专注查看图面",
            AutoSize = true
        };
        private readonly Label previewDisabledReason = CreateReasonLabel();
        private readonly Button writeToDrawing = new Button
        {
            Text = "确认并写入图纸",
            AutoSize = true
        };
        private readonly Label writebackDisabledReason = CreateReasonLabel();

        private readonly Button previous = new Button { Text = "上一步" };
        private readonly Button next = new Button { Text = "下一步" };
        private readonly Button modify = new Button { Text = "返回修改" };
        private readonly Button reselect = new Button { Text = "重选房间" };
        private readonly Button finish = new Button { Text = "结束本次预览" };
        private readonly Button cancelAll = new Button { Text = "取消整个任务" };
        private readonly Label navigationDisabledReason = CreateReasonLabel();

        private OrthogonalDecisionGuidedWorkflow workflow =
            new OrthogonalDecisionGuidedWorkflow();
        private bool refreshing;
        private bool updatingProjectAbsoluteMinimumMode;
        private IReadOnlyList<GuidedCandidatePresentation> renderedCandidateView;
        private GuidedEliminatedGroup? renderedEliminatedFilter;
        private int renderedEliminatedPageIndex = -1;
        private bool renderedShowEliminatedCandidates;
        private LayoutDrawingPlan renderedDiagnosticPlan;
        private bool renderedShowAllAssessedTiles;
        private bool diagnosticTilesRendered;
        private EngineeringOrthogonalDecisionResult renderedDetailsResult;
        private bool renderedDetailsPreferWallCornerAlignment;
        private LayoutDrawingPlan renderedDetailsPreviewPlan;
        private bool renderedWallCornerPreferWallCornerAlignment;
        private bool resultDetailsRendered;
        private bool wallCornerDetailsRendered;
        private string renderedCandidateSearchStatistics;
        private string renderedNeutralRegionDetails;
        private string renderedWallCornerDetails;
        private double pendingTileWidth = 600.0;
        private double pendingTileHeight = 600.0;
        private double pendingGroutWidth = 1.5;
        private double pendingPlasterThickness;

        public OrthogonalDecisionPaletteControl()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            MinimumSize = new Size(600, 460);
            mode.Items.Add("项目执行");
            mode.Items.Add("方案研究");
            mode.SelectedIndex = 0;
            eliminatedFilter.Items.Add(new EliminatedFilterItem(null, "全部淘汰原因"));
            foreach (GuidedEliminatedGroup group in Enum.GetValues(
                typeof(GuidedEliminatedGroup)))
            {
                eliminatedFilter.Items.Add(new EliminatedFilterItem(
                    group,
                    OrthogonalDecisionGuidedText.FormatEliminatedGroup(group)));
            }
            eliminatedFilter.SelectedIndex = 0;
            foreach (ListBox list in CandidateLists())
            {
                list.Dock = DockStyle.Top;
                list.Height = 62;
                list.HorizontalScrollbar = true;
                list.MinimumSize = new Size(0, MinimumCandidateListHeight);
            }
            candidateDetail.MinimumSize = new Size(
                MinimumCandidateControlWidth,
                MinimumCandidateDetailHeight);
            diagnosticTiles.Dock = DockStyle.Top;
            diagnosticTiles.Height = 92;
            diagnosticTiles.HorizontalScrollbar = true;

            BuildLayout();
            WireEvents();
            RefreshView();
        }

        public event EventHandler<OrthogonalDecisionGuideActionEventArgs>
            GuideActionRequested;

        public event EventHandler<OrthogonalDecisionPreviewActionEventArgs>
            PreviewActionRequested;

        public event EventHandler DrawingFocusRequested;

        public event EventHandler FormalWritebackRequested;

        public OrthogonalDecisionGuidedWorkflow Workflow => workflow;

        public double PendingTileWidth => pendingTileWidth;

        public double PendingTileHeight => pendingTileHeight;

        public void StartNewSession()
        {
            refreshing = true;
            SuspendLayout();
            try
            {
            workflow = new OrthogonalDecisionGuidedWorkflow();
            renderedCandidateView = null;
            renderedEliminatedFilter = null;
            renderedEliminatedPageIndex = -1;
            renderedDiagnosticPlan = null;
            diagnosticTilesRendered = false;
            renderedDetailsResult = null;
            renderedDetailsPreviewPlan = null;
            renderedWallCornerPreferWallCornerAlignment = false;
            resultDetailsRendered = false;
            wallCornerDetailsRendered = false;
            renderedCandidateSearchStatistics = null;
            renderedNeutralRegionDetails = null;
            renderedWallCornerDetails = null;
            pendingTileWidth = 600.0;
            pendingTileHeight = 600.0;
            pendingGroutWidth = 1.5;
            pendingPlasterThickness = 0.0;
            tileWidth.Text = "600";
            tileHeight.Text = "600";
            groutWidth.Text = "1.5";
            plasterThickness.Text = "0";
            policyVersion.Text = "P-1";
            secondMinimum.Text = string.Empty;
            confirmRecommendedMinimum.Checked = false;
            absoluteMinimumUnconfirmed.Checked = true;
            visualConfirmationMode.Checked = false;
            mode.SelectedIndex = 0;
            wholeRoom.Checked = false;
            mainSecondary.Checked = false;
            showEliminatedCandidates.Checked = false;
            eliminatedFilter.SelectedIndex = 0;
            showAllAssessedTiles.Checked = false;
            showNeutralRegions.Checked = false;
            showWallCornerDiagnostics.Checked = false;
            preferWallCornerAlignment.Checked = false;
            neutralRegionDetailsToggle.Checked = false;
            }
            finally
            {
                try
                {
                    ResumeLayout(false);
                }
                finally
                {
                    refreshing = false;
                }
            }

            RefreshView();
        }

        public bool TryBeginAction(
            OrthogonalDecisionGuideAction action,
            out string disabledReason)
        {
            if (action == OrthogonalDecisionGuideAction.SelectRoom
                && !TryReadLayoutSettings(out disabledReason))
            {
                workflow.EndHostAction(disabledReason);
                RefreshView();
                return false;
            }

            bool started = workflow.BeginHostAction(action, out disabledReason);
            RefreshView();
            return started;
        }

        public void EndAction(string resultNotice)
        {
            workflow.EndHostAction(resultNotice);
            RefreshView();
        }

        public void MarkPreviewVisible()
        {
            workflow.MarkPreviewVisible();
            RefreshView();
        }

        public void MarkPreviewRefreshRequired(string message)
        {
            workflow.MarkPreviewRefreshRequired(message);
            RefreshView();
        }

        public void MarkPreviewCleared(string message)
        {
            workflow.MarkPreviewCleared(message);
            RefreshView();
        }

        public void MarkPreviewDisplayFailed(string message)
        {
            workflow.MarkPreviewDisplayFailed(message);
            RefreshView();
        }

        public void MarkFormalWritebackFailed(string message)
        {
            workflow.MarkFormalWritebackFailed(message);
            RefreshView();
        }

        public void MarkFormalWritebackSucceeded(int lineCount)
        {
            workflow.MarkFormalWritebackSucceeded(lineCount);
            RefreshView();
        }

        internal void RefreshAfterFormalWritebackOperation()
        {
            RefreshView();
        }

        public OrthogonalRoomValidationResult ApplyRoomBoundary(
            IReadOnlyCollection<LineSegment3D> boundaryLines)
        {
            OrthogonalRoomValidationResult result = workflow.LoadBoundary(
                boundaryLines,
                pendingTileWidth,
                pendingTileHeight,
                pendingGroutWidth,
                pendingPlasterThickness);
            if (result.IsValid)
            {
                confirmRecommendedMinimum.Checked = false;
            }
            RefreshView();
            return result;
        }

        public void ApplyControlRegion(AxisAlignedRectangle region)
        {
            workflow.SetControlRegion(region);
            RefreshView();
        }

        public void ApplyControlDoor(DoorOpening door)
        {
            workflow.SetControlDoor(door);
            RefreshView();
        }

        public void ApplyAutomaticallyLocatedDoor(
            AxisAlignedRectangle region,
            DoorOpening door)
        {
            workflow.SetAutomaticallyLocatedDoor(region, door);
            RefreshView();
        }

        public void ApplyMainRegion(AxisAlignedRectangle region)
        {
            workflow.SetMainRegionDraft(region);
            RefreshView();
        }

        public void ApplySecondaryRegion(AxisAlignedRectangle region)
        {
            workflow.SetSecondaryRegionDraft(region);
            RefreshView();
        }

        public void ApplyConnectionEdge(LineSegment3D edge)
        {
            workflow.SetConnectionEdge(edge);
            RefreshView();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 9,
                AutoScroll = false,
                Padding = new Padding(6)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            progress.AutoSize = true;
            progress.Font = new Font(Font, FontStyle.Bold);
            root.Controls.Add(progress, 0, 0);
            nextAction.AutoSize = true;
            nextAction.MaximumSize = new Size(560, 0);
            root.Controls.Add(nextAction, 0, 1);
            notice.AutoSize = true;
            notice.MaximumSize = new Size(560, 0);
            notice.ForeColor = Color.DarkBlue;
            root.Controls.Add(notice, 0, 2);

            root.Controls.Add(requirements, 0, 3);
            var detailHeader = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true
            };
            detailHeader.Controls.Add(engineeringToggle);
            invalidation.AutoSize = true;
            invalidation.MaximumSize = new Size(430, 0);
            invalidation.ForeColor = Color.DarkRed;
            detailHeader.Controls.Add(invalidation);
            root.Controls.Add(detailHeader, 0, 4);
            engineeringDetails.Height = 96;
            engineeringDetails.Dock = DockStyle.Top;
            engineeringDetails.Visible = false;
            root.Controls.Add(engineeringDetails, 0, 5);

            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.Add(BuildSetupPage());
            tabs.TabPages.Add(BuildGeometryPage());
            tabs.TabPages.Add(BuildCandidatesPage());
            tabs.TabPages.Add(BuildSummaryPage());
            root.Controls.Add(tabs, 0, 6);

            var navigation = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 6,
                RowCount = 1
            };
            for (int index = 0; index < 6; index++)
            {
                navigation.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 16.666f));
            }
            Button[] navigationButtons =
            {
                previous, next, modify, reselect, finish, cancelAll
            };
            for (int index = 0; index < navigationButtons.Length; index++)
            {
                navigationButtons[index].Dock = DockStyle.Fill;
                navigationButtons[index].AutoSize = false;
                navigationButtons[index].Height = 30;
                navigation.Controls.Add(navigationButtons[index], index, 0);
            }
            root.Controls.Add(navigation, 0, 7);
            navigationDisabledReason.AutoSize = true;
            navigationDisabledReason.MaximumSize = new Size(580, 0);
            root.Controls.Add(navigationDisabledReason, 0, 8);
            Controls.Add(root);
        }

        private TabPage BuildSetupPage()
        {
            var page = CreatePage("1 房间与规则");
            var panel = CreateStepPanel();
            panel.Controls.Add(CreateSectionLabel("房间和地砖"));
            panel.Controls.Add(CreateFieldRow("砖宽（mm）", tileWidth));
            panel.Controls.Add(CreateFieldRow("砖高（mm）", tileHeight));
            panel.Controls.Add(CreateFieldRow("砖间灰缝（mm）", groutWidth));
            panel.Controls.Add(CreateFieldRow("抹灰完成面厚度（mm）", plasterThickness));
            panel.Controls.Add(new Label
            {
                Text = "灰缝允许为 0，必须是有限非负数；砖墙之间按灰缝一半计算。正抹灰厚度表示从原始边界向房间内部生成完成面。",
                AutoSize = true,
                MaximumSize = new Size(520, 0)
            });
            panel.Controls.Add(applyLayoutSettings);
            panel.Controls.Add(layoutSettingsStatus);
            panel.Controls.Add(selectRoom);
            panel.Controls.Add(roomStatus);
            panel.Controls.Add(roomDisabledReason);
            panel.Controls.Add(CreateSectionLabel("项目铺贴规则"));
            panel.Controls.Add(recommendedMinimum);
            panel.Controls.Add(confirmRecommendedMinimum);
            panel.Controls.Add(CreateFieldRow(
                "项目绝对下限（mm）",
                secondMinimum));
            panel.Controls.Add(absoluteMinimumUnconfirmed);
            panel.Controls.Add(visualConfirmationMode);
            panel.Controls.Add(new Label
            {
                Text = "项目绝对下限请选择一种状态：填写数值、按图面确认，或保持尚未决定。按图面确认只允许明确选择后的候选进入预览和正式写回。",
                AutoSize = true,
                MaximumSize = new Size(520, 0)
            });
            panel.Controls.Add(preferWallCornerAlignment);
            panel.Controls.Add(new Label
            {
                Text = "勾选后才运行可选质量候选搜索，并在可保留候选中优先避开入口第一视觉范围内低于推荐下限的窄砖，再考虑角点的 2/3 安全分格；关闭时沿用 G1 候选生成与顺序。绝对下限硬淘汰和原始指标不变。",
                AutoSize = true,
                MaximumSize = new Size(520, 0)
            });
            panel.Controls.Add(applyProject);
            panel.Controls.Add(projectStatus);
            panel.Controls.Add(new Label
            {
                Text = "房间内部区域由程序自动分解，仅作为计算和只读核对依据。"
                    + "自动区域共享边两侧沿用同一套全房砖缝相位，不在共享边重新起铺。",
                AutoSize = true,
                MaximumSize = new Size(520, 0)
            });
            page.Controls.Add(panel);
            return page;
        }

        private TabPage BuildGeometryPage()
        {
            var page = CreatePage("2 门洞");
            var panel = CreateStepPanel();
            panel.Controls.Add(new Label
            {
                Text = "在完成面外边界上依次捕捉门洞两侧边缘；已设置抹灰时也可捕捉原始外墙，程序会同步到完成面。"
                    + "无需选择主要区、相邻区或接合边。",
                AutoSize = true,
                MaximumSize = new Size(620, 0)
            });
            panel.Controls.Add(CreateSelectionRow(
                selectDoor,
                doorStatus,
                doorReason));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage BuildCandidatesPage()
        {
            var page = CreatePage("3 选择方案");
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(candidateSearchStatistics, 0, 0);

            var views = new TabControl { Dock = DockStyle.Fill };
            var availablePage = CreatePage("可保留方案");
            var availableRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(2)
            };
            availableRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            availableRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var availableViewport = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(2)
            };
            var availableContent = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            var candidateGroups = new TabControl
            {
                Dock = DockStyle.None,
                Height = 180,
                MinimumSize = new Size(
                    MinimumCandidateControlWidth,
                    MinimumCandidateListHeight),
                Margin = new Padding(0, 0, 0, 6)
            };
            automaticCandidates.Dock = DockStyle.Fill;
            manualCandidates.Dock = DockStyle.Fill;
            missingPolicyCandidates.Dock = DockStyle.Fill;
            var automaticPage = CreatePage("满足规则");
            automaticPage.Controls.Add(automaticCandidates);
            var manualPage = CreatePage("待项目复核");
            manualPage.Controls.Add(manualCandidates);
            var missingPage = CreatePage("规则缺失");
            missingPage.Controls.Add(missingPolicyCandidates);
            candidateGroups.TabPages.Add(automaticPage);
            candidateGroups.TabPages.Add(manualPage);
            candidateGroups.TabPages.Add(missingPage);
            availableContent.Controls.Add(candidateGroups);
            candidateDetail.Dock = DockStyle.None;
            candidateDetail.AutoSize = false;
            candidateDetail.Height = MinimumCandidateDetailHeight;
            candidateDetail.MinimumSize = new Size(
                MinimumCandidateControlWidth,
                MinimumCandidateDetailHeight);
            candidateDetail.Margin = new Padding(0, 0, 0, 8);
            availableContent.Controls.Add(candidateDetail);
            reviewNotice.Dock = DockStyle.None;
            reviewNotice.AutoSize = true;
            reviewNotice.MaximumSize = new Size(560, 0);
            reviewNotice.Margin = new Padding(0, 0, 0, 8);
            availableContent.Controls.Add(reviewNotice);

            availableViewport.Controls.Add(availableContent);
            availableViewport.ClientSizeChanged += (sender, args) =>
                FitScrollableContent(availableViewport, availableContent);
            FitScrollableContent(availableViewport, availableContent);
            availableRoot.Controls.Add(availableViewport, 0, 0);

            var candidateActionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = Padding.Empty,
                Padding = new Padding(2, 4, 2, 2)
            };
            candidateActionBar.Controls.Add(inspectCandidate);
            candidateActionBar.Controls.Add(inspectionDisabledReason);
            availableRoot.Controls.Add(candidateActionBar, 0, 1);
            availablePage.Controls.Add(availableRoot);
            views.TabPages.Add(availablePage);

            var auditPage = CreatePage("淘汰方案审计");
            var audit = CreateStepPanel();
            audit.Controls.Add(new Label
            {
                Text = "淘汰方案只保留文字事实，不能图面诊断、确认采用或写回。",
                AutoSize = true,
                MaximumSize = new Size(620, 0)
            });
            audit.Controls.Add(showEliminatedCandidates);
            audit.Controls.Add(eliminatedFilter);
            audit.Controls.Add(unavailableCandidates);
            var eliminatedNavigation = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = false
            };
            eliminatedNavigation.Controls.Add(previousEliminatedPage);
            eliminatedNavigation.Controls.Add(nextEliminatedPage);
            eliminatedNavigation.Controls.Add(eliminatedPageStatus);
            audit.Controls.Add(eliminatedNavigation);
            auditPage.Controls.Add(audit);
            views.TabPages.Add(auditPage);
            root.Controls.Add(views, 0, 1);
            page.Controls.Add(root);
            return page;
        }

        private TabPage BuildSummaryPage()
        {
            var page = CreatePage("4 图面核对");
            var views = new TabControl { Dock = DockStyle.Fill };
            var drawingPage = CreatePage("预览与窄砖诊断");
            var drawing = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(2)
            };
            drawing.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            drawing.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            drawing.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            drawing.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            drawing.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            drawing.Controls.Add(new Label
            {
                Text = "先在第 3 页选择或确认可保留方案，再显示同源临时矢量。"
                    + "窄砖轮廓、尺寸和原因随预览显示；正式写回只在最后确认后执行。",
                AutoSize = true,
                Dock = DockStyle.Fill,
                MaximumSize = new Size(560, 0)
            }, 0, 0);
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true
            };
            actions.Controls.Add(preview);
            actions.Controls.Add(refreshPreview);
            actions.Controls.Add(cancelPreview);
            actions.Controls.Add(focusDrawing);
            actions.Controls.Add(writeToDrawing);
            actions.Controls.Add(writebackDisabledReason);
            drawing.Controls.Add(actions, 0, 1);
            drawing.Controls.Add(previewDisabledReason, 0, 2);

            var options = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            options.Controls.Add(showAllAssessedTiles);
            options.Controls.Add(showNeutralRegions);
            options.Controls.Add(showWallCornerDiagnostics);
            drawing.Controls.Add(options, 0, 3);

            var diagnosticViews = new TabControl { Dock = DockStyle.Fill };
            var narrowTilePage = CreatePage("窄砖诊断");
            var narrowTileLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            narrowTileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            narrowTileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            diagnosticTiles.Dock = DockStyle.Fill;
            diagnosticTileDetail.Dock = DockStyle.Fill;
            narrowTileLayout.Controls.Add(diagnosticTiles, 0, 0);
            narrowTileLayout.Controls.Add(diagnosticTileDetail, 0, 1);
            narrowTilePage.Controls.Add(narrowTileLayout);
            diagnosticViews.TabPages.Add(narrowTilePage);

            var structurePage = CreatePage("房间结构（只读）");
            neutralRegionDetails.Dock = DockStyle.Fill;
            neutralRegionDetails.Visible = true;
            structurePage.Controls.Add(neutralRegionDetails);
            diagnosticViews.TabPages.Add(structurePage);

            var wallCornerPage = CreatePage("角点分格诊断（只读）");
            wallCornerDetails.Dock = DockStyle.Fill;
            wallCornerPage.Controls.Add(wallCornerDetails);
            diagnosticViews.TabPages.Add(wallCornerPage);
            drawing.Controls.Add(diagnosticViews, 0, 4);
            drawingPage.Controls.Add(drawing);
            views.TabPages.Add(drawingPage);

            var summaryPage = CreatePage("只读汇总");
            summary.Dock = DockStyle.Fill;
            summaryPage.Controls.Add(summary);
            views.TabPages.Add(summaryPage);
            page.Controls.Add(views);
            return page;
        }

        private void WireEvents()
        {
            selectRoom.Click += (sender, args) => RequestRoomSelection(
                workflow.Input.HasRoom);
            selectControlRegion.Click += (sender, args) => RequestAction(
                OrthogonalDecisionGuideAction.SelectControlRegion);
            selectDoor.Click += (sender, args) => RequestAction(
                OrthogonalDecisionGuideAction.SelectControlDoor);
            selectMainRegion.Click += (sender, args) => RequestAction(
                OrthogonalDecisionGuideAction.SelectMainRegion);
            selectSecondaryRegion.Click += (sender, args) => RequestAction(
                OrthogonalDecisionGuideAction.SelectSecondaryRegion);
            selectConnectionEdge.Click += (sender, args) => RequestAction(
                OrthogonalDecisionGuideAction.SelectConnectionEdge);
            applyLayoutSettings.Click += OnApplyLayoutSettings;
            applyProject.Click += OnApplyProject;
            applyIntent.Click += OnApplyIntent;
            automaticCandidates.SelectedIndexChanged += OnCandidateSelected;
            manualCandidates.SelectedIndexChanged += OnCandidateSelected;
            missingPolicyCandidates.SelectedIndexChanged += OnCandidateSelected;
            unavailableCandidates.SelectedIndexChanged += OnCandidateSelected;
            diagnosticTiles.SelectedIndexChanged += OnDiagnosticTileSelected;
            inspectCandidate.Click += OnComparisonPreviewRequested;
            preview.Click += OnPreviewRequested;
            refreshPreview.Click += OnPreviewRefreshRequested;
            cancelPreview.Click += OnPreviewClearRequested;
            writeToDrawing.Click += OnFormalWritebackButtonClicked;
            focusDrawing.Click += (sender, args) =>
            {
                EventHandler handler = DrawingFocusRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            previous.Click += (sender, args) =>
            {
                workflow.MovePrevious();
                RefreshView();
            };
            next.Click += (sender, args) =>
            {
                string reason;
                workflow.MoveNext(out reason);
                RefreshView();
            };
            modify.Click += (sender, args) =>
            {
                QueuePreviewClearIfNeeded();
                workflow.BeginModify(StepForProductStage(tabs.SelectedIndex));
                RefreshView();
            };
            reselect.Click += (sender, args) => RequestRoomSelection(true);
            finish.Click += (sender, args) =>
            {
                QueuePreviewClearIfNeeded();
                string reason;
                workflow.Complete(out reason);
                RefreshView();
            };
            cancelAll.Click += (sender, args) =>
            {
                if (OrthogonalDecisionPaletteHost.IsFormalWritebackInProgress
                    || workflow.FormalWritebackAwaitingCompletion)
                {
                    workflow.EndHostAction(
                        "正式写回结果尚未返回，请等待写回完成后再取消当前向导。" );
                    RefreshView();
                    return;
                }

                if (!workflow.FormalWritebackCompleted)
                {
                    QueuePreviewClearIfNeeded();
                }
                refreshing = true;
                try
                {
                    workflow.CancelAll();
                    confirmRecommendedMinimum.Checked = false;
                    absoluteMinimumUnconfirmed.Checked = true;
                    visualConfirmationMode.Checked = false;
                    secondMinimum.Text = string.Empty;
                    showEliminatedCandidates.Checked = false;
                    eliminatedFilter.SelectedIndex = 0;
                    showAllAssessedTiles.Checked = false;
                    showNeutralRegions.Checked = false;
                    showWallCornerDiagnostics.Checked = false;
                    preferWallCornerAlignment.Checked = false;
                    neutralRegionDetailsToggle.Checked = false;
                }
                finally
                {
                    refreshing = false;
                }

                RefreshView();
            };
            engineeringToggle.CheckedChanged += (sender, args) => RefreshView();
            absoluteMinimumUnconfirmed.CheckedChanged +=
                OnProjectAbsoluteMinimumModeChanged;
            visualConfirmationMode.CheckedChanged +=
                OnProjectAbsoluteMinimumModeChanged;
            showEliminatedCandidates.CheckedChanged += (sender, args) =>
            {
                if (!refreshing)
                {
                    RefreshView();
                }
            };
            eliminatedFilter.SelectedIndexChanged += (sender, args) =>
            {
                if (refreshing)
                {
                    return;
                }

                var item = eliminatedFilter.SelectedItem as EliminatedFilterItem;
                workflow.SetEliminatedFilter(item == null ? null : item.Group);
                RefreshView();
            };
            previousEliminatedPage.Click += (sender, args) =>
            {
                workflow.MoveEliminatedPage(-1);
                RefreshView();
            };
            nextEliminatedPage.Click += (sender, args) =>
            {
                workflow.MoveEliminatedPage(1);
                RefreshView();
            };
            showAllAssessedTiles.CheckedChanged += OnDiagnosticOptionsChanged;
            showNeutralRegions.CheckedChanged += OnDiagnosticOptionsChanged;
            showWallCornerDiagnostics.CheckedChanged +=
                OnDiagnosticOptionsChanged;
            preferWallCornerAlignment.CheckedChanged +=
                OnWallCornerPreferenceChanged;
            tabs.SelectedIndexChanged += OnTabSelected;
        }

        private void OnProjectAbsoluteMinimumModeChanged(
            object sender,
            EventArgs args)
        {
            if (updatingProjectAbsoluteMinimumMode)
            {
                UpdateProjectAbsoluteMinimumModeControls();
                return;
            }

            updatingProjectAbsoluteMinimumMode = true;
            try
            {
                if (ReferenceEquals(sender, visualConfirmationMode)
                    && visualConfirmationMode.Checked)
                {
                    absoluteMinimumUnconfirmed.Checked = false;
                }
                else if (ReferenceEquals(sender, absoluteMinimumUnconfirmed)
                    && absoluteMinimumUnconfirmed.Checked)
                {
                    visualConfirmationMode.Checked = false;
                }
            }
            finally
            {
                updatingProjectAbsoluteMinimumMode = false;
            }

            UpdateProjectAbsoluteMinimumModeControls();
            if (!refreshing)
            {
                RefreshStatusOnly();
            }
        }

        private void UpdateProjectAbsoluteMinimumModeControls()
        {
            secondMinimum.Enabled = !absoluteMinimumUnconfirmed.Checked
                && !visualConfirmationMode.Checked;
        }

        private void OnApplyProject(object sender, EventArgs args)
        {
            double? minimum = null;
            ProjectAbsoluteMinimumMode minimumMode =
                ProjectAbsoluteMinimumMode.NotDecided;
            if (visualConfirmationMode.Checked)
            {
                minimumMode = ProjectAbsoluteMinimumMode.VisualConfirmation;
            }
            else if (!absoluteMinimumUnconfirmed.Checked)
            {
                double parsed;
                if (!OrthogonalDecisionGuidedText.TryParsePositiveMillimeters(
                    secondMinimum.Text,
                    out parsed))
                {
                    workflow.EndHostAction(
                        "项目绝对下限必须是有限正数；如不填写数值，请选择“按图面确认”或“项目绝对下限尚未确认”。" );
                    RefreshView();
                    return;
                }

                minimum = parsed;
                minimumMode = ProjectAbsoluteMinimumMode.Numeric;
            }

            QueuePreviewClearIfNeeded();
            try
            {
                workflow.ApplyOrdinaryProjectRules(
                    minimum,
                    confirmRecommendedMinimum.Checked,
                    minimumMode);
            }
            catch (ArgumentException error)
            {
                workflow.EndHostAction(
                    error.ParamName == "recommendedMinimumIsConfirmed"
                        ? "请先确认固定推荐下限 0.42T。"
                        : "项目绝对下限必须大于 0，且不得高于 X/Y 两轴推荐下限中的较小值。" );
            }
            RefreshView();
        }

        private void OnApplyIntent(object sender, EventArgs args)
        {
            if (!wholeRoom.Checked && !mainSecondary.Checked)
            {
                workflow.EndHostAction(
                    "请选择“整房连续铺贴”或“分区铺贴”。" );
                RefreshView();
                return;
            }

            QueuePreviewClearIfNeeded();
            workflow.SetLayoutIntent(
                mainSecondary.Checked
                    ? RoomLayoutIntent.MainSecondary
                    : RoomLayoutIntent.WholeRoomSinglePhase);
            RefreshView();
        }

        private void OnWallCornerPreferenceChanged(
            object sender,
            EventArgs args)
        {
            if (refreshing)
            {
                return;
            }

            QueuePreviewClearIfNeeded();
            workflow.SetWallCornerAlignmentPreference(
                preferWallCornerAlignment.Checked);
            RefreshView();
        }

        private void OnCandidateSelected(object sender, EventArgs args)
        {
            if (refreshing)
            {
                return;
            }

            var list = (ListBox)sender;
            var item = list.SelectedItem as CandidateItem;
            if (item == null)
            {
                return;
            }

            refreshing = true;
            try
            {
                foreach (ListBox other in CandidateLists())
                {
                    if (!ReferenceEquals(other, list))
                    {
                        other.ClearSelected();
                    }
                }
            }
            finally
            {
                refreshing = false;
            }

            bool hadDifferentPreview = workflow.PreviewPlan != null
                && workflow.PreviewPlan.CandidateId
                    != item.Value.Candidate.Id;
            bool canAutoPreview = item.Value.Candidate.State
                == LayoutCandidateState.AutomaticUsable
                || item.Value.Candidate.State
                    == LayoutCandidateState.RequiresUserDecision
                || item.Value.Candidate.State
                    == LayoutCandidateState.RequiresProjectPolicy
                    && workflow.Input.Policy != null
                    && workflow.Input.Policy.AllowsVisualConfirmation;
            if (!workflow.TrySelectCandidate(item.Value.Candidate.Id))
            {
                return;
            }

            candidateDetail.Text = FormatFriendlyCandidate(item.Value);
            bool previewRequested = canAutoPreview
                && workflow.TryRequestPreview(out _);
            if (previewRequested)
            {
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Show);
            }
            else if (hadDifferentPreview)
            {
                // TrySelectCandidate invalidates the workflow plan before this
                // point. Clear the old AutoCAD transient explicitly when the
                // newly selected candidate cannot produce a preview.
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Clear);
            }

            RefreshStatusOnly();
        }

        private void OnDiagnosticTileSelected(object sender, EventArgs args)
        {
            if (refreshing)
            {
                return;
            }

            var item = diagnosticTiles.SelectedItem as DiagnosticTileItem;
            if (item == null || !workflow.SelectDiagnosticTile(item.Tile.Id))
            {
                return;
            }

            diagnosticTileDetail.Text =
                OrthogonalDecisionGuidedText.FormatDiagnosticTile(item.Tile);
            RefreshTransientDiagnosticIfVisible();
            RefreshStatusOnly();
        }

        private void OnDiagnosticOptionsChanged(object sender, EventArgs args)
        {
            if (refreshing)
            {
                return;
            }

            workflow.SetDiagnosticDisplayOptions(
                showAllAssessedTiles.Checked,
                showNeutralRegions.Checked,
                showWallCornerDiagnostics.Checked);
            RefreshTransientDiagnosticIfVisible();
            RefreshStatusOnly();
        }

        private void RefreshTransientDiagnosticIfVisible()
        {
            if (workflow.CanRefreshPreview)
            {
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Refresh);
            }
        }

        private void OnFormalWritebackButtonClicked(
            object sender,
            EventArgs args)
        {
            if (OrthogonalDecisionPaletteHost.IsFormalWritebackInProgress)
            {
                workflow.EndHostAction(
                    "上一笔正式写回仍在执行，请等待 AutoCAD 完成后再操作。" );
                RefreshView();
                return;
            }

            string disabledReason = workflow.GetFormalWritebackDisabledReason();
            if (!string.IsNullOrWhiteSpace(disabledReason))
            {
                workflow.EndHostAction(disabledReason);
                RefreshView();
                return;
            }

            DialogResult confirmation = MessageBox.Show(
                this,
                workflow.GetFormalWritebackConfirmationMessage(),
                "确认并写入图纸",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.OK)
            {
                workflow.EndHostAction("已取消正式写回；图纸没有变化。" );
                RefreshView();
                return;
            }

            string error;
            if (!workflow.TryAcknowledgeFormalWriteback(out error))
            {
                workflow.EndHostAction(error);
                RefreshView();
                return;
            }

            EventHandler handler = FormalWritebackRequested;
            if (handler == null)
            {
                workflow.MarkFormalWritebackFailed(
                    "当前没有可用的 AutoCAD 正式写回桥接" );
                RefreshView();
                return;
            }

            handler(this, EventArgs.Empty);
            RefreshView();
        }

        private void OnPreviewRequested(object sender, EventArgs args)
        {
            LayoutCandidate candidate;
            if (workflow.TryRequestPreview(out candidate))
            {
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Show);
            }

            RefreshStatusOnly();
        }

        private void OnComparisonPreviewRequested(object sender, EventArgs args)
        {
            LayoutCandidate candidate;
            if (workflow.TryRequestComparisonPreview(out candidate))
            {
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Show);
            }

            RefreshStatusOnly();
        }

        private void OnPreviewRefreshRequested(object sender, EventArgs args)
        {
            string disabledReason;
            if (workflow.TryRequestPreviewRefresh(out disabledReason))
            {
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Refresh);
            }

            RefreshStatusOnly();
        }

        private void OnPreviewClearRequested(object sender, EventArgs args)
        {
            if (workflow.BeginClearPreview())
            {
                RaisePreviewAction(OrthogonalDecisionPreviewAction.Clear);
            }

            RefreshStatusOnly();
        }

        private void RaisePreviewAction(OrthogonalDecisionPreviewAction action)
        {
            EventHandler<OrthogonalDecisionPreviewActionEventArgs> handler =
                PreviewActionRequested;
            if (handler == null)
            {
                workflow.MarkPreviewDisplayFailed(
                    "当前没有可用的 AutoCAD 临时预览桥接");
                return;
            }

            handler(this, new OrthogonalDecisionPreviewActionEventArgs(action));
        }

        private void QueuePreviewClearIfNeeded()
        {
            if (workflow.PreviewState == OrthogonalDecisionPreviewState.None)
            {
                return;
            }

            EventHandler<OrthogonalDecisionPreviewActionEventArgs> handler =
                PreviewActionRequested;
            if (handler != null)
            {
                handler(
                    this,
                    new OrthogonalDecisionPreviewActionEventArgs(
                        OrthogonalDecisionPreviewAction.Clear));
            }
        }

        private void OnTabSelected(object sender, EventArgs args)
        {
            if (refreshing || tabs.SelectedIndex < 0)
            {
                return;
            }

            workflow.ViewStep(StepForProductStage(tabs.SelectedIndex));
            RefreshStatusOnly();
        }

        private void RequestRoomSelection(bool clearExisting)
        {
            if (clearExisting)
            {
                DialogResult confirm = MessageBox.Show(
                    this,
                    "重选会清除旧房间、图面重点、排版方案和人工确认记录，"
                        + "但保留项目规则与使用方式。是否继续？",
                    "重选房间",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.OK)
                {
                    return;
                }

                QueuePreviewClearIfNeeded();
                workflow.BeginRoomReselection();
            }

            RequestAction(OrthogonalDecisionGuideAction.SelectRoom);
        }

        private void RequestAction(OrthogonalDecisionGuideAction action)
        {
            string disabledReason;
            if (!TryBeginAction(action, out disabledReason))
            {
                return;
            }

            EventHandler<OrthogonalDecisionGuideActionEventArgs> handler =
                GuideActionRequested;
            if (handler == null)
            {
                EndAction("当前没有可用的 AutoCAD 图面选择桥接。" );
                return;
            }

            handler(this, new OrthogonalDecisionGuideActionEventArgs(action));
        }

        private bool TryReadLayoutSettings(out string error)
        {
            error = string.Empty;
            if (!OrthogonalDecisionGuidedText.TryParsePositiveMillimeters(
                tileWidth.Text,
                out pendingTileWidth))
            {
                error = "砖宽必须是有限正数。";
                return false;
            }

            if (!OrthogonalDecisionGuidedText.TryParsePositiveMillimeters(
                tileHeight.Text,
                out pendingTileHeight))
            {
                error = "砖高必须是有限正数。";
                return false;
            }

            if (!OrthogonalDecisionGuidedText.TryParseNonNegativeMillimeters(
                groutWidth.Text,
                out pendingGroutWidth))
            {
                error = "灰缝必须是有限非负数；默认值为 1.5 mm。";
                return false;
            }

            if (!OrthogonalDecisionGuidedText.TryParseNonNegativeMillimeters(
                plasterThickness.Text,
                out pendingPlasterThickness))
            {
                error = "抹灰完成面厚度必须是有限非负数；负值不支持。";
                return false;
            }

            return true;
        }

        private void OnApplyLayoutSettings(object sender, EventArgs args)
        {
            string error;
            if (!TryReadLayoutSettings(out error))
            {
                workflow.EndHostAction(error);
                RefreshView();
                return;
            }

            QueuePreviewClearIfNeeded();
            try
            {
                workflow.ApplyLayoutSettings(
                    pendingTileWidth,
                    pendingTileHeight,
                    pendingGroutWidth,
                    pendingPlasterThickness);
                layoutSettingsStatus.Text =
                    "砖、灰缝和抹灰完成面设置已保存；旧预览已失效。";
            }
            catch (ArgumentException exception)
            {
                workflow.EndHostAction(exception.Message);
                layoutSettingsStatus.Text = exception.Message;
            }

            RefreshView();
        }

        private void RefreshView()
        {
            refreshing = true;
            SuspendLayout();
            try
            {
            int productStage = ProductStageIndex(workflow.ActiveStep);
            progress.Text = string.Format(
                CultureInfo.CurrentCulture,
                "阶段 {0}/4：{1}（当前事项：{2}）",
                productStage + 1,
                ProductStageTitles[productStage],
                InternalStepTitles[(int)workflow.ActiveStep - 1]);
            notice.Text = workflow.Notice;
            notice.Visible = !string.IsNullOrWhiteSpace(notice.Text);
            invalidation.Text = workflow.InvalidationNotice;
            invalidation.Visible = !string.IsNullOrWhiteSpace(invalidation.Text);
            tabs.SelectedIndex = productStage;
            UpdateProjectAbsoluteMinimumModeControls();
            preferWallCornerAlignment.Checked = workflow.PreferWallCornerAlignment;
            preferWallCornerAlignment.Enabled = workflow.Input.HasRoom;
            toolTip.SetToolTip(
                preferWallCornerAlignment,
                "默认关闭。勾选后运行墙角锚定候选搜索并改变推荐顺序；关闭时沿用 G1 候选生成与顺序。不会放宽绝对下限，也不写入图纸。" );

            requirements.Text = FormatFriendlyRequirements();
            engineeringDetails.Visible = engineeringToggle.Checked;
            requirements.Height = 46;

            roomStatus.Text = workflow.Input.HasRoom
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "已验证房间；砖规格 {0:0.###} × {1:0.###} mm。",
                    workflow.Input.TileWidth,
                    workflow.Input.TileHeight)
                    + string.Format(
                        CultureInfo.CurrentCulture,
                        "；灰缝 {0:0.###} mm；抹灰完成面 {1:0.###} mm。",
                        workflow.Input.GroutWidthMm,
                        workflow.Input.PlasterThicknessMm)
                    + (workflow.Input.BoundaryWasNormalized
                        ? Environment.NewLine
                            + "已按近似正交规则建立只读计算副本；原始 LINE 未修改。"
                        : string.Empty)
                : "尚未选择房间边界。";
            recommendedMinimum.Text = workflow.Input.HasRoom
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    "固定推荐下限 0.42T：X 轴 {0:0.###} mm；Y 轴 {1:0.###} mm。"
                        + "等于阈值视为满足。该值不可编辑，保存项目前必须明确确认。",
                    workflow.Input.TileWidth * EngineeringLayoutRules.DefaultMinimumCutRatio,
                    workflow.Input.TileHeight * EngineeringLayoutRules.DefaultMinimumCutRatio)
                : "固定推荐下限 0.42T：选择房间并确认砖规格后显示 X/Y 毫米值。";
            projectStatus.Text = !workflow.Input.HasRoom
                ? "当前不能应用：请先在图中选择并验证房间边界。"
                : workflow.Input.Policy == null
                    ? "项目规则尚未确认。"
                    : "项目规则已确认；项目绝对下限："
                    + (workflow.Input.Policy.HasProjectAbsoluteMinimum
                        ? workflow.Input.Policy.ProjectAbsoluteMinimumCut.Value.ToString(
                            "0.###", CultureInfo.CurrentCulture) + " mm"
                        : workflow.Input.Policy.AllowsVisualConfirmation
                            ? "按图面确认（不设置数值）"
                            : "尚未决定") + "。";
            intentStatus.Text = workflow.Input.Policy == null
                ? "当前不能应用：请先完成项目规则。"
                : "当前：" + OrthogonalDecisionGuidedText.FormatIntent(
                    workflow.Input.LayoutIntent) + "。";
            controlRegionStatus.Text = workflow.Input.ControlRegion == null
                ? "门洞影响范围：未选择"
                : "门洞影响范围：已选择";
            doorStatus.Text = workflow.Input.ControlDoor == null
                ? "门洞：未选择"
                : workflow.DoorSelectionCoversEntireBoundarySegment()
                    ? "门洞：已选择，但两点覆盖整段墙线；请重选实际门洞两侧边缘"
                    : "门洞：已选择";
            mainRegionStatus.Text = workflow.MainRegionDraft == null
                ? "主要铺贴区：未选择"
                : "主要铺贴区：已选择";
            secondaryRegionStatus.Text = workflow.SecondaryRegionDraft == null
                ? "相邻铺贴区：未选择"
                : "相邻铺贴区：已选择";
            connectionStatus.Text = workflow.Input.SelectedConnectionEdge.HasValue
                ? workflow.ConnectionSelectionDoesNotMatchValidatedBoundary()
                    ? "两区接合边：已选择，但位置不匹配；请重选"
                        + workflow.DescribeExpectedConnectionEdge()
                    : "两区接合边：已选择"
                : "两区接合边：未选择";
            mainSecondaryPanel.Visible = workflow.ShowsMainSecondaryControls;

            RefreshExpensiveReadOnlyDetails();

            RefreshCandidates();
            summary.Text = workflow.BuildOrdinarySummary();
            if (engineeringToggle.Checked)
            {
                engineeringDetails.Text = FormatEngineeringDetails();
            }

            engineeringDetails.Visible = engineeringToggle.Checked;
            RefreshActionStates();
            }
            finally
            {
                try
                {
                    ResumeLayout(true);
                }
                finally
                {
                    refreshing = false;
                }
            }
        }

        private void RefreshExpensiveReadOnlyDetails()
        {
            EngineeringOrthogonalDecisionResult result = workflow.Input.Result;
            bool preferWallCornerAlignment =
                workflow.PreferWallCornerAlignment;
            LayoutDrawingPlan previewPlan = workflow.PreviewPlan;
            if (!resultDetailsRendered
                || !ReferenceEquals(renderedDetailsResult, result)
                || renderedDetailsPreferWallCornerAlignment
                    != preferWallCornerAlignment)
            {
                renderedDetailsResult = result;
                renderedDetailsPreferWallCornerAlignment =
                    preferWallCornerAlignment;
                resultDetailsRendered = true;
                renderedCandidateSearchStatistics = result == null
                        || result.RawResult == null
                    ? "候选搜索统计：尚未生成。"
                    : OrthogonalDecisionGuidedText.FormatCandidateGenerationReport(
                        result.RawResult.GenerationReport,
                        preferWallCornerAlignment)
                        + string.Format(
                            CultureInfo.CurrentCulture,
                            " 当前分组：满足规则 {0}、待复核 {1}、规则缺失 {2}、硬淘汰 {3}。",
                            workflow.RuleSatisfiedCandidates.Count,
                            workflow.ReviewCandidates.Count,
                            workflow.MissingRuleCandidates.Count,
                            workflow.EliminatedCandidateCount)
                        + (preferWallCornerAlignment
                            ? "；当前已启用可选质量搜索及排序（入口视觉窄砖 → 2/3 安全分格 → 盲区窄砖）"
                            : "；当前按 G1 原始顺序展示，可选质量未参与生成或排序");
                renderedNeutralRegionDetails = result == null
                        || result.RawResult == null
                    ? "房间结构参考：尚未生成。"
                    : OrthogonalDecisionGuidedText.FormatNeutralRegionReference(
                        result.RawResult.NeutralRegionPartition);
            }

            if (!wallCornerDetailsRendered
                || renderedWallCornerPreferWallCornerAlignment
                    != preferWallCornerAlignment
                || !ReferenceEquals(renderedDetailsPreviewPlan, previewPlan))
            {
                renderedDetailsPreviewPlan = previewPlan;
                renderedWallCornerPreferWallCornerAlignment =
                    preferWallCornerAlignment;
                wallCornerDetailsRendered = true;
                renderedWallCornerDetails = preferWallCornerAlignment
                    ? OrthogonalDecisionGuidedText.FormatWallCornerDiagnostics(
                        previewPlan)
                    : "角点分格诊断：当前未启用可选质量优先，"
                        + "该事实不参与候选生成、排序或确认。"
                        + "如需查看原始角点尺寸和命中事实，请展开“工程详情”。";
            }

            candidateSearchStatistics.Text = renderedCandidateSearchStatistics;
            neutralRegionDetails.Text = renderedNeutralRegionDetails;
            wallCornerDetails.Text = renderedWallCornerDetails;
            neutralRegionDetails.Visible = true;
        }

        private void RefreshCandidates()
        {
            bool candidateViewChanged = !ReferenceEquals(
                    renderedCandidateView,
                    workflow.Candidates)
                || renderedShowEliminatedCandidates
                    != showEliminatedCandidates.Checked
                || renderedEliminatedFilter != workflow.EliminatedFilter
                || renderedEliminatedPageIndex
                    != workflow.EliminatedPageIndex;
            if (candidateViewChanged)
            {
                ReplaceCandidateItems(
                    automaticCandidates,
                    workflow.RuleSatisfiedCandidates);
                ReplaceCandidateItems(
                    manualCandidates,
                    workflow.ReviewCandidates);
                ReplaceCandidateItems(
                    missingPolicyCandidates,
                    workflow.MissingRuleCandidates);
                ReplaceCandidateItems(
                    unavailableCandidates,
                    showEliminatedCandidates.Checked
                        ? workflow.EliminatedCandidatePage
                        : Enumerable.Empty<GuidedCandidatePresentation>());

                renderedCandidateView = workflow.Candidates;
                renderedShowEliminatedCandidates =
                    showEliminatedCandidates.Checked;
                renderedEliminatedFilter = workflow.EliminatedFilter;
                renderedEliminatedPageIndex = workflow.EliminatedPageIndex;
            }

            eliminatedFilter.Visible = showEliminatedCandidates.Checked;
            unavailableCandidates.Visible = showEliminatedCandidates.Checked;
            previousEliminatedPage.Visible = showEliminatedCandidates.Checked;
            nextEliminatedPage.Visible = showEliminatedCandidates.Checked;
            eliminatedPageStatus.Visible = showEliminatedCandidates.Checked;
            eliminatedPageStatus.Text = string.Format(
                CultureInfo.CurrentCulture,
                "第 {0}/{1} 页；当前筛选 {2} / 全部 {3} 个硬淘汰候选；每页最多 {4} 个。",
                workflow.EliminatedPageIndex + 1,
                workflow.EliminatedPageCount,
                workflow.FilteredEliminatedCandidateCount,
                workflow.EliminatedCandidateCount,
                OrthogonalDecisionGuidedWorkflow.EliminatedCandidatePageSize);

            EvaluatedLayoutCandidate selected = workflow.Palette.SelectedCandidate;
            if (selected != null)
            {
                foreach (ListBox list in CandidateLists())
                {
                    for (int index = 0; index < list.Items.Count; index++)
                    {
                        var item = (CandidateItem)list.Items[index];
                        if (item.Value.Candidate.Id == selected.Id)
                        {
                            list.SelectedIndex = index;
                            candidateDetail.Text = FormatFriendlyCandidate(item.Value);
                        }
                    }
                }
            }
            else if (workflow.Candidates.Count == 0)
            {
                candidateDetail.Text = "尚无排版方案。请按上方待办补齐信息。";
            }
            else
            {
                candidateDetail.Text = "请选择一个方案查看说明；"
                    + "仅查看不会写入图纸；正式写回仍需在预览页最后确认。";
            }

            RefreshDiagnosticTiles();
        }

        private static void ReplaceCandidateItems(
            ListBox list,
            IEnumerable<GuidedCandidatePresentation> candidates)
        {
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (GuidedCandidatePresentation candidate in candidates)
                {
                    list.Items.Add(new CandidateItem(candidate));
                }
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private void RefreshDiagnosticTiles()
        {
            if (diagnosticTilesRendered
                && ReferenceEquals(renderedDiagnosticPlan, workflow.PreviewPlan)
                && renderedShowAllAssessedTiles
                    == showAllAssessedTiles.Checked)
            {
                return;
            }

            diagnosticTiles.BeginUpdate();
            try
            {
                diagnosticTiles.Items.Clear();
                renderedDiagnosticPlan = workflow.PreviewPlan;
                renderedShowAllAssessedTiles = showAllAssessedTiles.Checked;
                diagnosticTilesRendered = true;
                diagnosticTileDetail.Text = workflow.PreviewPlan == null
                    ? "请先选中一个可保留方案；程序会自动生成同源临时预览。"
                    : "当前方案没有需要显示的边界砖诊断。";
                if (workflow.PreviewPlan == null)
                {
                    return;
                }

                foreach (LayoutDrawingTile tile in workflow.PreviewPlan.Tiles.Where(
                    item => item.IsBelowRecommended
                        || showAllAssessedTiles.Checked
                            && item.HasApplicableBoundaryCut))
                {
                    var entry = new DiagnosticTileItem(tile);
                    diagnosticTiles.Items.Add(entry);
                    if (tile.Id == workflow.SelectedDiagnosticTileId)
                    {
                        diagnosticTiles.SelectedItem = entry;
                        diagnosticTileDetail.Text =
                            OrthogonalDecisionGuidedText.FormatDiagnosticTile(tile);
                    }
                }
            }
            finally
            {
                diagnosticTiles.EndUpdate();
            }
        }

        private void RefreshActionStates()
        {
            bool formalWritebackInProgress =
                OrthogonalDecisionPaletteHost.IsFormalWritebackInProgress
                || workflow.FormalWritebackAwaitingCompletion;
            ApplyActionState(
                selectRoom,
                roomDisabledReason,
                OrthogonalDecisionGuideAction.SelectRoom);
            ApplyActionState(
                selectControlRegion,
                controlRegionReason,
                OrthogonalDecisionGuideAction.SelectControlRegion);
            ApplyActionState(
                selectDoor,
                doorReason,
                OrthogonalDecisionGuideAction.SelectControlDoor);
            ApplyActionState(
                selectMainRegion,
                mainRegionReason,
                OrthogonalDecisionGuideAction.SelectMainRegion);
            ApplyActionState(
                selectSecondaryRegion,
                secondaryRegionReason,
                OrthogonalDecisionGuideAction.SelectSecondaryRegion);
            ApplyActionState(
                selectConnectionEdge,
                connectionReason,
                OrthogonalDecisionGuideAction.SelectConnectionEdge);

            preview.Enabled = !formalWritebackInProgress
                && workflow.Palette.CanRequestPreview;
            previewDisabledReason.Text = preview.Enabled
                ? "可以在图中临时显示真实铺贴分格；不会写入图纸。"
                : workflow.PreviewState == OrthogonalDecisionPreviewState.Visible
                    ? "临时铺贴图已显示；可刷新、清除、返回修改或结束。"
                    : OrthogonalDecisionGuidedText.PreviewDisabledReason(
                        workflow.Palette);
            toolTip.SetToolTip(preview, previewDisabledReason.Text);
            refreshPreview.Enabled = !formalWritebackInProgress
                && workflow.CanRefreshPreview;
            cancelPreview.Enabled = !formalWritebackInProgress
                && workflow.PreviewState
                != OrthogonalDecisionPreviewState.None;
            focusDrawing.Enabled = !formalWritebackInProgress
                && workflow.Input.HasRoom
                && !workflow.PendingAction.HasValue;
            toolTip.SetToolTip(
                focusDrawing,
                focusDrawing.Enabled
                    ? "把主窗口收成右上角的小控制条，腾出图面；点击控制条即可返回。"
                    : "请先载入房间，并结束当前图面选择。" );

            inspectCandidate.Enabled = !formalWritebackInProgress
                && workflow.Palette.CanInspectSelectedCandidate;
            inspectionDisabledReason.Text = inspectCandidate.Enabled
                ? "选中后会自动显示同源预览；此按钮可手动重新查看，不会写入图纸。"
                : workflow.Palette.SelectedCandidate == null
                    ? "请选择一个方案；若只有一个待确认方案，程序会自动选中。"
                    : workflow.Palette.SelectedCandidate.State
                        == LayoutCandidateState.Eliminated
                        ? "硬淘汰方案只保留文字审计，不能进入图面诊断。"
                        : "当前方案没有可用的同源铺贴计划。";
            toolTip.SetToolTip(
                inspectCandidate,
                inspectionDisabledReason.Text);

            EvaluatedLayoutCandidate selectedCandidate =
                workflow.Palette.SelectedCandidate;
            bool selectedNeedsReview = selectedCandidate != null
                && selectedCandidate.State
                    == LayoutCandidateState.RequiresUserDecision;
            bool selectedVisualConfirmation = selectedCandidate != null
                && selectedCandidate.State
                    == LayoutCandidateState.RequiresProjectPolicy
                && workflow.Input.Policy != null
                && workflow.Input.Policy.AllowsVisualConfirmation;
            reviewNotice.Visible = selectedNeedsReview
                || selectedVisualConfirmation;
            reviewNotice.Text = selectedVisualConfirmation
                ? workflow.GetVisualConfirmationWarning()
                : selectedNeedsReview
                ? "人工复核提醒：该候选满足项目绝对下限，但存在低于推荐下限或墙角对缝资格等需要查看的情况。这里只需看图确认；不填写复核原因。"
                : string.Empty;

            applyProject.Enabled = !formalWritebackInProgress
                && workflow.Input.HasRoom
                && !workflow.PendingAction.HasValue;
            toolTip.SetToolTip(
                applyProject,
                applyProject.Enabled
                    ? "确认固定推荐下限，并保存项目规则版本与明确填写或明确未确认的项目绝对下限。"
                    : "请先在图中选择并验证房间边界。" );
            applyIntent.Enabled = !formalWritebackInProgress
                && workflow.Input.Policy != null
                && !workflow.PendingAction.HasValue;
            toolTip.SetToolTip(
                applyIntent,
                applyIntent.Enabled
                    ? "保存明确选择的铺贴方式。"
                    : "请先保存带版本号的项目铺贴规则。" );

            writeToDrawing.Enabled = !formalWritebackInProgress
                && workflow.CanRequestFormalWriteback;
            writebackDisabledReason.Text = writeToDrawing.Enabled
                ? "最终确认后只写 DivisionLines + Connections；不会写入诊断标记。"
                : workflow.GetFormalWritebackDisabledReason();
            toolTip.SetToolTip(writeToDrawing, writebackDisabledReason.Text);
            writebackDisabledReason.Visible = true;

            previousEliminatedPage.Enabled = showEliminatedCandidates.Checked
                && workflow.EliminatedPageIndex > 0;
            nextEliminatedPage.Enabled = showEliminatedCandidates.Checked
                && workflow.EliminatedPageIndex + 1 < workflow.EliminatedPageCount;
            diagnosticTiles.Enabled = workflow.PreviewPlan != null;
            showAllAssessedTiles.Enabled = workflow.PreviewPlan != null;
            showNeutralRegions.Enabled = workflow.PreviewPlan != null;
            showWallCornerDiagnostics.Enabled = workflow.PreviewPlan != null;

            previous.Enabled = !formalWritebackInProgress
                && workflow.ActiveStep != OrthogonalDecisionGuideStep.Room;
            string nextReason = workflow.GetNextDisabledReason();
            next.Enabled = !formalWritebackInProgress
                && string.IsNullOrEmpty(nextReason);
            nextAction.Text = string.IsNullOrEmpty(nextReason)
                ? "下一步：可以继续。"
                : "当前下一步：" + nextReason;
            toolTip.SetToolTip(next, nextAction.Text);
            reselect.Enabled = !formalWritebackInProgress
                && (workflow.Input.HasRoom || workflow.ActiveStep
                    != OrthogonalDecisionGuideStep.Room);
            finish.Enabled = !formalWritebackInProgress
                && workflow.Input.HasRoom
                && !workflow.PendingAction.HasValue;
            modify.Enabled = !formalWritebackInProgress;
            cancelAll.Enabled = !formalWritebackInProgress;
            navigationDisabledReason.Text = NavigationDisabledReason();
            toolTip.SetToolTip(previous, previous.Enabled
                ? "返回上一步，不改变已确认内容。"
                : "当前已经是第一步。" );
            toolTip.SetToolTip(reselect, reselect.Enabled
                ? "清除旧房间和房间级内容后重新选择。"
                : "尚未载入房间，无需重选。" );
            toolTip.SetToolTip(finish, finish.Enabled
                ? "保留只读汇总并结束本轮编辑，不写入图纸。"
                : "请先载入合法房间，并完成或取消正在进行的图面选择。" );
            toolTip.SetToolTip(cancelAll, cancelAll.Enabled
                ? "清除当前向导状态；已经正式写回图纸的对象不会被删除。"
                : "正式写回正在执行，请等待 AutoCAD 完成。" );
            toolTip.SetToolTip(cancelPreview, cancelPreview.Enabled
                ? "清除图中的临时铺贴线，但保留当前方案和人工记录。"
                : "当前没有可清除的图面预览。" );
            toolTip.SetToolTip(refreshPreview, refreshPreview.Enabled
                ? "用同一份绘图计划清除后重画，不重新计算方案。"
                : "当前没有可刷新的已显示预览。" );
        }

        private void RefreshStatusOnly()
        {
            notice.Text = workflow.Notice;
            summary.Text = workflow.BuildOrdinarySummary();
            RefreshActionStates();
        }

        private void ApplyActionState(
            Button button,
            Label reasonLabel,
            OrthogonalDecisionGuideAction action)
        {
            string reason = workflow.GetActionDisabledReason(action);
            bool formalWritebackInProgress =
                OrthogonalDecisionPaletteHost.IsFormalWritebackInProgress
                || workflow.FormalWritebackAwaitingCompletion;
            button.Enabled = !formalWritebackInProgress
                && string.IsNullOrEmpty(reason);
            reasonLabel.Text = button.Enabled
                ? "可执行；选择将在 AutoCAD 安全命令上下文中完成。"
                : formalWritebackInProgress
                    ? "正式写回正在执行，请等待完成后再继续操作。"
                : reason;
            toolTip.SetToolTip(button, reasonLabel.Text);
        }

        private string FormatFriendlyRequirements()
        {
            if (!workflow.Input.HasRoom)
            {
                return "当前待办：填写砖规格，然后在图中选择完整房间边界。";
            }

            if (workflow.Input.Policy == null)
            {
                return "当前待办：确认固定推荐下限，并选择填写项目绝对下限、按图面确认或尚未决定。";
            }

            if (workflow.Input.ControlDoor == null)
            {
                return "当前待办：在完整房间的同一段外墙上选择门洞两侧边缘。"
                    + "邻接区域和全房连续相位由程序自动处理。";
            }

            return workflow.GetCurrentGuidance();
        }

        private string FormatEngineeringDetails()
        {
            if (!engineeringToggle.Checked)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            if (workflow.Input.BoundaryNormalization != null)
            {
                builder.AppendLine(
                    OrthogonalDecisionGuidedText.FormatBoundaryNormalization(
                        workflow.Input.BoundaryNormalization));
            }

            foreach (GuidedRequirementPresentation item in workflow.Requirements)
            {
                builder.AppendLine(item.EngineeringDetails);
            }

            if (workflow.Input.Result != null
                && workflow.Input.Result.RawResult != null)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine(
                    OrthogonalDecisionGuidedText.FormatCandidateGenerationReport(
                        workflow.Input.Result.RawResult.GenerationReport));
            }

            GuidedCandidatePresentation selected = workflow.Candidates
                .FirstOrDefault(item => workflow.Palette.SelectedCandidate != null
                    && item.Candidate.Id
                        == workflow.Palette.SelectedCandidate.Id);
            if (selected != null)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append("当前选中候选原始序号 ");
                builder.Append(selected.OriginalIndex);
                builder.AppendLine("：");
                builder.AppendLine(
                    OrthogonalDecisionPaletteText.FormatCandidate(
                        selected.Candidate));
            }

            return builder.Length == 0
                ? "当前没有工程详情。"
                : builder.ToString();
        }

        private string FormatFriendlyCandidate(
            GuidedCandidatePresentation value)
        {
            string auditBoundary = value.Group == GuidedCandidateGroup.Unavailable
                ? Environment.NewLine + "审计边界：硬淘汰候选仅供文字审计，"
                    + "不能图面诊断、确认采用或写回。淘汰分组："
                    + OrthogonalDecisionGuidedText.FormatEliminatedGroup(
                        value.EliminatedGroup.Value) + "。"
                : string.Empty;
            return value.Title + Environment.NewLine
                + "分类：" + value.Status + Environment.NewLine
                + OrthogonalDecisionGuidedText.FormatCandidateOverview(
                    value.Candidate,
                    workflow.PreferWallCornerAlignment) + Environment.NewLine
                + "说明：" + OrthogonalDecisionGuidedText.FormatCandidateReason(
                    value.Candidate,
                    workflow.Input.TileWidth,
                    workflow.Input.TileHeight) + Environment.NewLine
                + "原始顺序：第 "
                + value.OriginalIndex.ToString(CultureInfo.CurrentCulture)
                + " 个。完整代码、诊断和指标请展开“工程详情”。"
                + auditBoundary;
        }

        private string NavigationDisabledReason()
        {
            var values = new List<string>();
            if (!previous.Enabled)
            {
                values.Add("“上一步”不可用：当前已经是第一步");
            }

            if (!next.Enabled)
            {
                values.Add("“下一步”不可用：" + workflow.GetNextDisabledReason());
            }

            if (!reselect.Enabled)
            {
                values.Add("“重选房间”不可用：尚未载入房间");
            }

            if (!finish.Enabled)
            {
                values.Add("“结束本次预览”不可用：请先载入房间并结束当前图面选择");
            }

            if (!cancelPreview.Enabled)
            {
                values.Add("“清除预览”不可用：当前没有图面预览");
            }

            return values.Count == 0
                ? "当前操作均可用；结束只会清除临时预览并保留只读汇总。"
                : string.Join("；", values) + "。";
        }

        private IEnumerable<ListBox> CandidateLists()
        {
            yield return automaticCandidates;
            yield return manualCandidates;
            yield return missingPolicyCandidates;
            yield return unavailableCandidates;
        }

        private static int ProductStageIndex(OrthogonalDecisionGuideStep step)
        {
            switch (step)
            {
                case OrthogonalDecisionGuideStep.Room:
                case OrthogonalDecisionGuideStep.Project:
                case OrthogonalDecisionGuideStep.Intent:
                    return 0;
                case OrthogonalDecisionGuideStep.Geometry:
                    return 1;
                case OrthogonalDecisionGuideStep.Candidates:
                    return 2;
                case OrthogonalDecisionGuideStep.Summary:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step));
            }
        }

        private static OrthogonalDecisionGuideStep StepForProductStage(
            int productStage)
        {
            switch (productStage)
            {
                case 0:
                    return OrthogonalDecisionGuideStep.Room;
                case 1:
                    return OrthogonalDecisionGuideStep.Geometry;
                case 2:
                    return OrthogonalDecisionGuideStep.Candidates;
                case 3:
                    return OrthogonalDecisionGuideStep.Summary;
                default:
                    throw new ArgumentOutOfRangeException(nameof(productStage));
            }
        }

        private static TabPage CreatePage(string title)
        {
            return new TabPage(title)
            {
                AutoScroll = false,
                Padding = new Padding(8)
            };
        }

        private static FlowLayoutPanel CreateStepPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                AutoSize = false
            };
            panel.ClientSizeChanged += (sender, args) =>
                FitTopDownChildren(panel);
            panel.ControlAdded += (sender, args) =>
                FitTopDownChildren(panel);
            return panel;
        }

        private static Label CreateSectionLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4)
            };
        }

        private static Control CreateFieldRow(string label, Control control)
        {
            var row = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            }, 0, 0);
            control.Width = 180;
            row.Controls.Add(control, 1, 0);
            return row;
        }

        private static Control CreateSelectionRow(
            Button button,
            Label status,
            Label reason)
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            panel.Controls.Add(button);
            panel.Controls.Add(status);
            panel.Controls.Add(reason);
            return panel;
        }

        private static void FitTopDownChildren(FlowLayoutPanel panel)
        {
            int width = Math.Max(
                240,
                panel.ClientSize.Width
                    - SystemInformation.VerticalScrollBarWidth
                    - panel.Padding.Horizontal
                    - 8);
            foreach (Control child in panel.Controls)
            {
                var label = child as Label;
                if (label != null && label.AutoSize)
                {
                    label.MaximumSize = new Size(width, 0);
                    continue;
                }

                if (child is TextBox
                    || child is ListBox
                    || child is TableLayoutPanel
                    || child is TabControl)
                {
                    child.Width = width;
                }
            }
        }

        private static void FitScrollableContent(
            Panel viewport,
            FlowLayoutPanel content)
        {
            int width = Math.Max(
                240,
                viewport.ClientSize.Width
                    - viewport.Padding.Horizontal
                    - SystemInformation.VerticalScrollBarWidth
                    - 4);
            content.MaximumSize = new Size(width, 0);
            content.Width = width;
            FitTopDownChildren(content);
        }

        private static Button CreateSelectionButton(string text)
        {
            return new Button { Text = text, AutoSize = true };
        }

        private static Label CreateWrapLabel()
        {
            return new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0)
            };
        }

        private static Label CreateReasonLabel()
        {
            return new Label
            {
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                ForeColor = Color.DimGray
            };
        }

        private static Label CreateCandidateLabel(string text, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = color,
                Font = new Font(Control.DefaultFont, FontStyle.Bold)
            };
        }

        private static TextBox CreateReadOnlyTextBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
        }

        private sealed class CandidateItem
        {
            public CandidateItem(GuidedCandidatePresentation value)
            {
                Value = value;
            }

            public GuidedCandidatePresentation Value { get; }

            public override string ToString()
            {
                return Value.Title + " — " + Value.Status;
            }
        }

        private sealed class DiagnosticTileItem
        {
            public DiagnosticTileItem(LayoutDrawingTile tile)
            {
                Tile = tile;
            }

            public LayoutDrawingTile Tile { get; }

            public override string ToString()
            {
                return Tile.Id + " — "
                    + Tile.AssessmentStatus.ToString();
            }
        }

        private sealed class EliminatedFilterItem
        {
            public EliminatedFilterItem(
                GuidedEliminatedGroup? group,
                string label)
            {
                Group = group;
                Label = label;
            }

            public GuidedEliminatedGroup? Group { get; }

            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
