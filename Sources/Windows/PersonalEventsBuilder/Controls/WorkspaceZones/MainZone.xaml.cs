using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class MainZone : UserControl {
        public MainZone() {
            this.InitializeComponent();
            Fixtures.MatrixWorkspaceFixture.ApplyTo(this.MatrixZone);
        }
    }
}