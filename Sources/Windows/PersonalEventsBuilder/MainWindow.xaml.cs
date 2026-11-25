using System;
using System.IO;
using System.Security;

using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

using Windows.Graphics;

namespace PersonalEventsBuilder {
    public sealed partial class MainWindow : Window {
        private const string WindowWidthValue = "WindowWidth";
        private const string WindowHeightValue = "WindowHeight";
        private const string WindowMaximizedValue = "WindowMaximized";
        private SizeInt32 restoredSize;
        private bool wasMaximized;

        public MainWindow() {
            this.InitializeComponent();
            this.Title = Localization.UiStrings.Get("Tr_AppTitle");
            this.RestoreWindowState();
            this.AppWindow.Changed += this.OnAppWindowChanged;
            this.Closed += this.OnClosed;
        }

        private void RestoreWindowState() {
            var appWindow = this.AppWindow;
            this.restoredSize = appWindow.Size;
            var width = Helpers.UserSettings.Read(WindowWidthValue, 0);
            var height = Helpers.UserSettings.Read(WindowHeightValue, 0);
            if (width > 0 && height > 0) {
                appWindow.Resize(new SizeInt32 { Width = width, Height = height });
                this.restoredSize = appWindow.Size;
            }

            if (Helpers.UserSettings.Read(WindowMaximizedValue, 0) == 1
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
                Helpers.UserSettings.Write(WindowWidthValue, this.restoredSize.Width);
                Helpers.UserSettings.Write(WindowHeightValue, this.restoredSize.Height);
                Helpers.UserSettings.Write(WindowMaximizedValue, this.wasMaximized ? 1 : 0);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException) {
                System.Diagnostics.Debug.WriteLine(exception);
            }
        }

    }
}