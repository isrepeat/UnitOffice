using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using GridScale = PersonalEventsBuilder.Controls.GridScale;

namespace PersonalEventsBuilder.Views {
    public sealed partial class EventForm : UserControl {
        public GridScale? GridScale {
            get => (GridScale?)this.GetValue(GridScaleProperty);
            set => this.SetValue(GridScaleProperty, value);
        }
        public static readonly DependencyProperty GridScaleProperty = DependencyProperty.Register(
            nameof(GridScale),
            typeof(GridScale),
            typeof(EventForm),
            new PropertyMetadata(null)
        );

        public EventForm() {
            this.InitializeComponent();
        }
    }
}