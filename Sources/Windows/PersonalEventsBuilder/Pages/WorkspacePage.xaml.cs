using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Pages {
    public sealed partial class WorkspacePage : Page {
        private ViewModels.WorkspacePageViewModel WorkspacePageViewModel { get; }

        public WorkspacePage() : this(new Workspace.DemoWorkspaceSource()) {
        }

        public WorkspacePage(Workspace.IWorkspaceSource workspaceSource) {
            this.WorkspacePageViewModel = new ViewModels.WorkspacePageViewModel(workspaceSource);
            InitializeComponent();
            this.ModeSelectorZone.ModeSelected += this.OnModeSelected;
            this.NewEventZone.NewEventRequested += this.WorkspacePageViewModel.StartNewEvent;
            this.ActionsZone.ActionRequested += this.ExecuteAction;
        }

        internal void ExecuteAction(string actionId) {
            this.WorkspacePageViewModel.ExecuteAction(actionId);
        }

        private void OnModeSelected(Workspace.EventMode eventMode) {
            this.WorkspacePageViewModel.SelectMode(eventMode);
        }
    }
}