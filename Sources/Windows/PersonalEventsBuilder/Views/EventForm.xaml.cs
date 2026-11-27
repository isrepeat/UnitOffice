using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class EventForm : UserControl {
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(UDK.WinUI.Grid.GridLayout),
            typeof(EventForm),
            new PropertyMetadata(null, (sender, _) => ((EventForm)sender).UpdateGridSize())
        );

        public static readonly DependencyProperty FieldsProperty = DependencyProperty.Register(
            nameof(Fields), typeof(IReadOnlyList<EventFormParts.PlacedFormField>), typeof(EventForm),
            new PropertyMetadata(null, (sender, _) => ((EventForm)sender).UpdateFields()));

        public UDK.WinUI.Grid.GridLayout? GridLayout {
            get => (UDK.WinUI.Grid.GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }

        public IReadOnlyList<EventFormParts.PlacedFormField> Fields {
            get => (IReadOnlyList<EventFormParts.PlacedFormField>)this.GetValue(FieldsProperty);
            set => this.SetValue(FieldsProperty, value);
        }

        private int requiredRows;
        private int requiredColumns;

        public EventForm() {
            this.InitializeComponent();
        }

        private void UpdateFields() {
            var inputs = new List<EventFormParts.Controls.FormInput>();
            this.requiredRows = 0;
            this.requiredColumns = 0;
            foreach (var placedFormField in this.Fields ?? []) {
                this.requiredRows = Math.Max(this.requiredRows, placedFormField.Bounds.Bottom);
                this.requiredColumns = Math.Max(this.requiredColumns, placedFormField.Bounds.Right);
                var styleKey = placedFormField.Group.Placement switch {
                    EventFormParts.Scheme.FormPlacement.Primary => "PrimaryFieldStyle",
                    EventFormParts.Scheme.FormPlacement.Left or EventFormParts.Scheme.FormPlacement.Right => "SideExtensionFieldStyle",
                    _ => "ExtensionFieldStyle"
                };
                var label = $"{Localization.UiStrings.Get(placedFormField.Group.LabelKey)} {placedFormField.Field.Id}";
                inputs.Add(new EventFormParts.Controls.FormInput(placedFormField.Bounds, label, (Style)this.Resources[styleKey]));
            }
            this.Surface.ReplaceRegions(inputs);
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