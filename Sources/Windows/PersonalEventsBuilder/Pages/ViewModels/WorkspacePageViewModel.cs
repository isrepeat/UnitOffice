using System;
using System.Linq;
using System.ComponentModel;
using Microsoft.UI.Xaml.Input;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed class WorkspacePageViewModel : INotifyPropertyChanged {
        //
        // INotifyPropertyChanged
        //
        public event PropertyChangedEventHandler? PropertyChanged;

        public XamlUICommand SelectModeCommand { get; } = new();
        public Views.EventFormParts.EventLayout EventLayout {
            get => this.eventLayout;
            private set => Helpers.PropertyChange.Set(this, ref this.eventLayout, value, this.PropertyChanged);
        }
        public Views.EventFormParts.Scheme.FormDefinition FormDefinition {
            get => this.formDefinition;
            set => this.RebuildLayout(value, this.selectedMode);
        }

        private string? selectedMode;
        private Views.EventFormParts.EventLayout eventLayout = Views.EventFormParts.Scheme.FormDefinition.CreateLayout([]);
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
            var formFeatures = formDefinition.Modes
                .SingleOrDefault(formModeDefinition => formModeDefinition.Mode == mode)
                ?.Features ?? Views.EventFormParts.Scheme.FormFeature.None;

            // Include the primary group and extensions that require at least one feature and have all their required features enabled.
            var formGroups = formDefinition.Groups
                 .Where(formGroup =>
                        formGroup.Placement == Views.EventFormParts.Scheme.FormPlacement.Primary ||
                        formGroup.Feature != Views.EventFormParts.Scheme.FormFeature.None && (formFeatures & formGroup.Feature) == formGroup.Feature)
                 .ToArray();

            var eventLayout = Views.EventFormParts.Scheme.FormDefinition.CreateLayout(formGroups);
            this.formDefinition = formDefinition;
            this.selectedMode = mode;
            this.EventLayout = eventLayout;
        }
    }
}