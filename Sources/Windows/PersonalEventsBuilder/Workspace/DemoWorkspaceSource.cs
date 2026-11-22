using System;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Workspace {
    internal sealed class DemoWorkspaceSource : IWorkspaceSource {
        private const string AnnualLeaveReturn = "arrival.annual-leave";
        private const string BusinessTrip = "departure.business-trip";

        public IReadOnlyList<EventMode> GetModes(ModeCategory category) => category switch {
            ModeCategory.Arrival => new EventMode[] {
                new("arrival.treatment", Localization.UiStrings.Get("Tr_ModeArrivalTreatment")),
                new("arrival.business-trip", Localization.UiStrings.Get("Tr_ModeArrivalBusinessTrip")),
                new(AnnualLeaveReturn, Localization.UiStrings.Get("Tr_ModeArrivalAnnualLeave"), IsDefault: true),
                new("arrival.family-leave", Localization.UiStrings.Get("Tr_ModeArrivalFamilyLeave")),
                new("arrival.medical-board", Localization.UiStrings.Get("Tr_ModeArrivalMedicalBoard")),
            },
            ModeCategory.Departure => new EventMode[] {
                new("departure.treatment", Localization.UiStrings.Get("Tr_ModeDepartureTreatment")),
                new(BusinessTrip, Localization.UiStrings.Get("Tr_ModeDepartureBusinessTrip")),
                new("departure.annual-leave", Localization.UiStrings.Get("Tr_ModeDepartureAnnualLeave")),
                new("departure.family-leave", Localization.UiStrings.Get("Tr_ModeDepartureFamilyLeave")),
                new("departure.medical-board", Localization.UiStrings.Get("Tr_ModeDepartureMedicalBoard")),
            },
            ModeCategory.StatusChange => new EventMode[] {
                new("status.treatment-to-leave", Localization.UiStrings.Get("Tr_ModeStatusTreatmentToLeave")),
                new("status.leave-to-treatment", Localization.UiStrings.Get("Tr_ModeStatusLeaveToTreatment")),
                new("status.treatment-location", Localization.UiStrings.Get("Tr_ModeStatusTreatmentLocation")),
                new("status.extend-leave", Localization.UiStrings.Get("Tr_ModeStatusExtendLeave")),
                new("status.other", Localization.UiStrings.Get("Tr_ModeStatusOther")),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        public ModeWorkspace OpenMode(string modeId) {
            var preview = modeId == AnnualLeaveReturn
                ? Localization.UiStrings.Get("Tr_DemoAnnualLeavePreview")
                : Localization.UiStrings.Get("Tr_PreviewPending");

            CoreFieldState[] coreFields = modeId == BusinessTrip
                ? [new(CoreField.DocumentType, InitialValue: new(Localization.UiStrings.Get("Tr_DemoOrder")), Options: new[] { Localization.UiStrings.Get("Tr_DemoOrder"), Localization.UiStrings.Get("Tr_DemoReport") }),
                    new(CoreField.IncomingNumber, IsEnabled: false)]
                : [];
            ExtensionField[] extensions = modeId == BusinessTrip
                ? [new("destination", Localization.UiStrings.Get("Tr_FieldDestination"), FieldKind.Text, FieldPlacement.Left),
                    new("return-date", Localization.UiStrings.Get("Tr_FieldReturnDate"), FieldKind.Date, FieldPlacement.Right),
                    new("purpose", Localization.UiStrings.Get("Tr_FieldTripPurpose"), FieldKind.Text, FieldPlacement.Below, CoreField.DocumentNumber)]
                : [];

            WorkspaceAction[] actions = [
                new(WorkspaceActions.LoadDatabase, Localization.UiStrings.Get("Tr_LoadDatabase")),
                new(WorkspaceActions.ReloadConfiguration, Localization.UiStrings.Get("Tr_ReloadConfiguration")),
                new(WorkspaceActions.SaveEvent, Localization.UiStrings.Get("Tr_SaveEvent"), IsEnabled: modeId != BusinessTrip),
            ];

            CandidateColumn[] columns = modeId == BusinessTrip
                ? [new("person", Localization.UiStrings.Get("Tr_FieldPerson")), new("rank", Localization.UiStrings.Get("Tr_FieldRank")), new("role", Localization.UiStrings.Get("Tr_FieldRole"))]
                : [new("person", Localization.UiStrings.Get("Tr_FieldPerson")), new("rank", Localization.UiStrings.Get("Tr_FieldRank")), new("role", Localization.UiStrings.Get("Tr_FieldRole")),
                    new("document", Localization.UiStrings.Get("Tr_FieldDocument")), new("document-number", Localization.UiStrings.Get("Tr_FieldDocumentNumber")), new("document-date", Localization.UiStrings.Get("Tr_FieldDocumentDate"))];
            CandidateRow[] rows = modeId == BusinessTrip
                ? [new("demo-person", new Dictionary<string, string> {
                    ["person"] = Localization.UiStrings.Get("Tr_DemoPerson"), ["rank"] = Localization.UiStrings.Get("Tr_DemoRank"), ["role"] = Localization.UiStrings.Get("Tr_DemoRole"),
                })]
                : [];

            return new ModeWorkspace(preview, coreFields, extensions, actions, new CandidateTable(columns, rows));
        }

        public IReadOnlyDictionary<CoreField, FieldValue> ApplyCandidate(string modeId, string candidateId, FormValues values) {
            return modeId == BusinessTrip && candidateId == "demo-person"
                ? new Dictionary<CoreField, FieldValue> {
                    [CoreField.Person] = new(Localization.UiStrings.Get("Tr_DemoPerson")),
                    [CoreField.Rank] = new(Localization.UiStrings.Get("Tr_DemoRank")),
                }
                : new Dictionary<CoreField, FieldValue>();
        }

        public string ExecuteAction(string? modeId, string actionId, FormValues values) => Localization.UiStrings.Get("Tr_ActionPending");
    }
}