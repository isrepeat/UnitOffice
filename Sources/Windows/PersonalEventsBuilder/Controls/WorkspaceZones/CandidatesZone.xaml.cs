using System;

using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class CandidatesZone : UserControl {
        public CandidatesZone() {
            InitializeComponent();
        }

        internal event Action<string>? CandidateSelected;

        private void OnCandidateSelected(object sender, SelectionChangedEventArgs args) {
            if (args.AddedItems.Count > 0 && args.AddedItems[0] is Pages.ViewModels.CandidateDisplayRow { Id: var candidateId }) {
                this.CandidateSelected?.Invoke(candidateId);
            }
        }
    }
}