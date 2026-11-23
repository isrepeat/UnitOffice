using System;
using System.IO;
using System.Security;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using Microsoft.Win32;

using Windows.Graphics;

namespace PersonalEventsBuilder {
    public sealed partial class MainWindow : Window {
        private const string WindowWidthValue = "WindowWidth";
        private const string WindowHeightValue = "WindowHeight";
        private const string WindowMaximizedValue = "WindowMaximized";
        private SizeInt32 restoredSize;
        private bool wasMaximized;

        private static Navigation.PageManager PageManager => States.AppData.AppSessionState.PageManager;

        public MainWindow() {
            this.InitializeComponent();
            States.AppData.AppSessionState.InitializeNavigation(this.PageFrame);
            this.Title = Localization.UiStrings.Get("Tr_AppTitle");
            this.WorkspaceTitleBar.CloseRequested += this.Close;
            this.WorkspaceTitleBar.AttachTo(this);
            MainWindow.PageManager.Navigate<Pages.WorkspacePage>();
            this.RestoreWindowState();
            this.AppWindow.Changed += this.OnAppWindowChanged;
            this.Closed += this.OnClosed;
        }

        private void RestoreWindowState() {
            var appWindow = this.AppWindow;
            this.restoredSize = appWindow.Size;
            var settingsKey = States.AppData.SettingsKey;
            if (Registry.GetValue(settingsKey, WindowWidthValue, 0) is int width && width > 0
                && Registry.GetValue(settingsKey, WindowHeightValue, 0) is int height && height > 0) {
                appWindow.Resize(new SizeInt32 { Width = width, Height = height });
                this.restoredSize = appWindow.Size;
            }

            if (Registry.GetValue(settingsKey, WindowMaximizedValue, 0) is 1
                && appWindow.Presenter is OverlappedPresenter presenter) {
                this.wasMaximized = true;
                presenter.Maximize();
            }
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args) {
            if ((!args.DidSizeChange && !args.DidPresenterChange) || sender.Presenter is not OverlappedPresenter presenter) {
                return;
            }

            switch (presenter.State) {
                case OverlappedPresenterState.Restored:
                    this.restoredSize = sender.Size;
                    this.wasMaximized = false;
                    break;
                case OverlappedPresenterState.Maximized:
                    this.wasMaximized = true;
                    break;
            }
        }

        private void OnClosed(object sender, WindowEventArgs args) {
            try {
                var settingsKey = States.AppData.SettingsKey;
                Registry.SetValue(settingsKey, WindowWidthValue, this.restoredSize.Width);
                Registry.SetValue(settingsKey, WindowHeightValue, this.restoredSize.Height);
                Registry.SetValue(settingsKey, WindowMaximizedValue, this.wasMaximized ? 1 : 0);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException) {
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }

        private void OnPageNavigated(object sender, NavigationEventArgs args) {
            var pageManager = MainWindow.PageManager;
            this.WorkspaceTitleBar.UpdateNavigationState(pageManager.CurrentWorkspace is not null, pageManager.CanGoBack);
        }
    }
}