using System;
using System.Linq;

using Windows.System;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;
using INavigableZone = PersonalEventsBuilder.Workspace.INavigableZone;
using WorkspaceSectionBase = PersonalEventsBuilder.Workspace.WorkspaceSectionBase;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    internal sealed record EventField(GridArea Area, string Label, string Text = "") {
        internal string DisplayText => this.Text.Length == 0 ? this.Label : this.Text;
    }

    public sealed class EventFormSectionViewModel : WorkspaceSectionBase, INavigableZone {
        internal override int RowCount { get; }
        internal override int ColumnCount { get; }
        internal int? EditingIndex { get; private set; }
        internal string EditorText { get; set; } = "";
        int INavigableZone.StartRow => this.StartRow;
        int INavigableZone.RowCount => this.RowCount;
        int INavigableZone.TargetCount => this.eventFields.Length;
        bool INavigableZone.CanEdit => true;
        bool INavigableZone.IsEditing => this.EditingIndex.HasValue;
        internal System.Collections.Generic.IReadOnlyList<EventField> Fields => this.eventFields;

        private readonly EventField[] eventFields;

        internal EventFormSectionViewModel(EventField[] eventFields) {
            this.eventFields = (EventField[])eventFields.Clone();
            this.RowCount = Math.Max(1, this.eventFields.Select(eventField => eventField.Area.Bottom).DefaultIfEmpty().Max());
            this.ColumnCount = this.eventFields.Select(eventField => eventField.Area.Right).DefaultIfEmpty().Max();
        }

        internal event Action? EditingChanged;
        internal event Action<int>? FieldChanged;

        GridArea INavigableZone.GetTargetArea(int index) => this.eventFields[index].Area;
        int? INavigableZone.FindTargetAt(int row, int column) => this.FindTargetAt(row, column);
        void INavigableZone.ActivateTarget(int index) {
            if (this.EditingIndex.HasValue) {
                return;
            }

            this.EditorText = this.eventFields[index].Text;
            this.EditingIndex = index;
            this.EditingChanged?.Invoke();
        }

        bool INavigableZone.TryHandleEditorKey(VirtualKey key) {
            if (!this.EditingIndex.HasValue || key is not (VirtualKey.Enter or VirtualKey.Escape)) {
                return false;
            }

            this.EndEditing(commit: key == VirtualKey.Enter);
            return true;
        }

        void INavigableZone.LeaveTarget() => this.EndEditing(commit: true);

        internal void EndEditing(bool commit) {
            if (this.EditingIndex is not { } index) {
                return;
            }

            this.EditingIndex = null;
            if (commit) {
                this.eventFields[index] = this.eventFields[index] with { Text = this.EditorText };
                this.FieldChanged?.Invoke(index);
            }

            this.EditingChanged?.Invoke();
        }

        internal override bool IsInsideRowSpan(int row, int column) {
            var above = this.FindTargetAt(row - 1, column);
            return above.HasValue && above == this.FindTargetAt(row, column);
        }

        internal override bool IsInsideColumnSpan(int row, int column) {
            var left = this.FindTargetAt(row, column - 1);
            return left.HasValue && left == this.FindTargetAt(row, column);
        }

        private int? FindTargetAt(int row, int column) {
            for (var index = 0; index < this.eventFields.Length; index++) {
                if (this.eventFields[index].Area.Contains(row, column)) {
                    return index;
                }
            }

            return null;
        }
    }
}