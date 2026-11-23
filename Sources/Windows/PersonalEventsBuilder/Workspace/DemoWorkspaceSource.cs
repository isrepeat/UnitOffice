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

            return new ModeWorkspace(preview, actions);
        }

        public string ExecuteAction(string? modeId, string actionId) => Localization.UiStrings.Get("Tr_ActionPending");
    }
}