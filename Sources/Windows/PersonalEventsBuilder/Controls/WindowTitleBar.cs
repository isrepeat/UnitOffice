using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Color = Windows.UI.Color;

namespace PersonalEventsBuilder.Controls {
    internal static class WindowTitleBar {
        public static void Configure(Window window, TitleBar workspaceTitleBar) {
            if (!AppWindowTitleBar.IsCustomizationSupported()) {
                return;
            }

            window.ExtendsContentIntoTitleBar = true;
            workspaceTitleBar.Visibility = Visibility.Visible;
            window.SetTitleBar(workspaceTitleBar);

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
    }
}