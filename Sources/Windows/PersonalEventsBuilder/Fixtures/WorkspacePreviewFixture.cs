using CandidatesSectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.CandidatesSectionViewModel;
using EventFormSectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.EventFormSectionViewModel;
using EventFormGroup = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.EventFormGroup;
using EventFormLayout = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.EventFormLayout;
using FormExtensionPlacement = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.FormExtensionPlacement;
using SummarySectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.SummarySectionViewModel;
using WorkspaceLayout = PersonalEventsBuilder.Workspace.WorkspaceLayout;
using GridArea = PersonalEventsBuilder.Workspace.GridArea;
using LabelPlacement = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.LabelPlacement;

namespace PersonalEventsBuilder.Fixtures {
    internal static class WorkspacePreviewFixture {
        internal static WorkspaceLayout Create() {
            var person = Localization.UiStrings.Get("Tr_WorkspacePerson");
            var rank = Localization.UiStrings.Get("Tr_WorkspaceRank");
            var documentNumber = Localization.UiStrings.Get("Tr_WorkspaceDocumentNumber");
            var documentDate = Localization.UiStrings.Get("Tr_WorkspaceDocumentDate");
            var primaryGroup = new EventFormGroup([
                new("person", new GridArea(1, 0, ColumnSpan: 3), person, LabelPlacement: LabelPlacement.Above),
                new("rank", new GridArea(1, 3, ColumnSpan: 2), rank, LabelPlacement: LabelPlacement.Above),
                new("document-number", new GridArea(1, 5, ColumnSpan: 4), documentNumber, LabelPlacement: LabelPlacement.Above),
            ]);
            var eventFormLayout = new EventFormLayout(
                primaryGroup,
                2,
                new(new EventFormGroup([
                    new("destination", new GridArea(1, 0, ColumnSpan: 2), Localization.UiStrings.Get("Tr_WorkspaceDestination"), LabelPlacement: LabelPlacement.Above),
                ]), FormExtensionPlacement.Left),
                new(new EventFormGroup([
                    new("departure-date", new GridArea(1, 0, ColumnSpan: 2), Localization.UiStrings.Get("Tr_WorkspaceDepartureDate"), LabelPlacement: LabelPlacement.Above),
                ]), FormExtensionPlacement.Right),
                new(new EventFormGroup([
                    new("notes", new GridArea(1, 0, ColumnSpan: 4), Localization.UiStrings.Get("Tr_WorkspaceNotes"), LabelPlacement: LabelPlacement.Above),
                ]), FormExtensionPlacement.BelowField, "document-number")
            );
            var eventFormSectionViewModel = new EventFormSectionViewModel(eventFormLayout);

            var summarySectionViewModel = new SummarySectionViewModel(Localization.UiStrings.Get("Tr_WorkspaceSummaryPreview"));
            var documentArea = eventFormLayout.GetFieldArea("document-number");
            var candidatesSectionViewModel = new CandidatesSectionViewModel(
                [
                    new(person, eventFormLayout.GetFieldArea("person")),
                    new(rank, eventFormLayout.GetFieldArea("rank")),
                    new(documentNumber, documentArea.Column, 2),
                    new(documentDate, documentArea.Column + 2, 2),
                ],
                [
                    new([Localization.UiStrings.Get("Tr_WorkspaceCandidateOne"), Localization.UiStrings.Get("Tr_WorkspaceRankOne"), "123/26", "20.11.2026"]),
                    new([Localization.UiStrings.Get("Tr_WorkspaceCandidateTwo"), Localization.UiStrings.Get("Tr_WorkspaceRankTwo"), "124/26", "21.11.2026"]),
                ]
            );

            candidatesSectionViewModel.CandidateConfirmed += candidateRow => summarySectionViewModel.Text = candidateRow.Cells[0];

            return new WorkspaceLayout(eventFormSectionViewModel, summarySectionViewModel, candidatesSectionViewModel);
        }
    }
}