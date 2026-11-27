using System;
using System.Linq;
using System.ComponentModel;
using Microsoft.UI.Xaml.Input;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed class WorkspacePageViewModel : INotifyPropertyChanged {
        public XamlUICommand SelectModeCommand { get; } = new();
        public Views.EventFormParts.EventLayout EventLayout { get; private set; } = new(Array.Empty<Views.EventFormParts.PlacedFormField>(), new Views.EventFormParts.ColumnRange(0, 0));
        public Views.EventFormParts.Scheme.FormDefinition FormDefinition {
            get => this.formDefinition;
            set => this.RebuildLayout(value, this.selectedMode);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private string? selectedMode;
        private Views.EventFormParts.Scheme.FormDefinition formDefinition = new();

        public WorkspacePageViewModel() {
            this.SelectModeCommand.ExecuteRequested += (_, args) => this.SelectMode(args.Parameter as string);
        }

        public void SelectMode(string? mode) {
            if (this.selectedMode != mode) {
                this.RebuildLayout(this.FormDefinition, mode);
            }
        }

        private void RebuildLayout(Views.EventFormParts.Scheme.FormDefinition formDefinition, string? mode) {
            var formFeatures = formDefinition.Modes.SingleOrDefault(formModeDefinition => formModeDefinition.Mode == mode)?.Features ?? Views.EventFormParts.Scheme.FormFeature.None;
            var formGroups = formDefinition.Groups.Where(formGroup => formGroup.Placement == Views.EventFormParts.Scheme.FormPlacement.Primary ||
                formGroup.Feature != Views.EventFormParts.Scheme.FormFeature.None && (formFeatures & formGroup.Feature) == formGroup.Feature).ToArray();
            var eventLayout = formGroups.Length == 0
                ? new Views.EventFormParts.EventLayout(Array.Empty<Views.EventFormParts.PlacedFormField>(), new Views.EventFormParts.ColumnRange(0, 0))
                : Views.EventFormParts.Scheme.FormDefinition.CreateLayout(formGroups);
            this.formDefinition = formDefinition;
            this.selectedMode = mode;
            this.EventLayout = eventLayout;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EventLayout)));
        }
    }
}