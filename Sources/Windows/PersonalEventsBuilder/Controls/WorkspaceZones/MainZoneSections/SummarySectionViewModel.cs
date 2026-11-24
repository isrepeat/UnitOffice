using WorkspaceSectionBase = PersonalEventsBuilder.Workspace.WorkspaceSectionBase;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public sealed class SummarySectionViewModel : WorkspaceSectionBase {
        public string Text {
            get => this.text;
            internal set {
                if (this.text == value) {
                    return;
                }

                this.text = value;
                this.NotifyPropertyChanged();
            }
        }
        internal override int RowCount => this.rowCount;

        private int rowCount = 2;
        private string text;

        internal SummarySectionViewModel(string text) {
            this.text = text;
        }

        internal void SetMeasuredRowCount(int rowCount) {
            if (this.rowCount != rowCount) {
                this.rowCount = rowCount;
                this.NotifyExtentChanged();
            }
        }

        internal override bool IsInsideRowSpan(int row, int column) => row > 0 && row < this.RowCount;
        internal override bool IsInsideColumnSpan(int row, int column) => column > 0;
    }
}