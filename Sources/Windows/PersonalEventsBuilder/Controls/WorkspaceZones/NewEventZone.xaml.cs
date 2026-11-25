using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class NewEventZone : UserControl {
        public NewEventZone() {
            InitializeComponent();
        }

        private void OnNewEventClick(object sender, RoutedEventArgs args) {
            (this.DataContext as Pages.ViewModels.WorkspacePageViewModel)?.StartNewEvent();
        }
    }
}