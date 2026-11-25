using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class ModeSelectorZone : UserControl {
        public ModeSelectorZone() {
            InitializeComponent();
        }

        private void OnModeChecked(object sender, RoutedEventArgs args) {
            if (sender is RadioButton { Tag: Workspace.EventMode mode }) {
                (this.DataContext as Pages.ViewModels.WorkspacePageViewModel)?.SelectMode(mode);
            }
        }
    }
}