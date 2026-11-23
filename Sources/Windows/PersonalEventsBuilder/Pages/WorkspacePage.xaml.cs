using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Pages {
    public sealed partial class WorkspacePage : Page {
        private ViewModels.WorkspacePageViewModel ViewModel { get; }

        public WorkspacePage() : this(new Workspace.DemoWorkspaceSource()) {
        }

        public WorkspacePage(Workspace.IWorkspaceSource workspaceSource) {
            this.ViewModel = new ViewModels.WorkspacePageViewModel(workspaceSource);
            InitializeComponent();
            this.ModeSelector.ModeSelected += this.OnModeSelected;
            this.NewEvent.NewEventRequested += this.ViewModel.StartNewEvent;
            this.WorkspaceActions.ActionRequested += this.ExecuteAction;
        }

        internal void ExecuteAction(string actionId) {
            this.ViewModel.ExecuteAction(actionId);
        }

        private void OnModeSelected(Workspace.EventMode mode) {
            this.ViewModel.SelectMode(mode);
        }
    }
}