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

            CoreFieldState[] coreFields = modeId switch {
                BusinessTrip => [
                    new(CoreField.DocumentType, InitialValue: new(Localization.UiStrings.Get("Tr_DemoOrder")), Options: new[] { Localization.UiStrings.Get("Tr_DemoOrder"), Localization.UiStrings.Get("Tr_DemoReport") }),
                    new(CoreField.IncomingNumber, IsEnabled: false),
                ],
                Treatment => [
                    new(CoreField.Reporter, Title: Localization.UiStrings.Get("Tr_FieldReporterPerson")),
                    new(CoreField.EventDate, Title: Localization.UiStrings.Get("Tr_FieldTreatmentDepartureDate")),
                ],
                _ => [],
            };
            ExtensionField[] extensions = modeId switch {
                Treatment => [
                    new("hospital-short-name", Localization.UiStrings.Get("Tr_FieldHospitalShortName"), FieldKind.Text, FieldPlacement.Left, Width: 160),
                    new("hospital", Localization.UiStrings.Get("Tr_FieldHospital"), FieldKind.Text, FieldPlacement.Left),
                    new("tax-number", Localization.UiStrings.Get("Tr_FieldTaxNumber"), FieldKind.Text, FieldPlacement.Inline),
                    new("role-code", Localization.UiStrings.Get("Tr_FieldRoleCode"), FieldKind.Text, FieldPlacement.Inline),
                    new("role", Localization.UiStrings.Get("Tr_FieldRole"), FieldKind.Text, FieldPlacement.Inline),
                    new("reporter-rank", Localization.UiStrings.Get("Tr_FieldReporterRank"), FieldKind.Text, FieldPlacement.Inline),
                    new("reporter-role-code", Localization.UiStrings.Get("Tr_FieldReporterRoleCode"), FieldKind.Text, FieldPlacement.Inline, Width: 140),
                    new("document-date", Localization.UiStrings.Get("Tr_FieldDocumentDate"), FieldKind.Date, FieldPlacement.Inline),
                ],
                BusinessTrip => [
                    new("destination", Localization.UiStrings.Get("Tr_FieldDestination"), FieldKind.Text, FieldPlacement.Left),
                    new("host-unit", Localization.UiStrings.Get("Tr_FieldHostUnit"), FieldKind.Text, FieldPlacement.Left),
                    new("contact", Localization.UiStrings.Get("Tr_FieldTripContact"), FieldKind.Text, FieldPlacement.Left),
                    new("return-date", Localization.UiStrings.Get("Tr_FieldReturnDate"), FieldKind.Date, FieldPlacement.Right),
                    new("departure-date", Localization.UiStrings.Get("Tr_FieldDepartureDate"), FieldKind.Date, FieldPlacement.Right),
                    new("purpose", Localization.UiStrings.Get("Tr_FieldTripPurpose"), FieldKind.Text, FieldPlacement.Below, CoreField.DocumentNumber),
                ],
                ExtendLeave => [
                    new("leave-place", Localization.UiStrings.Get("Tr_FieldDestination"), FieldKind.Text, FieldPlacement.Left),
                    new("contact", Localization.UiStrings.Get("Tr_FieldTripContact"), FieldKind.Text, FieldPlacement.Left),
                    new("previous-return", Localization.UiStrings.Get("Tr_FieldPreviousReturnDate"), FieldKind.Date, FieldPlacement.Right),
                    new("new-return", Localization.UiStrings.Get("Tr_FieldReturnDate"), FieldKind.Date, FieldPlacement.Right),
                    new("extra-days", Localization.UiStrings.Get("Tr_FieldExtraDays"), FieldKind.Text, FieldPlacement.Right, InitialValue: new("3")),
                    new("basis", Localization.UiStrings.Get("Tr_FieldExtensionBasis"), FieldKind.Text, FieldPlacement.Below, CoreField.DocumentNumber),
                    new("basis-date", Localization.UiStrings.Get("Tr_FieldDocumentDate"), FieldKind.Date, FieldPlacement.Below, CoreField.DocumentNumber),
                ],
                _ => [],
            };

            WorkspaceAction[] actions = [
                new(WorkspaceActions.LoadDatabase, Localization.UiStrings.Get("Tr_LoadDatabase")),
                new(WorkspaceActions.ReloadConfiguration, Localization.UiStrings.Get("Tr_ReloadConfiguration")),
                new(WorkspaceActions.SaveEvent, Localization.UiStrings.Get("Tr_SaveEvent"), IsEnabled: modeId != BusinessTrip),
            ];

            CandidateColumn[] columns = modeId == BusinessTrip
                ? [
                    new("person", Localization.UiStrings.Get("Tr_FieldPerson"), CoreField.Person),
                    new("rank", Localization.UiStrings.Get("Tr_FieldRank"), CoreField.Rank),
                    new("role", Localization.UiStrings.Get("Tr_FieldRole")),
                ]
                : [
                    new("person", Localization.UiStrings.Get("Tr_FieldPerson"), CoreField.Person),
                    new("rank", Localization.UiStrings.Get("Tr_FieldRank"), CoreField.Rank),
                    new("role", Localization.UiStrings.Get("Tr_FieldRole"), TargetExtensionId: "role"),
                    new("document", Localization.UiStrings.Get("Tr_FieldDocument"), CoreField.DocumentType),
                    new("document-number", Localization.UiStrings.Get("Tr_FieldDocumentNumber"), CoreField.DocumentNumber),
                    new("document-date", Localization.UiStrings.Get("Tr_FieldDocumentDate"), TargetExtensionId: "document-date"),
                ];
            CandidateRow[] rows = modeId == BusinessTrip
                ? CreateDemoCandidates()
                : [];

            string[]? centerFieldOrder = modeId == Treatment ? [
                nameof(CoreField.Rank),
                nameof(CoreField.Person),
                "tax-number",
                "role-code",
                "role",
                "reporter-rank",
                nameof(CoreField.Reporter),
                "reporter-role-code",
                nameof(CoreField.IncomingNumber),
                nameof(CoreField.IncomingDate),
                nameof(CoreField.DocumentType),
                nameof(CoreField.DocumentNumber),
                "document-date",
                nameof(CoreField.EventDate),
            ] : null;

            return new ModeWorkspace(preview, coreFields, extensions, actions, new CandidateTable(columns, rows), centerFieldOrder);
        }

        public IReadOnlyDictionary<CoreField, FieldValue> ApplyCandidate(string modeId, string candidateId, FormValues values) {
            var candidate = modeId == BusinessTrip
                ? Array.Find(CreateDemoCandidates(), row => row.Id == candidateId)
                : null;
            return candidate is not null
                ? new Dictionary<CoreField, FieldValue> {
                    [CoreField.Person] = new(candidate.Cells["person"]),
                    [CoreField.Rank] = new(candidate.Cells["rank"]),
                }
                : new Dictionary<CoreField, FieldValue>();
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

        public string ExecuteAction(string? modeId, string actionId, FormValues values) => Localization.UiStrings.Get("Tr_ActionPending");
    }
}