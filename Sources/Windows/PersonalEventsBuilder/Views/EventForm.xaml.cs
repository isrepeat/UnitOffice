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
        }
    }
}