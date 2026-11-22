using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;

using Windows.UI.Core;
using Windows.System;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class NavigationMatrix : ContentControl {
        private sealed record Cell(Form.Editor Editor, Navigation.CellPosition Position) {
            public TextBox TextBox => this.Editor.TextBox;
        }

        private Form[] forms = [];
        private TextBox? activeEditor;
        private string originalText = "";
        private bool isTyping;
        private bool isEditingWithCaret;

        internal event Action<string>? ActiveFieldTextChanged;

        public NavigationMatrix() {
            this.InitializeComponent();
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
            this.PreviewKeyDown += this.OnFormPreviewKeyDown;
        }

        internal Workspace.FormValues ReadValues() {
            var editors = this.forms.SelectMany(form => form.Editors).ToArray();
            return new Workspace.FormValues(
                editors.Where(editor => editor.Field.CoreField.HasValue)
                    .ToDictionary(editor => editor.Field.CoreField.GetValueOrDefault(), Form.ReadValue),
                editors.Where(editor => !editor.Field.CoreField.HasValue)
                    .ToDictionary(editor => editor.Field.Id, Form.ReadValue)
            );
        }

        internal void ApplyValues(IReadOnlyDictionary<Workspace.CoreField, Workspace.FieldValue> values) {
            foreach (var editor in this.forms.SelectMany(form => form.Editors)) {
                if (editor.Field.CoreField is { } field && values.TryGetValue(field, out var value)) {
                    Form.SetValue(editor, value);
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs args) {
            this.forms = this.Content is Grid grid ? grid.Children.OfType<Form>().ToArray() : [];
            foreach (var form in this.forms) {
                form.EditorFocused += this.OnEditorFocused;
                form.EditorTextChanged += this.OnEditorTextChanged;
                form.EditorsReplaced += this.OnEditorsReplaced;
            }

            if (this.XamlRoot is { } root && FocusManager.GetFocusedElement(root) is TextBox focused
                && this.forms.SelectMany(form => form.Editors).FirstOrDefault(editor => editor.TextBox == focused) is { } editor) {
                this.OnEditorFocused(editor);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs args) {
            foreach (var form in this.forms) {
                form.EditorFocused -= this.OnEditorFocused;
                form.EditorTextChanged -= this.OnEditorTextChanged;
                form.EditorsReplaced -= this.OnEditorsReplaced;
            }

            this.forms = [];
            this.activeEditor = null;
        }

        private void OnEditorFocused(Form.Editor editor) {
            this.activeEditor = editor.TextBox;
            this.AcceptInput(editor);
            this.ActiveFieldTextChanged?.Invoke(editor.TextBox.Text);
        }

        private void OnEditorTextChanged(TextBox textBox) {
            if (this.activeEditor == textBox) {
                this.ActiveFieldTextChanged?.Invoke(textBox.Text);
            }
        }

        private void OnEditorsReplaced() {
            if (this.activeEditor is { } active && !this.forms.Any(zone => zone.Editors.Any(editor => editor.TextBox == active))) {
                this.activeEditor = null;
                this.ActiveFieldTextChanged?.Invoke("");
            }
        }

        private IEnumerable<Cell> GetCells() => this.forms
            .SelectMany(form => form.Editors.Select((editor, index) => new Cell(
                editor,
                new Navigation.CellPosition(
                    Grid.GetRow(form),
                    Grid.GetColumn(form),
                    form.FlowDirection == FlowDirection.RightToLeft ? form.Editors.Count - 1 - index : index
                )
            )));

        private void OnFormPreviewKeyDown(object sender, KeyRoutedEventArgs args) {
            if (this.XamlRoot is not { } xamlRoot || FocusManager.GetFocusedElement(xamlRoot) is not TextBox focused) {
                return;
            }

            var fields = this.GetCells().Where(cell => cell.TextBox.IsEnabled).ToArray();
            var index = Array.FindIndex(fields, field => field.TextBox == focused);
            if (index < 0) {
                return;
            }

            var current = fields[index];
            switch (args.Key) {
                case VirtualKey.F2:
                    this.isEditingWithCaret = true;
                    current.Editor.Control.SetCaretVisible(true);
                    focused.Select(focused.Text.Length, 0);
                    break;
                case VirtualKey.Escape when this.isTyping || this.isEditingWithCaret:
                    focused.Text = this.originalText;
                    this.AcceptInput(current.Editor);
                    break;
                case VirtualKey.Tab:
                    var tabDirection = IsKeyDown(VirtualKey.Shift)
                        ? Navigation.CellDirection.Previous
                        : Navigation.CellDirection.Next;
                    if (!this.NavigateFrom(fields, index, tabDirection)) {
                        return;
                    }
                    break;
                case VirtualKey.Enter:
                    this.NavigateFrom(fields, index, IsKeyDown(VirtualKey.Shift) ? Navigation.CellDirection.Up : Navigation.CellDirection.Down);
                    break;
                case VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down when !this.isEditingWithCaret:
                    var arrowDirection = args.Key switch {
                        VirtualKey.Left => Navigation.CellDirection.Left,
                        VirtualKey.Right => Navigation.CellDirection.Right,
                        VirtualKey.Up => Navigation.CellDirection.Up,
                        _ => Navigation.CellDirection.Down,
                    };
                    this.NavigateFrom(fields, index, arrowDirection);
                    break;
                default:
                    if (!this.isTyping && !this.isEditingWithCaret && IsTextInputKey(args.Key)) {
                        focused.SelectAll();
                        this.isTyping = true;
                    }

                    return;
            }

            args.Handled = true;
        }

        private bool NavigateFrom(Cell[] fields, int index, Navigation.CellDirection direction) {
            var positions = fields.Select(field => field.Position).ToArray();
            var target = Navigation.CellNavigation.FindTarget(positions, index, direction);
            this.AcceptInput(fields[index].Editor);
            if (target is not { } targetIndex) {
                return false;
            }

            fields[targetIndex].TextBox.Focus(FocusState.Keyboard);
            return true;
        }

        private void AcceptInput(Form.Editor editor) {
            this.originalText = editor.TextBox.Text;
            this.isTyping = false;
            this.isEditingWithCaret = false;
            editor.Control.SetCaretVisible(false);
        }

        private static bool IsTextInputKey(VirtualKey key) {
            var control = IsKeyDown(VirtualKey.Control);
            var menu = IsKeyDown(VirtualKey.Menu);
            if (control != menu) {
                return control && key == VirtualKey.V;
            }

            return key is VirtualKey.Space or VirtualKey.Back or VirtualKey.Delete
                || (int)key is >= 0x30 and <= 0x5A or >= 0x60 and <= 0x6F or >= 0xBA and <= 0xE2
                || (int)key == 0xE5;
        }

        private static bool IsKeyDown(VirtualKey key) =>
            InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
    }
}