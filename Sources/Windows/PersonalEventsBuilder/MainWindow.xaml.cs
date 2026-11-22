using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;

namespace PersonalEventsBuilder {
    public sealed partial class MainWindow : Window {
        private static Navigation.PageManager PageManager => States.AppData.AppSessionState.PageManager;

        public MainWindow() {
            this.InitializeComponent();
            States.AppData.AppSessionState.InitializeNavigation(this.PageFrame);
            this.Title = Localization.UiStrings.Get("Tr_AppTitle");
            this.WorkspaceTitleBar.CloseRequested += this.Close;
            this.WorkspaceTitleBar.AttachTo(this);
            MainWindow.PageManager.Navigate<Pages.WorkspacePage>();
        }

        private void OnPageNavigated(object sender, NavigationEventArgs args) {
            var pageManager = MainWindow.PageManager;
            this.WorkspaceTitleBar.UpdateNavigationState(pageManager.CurrentWorkspace is not null, pageManager.CanGoBack);
        }
    }
}