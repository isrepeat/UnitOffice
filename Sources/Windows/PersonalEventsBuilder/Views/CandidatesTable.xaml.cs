using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using GridLayout = PersonalEventsBuilder.Controls.GridLayout;

namespace PersonalEventsBuilder.Views {
    public sealed partial class CandidatesTable : UserControl {
        public GridLayout? GridLayout {
            get => (GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(GridLayout),
            typeof(CandidatesTable),
            new PropertyMetadata(null)
        );

        public CandidatesTable() {
            this.InitializeComponent();
            this.Surface.Cells = [
                new(2, 0), new(3, 0), new(4, 0), new(5, 0), new(6, 0), new(7, 0), new(8, 0),
                new(2, 1), new(3, 1), new(7, 1), new(8, 1), new(12, 1), new(13, 1),
                new(4, 2), new(5, 2), new(9, 2), new(10, 2),
                new(2, 4), new(3, 4), new(6, 4), new(7, 4), new(16, 4), new(17, 4),
                new(0, 6), new(1, 6), new(2, 6), new(11, 6), new(20, 6), new(21, 6),
            ];
            this.Surface.ViewportChanged += this.UpdateButtons;
            this.UpdateButtons();
        }

        private void UpdateButtons() {
            this.Previous.IsEnabled = this.Surface.CanScrollLeft;
            this.Next.IsEnabled = this.Surface.CanScrollRight;
        }

        private void OnPrevious(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(-1);

        private void OnNext(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(1);
    }
}