using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed class WorkspacePageViewModel : INotifyPropertyChanged {
        private readonly Workspace.IWorkspaceSource workspaceSource;
        private Workspace.EventMode? eventMode;

        public WorkspacePageViewModel(Workspace.IWorkspaceSource workspaceSource) {
            this.workspaceSource = workspaceSource;
            Workspace.ModeGroup[] groups = [
                new(Localization.UiStrings.Get("Tr_CategoryArrival"), workspaceSource.GetModes(Workspace.ModeCategory.Arrival)),
                new(Localization.UiStrings.Get("Tr_CategoryDeparture"), workspaceSource.GetModes(Workspace.ModeCategory.Departure)),
                new(Localization.UiStrings.Get("Tr_CategoryStatusChange"), workspaceSource.GetModes(Workspace.ModeCategory.StatusChange)),
            ];

            var modes = groups.SelectMany(group => group.Modes).ToArray();
            var initialMode = modes.FirstOrDefault(mode => mode.IsDefault) ?? modes.FirstOrDefault();
            this.ModeGroups = groups.Select(group => group with {
                Modes = group.Modes.Select(mode => mode with { IsDefault = mode.Id == initialMode?.Id }).ToArray(),
            }).ToArray();
            if (initialMode is not null) {
                this.SelectMode(initialMode);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public Workspace.ModeGroup[] ModeGroups { get; }
        public Workspace.ModeWorkspace? Current { get; private set; }
        public bool CanStartNewEvent => this.eventMode is not null;
        public Workspace.EventFormLayout? FormLayout { get; private set; }
        public string Preview => this.Current?.Preview ?? Localization.UiStrings.Get("Tr_ChooseMode");
        public string Status { get; private set; } = Localization.UiStrings.Get("Tr_Ready");
        public IReadOnlyList<Workspace.WorkspaceAction>? Actions => this.Current?.Actions;
        public CandidateTableLayout? CandidateTable { get; private set; }
        public string CandidateStatus => this.CandidateTable?.Rows.Length > 0 ? "" : Localization.UiStrings.Get("Tr_NoCandidates");

        public bool SelectMode(Workspace.EventMode eventMode) {
            if (this.eventMode?.Id == eventMode.Id) {
                return false;
            }

            this.LoadMode(eventMode);
            return true;
        }

        public void StartNewEvent() {
            if (this.eventMode is { } mode) {
                this.LoadMode(mode);
            }
        }

        private void LoadMode(Workspace.EventMode eventMode) {
            var modeWorkspace = this.workspaceSource.OpenMode(eventMode.Id);
            var formLayout = Workspace.EventFormLayoutBuilder.Build(modeWorkspace);
            this.FormLayout = formLayout;
            this.CandidateTable = CandidateTableBuilder.Build(modeWorkspace.Candidates, formLayout.PrimaryRow);
            this.Current = modeWorkspace;
            this.eventMode = eventMode;
            this.Status = Localization.UiStrings.Get("Tr_Ready");
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        public IReadOnlyDictionary<Workspace.CoreField, Workspace.FieldValue> ApplyCandidate(string candidateId, Workspace.FormValues values) =>
            this.eventMode is { } mode
                ? this.workspaceSource.ApplyCandidate(mode.Id, candidateId, values)
                : new Dictionary<Workspace.CoreField, Workspace.FieldValue>();

        public void ExecuteAction(string actionId, Workspace.FormValues values) {
            if (this.Actions?.Any(action => action.Id == actionId && action.IsEnabled) != true) {
                return;
            }

            this.Status = this.workspaceSource.ExecuteAction(this.eventMode?.Id, actionId, values);
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
        }
    }
}