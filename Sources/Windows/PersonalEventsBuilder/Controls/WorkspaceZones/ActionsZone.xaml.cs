using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class ActionsZone : UserControl {
        public ActionsZone() {
            InitializeComponent();
        }

        private void OnActionClick(object sender, RoutedEventArgs args) {
            if (sender is Button { Tag: string actionId }) {
                (this.DataContext as Pages.ViewModels.WorkspacePageViewModel)?.ExecuteAction(actionId);
            }
        }
    }
}