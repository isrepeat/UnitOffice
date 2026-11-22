using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class CandidatesZone : UserControl {
        public CandidatesZone() {
            InitializeComponent();
        }

        internal event Action<string>? CandidateSelected;

        internal void SetColumnInset(double inset) {
            if (this.CandidateHeader.Margin.Left != inset) {
                var margin = new Thickness(inset, 0, 0, 0);
                this.CandidateHeader.Margin = margin;
                this.CandidateList.Margin = margin;
            }
        }

        private void OnCandidateContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) {
            if (!args.InRecycleQueue) {
                var brushKey = args.ItemIndex % 2 == 0 ? "PanelBrush" : "CandidateStripeBrush";
                args.ItemContainer.Background = (Brush)Application.Current.Resources[brushKey];
            }
        }

        private void OnCandidateSelected(object sender, SelectionChangedEventArgs args) {
            if (args.AddedItems.Count > 0 && args.AddedItems[0] is Pages.ViewModels.CandidateDisplayRow { Id: var candidateId }) {
                this.CandidateSelected?.Invoke(candidateId);
            }
        }
    }
}