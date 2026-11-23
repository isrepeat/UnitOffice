using System;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Workspace {
    internal sealed class DemoWorkspaceSource : IWorkspaceSource {
        private const string AnnualLeaveReturn = "arrival.annual-leave";
        private const string BusinessTrip = "departure.business-trip";
        private const string Treatment = "departure.treatment";
        private const string ExtendLeave = "status.extend-leave";

        public IReadOnlyList<EventMode> GetModes(ModeCategory category) => category switch {
            ModeCategory.Arrival => new EventMode[] {
                new("arrival.treatment", Localization.UiStrings.Get("Tr_ModeArrivalTreatment")),
                new("arrival.business-trip", Localization.UiStrings.Get("Tr_ModeArrivalBusinessTrip")),
                new(AnnualLeaveReturn, Localization.UiStrings.Get("Tr_ModeArrivalAnnualLeave"), IsDefault: true),
                new("arrival.family-leave", Localization.UiStrings.Get("Tr_ModeArrivalFamilyLeave")),
                new("arrival.medical-board", Localization.UiStrings.Get("Tr_ModeArrivalMedicalBoard")),
            },
            ModeCategory.Departure => new EventMode[] {
                new(Treatment, Localization.UiStrings.Get("Tr_ModeDepartureTreatment")),
                new(BusinessTrip, Localization.UiStrings.Get("Tr_ModeDepartureBusinessTrip")),
                new("departure.annual-leave", Localization.UiStrings.Get("Tr_ModeDepartureAnnualLeave")),
                new("departure.family-leave", Localization.UiStrings.Get("Tr_ModeDepartureFamilyLeave")),
                new("departure.medical-board", Localization.UiStrings.Get("Tr_ModeDepartureMedicalBoard")),
            },
            ModeCategory.StatusChange => new EventMode[] {
                new("status.treatment-to-leave", Localization.UiStrings.Get("Tr_ModeStatusTreatmentToLeave")),
                new("status.leave-to-treatment", Localization.UiStrings.Get("Tr_ModeStatusLeaveToTreatment")),
                new("status.treatment-location", Localization.UiStrings.Get("Tr_ModeStatusTreatmentLocation")),
                new(ExtendLeave, Localization.UiStrings.Get("Tr_ModeStatusExtendLeave")),
                new("status.other", Localization.UiStrings.Get("Tr_ModeStatusOther")),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        public ModeWorkspace OpenMode(string modeId) {
            var preview = modeId == AnnualLeaveReturn
                ? Localization.UiStrings.Get("Tr_DemoAnnualLeavePreview")
                : Localization.UiStrings.Get("Tr_PreviewPending");

            WorkspaceAction[] actions = [
                new(WorkspaceActions.LoadDatabase, Localization.UiStrings.Get("Tr_LoadDatabase")),
                new(WorkspaceActions.ReloadConfiguration, Localization.UiStrings.Get("Tr_ReloadConfiguration")),
                new(WorkspaceActions.SaveEvent, Localization.UiStrings.Get("Tr_SaveEvent"), IsEnabled: modeId != BusinessTrip),
            ];

            CandidateColumn[] columns = modeId == BusinessTrip
                ? [
                    new("person", Localization.UiStrings.Get("Tr_FieldPerson")),
                    new("rank", Localization.UiStrings.Get("Tr_FieldRank")),
                    new("role", Localization.UiStrings.Get("Tr_FieldRole")),
                ]
                : [
                    new("person", Localization.UiStrings.Get("Tr_FieldPerson")),
                    new("rank", Localization.UiStrings.Get("Tr_FieldRank")),
                    new("role", Localization.UiStrings.Get("Tr_FieldRole")),
                    new("document", Localization.UiStrings.Get("Tr_FieldDocument")),
                    new("document-number", Localization.UiStrings.Get("Tr_FieldDocumentNumber")),
                    new("document-date", Localization.UiStrings.Get("Tr_FieldDocumentDate")),
                ];
            CandidateRow[] rows = modeId == BusinessTrip
                ? CreateDemoCandidates()
                : [];

            return new ModeWorkspace(preview, actions, new CandidateTable(columns, rows));
        }

        private static CandidateRow[] CreateDemoCandidates() => [
            Create("demo-person", "Tr_DemoPerson", "Tr_DemoRank", "Tr_DemoRole"),
            Create("demo-person-2", "Tr_DemoPerson2", "Tr_DemoRank2", "Tr_DemoRole2"),
            Create("demo-person-3", "Tr_DemoPerson3", "Tr_DemoRank3", "Tr_DemoRole3"),
            Create("demo-person-4", "Tr_DemoPerson4", "Tr_DemoRank", "Tr_DemoRole4"),
        ];

        private static CandidateRow Create(string id, string personKey, string rankKey, string roleKey) =>
            new(id, new Dictionary<string, string> {
                ["person"] = Localization.UiStrings.Get(personKey),
                ["rank"] = Localization.UiStrings.Get(rankKey),
                ["role"] = Localization.UiStrings.Get(roleKey),
            });

        public string ExecuteAction(string? modeId, string actionId) => Localization.UiStrings.Get("Tr_ActionPending");
    }
}