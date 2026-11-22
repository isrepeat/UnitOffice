using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class ActiveFieldPreviewZone : UserControl {
        public ActiveFieldPreviewZone() {
            InitializeComponent();
        }

        internal void SetText(string text) {
            this.ActiveFieldPreviewText.Text = text;
        }
    }
}