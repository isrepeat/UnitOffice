using Windows.System;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class CommandPanel : UserControl, UDK.WinUI.Navigation.IKeyboardNavigationArea {
        public CommandPanel() {
            this.InitializeComponent();
        }

        public bool FocusArea() => this.Buttons.FocusArea();

        public void MoveFocus(VirtualKey direction) => this.Buttons.MoveFocus(direction);
    }
}