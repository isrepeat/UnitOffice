using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

using WorkspaceGrid = PersonalEventsBuilder.Workspace.WorkspaceGrid;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public sealed partial class EventFormSection : UserControl {
        public EventFormSectionViewModel? ViewModel {
            get => (EventFormSectionViewModel?)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(EventFormSectionViewModel),
            typeof(EventFormSection),
            new PropertyMetadata(null, OnViewModelChanged)
        );

        public EventFormSection() {
            this.InitializeComponent();
        }

        private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var eventFormSection = (EventFormSection)sender;
            if (args.OldValue is EventFormSectionViewModel oldViewModel) {
                oldViewModel.EndEditing(commit: false);
                oldViewModel.EditingChanged -= eventFormSection.UpdateEditor;
                oldViewModel.FieldChanged -= eventFormSection.UpdateField;
            }

            if (args.NewValue is EventFormSectionViewModel eventFormSectionViewModel) {
                eventFormSectionViewModel.EditingChanged += eventFormSection.UpdateEditor;
                eventFormSectionViewModel.FieldChanged += eventFormSection.UpdateField;
            }

            eventFormSection.BuildLabels();
            eventFormSection.UpdateEditor();
        }

        private void BuildLabels() {
            this.FieldLabels.Children.Clear();
            this.FieldValues.Children.Clear();
            if (this.ViewModel is not { } eventFormSectionViewModel) {
                return;
            }

            var cellStyle = (Style)Application.Current.Resources["WorkspaceCellText"];
            var labelStyle = (Style)Application.Current.Resources["WorkspaceHeaderText"];
            foreach (var eventField in eventFormSectionViewModel.Fields) {
                var valueTextBlock = new TextBlock { Text = eventField.DisplayText, Style = cellStyle };
                WorkspaceGrid.Place(valueTextBlock, eventField.Area);
                this.FieldValues.Children.Add(valueTextBlock);
                if (eventField.LabelArea is { } labelArea) {
                    var labelTextBlock = new TextBlock { Text = eventField.Label, Style = labelStyle };
                    WorkspaceGrid.Place(labelTextBlock, labelArea);
                    this.FieldLabels.Children.Add(labelTextBlock);
                }
            }
        }

        private void UpdateEditor() {
            if (this.ViewModel is not { EditingIndex: int index } eventFormSectionViewModel) {
                this.FieldEditor.Visibility = Visibility.Collapsed;
                return;
            }

            var eventField = eventFormSectionViewModel.Fields[index];
            WorkspaceGrid.Place(this.FieldEditor, eventField.Area);
            AutomationProperties.SetName(this.FieldEditor, eventField.Label);
            this.FieldEditor.Text = eventFormSectionViewModel.EditorText;
            this.FieldEditor.PlaceholderText = eventField.Placeholder;
            this.FieldEditor.Visibility = Visibility.Visible;
            this.FieldEditor.Focus(FocusState.Programmatic);
            this.FieldEditor.SelectAll();
        }

        private void UpdateField(int index) {
            if (this.ViewModel is { } eventFormSectionViewModel) {
                ((TextBlock)this.FieldValues.Children[index]).Text = eventFormSectionViewModel.Fields[index].DisplayText;
            }
        }

        private void OnFieldEditorTextChanged(object sender, TextChangedEventArgs args) {
            if (this.ViewModel is { EditingIndex: not null } eventFormSectionViewModel) {
                eventFormSectionViewModel.EditorText = this.FieldEditor.Text;
            }
        }

        private void OnFieldEditorLostFocus(object sender, RoutedEventArgs args) => this.ViewModel?.EndEditing(commit: true);
    }
}