using System;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;

using Windows.System;

namespace PersonalEventsBuilder.Pages {
    public sealed partial class WorkspacePage : Page {
        private const double MaximumPreviewHeight = 240;
        private readonly InputSystemCursor previewResizeCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        private double fieldPreviewHeight;
        private ViewModels.WorkspacePageViewModel ViewModel { get; }

        public WorkspacePage() : this(new Workspace.DemoWorkspaceSource()) {
        }

        public WorkspacePage(Workspace.IWorkspaceSource workspaceSource) {
            this.ViewModel = new ViewModels.WorkspacePageViewModel(workspaceSource);
            InitializeComponent();
            this.fieldPreviewHeight = this.ActiveFieldPreview.Height;
            this.EventForm.Matrix.ActiveFieldTextChanged += this.ActiveFieldPreview.SetText;
            this.ModeSelector.ModeSelected += this.OnModeSelected;
            this.NewEvent.NewEventRequested += this.ViewModel.StartNewEvent;
            this.Candidates.CandidateSelected += this.OnCandidateSelected;
            this.WorkspaceActions.ActionRequested += this.ExecuteAction;
            AutomationProperties.SetName(this.ActiveFieldPreview, Localization.UiStrings.Get("Tr_ActiveFieldPreview"));
            AutomationProperties.SetName(this.PreviewResizeHandle, Localization.UiStrings.Get("Tr_ResizeActiveFieldPreview"));
        }

        internal void ExecuteAction(string actionId) {
            this.ViewModel.ExecuteAction(actionId, this.EventForm.Matrix.ReadValues());
        }

        private void OnWorkspaceSizeChanged(object sender, SizeChangedEventArgs args) {
            this.WorkspaceSurface.Width = Math.Max(this.WorkspaceSurface.MinWidth, args.NewSize.Width);
        }

        private void OnPreviewDragDelta(object sender, DragDeltaEventArgs args) {
            this.ResizeFieldPreview(args.VerticalChange);
        }

        private void OnPreviewResizePointerEntered(object sender, PointerRoutedEventArgs args) {
            this.ProtectedCursor = this.previewResizeCursor;
        }

        private void OnPreviewResizePointerExited(object sender, PointerRoutedEventArgs args) {
            this.ProtectedCursor = null;
        }

        private void OnPreviewResizeKeyDown(object sender, KeyRoutedEventArgs args) {
            if (args.Key is VirtualKey.Up or VirtualKey.Down) {
                this.ResizeFieldPreview(args.Key == VirtualKey.Up ? -12 : 12);
                args.Handled = true;
            }
        }

        private void ResizeFieldPreview(double change) {
            this.fieldPreviewHeight = Math.Clamp(this.fieldPreviewHeight + change, 0, MaximumPreviewHeight);
            this.ActiveFieldPreview.Height = this.fieldPreviewHeight;
            this.ActiveFieldPreview.Visibility = this.fieldPreviewHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnModeSelected(Workspace.EventMode mode) {
            this.ViewModel.SelectMode(mode);
        }

        private void OnCandidateSelected(string candidateId) {
            var matrix = this.EventForm.Matrix;
            matrix.ApplyValues(this.ViewModel.ApplyCandidate(candidateId, matrix.ReadValues()));
        }
    }
}