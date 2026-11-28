using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.CandidatesTableParts.Controls {
    public sealed class CandidateCellContent {
        public string Value { get; }
        public bool ShowPlaceholder { get; }

        public CandidateCellContent(string value, bool showPlaceholder) {
            this.Value = value;
            this.ShowPlaceholder = showPlaceholder;
        }
    }

    public sealed partial class CandidateCell : UserControl {
        public CandidateCell() {
            this.InitializeComponent();
        }

        internal void SetSelected(bool isActive) => States.NavigationFocus.SetActive(this.FocusFrame, isActive);

        internal void SetContent(string value, bool header, bool showPlaceholder) {
            // Повторная компоновка часто меняет только размеры ячейки. Сохраняем DataContext, чтобы не запускать привязки заново.
            if (this.DataContext is CandidateCellContent content && content.Value == value && content.ShowPlaceholder == showPlaceholder &&
                this.Surface.Background == (Brush)Application.Current.Resources[header ? "CommandBrush" : "PanelBrush"]) {
                return;
            }
            this.DataContext = new CandidateCellContent(value, showPlaceholder);
            this.Surface.Background = (Brush)Application.Current.Resources[header ? "CommandBrush" : "PanelBrush"];
        }
    }
}