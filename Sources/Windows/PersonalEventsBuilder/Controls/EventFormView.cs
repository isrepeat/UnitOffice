using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

using Color = Windows.UI.Color;

namespace PersonalEventsBuilder.Controls {
    public sealed class EventFormView : UserControl {
        private readonly Grid layout = new() { ColumnSpacing = 6 };
        private readonly Dictionary<Workspace.CoreField, StackPanel> columns = new();
        private readonly Dictionary<Workspace.CoreField, Control> editors = new();
        private readonly Dictionary<string, Control> extensionEditors = new(StringComparer.Ordinal);
        private readonly StackPanel left = new() { Spacing = 3 };
        private readonly StackPanel right = new() { Spacing = 3 };

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

                var editor = AddEditor(column, extension.Title, extension.Kind);
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
            this.editors.Add(field, AddEditor(column, title, kind));
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

        private static Control AddEditor(StackPanel column, string title, Workspace.FieldKind kind) {
            Control editor = kind switch {
                Workspace.FieldKind.Text => new TextBox { Padding = new Thickness(5, 3, 5, 3) },
                Workspace.FieldKind.Date => new CalendarDatePicker { PlaceholderText = Localization.UiStrings.Get("Tr_ChooseDate") },
                Workspace.FieldKind.Choice => new ComboBox(),
                Workspace.FieldKind.CheckBox => new CheckBox(),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            editor.FontSize = 12;
            editor.MinWidth = 0;
            editor.MinHeight = 28;
            editor.CornerRadius = new CornerRadius(0);
            editor.HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(editor, title);
            column.Children.Add(new TextBlock { Text = title, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            column.Children.Add(editor);
            return editor;
        }

        private static void Configure(Control editor, bool enabled, Workspace.FieldValue? value, IReadOnlyList<string>? options) {
            editor.IsEnabled = enabled;
            if (editor is ComboBox comboBox) {
                comboBox.ItemsSource = options;
            }

            SetValue(editor, value);
        }

        private static void SetValue(Control editor, Workspace.FieldValue? value) {
            switch (editor) {
                case TextBox textBox: textBox.Text = value?.Text ?? ""; break;
                case CalendarDatePicker datePicker: datePicker.Date = value?.Date; break;
                case ComboBox comboBox: comboBox.SelectedItem = value?.Text; break;
                case CheckBox checkBox: checkBox.IsChecked = value?.Checked ?? false; break;
            }
        }

        private static Workspace.FieldValue ReadValue(Control editor) => editor switch {
            TextBox textBox => new(Text: textBox.Text),
            CalendarDatePicker datePicker => new(Date: datePicker.Date),
            ComboBox comboBox => new(Text: comboBox.SelectedItem as string),
            CheckBox checkBox => new(Checked: checkBox.IsChecked),
            _ => throw new ArgumentOutOfRangeException(nameof(editor)),
        };
    }
}