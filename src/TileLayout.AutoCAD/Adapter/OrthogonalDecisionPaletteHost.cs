using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace TileLayout.AutoCAD.Adapter
{
    public static class OrthogonalDecisionPaletteHost
    {
        private static Form dialog;
        private static OrthogonalDecisionPaletteControl control;
        private static Document ownerDocument;
        private static OrthogonalDecisionPreviewAction? pendingPreviewAction;
        private static bool previewCommandQueued;
        private static volatile bool pendingFormalWriteback;
        private static volatile bool formalWritebackExecuting;
        private static Panel drawingFocusPanel;
        private static Rectangle normalDialogBounds;
        private static Size normalMinimumSize;
        private static bool drawingFocusActive;
        private static bool restoreAfterGuideAction;

        public static void Show(Document document)
        {
            EnsureCreated();
            RestoreFromDrawingFocus(false);
            if (document != null && !ReferenceEquals(ownerDocument, document))
            {
                OrthogonalLayoutTransientPreview.ClearAny();
                pendingPreviewAction = null;
                previewCommandQueued = false;
                pendingFormalWriteback = false;
                formalWritebackExecuting = false;
                ownerDocument = document;
                control.StartNewSession();
            }

            if (!dialog.Visible)
            {
                if (dialog.IsHandleCreated)
                {
                    dialog.Show();
                }
                else
                {
                    AcApplication.ShowModelessDialog(dialog);
                }
            }
            else
            {
                dialog.Activate();
            }
        }

        internal static bool TryGetPendingAction(
            Document document,
            OrthogonalDecisionGuideAction expected,
            out OrthogonalDecisionPaletteControl paletteControl)
        {
            EnsureCreated();
            paletteControl = null;
            if (document == null
                || !ReferenceEquals(document, ownerDocument)
                || control.Workflow.PendingAction != expected)
            {
                return false;
            }

            paletteControl = control;
            return true;
        }

        internal static bool TryTakePendingPreviewAction(
            Document document,
            out OrthogonalDecisionPreviewAction action,
            out OrthogonalDecisionPaletteControl paletteControl)
        {
            EnsureCreated();
            action = OrthogonalDecisionPreviewAction.Clear;
            paletteControl = null;
            if (document == null
                || !ReferenceEquals(document, ownerDocument))
            {
                pendingPreviewAction = null;
                previewCommandQueued = false;
                return false;
            }

            if (!pendingPreviewAction.HasValue)
            {
                return false;
            }

            action = pendingPreviewAction.Value;
            pendingPreviewAction = null;
            previewCommandQueued = false;
            paletteControl = control;
            return true;
        }

        internal static bool TryTakePendingFormalWriteback(
            Document document,
            out OrthogonalDecisionPaletteControl paletteControl)
        {
            EnsureCreated();
            paletteControl = null;
            if (document == null
                || !ReferenceEquals(document, ownerDocument)
                || !pendingFormalWriteback)
            {
                return false;
            }

            pendingFormalWriteback = false;
            formalWritebackExecuting = true;
            paletteControl = control;
            return true;
        }

        internal static bool IsFormalWritebackInProgress
        {
            get
            {
                return pendingFormalWriteback || formalWritebackExecuting;
            }
        }

        internal static void FinishPendingFormalWriteback(
            Document document,
            OrthogonalDecisionPaletteControl paletteControl)
        {
            if (!ReferenceEquals(ownerDocument, document)
                || !ReferenceEquals(control, paletteControl))
            {
                return;
            }

            formalWritebackExecuting = false;
            paletteControl.RefreshAfterFormalWritebackOperation();
        }

        private static void EnsureCreated()
        {
            if (dialog != null && !dialog.IsDisposed)
            {
                return;
            }

            control = new OrthogonalDecisionPaletteControl();
            control.GuideActionRequested += OnGuideActionRequested;
            control.PreviewActionRequested += OnPreviewActionRequested;
            control.DrawingFocusRequested += OnDrawingFocusRequested;
            control.FormalWritebackRequested += OnFormalWritebackRequested;
            AcApplication.DocumentManager.DocumentActivated +=
                OnDocumentActivated;
            dialog = new OrthogonalDecisionPaletteForm
            {
                Text = "自动排砖插件",
                MinimumSize = new Size(640, 560),
                Size = new Size(760, 680),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.Sizable,
                ShowInTaskbar = false,
                MinimizeBox = false,
                AutoScaleMode = AutoScaleMode.Dpi,
                Font = SystemFonts.MessageBoxFont
            };
            dialog.Controls.Add(control);
            drawingFocusPanel = BuildDrawingFocusPanel();
            drawingFocusPanel.Visible = false;
            dialog.Controls.Add(drawingFocusPanel);
            dialog.FormClosing += (sender, args) =>
            {
                if (args.CloseReason == CloseReason.UserClosing)
                {
                    args.Cancel = true;
                    dialog.Hide();
                }
            };
        }

        private static void OnDocumentActivated(
            object sender,
            DocumentCollectionEventArgs args)
        {
            if (IsFormalWritebackInProgress)
            {
                return;
            }

            if (ownerDocument != null
                && args != null
                && args.Document != null
                && !ReferenceEquals(args.Document, ownerDocument))
            {
                pendingPreviewAction = null;
                previewCommandQueued = false;
            }

            if (ownerDocument == null
                || args == null
                || args.Document == null
                || ReferenceEquals(args.Document, ownerDocument)
                || !OrthogonalLayoutTransientPreview.IsVisible(ownerDocument))
            {
                return;
            }

            try
            {
                OrthogonalLayoutTransientPreview.ClearAny();
            }
            catch (Exception)
            {
            }

            pendingFormalWriteback = false;
            formalWritebackExecuting = false;

            control.MarkPreviewRefreshRequired(
                "已切换到另一张图，原图中的临时铺贴线已清除。"
                    + "返回原图后点击“刷新预览”即可恢复；DWG 没有变化。" );
        }

        private static void OnGuideActionRequested(
            object sender,
            OrthogonalDecisionGuideActionEventArgs args)
        {
            Document active = AcApplication.DocumentManager.MdiActiveDocument;
            if (ownerDocument == null
                || active == null
                || !ReferenceEquals(active, ownerDocument))
            {
                control.EndAction(
                    "当前活动图纸不是本向导所属图纸；请返回原图纸，或在新图纸重新执行 TILEORTHOUI。" );
                return;
            }

            string command = CommandFor(args.Action);
            try
            {
                HideForGuideAction();
                ownerDocument.SendStringToExecute(
                    command + " ",
                    true,
                    false,
                    false);
            }
            catch (Exception exception)
            {
                control.EndAction(
                    "无法进入 AutoCAD 图面选择：" + exception.Message);
                RestoreAfterGuideAction();
            }
        }

        internal static void RestoreAfterGuideAction()
        {
            if (!restoreAfterGuideAction
                || dialog == null
                || dialog.IsDisposed)
            {
                restoreAfterGuideAction = false;
                return;
            }

            restoreAfterGuideAction = false;
            dialog.Show();
            dialog.Activate();
        }

        private static void OnPreviewActionRequested(
            object sender,
            OrthogonalDecisionPreviewActionEventArgs args)
        {
            Document active = AcApplication.DocumentManager.MdiActiveDocument;
            if (ownerDocument == null
                || active == null
                || !ReferenceEquals(active, ownerDocument))
            {
                control.MarkPreviewRefreshRequired(
                    "当前图纸不是本次预览所属图纸；请返回原图后点击“刷新预览”，"
                        + "或在新图重新执行 TILEORTHOUI。" );
                return;
            }

            pendingPreviewAction = args.Action;
            if (previewCommandQueued)
            {
                // Keep only the latest show/clear request while AutoCAD is
                // still dispatching the queued internal command. The command
                // reads the latest workflow plan when it finally runs.
                return;
            }

            previewCommandQueued = true;
            try
            {
                ownerDocument.SendStringToExecute(
                    "TILEORTHOUIPREVIEW ",
                    true,
                    false,
                    false);
            }
            catch (Exception exception)
            {
                pendingPreviewAction = null;
                previewCommandQueued = false;
                control.MarkPreviewDisplayFailed(exception.Message);
            }
        }

        private static void HideForGuideAction()
        {
            restoreAfterGuideAction = dialog != null && dialog.Visible;
            if (restoreAfterGuideAction)
            {
                dialog.Hide();
            }
        }

        private static void OnDrawingFocusRequested(object sender, EventArgs args)
        {
            if (dialog == null || dialog.IsDisposed || drawingFocusActive)
            {
                return;
            }

            normalDialogBounds = dialog.Bounds;
            normalMinimumSize = dialog.MinimumSize;
            drawingFocusActive = true;
            dialog.SuspendLayout();
            control.Visible = false;
            drawingFocusPanel.Visible = true;
            drawingFocusPanel.BringToFront();
            dialog.Text = "自动排砖插件 — 图面查看";
            dialog.MinimumSize = new Size(330, 105);
            dialog.Size = new Size(370, 120);
            Rectangle workingArea = Screen.FromControl(dialog).WorkingArea;
            dialog.Location = new Point(
                workingArea.Right - dialog.Width - 20,
                workingArea.Top + 70);
            dialog.ResumeLayout(true);
        }

        private static void OnFormalWritebackRequested(
            object sender,
            EventArgs args)
        {
            Document active = AcApplication.DocumentManager.MdiActiveDocument;
            if (ownerDocument == null
                || active == null
                || !ReferenceEquals(active, ownerDocument))
            {
                control.MarkFormalWritebackFailed(
                    "当前活动图纸不是本次预览所属图纸。" );
                return;
            }

            if (IsFormalWritebackInProgress)
            {
                control.MarkFormalWritebackFailed(
                    "上一次正式写回请求仍在等待 AutoCAD 完成。" );
                return;
            }

            pendingFormalWriteback = true;
            formalWritebackExecuting = true;
            try
            {
                ownerDocument.SendStringToExecute(
                    "TILEORTHOUIWRITE ",
                    true,
                    false,
                    false);
            }
            catch (Exception exception)
            {
                pendingFormalWriteback = false;
                formalWritebackExecuting = false;
                control.MarkFormalWritebackFailed(exception.Message);
            }
        }

        private static Panel BuildDrawingFocusPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(8)
            };
            content.Controls.Add(new Label
            {
                Text = "主窗口已收起，临时预览仍保持零写入。",
                AutoSize = true,
                Margin = new Padding(3, 7, 8, 3)
            });
            var restore = new Button
            {
                Text = "返回排版窗口",
                AutoSize = true
            };
            restore.Click += (sender, args) => RestoreFromDrawingFocus(true);
            content.Controls.Add(restore);
            panel.Controls.Add(content);
            return panel;
        }

        private static void RestoreFromDrawingFocus(bool activate)
        {
            if (!drawingFocusActive || dialog == null || dialog.IsDisposed)
            {
                return;
            }

            drawingFocusActive = false;
            dialog.SuspendLayout();
            drawingFocusPanel.Visible = false;
            control.Visible = true;
            dialog.Text = "自动排砖插件";
            dialog.MinimumSize = normalMinimumSize;
            dialog.Bounds = normalDialogBounds;
            dialog.ResumeLayout(true);
            if (activate && dialog.Visible)
            {
                dialog.Activate();
            }
        }

        private static string CommandFor(OrthogonalDecisionGuideAction action)
        {
            switch (action)
            {
                case OrthogonalDecisionGuideAction.SelectRoom:
                    return "TILEORTHOUIROOM";
                case OrthogonalDecisionGuideAction.SelectControlRegion:
                    return "TILEORTHOUICONTROL";
                case OrthogonalDecisionGuideAction.SelectControlDoor:
                    return "TILEORTHOUIDOOR";
                case OrthogonalDecisionGuideAction.SelectMainRegion:
                    return "TILEORTHOUIMAIN";
                case OrthogonalDecisionGuideAction.SelectSecondaryRegion:
                    return "TILEORTHOUISECONDARY";
                case OrthogonalDecisionGuideAction.SelectConnectionEdge:
                    return "TILEORTHOUIEDGE";
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
    }

    internal sealed class OrthogonalDecisionPaletteForm : Form
    {
        private const int WmEnterSizeMove = 0x0231;
        private const int WmExitSizeMove = 0x0232;
        private const int WmSetRedraw = 0x000B;

        public OrthogonalDecisionPaletteForm()
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer,
                true);
            UpdateStyles();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmEnterSizeMove)
            {
                SetRedraw(false);
            }

            base.WndProc(ref message);

            if (message.Msg == WmExitSizeMove)
            {
                SetRedraw(true);
                Invalidate(true);
                Update();
            }
        }

        private void SetRedraw(bool enabled)
        {
            if (!IsHandleCreated)
            {
                return;
            }

            SendMessage(
                Handle,
                WmSetRedraw,
                enabled ? new IntPtr(1) : IntPtr.Zero,
                IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            int message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
