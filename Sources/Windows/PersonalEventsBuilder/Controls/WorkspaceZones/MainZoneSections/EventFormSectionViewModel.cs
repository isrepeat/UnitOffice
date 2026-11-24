using System;

using Windows.System;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;
using INavigableZone = PersonalEventsBuilder.Workspace.INavigableZone;
using WorkspaceSectionBase = PersonalEventsBuilder.Workspace.WorkspaceSectionBase;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    internal enum LabelPlacement {
        Placeholder,
        Left,
        Right,
        Below,
        Above,
    }

    internal sealed record EventField(GridArea Area, string Label, string Text = "", LabelPlacement LabelPlacement = LabelPlacement.Placeholder) {
        internal string Placeholder => this.LabelPlacement == LabelPlacement.Placeholder ? this.Label : "";
        internal string DisplayText => this.Text.Length == 0 ? this.Placeholder : this.Text;
        internal GridArea? LabelArea => this.LabelPlacement switch {
            LabelPlacement.Left => new GridArea(this.Area.Row, this.Area.Column - 1, RowSpan: this.Area.RowSpan),
            LabelPlacement.Right => new GridArea(this.Area.Row, this.Area.Right, RowSpan: this.Area.RowSpan),
            LabelPlacement.Below => new GridArea(this.Area.Bottom, this.Area.Column, ColumnSpan: this.Area.ColumnSpan),
            LabelPlacement.Above => new GridArea(this.Area.Row - 1, this.Area.Column, ColumnSpan: this.Area.ColumnSpan),
            _ => null,
        };
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
            this.RowCount = 1;
            foreach (var eventField in this.eventFields) {
                this.RowCount = Math.Max(this.RowCount, eventField.Area.Bottom);
                this.ColumnCount = Math.Max(this.ColumnCount, eventField.Area.Right);
                if (eventField.LabelArea is { } labelArea) {
                    if (labelArea.Row < 0 || labelArea.Column < 0) {
                        throw new ArgumentException("A field label must fit inside the form grid.", nameof(eventFields));
                    }

                    this.RowCount = Math.Max(this.RowCount, labelArea.Bottom);
                    this.ColumnCount = Math.Max(this.ColumnCount, labelArea.Right);
                }
            }
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
            var above = this.FindDisplayArea(row - 1, column);
            return above.HasValue && above == this.FindDisplayArea(row, column);
        }

        internal override bool IsInsideColumnSpan(int row, int column) {
            var left = this.FindDisplayArea(row, column - 1);
            return left.HasValue && left == this.FindDisplayArea(row, column);
        }

        private GridArea? FindDisplayArea(int row, int column) {
            foreach (var eventField in this.eventFields) {
                if (eventField.Area.Contains(row, column)) {
                    return eventField.Area;
                }

                if (eventField.LabelArea is { } labelArea && labelArea.Contains(row, column)) {
                    return labelArea;
                }
            }

            return null;
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