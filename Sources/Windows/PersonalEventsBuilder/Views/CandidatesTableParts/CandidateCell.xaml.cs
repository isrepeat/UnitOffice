using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.CandidatesTableParts {
    public sealed partial class CandidateCell : UserControl {
        public CandidateCell() {
            this.InitializeComponent();
            this.DataContextChanged += (_, args) => this.Stroke.Visibility =
                args.NewValue is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void SetSelected(bool isActive) => States.NavigationFocus.SetActive(this.FocusFrame, isActive);
    }
}