using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.CandidatesTableParts.Controls {
    public sealed partial class CandidateCell : UserControl {
        public CandidateCell() {
            this.InitializeComponent();
            this.DataContextChanged += (_, args) => this.Stroke.Visibility =
                args.NewValue is string text && !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void SetSelected(bool isActive) => States.NavigationFocus.SetActive(this.FocusFrame, isActive);

        internal void SetContent(string value, bool header) {
            this.DataContext = value;
            this.Surface.Background = (Brush)Application.Current.Resources[header ? "CommandBrush" : "PanelBrush"];
        }
    }
}