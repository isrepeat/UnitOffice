using CandidatesSectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.CandidatesSectionViewModel;
using EventFormSectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.EventFormSectionViewModel;
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
            var eventFormSectionViewModel = new EventFormSectionViewModel(
                [
                    new(new GridArea(1, 0, ColumnSpan: 3), person, LabelPlacement: LabelPlacement.Above),
                    new(new GridArea(1, 3, ColumnSpan: 2), rank, LabelPlacement: LabelPlacement.Above),
                    new(new GridArea(1, 5, ColumnSpan: 4), documentNumber, LabelPlacement: LabelPlacement.Above),
                ]
            );

            var summarySectionViewModel = new SummarySectionViewModel(Localization.UiStrings.Get("Tr_WorkspaceSummaryPreview"));
            var candidatesSectionViewModel = new CandidatesSectionViewModel(
                [new(person, 0, 3), new(rank, 3, 2), new(documentNumber, 5, 2), new(documentDate, 7, 2)],
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