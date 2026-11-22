using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Color = Windows.UI.Color;

namespace PersonalEventsBuilder {
    public sealed partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
            this.ConfigureTitleBar();
        }

        private void ConfigureTitleBar() {
            if (!AppWindowTitleBar.IsCustomizationSupported()) {
                return;
            }

            this.ExtendsContentIntoTitleBar = true;
            this.WorkspaceTitleBar.Visibility = Visibility.Visible;
            this.SetTitleBar(this.WorkspaceTitleBar);

            var titleBar = this.AppWindow.TitleBar;
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

        private void OnModeChecked(object sender, RoutedEventArgs args) {
            if (this.PreviewText is not null && sender is RadioButton { Content: string mode }) {
                this.PreviewText.Text = $"Обраний режим: {mode}\nПопередній перегляд з’явиться після підключення шаблонів.";
            }
        }

        private void OnPendingActionClick(object sender, RoutedEventArgs args) {
            this.ActionStatus.Text = "Команда ще не підключена.";
        }

        private void OnCloseWindowClick(object sender, RoutedEventArgs args) {
            this.Close();
        }
    }
}