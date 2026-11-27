using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.CandidatesTableParts.Controls {
    public sealed partial class CandidateCell : UserControl {
        public CandidateCell() {
            this.InitializeComponent();
        }

        internal void SetSelected(bool isActive) => States.NavigationFocus.SetActive(this.FocusFrame, isActive);

        internal void SetContent(string value, bool header, bool showPlaceholder) {
            this.DataContext = value;
            this.Stroke.Visibility = showPlaceholder ? Visibility.Visible : Visibility.Collapsed;
            this.Surface.Background = (Brush)Application.Current.Resources[header ? "CommandBrush" : "PanelBrush"];
        }
    }
}