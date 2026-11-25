using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using GridLayout = PersonalEventsBuilder.Controls.GridLayout;

namespace PersonalEventsBuilder.Views {
    public sealed partial class EventForm : UserControl {
        public GridLayout? GridLayout {
            get => (GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(GridLayout),
            typeof(EventForm),
            new PropertyMetadata(null)
        );

        public EventForm() {
            this.InitializeComponent();
            this.Surface.Cells = [
                new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0),
                new(0, 1), new(1, 1), new(5, 1), new(6, 1), new(10, 1), new(11, 1),
                new(2, 2), new(3, 2), new(8, 2), new(9, 2),
                new(0, 4), new(4, 4), new(5, 4), new(12, 4), new(13, 4),
                new(0, 5), new(1, 5), new(6, 5), new(7, 5), new(14, 5), new(15, 5),
            ];
        }
    }
}