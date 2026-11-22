using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed record CandidateDisplayRow(string Id, string[] Cells);

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
        public Workspace.FormField[] LeftForm { get; private set; } = [];
        public Workspace.FormField[] CenterForm { get; private set; } = [];
        public Workspace.FormField[] RightForm { get; private set; } = [];
        public string Preview => this.Current?.Preview ?? Localization.UiStrings.Get("Tr_ChooseMode");
        public string Status { get; private set; } = Localization.UiStrings.Get("Tr_Ready");
        public IReadOnlyList<Workspace.WorkspaceAction>? Actions => this.Current?.Actions;
        public string[]? CandidateHeadings { get; private set; }
        public CandidateDisplayRow[]? CandidateRows { get; private set; }
        public string CandidateStatus => this.CandidateRows?.Length > 0 ? "" : Localization.UiStrings.Get("Tr_NoCandidates");

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
            this.LeftForm = CreateSideFields(modeWorkspace.Extensions, Workspace.FieldPlacement.Left);
            this.CenterForm = CreateCenterFields(modeWorkspace.CoreFields);
            this.RightForm = CreateSideFields(modeWorkspace.Extensions, Workspace.FieldPlacement.Right);
            var columns = modeWorkspace.Candidates.Columns;
            this.CandidateHeadings = columns.Select(column => column.Title).ToArray();
            this.CandidateRows = modeWorkspace.Candidates.Rows.Select(row => new CandidateDisplayRow(
                row.Id, columns.Select(column => row.Cells.GetValueOrDefault(column.Id, "")).ToArray()
            )).ToArray();
            this.Current = modeWorkspace;
            this.eventMode = eventMode;
            this.Status = Localization.UiStrings.Get("Tr_Ready");
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        private static Workspace.FormField[] CreateCenterFields(IReadOnlyList<Workspace.CoreFieldState> states) {
            var byField = states.ToDictionary(state => state.Field);
            return [
                Create(Workspace.CoreField.Person, "Tr_FieldPerson", Workspace.FieldKind.Text, 1.5),
                Create(Workspace.CoreField.Reporter, "Tr_FieldReporter", Workspace.FieldKind.Text, 1.4),
                Create(Workspace.CoreField.Rank, "Tr_FieldRank", Workspace.FieldKind.Text, 1),
                Create(Workspace.CoreField.IncomingNumber, "Tr_FieldIncomingNumber", Workspace.FieldKind.Text, 0.7),
                Create(Workspace.CoreField.IncomingDate, "Tr_FieldIncomingDate", Workspace.FieldKind.Date, 1.08),
                Create(Workspace.CoreField.DocumentType, "Tr_FieldDocument", Workspace.FieldKind.Choice, 1),
                Create(Workspace.CoreField.DocumentNumber, "Tr_FieldDocumentNumber", Workspace.FieldKind.Text, 1),
                Create(Workspace.CoreField.EventDate, "Tr_FieldEventDate", Workspace.FieldKind.Date, 1.08),
            ];

            Workspace.FormField Create(Workspace.CoreField field, string titleKey, Workspace.FieldKind kind, double width) {
                byField.TryGetValue(field, out var state);
                return new Workspace.FormField(
                    field.ToString(),
                    Localization.UiStrings.Get(titleKey),
                    kind,
                    field,
                    width,
                    state?.IsEnabled ?? true,
                    state?.InitialValue,
                    state?.Options
                );
            }
        }

        private static Workspace.FormField[] CreateSideFields(
            IReadOnlyList<Workspace.ExtensionField> extensions,
            Workspace.FieldPlacement placement
        ) => extensions.Where(field => field.Placement == placement)
            .Select(field => new Workspace.FormField(
                field.Id, field.Title, field.Kind, IsEnabled: field.IsEnabled, InitialValue: field.InitialValue, Options: field.Options
            )).ToArray();

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