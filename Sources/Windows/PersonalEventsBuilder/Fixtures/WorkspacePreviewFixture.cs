using CandidatesSectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.CandidatesSectionViewModel;
using EventFormSectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.EventFormSectionViewModel;
using EventFormLayout = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.EventFormLayout;
using SummarySectionViewModel = PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections.SummarySectionViewModel;
using WorkspaceLayout = PersonalEventsBuilder.Workspace.WorkspaceLayout;

namespace PersonalEventsBuilder.Fixtures {
    internal static class WorkspacePreviewFixture {
        internal static WorkspaceLayout Create(EventFormLayout eventFormLayout) {
            var person = Localization.UiStrings.Get("Tr_WorkspacePerson");
            var rank = Localization.UiStrings.Get("Tr_WorkspaceRank");
            var documentNumber = Localization.UiStrings.Get("Tr_WorkspaceDocumentNumber");
            var documentDate = Localization.UiStrings.Get("Tr_WorkspaceDocumentDate");
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