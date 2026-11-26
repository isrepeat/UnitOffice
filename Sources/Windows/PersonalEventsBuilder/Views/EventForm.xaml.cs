using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class EventForm : UserControl {
        public UDK.WinUI.Grid.GridLayout? GridLayout {
            get => (UDK.WinUI.Grid.GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(UDK.WinUI.Grid.GridLayout),
            typeof(EventForm),
            new PropertyMetadata(null, (sender, _) => ((EventForm)sender).UpdateGridSize())
        );

        private int requiredRows;
        private int requiredColumns;

        public EventForm() {
            this.InitializeComponent();
            var definition = (EventFormParts.FormDefinition)this.Resources["Definition"];
            foreach (var placedFormField in definition.CreateLayout()) {
                this.requiredRows = Math.Max(this.requiredRows, placedFormField.Bounds.Bottom);
                this.requiredColumns = Math.Max(this.requiredColumns, placedFormField.Bounds.Right);
                var styleKey = placedFormField.Group.Placement switch {
                    EventFormParts.FormPlacement.Primary => "PrimaryFieldStyle",
                    EventFormParts.FormPlacement.Left or EventFormParts.FormPlacement.Right => "SideExtensionFieldStyle",
                    _ => "ExtensionFieldStyle"
                };
                var label = $"{Localization.UiStrings.Get(placedFormField.Group.LabelKey)} {placedFormField.Field.Id}";
                this.Surface.Register(new EventFormParts.FormInput(placedFormField.Bounds, label, (Style)this.Resources[styleKey]));
            }
            this.UpdateGridSize();
        }

        private void UpdateGridSize() {
            if (this.GridLayout is { } layout) {
                layout.Rows.Count = Math.Max(layout.Rows.Count, this.requiredRows);
                layout.Columns.Count = Math.Max(layout.Columns.Count, this.requiredColumns);
            }
        }
    }
}