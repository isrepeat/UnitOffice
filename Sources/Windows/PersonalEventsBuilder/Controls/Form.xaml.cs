using System;
using System.Globalization;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class Form : UserControl {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<Workspace.FormField>),
            typeof(Form),
            new PropertyMetadata(null, OnItemsSourceChanged)
        );

        internal sealed record Editor(Workspace.FormField Field, FormFieldEditor Control) {
            public TextBox TextBox => this.Control.TextBox;
        }

        private readonly List<Editor> editors = new();

        internal event Action<Editor>? EditorFocused;
        internal event Action<TextBox>? EditorTextChanged;
        internal event Action? EditorsReplaced;

        public Form() {
            InitializeComponent();
        }

        public IEnumerable<Workspace.FormField>? ItemsSource {
            get => (IEnumerable<Workspace.FormField>?)this.GetValue(Form.ItemsSourceProperty);
            set => this.SetValue(Form.ItemsSourceProperty, value);
        }

        internal IReadOnlyList<Editor> Editors => this.editors;

        private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            ((Form)sender).RenderFields();
        }

        private void RenderFields() {
            this.editors.Clear();
            this.FieldGrid.Children.Clear();
            this.FieldGrid.ColumnDefinitions.Clear();

            foreach (var field in this.ItemsSource ?? Array.Empty<Workspace.FormField>()) {
                var control = new FormFieldEditor(field.Title);
                var editor = new Editor(field, control);
                Grid.SetColumn(control, this.FieldGrid.ColumnDefinitions.Count);
                this.FieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(field.Width, GridUnitType.Star) });
                this.FieldGrid.Children.Add(control);
                this.editors.Add(editor);
                Configure(editor, field.IsEnabled, field.InitialValue, field.Options);
                editor.TextBox.GotFocus += (_, _) => this.EditorFocused?.Invoke(editor);
                editor.TextBox.TextChanged += (_, _) => this.EditorTextChanged?.Invoke(editor.TextBox);
            }

            this.EditorsReplaced?.Invoke();
        }

        private static void Configure(Editor editor, bool enabled, Workspace.FieldValue? value, IReadOnlyList<string>? options) {
            editor.TextBox.IsEnabled = enabled;
            var hint = editor.Field.Kind == Workspace.FieldKind.Date
                ? Localization.UiStrings.Get("Tr_DateFormatHint")
                : options is { Count: > 0 } ? string.Join(", ", options) : null;
            ToolTipService.SetToolTip(editor.TextBox, hint);
            SetValue(editor, value);
        }

        internal static void SetValue(Editor editor, Workspace.FieldValue? value) {
            editor.TextBox.Text = value?.Text ?? (editor.Field.Kind switch {
                Workspace.FieldKind.Date => value?.Date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                Workspace.FieldKind.CheckBox => value?.Checked?.ToString(),
                _ => null,
            }) ?? "";
        }

        internal static Workspace.FieldValue ReadValue(Editor editor) {
            var text = editor.TextBox.Text;
            return editor.Field.Kind switch {
                Workspace.FieldKind.Date when DateTimeOffset.TryParseExact(
                    text.Trim(), "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date
                ) => new(Text: text, Date: date),
                Workspace.FieldKind.CheckBox when bool.TryParse(text, out var isChecked) => new(Text: text, Checked: isChecked),
                _ => new(Text: text),
            };
        }
    }
}