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
        private const double FormScrollStep = 104;
        private const double ScrollTolerance = 0.1;
        private readonly InputSystemCursor previewResizeCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        private double fieldPreviewHeight;
        private double defaultFormOffset;
        private bool resetFormOffset = true;
        private ViewModels.WorkspacePageViewModel ViewModel { get; }

        public WorkspacePage() : this(new Workspace.DemoWorkspaceSource()) {
        }

        public WorkspacePage(Workspace.IWorkspaceSource workspaceSource) {
            this.ViewModel = new ViewModels.WorkspacePageViewModel(workspaceSource);
            InitializeComponent();
            this.fieldPreviewHeight = this.ActiveFieldPreview.Height;
            this.EventForm.ActiveFieldTextChanged += this.ActiveFieldPreview.SetText;
            this.ModeSelector.ModeSelected += this.OnModeSelected;
            this.NewEvent.NewEventRequested += this.ViewModel.StartNewEvent;
            this.Candidates.CandidateSelected += this.OnCandidateSelected;
            this.WorkspaceActions.ActionRequested += this.ExecuteAction;
            AutomationProperties.SetName(this.ActiveFieldPreview, Localization.UiStrings.Get("Tr_ActiveFieldPreview"));
            AutomationProperties.SetName(this.PreviewResizeHandle, Localization.UiStrings.Get("Tr_ResizeActiveFieldPreview"));
            AutomationProperties.SetName(this.ScrollFormLeft, Localization.UiStrings.Get("Tr_ScrollFormLeft"));
            AutomationProperties.SetName(this.ScrollFormRight, Localization.UiStrings.Get("Tr_ScrollFormRight"));
        }

        internal void ExecuteAction(string actionId) {
            this.ViewModel.ExecuteAction(actionId, this.EventForm.ReadValues());
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
            if (this.ViewModel.SelectMode(mode)) {
                this.resetFormOffset = true;
            }
        }

        private void OnFormLayoutUpdated(object sender, object args) {
            this.Candidates.SetColumnInset(this.EventForm.CenterInset);
            var viewportWidth = this.FormScrollViewer.ActualWidth;
            var inset = this.EventForm.RequiredWidth > viewportWidth + ScrollTolerance
                ? this.EventForm.LeftOverflow
                : 0;
            if (this.defaultFormOffset != inset) {
                this.defaultFormOffset = inset;
                this.EventForm.SetHeadingInset(inset);
                this.resetFormOffset = true;
            }

            var minimumWidth = viewportWidth + inset;
            if (this.FormSurface.MinWidth != minimumWidth) {
                this.FormSurface.MinWidth = minimumWidth;
            }

            if (this.resetFormOffset && this.FormScrollViewer.ScrollableWidth + ScrollTolerance >= inset) {
                var scrollViewer = this.FormScrollViewer;
                this.resetFormOffset = Math.Abs(scrollViewer.HorizontalOffset - inset) > ScrollTolerance
                    && !scrollViewer.ChangeView(inset, null, null, disableAnimation: true);
            }

            this.UpdateFormScrollButtons();
        }

        private void OnFormViewChanged(object sender, ScrollViewerViewChangedEventArgs args) {
            this.UpdateFormScrollButtons();
        }

        private void UpdateFormScrollButtons() {
            var offset = this.FormScrollViewer.HorizontalOffset;
            var maximum = this.FormScrollViewer.ScrollableWidth;
            this.FormScrollButtons.Visibility = maximum > ScrollTolerance ? Visibility.Visible : Visibility.Collapsed;
            this.ScrollFormLeft.IsEnabled = offset > ScrollTolerance;
            this.ScrollFormRight.IsEnabled = maximum - offset > ScrollTolerance;
        }

        private void OnScrollFormLeft(object sender, RoutedEventArgs args) => this.ScrollForm(towardRight: false);

        private void OnScrollFormRight(object sender, RoutedEventArgs args) => this.ScrollForm(towardRight: true);

        private void ScrollForm(bool towardRight) {
            var offset = this.FormScrollViewer.HorizontalOffset - this.defaultFormOffset;
            var step = towardRight
                ? Math.Floor((offset + ScrollTolerance) / FormScrollStep) + 1
                : Math.Ceiling((offset - ScrollTolerance) / FormScrollStep) - 1;
            var target = this.defaultFormOffset + step * FormScrollStep;
            target = Math.Clamp(target, 0, this.FormScrollViewer.ScrollableWidth);
            this.FormScrollViewer.ChangeView(target, null, null, disableAnimation: true);
        }

        private void OnCandidateSelected(string candidateId) {
            var values = this.EventForm.ReadValues();
            var candidateValues = this.ViewModel.ApplyCandidate(candidateId, values);
            this.EventForm.ApplyValues(candidateValues);
        }
    }
}