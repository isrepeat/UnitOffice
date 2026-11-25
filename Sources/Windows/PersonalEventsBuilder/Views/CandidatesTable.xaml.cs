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