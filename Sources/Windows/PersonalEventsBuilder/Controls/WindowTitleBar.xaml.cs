using System;
using System.IO;
using System.Security;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.Windows.AppLifecycle;

using Color = Windows.UI.Color;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class WindowTitleBar : UserControl {
        private static Navigation.PageManager PageManager => States.AppData.AppSessionState.PageManager;

        internal event Action? CloseRequested;

        public WindowTitleBar() {
            this.InitializeComponent();
            this.UpdateLanguageSelection();
        }

        internal void AttachTo(Window window) {
            this.Caption.Title = window.Title;

            if (!AppWindowTitleBar.IsCustomizationSupported()) {
                return;
            }

            window.ExtendsContentIntoTitleBar = true;
            this.Visibility = Visibility.Visible;
            window.SetTitleBar(this.Caption);

            var titleBar = window.AppWindow.TitleBar;
            var background = Color.FromArgb(255, 20, 20, 20);
            var inactiveForeground = Color.FromArgb(255, 172, 172, 172);

            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 48, 48, 48);
            titleBar.ButtonHoverForegroundColor = Colors.White;
            titleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 64, 64, 64);
            titleBar.ButtonPressedForegroundColor = Colors.White;
        }

        internal void UpdateNavigationState(bool isWorkspace, bool canGoBack) {
            this.LoadDatabaseItem.IsEnabled = false;
            this.SaveEventItem.IsEnabled = false;
            this.ReloadConfigurationItem.IsEnabled = false;
            this.SettingsItem.IsEnabled = isWorkspace;
            this.Caption.IsBackButtonVisible = canGoBack;
            this.Caption.IsBackButtonEnabled = canGoBack;
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
                XamlRoot = this.XamlRoot,
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
                XamlRoot = this.XamlRoot,
                Title = Localization.UiStrings.Get("Tr_LanguageErrorTitle"),
                Content = Localization.UiStrings.Get("Tr_LanguageErrorMessage"),
                CloseButtonText = Localization.UiStrings.Get("Tr_LanguageDismiss"),
            };
            await error.ShowAsync();
        }

        private void OnSettingsClick(object sender, RoutedEventArgs args) {
            WindowTitleBar.PageManager.Navigate<Pages.SettingsPage>();
        }

        private void OnBackRequested(TitleBar sender, object args) {
            WindowTitleBar.PageManager.GoBack();
        }

        private void OnCloseWindowClick(object sender, RoutedEventArgs args) {
            this.CloseRequested?.Invoke();
        }
    }
}