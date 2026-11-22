using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

using Color = Windows.UI.Color;

namespace PersonalEventsBuilder.Controls {
    public sealed class EventFormView : UserControl {
        private sealed record FieldEditor(TextBox TextBox, Workspace.FieldKind Kind);

        private readonly Grid layout = new() { ColumnSpacing = 6 };
        private readonly Dictionary<Workspace.CoreField, StackPanel> columns = new();
        private readonly Dictionary<Workspace.CoreField, FieldEditor> editors = new();
        private readonly Dictionary<string, FieldEditor> extensionEditors = new(StringComparer.Ordinal);
        private readonly StackPanel left = new() { Spacing = 3 };
        private readonly StackPanel right = new() { Spacing = 3 };
        private TextBox? activeEditor;

        internal event Action<string>? ActiveFieldTextChanged;

        public EventFormView() {
            this.Content = this.layout;
            this.AddColumn(CreateExtensionZone(this.left), new GridLength(106));
            this.AddCoreField(Workspace.CoreField.Person, Localization.UiStrings.Get("Tr_FieldPerson"), Workspace.FieldKind.Text, 1.5);
            this.AddCoreField(Workspace.CoreField.Reporter, Localization.UiStrings.Get("Tr_FieldReporter"), Workspace.FieldKind.Text, 1.4);
            this.AddCoreField(Workspace.CoreField.Rank, Localization.UiStrings.Get("Tr_FieldRank"), Workspace.FieldKind.Text, 1);
            this.AddCoreField(Workspace.CoreField.IncomingNumber, Localization.UiStrings.Get("Tr_FieldIncomingNumber"), Workspace.FieldKind.Text, 0.7);
            this.AddCoreField(Workspace.CoreField.IncomingDate, Localization.UiStrings.Get("Tr_FieldIncomingDate"), Workspace.FieldKind.Date, 1.08);
            this.AddCoreField(Workspace.CoreField.DocumentType, Localization.UiStrings.Get("Tr_FieldDocument"), Workspace.FieldKind.Choice, 1);
            this.AddCoreField(Workspace.CoreField.DocumentNumber, Localization.UiStrings.Get("Tr_FieldDocumentNumber"), Workspace.FieldKind.Text, 1);
            this.AddCoreField(Workspace.CoreField.EventDate, Localization.UiStrings.Get("Tr_FieldEventDate"), Workspace.FieldKind.Date, 1.08);
            this.AddColumn(CreateExtensionZone(this.right), new GridLength(106));
        }

        public void ApplyLayout(Workspace.ModeWorkspace modeWorkspace) {
            this.activeEditor = null;
            this.ActiveFieldTextChanged?.Invoke("");
            this.left.Children.Clear();
            this.right.Children.Clear();
            this.extensionEditors.Clear();

            var states = modeWorkspace.CoreFields.ToDictionary(state => state.Field);
            foreach (var (field, editor) in this.editors) {
                var column = this.columns[field];
                while (column.Children.Count > 2) {
                    column.Children.RemoveAt(column.Children.Count - 1);
                }

                states.TryGetValue(field, out var state);
                Configure(editor, state?.IsEnabled ?? true, state?.InitialValue, state?.Options);
            }

            foreach (var extension in modeWorkspace.Extensions) {
                var column = extension.Placement switch {
                    Workspace.FieldPlacement.Left => this.left,
                    Workspace.FieldPlacement.Right => this.right,
                    Workspace.FieldPlacement.Below when extension.Anchor is { } anchor => GetBelowZone(this.columns[anchor]),
                    _ => throw new ArgumentException("Invalid field placement.", nameof(modeWorkspace)),
                };

                var editor = this.AddEditor(column, extension.Title, extension.Kind);
                Configure(editor, extension.IsEnabled, extension.InitialValue, extension.Options);
                this.extensionEditors.Add(extension.Id, editor);
            }
        }

