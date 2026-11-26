using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.CandidatesTableParts {
    public sealed partial class CandidateCell : UserControl {
        public CandidateCell() {
            this.InitializeComponent();
        }

        internal void SetSelected(bool isActive) => States.NavigationFocus.SetActive(this.FocusFrame, isActive);
    }
}