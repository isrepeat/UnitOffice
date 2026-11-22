using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class FormFieldEditor : UserControl {
        public FormFieldEditor() {
            InitializeComponent();
        }

        internal FormFieldEditor(string title) : this() {
            this.FieldLabel.Text = title;
            AutomationProperties.SetName(this.Input, title);
        }

        internal TextBox TextBox => this.Input;

        internal void SetCaretVisible(bool visible) {
            this.Readout.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnInputLostFocus(object sender, RoutedEventArgs args) {
            this.SetCaretVisible(true);
        }

        private void OnContextRequested(object sender, ContextRequestedEventArgs args) {
            args.Handled = true;
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs args) {
            args.Handled = true;
        }
    }
}