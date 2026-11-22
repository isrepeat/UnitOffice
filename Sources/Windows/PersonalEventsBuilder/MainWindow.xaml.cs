using System;
using System.IO;
using System.Security;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;

namespace PersonalEventsBuilder {
    public sealed partial class MainWindow : Window {
        private static Navigation.PageManager PageManager => States.AppData.AppSessionState.PageManager;

        public MainWindow() {
            InitializeComponent();
            States.AppData.AppSessionState.InitializeNavigation(this.PageFrame);
            this.Title = Localization.UiStrings.Get("Tr_AppTitle");
            this.WorkspaceTitleBar.Title = this.Title;
            Controls.WindowTitleBar.Configure(this, this.WorkspaceTitleBar);
            this.UpdateLanguageSelection();
            MainWindow.PageManager.Navigate<Pages.WorkspacePage>();
        }

        private void UpdateLanguageSelection() {
            var language = Localization.UiLanguage.Current;
            this.UkrainianLanguageItem.IsChecked = language == Localization.UiLanguage.Ukrainian;
            this.RussianLanguageItem.IsChecked = language == Localization.UiLanguage.Russian;
        }

        private async void OnLanguageClick(object sender, RoutedEventArgs args) {
            this.UpdateLanguageSelection();
            if (sender is not RadioMenuFlyoutItem { Tag: string language }
                || !Localization.UiLanguage.IsSupported(language) || language == Localization.UiLanguage.Current) {
                return;
            }

            var dialog = new ContentDialog {
                XamlRoot = this.Content.XamlRoot,
                Title = Localization.UiStrings.Get("Tr_LanguageRestartTitle"),
                Content = Localization.UiStrings.Get("Tr_LanguageRestartMessage"),
                PrimaryButtonText = Localization.UiStrings.Get("Tr_LanguageRestart"),
                CloseButtonText = Localization.UiStrings.Get("Tr_LanguageCancel"),
                DefaultButton = ContentDialogButton.Close,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) {
                return;
            }

            try {
                Localization.UiLanguage.Save(language);
                AppInstance.Restart("");
                Localization.UiLanguage.Save(Localization.UiLanguage.Current);
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException) {
            }

            var error = new ContentDialog {
                XamlRoot = this.Content.XamlRoot,
                Title = Localization.UiStrings.Get("Tr_LanguageErrorTitle"),
                Content = Localization.UiStrings.Get("Tr_LanguageErrorMessage"),
                CloseButtonText = Localization.UiStrings.Get("Tr_LanguageDismiss"),
            };
            await error.ShowAsync();
        }

        private void OnActionClick(object sender, RoutedEventArgs args) {
            if (sender is MenuFlyoutItem { Tag: string actionId }) {
                MainWindow.PageManager.CurrentWorkspace?.ExecuteAction(actionId);
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs args) {
            MainWindow.PageManager.Navigate<Pages.SettingsPage>();
        }

        private void OnBackRequested(TitleBar sender, object args) {
            MainWindow.PageManager.GoBack();
        }

        private void OnPageNavigated(object sender, NavigationEventArgs args) {
            var isWorkspace = MainWindow.PageManager.CurrentWorkspace is not null;
            this.LoadDatabaseItem.IsEnabled = isWorkspace;
            this.SaveEventItem.IsEnabled = isWorkspace;
            this.ReloadConfigurationItem.IsEnabled = isWorkspace;
            this.SettingsItem.IsEnabled = isWorkspace;
            this.WorkspaceTitleBar.IsBackButtonVisible = MainWindow.PageManager.CanGoBack;
            this.WorkspaceTitleBar.IsBackButtonEnabled = MainWindow.PageManager.CanGoBack;
        }

        private void OnCloseWindowClick(object sender, RoutedEventArgs args) {
            this.Close();
        }
    }
}