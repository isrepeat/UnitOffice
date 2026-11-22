using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed record CandidateDisplayCell(string Text, double Width);

    public sealed record CandidateDisplayRow(string Id, CandidateDisplayCell[] Cells);

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
        public CandidateDisplayCell[]? CandidateHeadings { get; private set; }
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
            this.LeftForm = CreateExtensionFields(modeWorkspace.Extensions, Workspace.FieldPlacement.Left).ToArray();
            var centerFields = CreateCenterFields(modeWorkspace.CoreFields)
                .Concat(CreateExtensionFields(modeWorkspace.Extensions, Workspace.FieldPlacement.Inline));
            if (modeWorkspace.CenterFieldOrder is { } order) {
                var positions = order.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
                centerFields = centerFields.OrderBy(field => positions.GetValueOrDefault(field.Id, int.MaxValue));
            }

            this.CenterForm = [
                .. centerFields,
                .. CreateExtensionFields(modeWorkspace.Extensions, Workspace.FieldPlacement.Below),
            ];
            this.RightForm = CreateExtensionFields(modeWorkspace.Extensions, Workspace.FieldPlacement.Right).ToArray();
            var columns = modeWorkspace.Candidates.Columns;
            var mainRowFields = this.CenterForm.Where(field => !field.Anchor.HasValue);
            var remainingColumns = columns.ToList();
            var slots = new List<(Workspace.CandidateColumn? Column, double Width)>();
            foreach (var field in mainRowFields) {
                var column = field.CoreField is { } target
                    ? remainingColumns.Find(column => column.TargetField == target)
                    : remainingColumns.Find(column => column.TargetExtensionId == field.Id);
                if (column is not null) {
                    remainingColumns.Remove(column);
                }

                slots.Add((column, field.Width + Controls.Form.ColumnSpacing));
            }

            slots.AddRange(remainingColumns.Select(column => ((Workspace.CandidateColumn?)column, 152d)));
            this.CandidateHeadings = slots.Select(slot => new CandidateDisplayCell(slot.Column?.Title ?? "", slot.Width)).ToArray();
            this.CandidateRows = modeWorkspace.Candidates.Rows.Select(row => new CandidateDisplayRow(
                row.Id,
                slots.Select(slot => new CandidateDisplayCell(
                    slot.Column is { } column ? row.Cells.GetValueOrDefault(column.Id, "") : "",
                    slot.Width
                )).ToArray()
            )).ToArray();
            this.Current = modeWorkspace;
            this.eventMode = eventMode;
            this.Status = Localization.UiStrings.Get("Tr_Ready");
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        private static Workspace.FormField[] CreateCenterFields(IReadOnlyList<Workspace.CoreFieldState> states) {
            var byField = states.ToDictionary(state => state.Field);
            return [
                Create(Workspace.CoreField.Person, "Tr_FieldPerson", Workspace.FieldKind.Text, 120),
                Create(Workspace.CoreField.Reporter, "Tr_FieldReporter", Workspace.FieldKind.Text, 90),
                Create(Workspace.CoreField.Rank, "Tr_FieldRank", Workspace.FieldKind.Text, 80),
                Create(Workspace.CoreField.IncomingNumber, "Tr_FieldIncomingNumber", Workspace.FieldKind.Text, 48),
                Create(Workspace.CoreField.IncomingDate, "Tr_FieldIncomingDate", Workspace.FieldKind.Date, 68),
                Create(Workspace.CoreField.DocumentType, "Tr_FieldDocument", Workspace.FieldKind.Choice, 60),
                Create(Workspace.CoreField.DocumentNumber, "Tr_FieldDocumentNumber", Workspace.FieldKind.Text, 76),
                Create(Workspace.CoreField.EventDate, "Tr_FieldEventDate", Workspace.FieldKind.Date, 80),
            ];

            Workspace.FormField Create(Workspace.CoreField field, string titleKey, Workspace.FieldKind kind, double width) {
                byField.TryGetValue(field, out var state);
                return new Workspace.FormField(
                    field.ToString(),
                    state?.Title ?? Localization.UiStrings.Get(titleKey),
                    kind,
                    field,
                    width,
                    state?.IsEnabled ?? true,
                    state?.InitialValue,
                    state?.Options
                );
            }
        }

        private static IEnumerable<Workspace.FormField> CreateExtensionFields(
            IReadOnlyList<Workspace.ExtensionField> extensions,
            Workspace.FieldPlacement placement
        ) => extensions.Where(field => field.Placement == placement
                && (placement != Workspace.FieldPlacement.Below || field.Anchor.HasValue))
            .Select(field => new Workspace.FormField(
                field.Id,
                field.Title,
                field.Kind,
                Width: field.Width,
                IsEnabled: field.IsEnabled,
                InitialValue: field.InitialValue,
                Options: field.Options,
                Anchor: placement == Workspace.FieldPlacement.Below ? field.Anchor : null
            ));

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