using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class EventFormZone : UserControl {
        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
            nameof(Layout),
            typeof(Workspace.EventFormLayout),
            typeof(EventFormZone),
            new PropertyMetadata(null)
        );

        public EventFormZone() {
            this.InitializeComponent();
        }

        internal event Action<string>? ActiveFieldTextChanged {
            add => this.FormMatrix.ActiveFieldTextChanged += value;
            remove => this.FormMatrix.ActiveFieldTextChanged -= value;
        }

        public Workspace.EventFormLayout? Layout {
            get => (Workspace.EventFormLayout?)this.GetValue(LayoutProperty);
            set => this.SetValue(LayoutProperty, value);
        }

        internal double LeftOverflow => Math.Max(0, this.LeftColumn.ActualWidth - this.LeftColumn.MinWidth);
        internal double CenterInset => this.LeftColumn.ActualWidth + this.FieldColumns.ColumnSpacing;
        internal double RequiredWidth => this.FieldColumns.ColumnDefinitions.Sum(column => column.ActualWidth)
            + (this.FieldColumns.ColumnDefinitions.Count - 1) * this.FieldColumns.ColumnSpacing
            + this.FormBorder.Padding.Left + this.FormBorder.Padding.Right
            + this.FormBorder.BorderThickness.Left + this.FormBorder.BorderThickness.Right;

        internal void SetHeadingInset(double inset) {
            this.FormHeading.Margin = new Thickness(inset, 0, 0, 0);
        }

        internal Workspace.FormValues ReadValues() {
            var editors = this.GetEditors().ToArray();
            return new Workspace.FormValues(
                editors.Where(editor => editor.Field.CoreField.HasValue)
                    .ToDictionary(editor => editor.Field.CoreField.GetValueOrDefault(), Form.ReadValue),
                editors.Where(editor => !editor.Field.CoreField.HasValue)
                    .ToDictionary(editor => editor.Field.Id, Form.ReadValue)
            );
        }

        internal void ApplyValues(IReadOnlyDictionary<Workspace.CoreField, Workspace.FieldValue> values) {
            foreach (var editor in this.GetEditors()) {
                if (editor.Field.CoreField is { } field && values.TryGetValue(field, out var value)) {
                    Form.SetValue(editor, value);
                }
            }
        }

        private IEnumerable<Form.Editor> GetEditors() =>
            this.FieldColumns.Children.OfType<Form>().SelectMany(form => form.Editors);
    }
}