using System;
using System.Collections.Generic;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;
using CellTextStyle = PersonalEventsBuilder.Workspace.CellTextStyle;
using INavigableZone = PersonalEventsBuilder.Workspace.INavigableZone;
using NavigationDirection = PersonalEventsBuilder.Workspace.NavigationDirection;
using WorkspaceSectionBase = PersonalEventsBuilder.Workspace.WorkspaceSectionBase;
using WorkspaceCell = PersonalEventsBuilder.Workspace.WorkspaceCell;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    internal sealed record CandidateColumn(string Title, int Column, int ColumnSpan);
    internal sealed record CandidateRow(string[] Cells);

    public sealed class CandidatesSectionViewModel : WorkspaceSectionBase, INavigableZone {
        public WorkspaceCell[] VisibleCells { get; private set; } = [];
        internal override int RowCount => this.candidateRows.Length + 1;
        internal override int ColumnCount { get; }
        int INavigableZone.StartRow => this.StartRow;
        int INavigableZone.RowCount => this.RowCount;
        int INavigableZone.TargetCount => this.candidateRows.Length * this.candidateColumns.Length;

        private readonly CandidateColumn[] candidateColumns;
        private readonly CandidateRow[] candidateRows;
        private GridArea previousVisibleArea;

        internal CandidatesSectionViewModel(CandidateColumn[] candidateColumns, CandidateRow[] candidateRows) {
            this.candidateColumns = candidateColumns;
            this.candidateRows = candidateRows;
            foreach (var candidateColumn in candidateColumns) {
                this.ColumnCount = Math.Max(this.ColumnCount, candidateColumn.Column + candidateColumn.ColumnSpan);
            }
        }

        internal event Action<CandidateRow>? CandidateConfirmed;

        internal override bool IsInsideColumnSpan(int row, int column) =>
            Array.Exists(
                this.candidateColumns,
                candidateColumn =>
                column > candidateColumn.Column && column < candidateColumn.Column + candidateColumn.ColumnSpan
            );

        GridArea INavigableZone.GetTargetArea(int index) => this.GetTargetArea(index);

        private GridArea GetTargetArea(int index) {
            var columnCount = this.candidateColumns.Length;
            var candidateColumn = this.candidateColumns[index % columnCount];
            return new GridArea(index / columnCount + 1, candidateColumn.Column, ColumnSpan: candidateColumn.ColumnSpan);
        }

        int? INavigableZone.FindTargetAt(int row, int column) {
            if (row <= 0 || row >= this.RowCount) {
                return null;
            }

            var columnIndex = Array.FindIndex(
                this.candidateColumns,
                candidateColumn =>
                column >= candidateColumn.Column && column < candidateColumn.Column + candidateColumn.ColumnSpan
            );

            return columnIndex < 0 ? null : (row - 1) * this.candidateColumns.Length + columnIndex;
        }

        void INavigableZone.ActivateTarget(int index) =>
            this.CandidateConfirmed?.Invoke(this.candidateRows[index / this.candidateColumns.Length]);

        int? INavigableZone.FindDirectionalTarget(GridArea origin, NavigationDirection direction) {
            var firstDataRow = this.StartRow + 1;
            var lastDataRow = this.StartRow + this.candidateRows.Length;
            var firstRow = direction switch {
                NavigationDirection.Down => Math.Max(firstDataRow, origin.Bottom),
                NavigationDirection.Up => Math.Min(lastDataRow, origin.Row - 1),
                _ => Math.Max(firstDataRow, origin.Row),
            };
            var lastRow = direction is NavigationDirection.Up or NavigationDirection.Down
                ? firstRow
                : Math.Min(lastDataRow, origin.Bottom - 1);

            if (firstRow < firstDataRow || lastRow > lastDataRow || firstRow > lastRow) {
                return null;
            }

            int? result = null;
            var best = (Distance: int.MaxValue, Alignment: int.MaxValue);
            for (var row = firstRow; row <= lastRow; row++) {
                for (var column = 0; column < this.candidateColumns.Length; column++) {
                    var index = (row - firstDataRow) * this.candidateColumns.Length + column;
                    var score = INavigableZone.Score(origin, ((INavigableZone)this).GetWorkspaceArea(index), direction);
                    if (score is { } candidate && candidate.CompareTo(best) < 0) {
                        result = index;
                        best = candidate;
                    }
                }
            }

            return result;
        }

        internal override void UpdateViewport(GridArea viewport) {
            var firstRow = Math.Max(0, viewport.Row - this.StartRow);
            var lastRow = Math.Min(this.RowCount, viewport.Bottom - this.StartRow);
            var visibleArea = new GridArea(firstRow, viewport.Column, Math.Max(0, lastRow - firstRow), viewport.ColumnSpan);
            if (this.previousVisibleArea == visibleArea) {
                return;
            }

            this.previousVisibleArea = visibleArea;
            var workspaceCells = new List<WorkspaceCell>();
            for (var row = visibleArea.Row; row < visibleArea.Bottom; row++) {
                for (var columnIndex = 0; columnIndex < this.candidateColumns.Length; columnIndex++) {
                    var candidateColumn = this.candidateColumns[columnIndex];
                    if (candidateColumn.Column >= visibleArea.Right
                        || candidateColumn.Column + candidateColumn.ColumnSpan <= visibleArea.Column) {
                        continue;
                    }

                    var area = new GridArea(row, candidateColumn.Column, ColumnSpan: candidateColumn.ColumnSpan);
                    var text = row == 0 ? candidateColumn.Title : this.candidateRows[row - 1].Cells[columnIndex];
                    workspaceCells.Add(new WorkspaceCell(area, text, row == 0 ? CellTextStyle.Heading : CellTextStyle.Value));
                }
            }

            this.VisibleCells = workspaceCells.ToArray();
            this.NotifyPropertyChanged(nameof(VisibleCells));
        }
    }
}