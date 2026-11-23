using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class CandidatesZone : UserControl {
        public CandidatesZone() {
            InitializeComponent();
        }

        private void OnCandidateContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) {
            if (!args.InRecycleQueue) {
                var brushKey = args.ItemIndex % 2 == 0 ? "PanelBrush" : "CandidateStripeBrush";
                args.ItemContainer.Background = (Brush)Application.Current.Resources[brushKey];
            }
        }
    }
}