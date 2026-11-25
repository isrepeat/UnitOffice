using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class CandidateCell : UserControl {
        public CandidateCell() {
            this.InitializeComponent();
        }

        internal void SetSelected(bool isActive) {
            this.Selection.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}