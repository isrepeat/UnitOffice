using System;

namespace PersonalEventsBuilder.Workspace {
    public abstract class WorkspaceSectionBase : Helpers.ObservableObjectBase {
        internal int StartRow { get; set; }
        internal abstract int RowCount { get; }
        internal virtual int ColumnCount => 0;
        public double Height => this.RowCount * WorkspaceGrid.RowHeight;
        public bool IsVisible {
            get => this.isVisible;
            set {
                if (this.isVisible == value) {
                    return;
                }

                this.isVisible = value;
                this.NotifyPropertyChanged();
                this.ExtentChanged?.Invoke();
            }
        }

        private bool isVisible = true;

        internal event Action? ExtentChanged;

        internal virtual bool IsInsideRowSpan(int row, int column) => false;
        internal virtual bool IsInsideColumnSpan(int row, int column) => false;
        internal virtual void UpdateViewport(GridArea viewport) { }

        protected void NotifyExtentChanged() {
            this.NotifyPropertyChanged(nameof(Height));
            this.ExtentChanged?.Invoke();
        }
    }
}