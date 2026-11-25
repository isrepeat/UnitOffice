using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Pages {
    public sealed partial class WorkspacePage : Page {
        internal ViewModels.WorkspacePageViewModel WorkspacePageViewModel { get; }

        public WorkspacePage() : this(new Workspace.DemoWorkspaceSource()) {
        }

        public WorkspacePage(Workspace.IWorkspaceSource workspaceSource) {
            this.WorkspacePageViewModel = new ViewModels.WorkspacePageViewModel(workspaceSource);
            this.InitializeComponent();
        }
    }
}