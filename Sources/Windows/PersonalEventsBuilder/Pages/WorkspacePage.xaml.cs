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
            this.EventForm.ActiveFieldTextChanged += this.OnActiveFieldTextChanged;
            AutomationProperties.SetName(this.ActiveFieldPreview, Localization.UiStrings.Get("Tr_ActiveFieldPreview"));
            AutomationProperties.SetName(this.PreviewResizeHandle, Localization.UiStrings.Get("Tr_ResizeActiveFieldPreview"));
            if (this.ViewModel.Current is { } modeWorkspace) {
                this.EventForm.ApplyLayout(modeWorkspace);
            }
        }

        internal void ExecuteAction(string actionId) {
            this.ViewModel.ExecuteAction(actionId, this.EventForm.ReadValues());
        }

        private void OnActiveFieldTextChanged(string text) {
            this.ActiveFieldPreviewText.Text = text;
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

        private void OnModeChecked(object sender, RoutedEventArgs args) {
            if (this.EventForm is not null && sender is RadioButton { Tag: Workspace.EventMode eventMode }
                && this.ViewModel.SelectMode(eventMode) && this.ViewModel.Current is { } modeWorkspace) {
                this.EventForm.ApplyLayout(modeWorkspace);
            }
        }

        private void OnCandidateSelected(object sender, SelectionChangedEventArgs args) {
            if (args.AddedItems.Count > 0 && args.AddedItems[0] is ViewModels.CandidateDisplayRow { Id: var candidateId }) {
                this.EventForm.ApplyValues(this.ViewModel.ApplyCandidate(candidateId, this.EventForm.ReadValues()));
            }
        }

        private void OnNewEventClick(object sender, RoutedEventArgs args) {
            this.ViewModel.StartNewEvent();
            if (this.ViewModel.Current is { } modeWorkspace) {
                this.EventForm.ApplyLayout(modeWorkspace);
            }
        }

        private void OnActionClick(object sender, RoutedEventArgs args) {
            if (sender is Button { Tag: string actionId }) {
                this.ExecuteAction(actionId);
            }
        }
    }
}