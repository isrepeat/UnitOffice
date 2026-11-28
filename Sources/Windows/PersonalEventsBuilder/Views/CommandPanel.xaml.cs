using Windows.System;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class CommandPanel : UserControl, UDK.WinUI.Navigation.IKeyboardNavigationArea {
        public CommandPanel() {
            this.InitializeComponent();
        }

        private Microsoft.UI.Xaml.Visibility ScrollVisibility(double range) =>
            range > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        private void OnCommandScroll(object sender, Microsoft.UI.Xaml.Controls.Primitives.ScrollEventArgs args) =>
            this.CommandViewport.ChangeView(null, args.NewValue, null, true);

        //
        // IKeyboardNavigationArea
        //
        public bool FocusArea() => this.Buttons.FocusArea();

        public void MoveFocus(VirtualKey direction) => this.Buttons.MoveFocus(direction);
    }
}