        public void ApplyValues(IReadOnlyDictionary<Workspace.CoreField, Workspace.FieldValue> values) {
            foreach (var (field, value) in values) {
                SetValue(this.editors[field], value);
            }
        }

        public Workspace.FormValues ReadValues() => new(
            this.editors.ToDictionary(entry => entry.Key, entry => ReadValue(entry.Value)),
            this.extensionEditors.ToDictionary(entry => entry.Key, entry => ReadValue(entry.Value)));

        private void AddCoreField(Workspace.CoreField field, string title, Workspace.FieldKind kind, double width) {
            var column = new StackPanel { Spacing = 3 };
            this.AddColumn(column, new GridLength(width, GridUnitType.Star));
            this.columns.Add(field, column);
            this.editors.Add(field, this.AddEditor(column, title, kind));
        }

        private void AddColumn(FrameworkElement column, GridLength width) {
            Grid.SetColumn(column, this.layout.ColumnDefinitions.Count);
            this.layout.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            this.layout.Children.Add(column);
        }

        private static StackPanel GetBelowZone(StackPanel column) {
            if (column.Children.Count == 3 && column.Children[2] is Border { Child: StackPanel existing }) {
                return existing;
            }

            var fields = new StackPanel { Spacing = 3 };
            column.Children.Add(CreateExtensionZone(fields));
            return fields;
        }

        private static Border CreateExtensionZone(StackPanel fields) {
            return new Border {
                Child = fields,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 155, 112, 61)),
                Padding = new Thickness(3),
            };
        }

        private FieldEditor AddEditor(StackPanel column, string title, Workspace.FieldKind kind) {
            var textBox = new TextBox {
                Padding = new Thickness(5, 3, 5, 3),
                FontSize = 12,
                MinWidth = 0,
                MinHeight = 28,
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            AutomationProperties.SetName(textBox, title);
            textBox.GotFocus += this.OnEditorGotFocus;
            textBox.TextChanged += this.OnEditorTextChanged;
            column.Children.Add(new TextBlock { Text = title, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            column.Children.Add(textBox);
            return new FieldEditor(textBox, kind);
        }

        private void OnEditorGotFocus(object sender, RoutedEventArgs args) {
            if (sender is TextBox textBox) {
                this.activeEditor = textBox;
                this.ActiveFieldTextChanged?.Invoke(textBox.Text);
            }
        }

        private void OnEditorTextChanged(object sender, TextChangedEventArgs args) {
            if (this.activeEditor is { } editor && ReferenceEquals(sender, editor)) {
                this.ActiveFieldTextChanged?.Invoke(editor.Text);
            }
        }

        private static void Configure(FieldEditor editor, bool enabled, Workspace.FieldValue? value, IReadOnlyList<string>? options) {
            editor.TextBox.IsEnabled = enabled;
            var hint = editor.Kind == Workspace.FieldKind.Date
                ? Localization.UiStrings.Get("Tr_DateFormatHint")
                : options is { Count: > 0 } ? string.Join(", ", options) : null;
            ToolTipService.SetToolTip(editor.TextBox, hint);
            SetValue(editor, value);
        }

        private static void SetValue(FieldEditor editor, Workspace.FieldValue? value) {
            editor.TextBox.Text = value?.Text ?? (editor.Kind switch {
                Workspace.FieldKind.Date => value?.Date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                Workspace.FieldKind.CheckBox => value?.Checked?.ToString(),
                _ => null,
            }) ?? "";
        }

        private static Workspace.FieldValue ReadValue(FieldEditor editor) {
            var text = editor.TextBox.Text;
            return editor.Kind switch {
                Workspace.FieldKind.Date when DateTimeOffset.TryParseExact(
                    text.Trim(), "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date
                ) => new(Text: text, Date: date),
                Workspace.FieldKind.CheckBox when bool.TryParse(text, out var isChecked) => new(Text: text, Checked: isChecked),
                _ => new(Text: text),
            };
        }
    }
}