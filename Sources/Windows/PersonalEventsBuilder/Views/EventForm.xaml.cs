using System;
using System.Collections.Generic;
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

        public IReadOnlyList<EventFormParts.PlacedFormField> Fields {
            get => (IReadOnlyList<EventFormParts.PlacedFormField>)this.GetValue(FieldsProperty);
            set => this.SetValue(FieldsProperty, value);
        }
        public static readonly DependencyProperty FieldsProperty = DependencyProperty.Register(
            nameof(Fields), typeof(IReadOnlyList<EventFormParts.PlacedFormField>), typeof(EventForm),
            new PropertyMetadata(null, (sender, _) => ((EventForm)sender).UpdateGridSize()));

        public EventForm() {
            this.InitializeComponent();
        }

        private void UpdateGridSize() {
            if (this.GridLayout is { } layout) {
                foreach (var field in this.Fields ?? []) {
                    layout.Rows.Count = Math.Max(layout.Rows.Count, field.Bounds.Bottom);
                    layout.Columns.Count = Math.Max(layout.Columns.Count, field.Bounds.Right);
                }
            }
        }
    }
}