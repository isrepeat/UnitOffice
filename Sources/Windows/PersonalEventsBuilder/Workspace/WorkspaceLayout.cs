using System;
using System.Linq;

namespace PersonalEventsBuilder.Workspace {
    public sealed class WorkspaceLayout {
        public Controls.WorkspaceZones.MainZoneSections.EventFormSectionViewModel Form { get; }
        public Controls.WorkspaceZones.MainZoneSections.SummarySectionViewModel Summary { get; }
        public Controls.WorkspaceZones.MainZoneSections.CandidatesSectionViewModel Candidates { get; }
        internal WorkspaceNavigation WorkspaceNavigation { get; private set; } = new();
        internal int ColumnCount => this.visibleSections.Select(workspaceSectionBase => workspaceSectionBase.ColumnCount).DefaultIfEmpty().Max();

        private readonly WorkspaceSectionBase[] sections;
        private WorkspaceSectionBase[] visibleSections = [];

        internal WorkspaceLayout(
            Controls.WorkspaceZones.MainZoneSections.EventFormSectionViewModel eventFormSectionViewModel,
            Controls.WorkspaceZones.MainZoneSections.SummarySectionViewModel summarySectionViewModel,
            Controls.WorkspaceZones.MainZoneSections.CandidatesSectionViewModel candidatesSectionViewModel
        ) {
            this.Form = eventFormSectionViewModel;
            this.Summary = summarySectionViewModel;
            this.Candidates = candidatesSectionViewModel;
            this.sections = [eventFormSectionViewModel, summarySectionViewModel, candidatesSectionViewModel];
            foreach (var workspaceSectionBase in this.sections) {
                workspaceSectionBase.ExtentChanged += this.OnExtentChanged;
            }

            this.UpdateVisibleSections();
        }

        internal event Action? ExtentChanged;

        internal bool IsVisible(INavigableZone navigableZone) =>
            navigableZone is WorkspaceSectionBase workspaceSectionBase && this.visibleSections.Contains(workspaceSectionBase);

        internal bool IsInsideRowSpan(int row, int column) {
            var workspaceSectionBase = this.FindSection(row - 1);
            return workspaceSectionBase?.IsInsideRowSpan(row - workspaceSectionBase.StartRow, column) == true;
        }

        internal bool IsInsideColumnSpan(int row, int column) {
            var workspaceSectionBase = this.FindSection(row);
            return workspaceSectionBase?.IsInsideColumnSpan(row - workspaceSectionBase.StartRow, column) == true;
        }

        internal void UpdateViewport(GridArea viewport) {
            foreach (var workspaceSectionBase in this.visibleSections) {
                workspaceSectionBase.UpdateViewport(viewport);
            }
        }

        private WorkspaceSectionBase? FindSection(int row) => this.visibleSections.FirstOrDefault(
            workspaceSectionBase => row >= workspaceSectionBase.StartRow && row < workspaceSectionBase.StartRow + workspaceSectionBase.RowCount
        );

        private void OnExtentChanged() {
            this.UpdateVisibleSections();
            this.ExtentChanged?.Invoke();
        }

        private void UpdateVisibleSections() {
            this.visibleSections = this.sections.Where(workspaceSectionBase => workspaceSectionBase.IsVisible).ToArray();
            var row = 0;
            foreach (var workspaceSectionBase in this.visibleSections) {
                workspaceSectionBase.StartRow = row;
                row += workspaceSectionBase.RowCount;
            }

            this.WorkspaceNavigation = new WorkspaceNavigation(this.visibleSections.OfType<INavigableZone>().ToArray());
        }
    }
}