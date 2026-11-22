using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Pages {
    public sealed partial class WorkspacePage : Page {
        private ViewModels.WorkspacePageViewModel ViewModel { get; }

        public WorkspacePage() : this(new Workspace.DemoWorkspaceSource()) {
        }

        public WorkspacePage(Workspace.IWorkspaceSource workspaceSource) {
            this.ViewModel = new ViewModels.WorkspacePageViewModel(workspaceSource);
            InitializeComponent();
            if (this.ViewModel.Current is { } modeWorkspace) {
                this.EventForm.ApplyLayout(modeWorkspace);
            }
        }

        internal void ExecuteAction(string actionId) {
            this.ViewModel.ExecuteAction(actionId, this.EventForm.ReadValues());
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

        private void OnActionClick(object sender, RoutedEventArgs args) {
            if (sender is Button { Tag: string actionId }) {
                this.ExecuteAction(actionId);
            }
        }
    }
}