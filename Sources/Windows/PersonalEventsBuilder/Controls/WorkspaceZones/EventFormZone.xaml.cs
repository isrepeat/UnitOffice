using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class EventFormZone : UserControl {
        public EventFormZone() {
            this.InitializeComponent();
        }

        internal NavigationMatrix Matrix => this.FormMatrix;
    }
}