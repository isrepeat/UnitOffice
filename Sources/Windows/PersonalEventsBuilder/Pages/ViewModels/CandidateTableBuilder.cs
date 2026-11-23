using System.Linq;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed record CandidateDisplayCell(string Text, double Width);

    public sealed record CandidateDisplayRow(string Id, CandidateDisplayCell[] Cells);

    public sealed record CandidateTableLayout(CandidateDisplayCell[] Headings, CandidateDisplayRow[] Rows);

    internal static class CandidateTableBuilder {
        private const double UnalignedColumnWidth = 152;

        private sealed record Slot(Workspace.CandidateColumn? Column, double Width);

        internal static CandidateTableLayout Build(Workspace.CandidateTable table, IReadOnlyList<Workspace.FormField> primaryRow) {
            var remainingColumns = table.Columns.ToList();
            var slots = new List<Slot>();
            foreach (var field in primaryRow) {
                var column = field.CoreField is { } target
                    ? remainingColumns.Find(column => column.TargetField == target)
                    : remainingColumns.Find(column => column.TargetExtensionId == field.Id);
                if (column is not null) {
                    remainingColumns.Remove(column);
                }

                slots.Add(new Slot(column, field.Width + Controls.Form.ColumnSpacing));
            }

            slots.AddRange(remainingColumns.Select(column => new Slot(column, UnalignedColumnWidth)));
            var headings = slots.Select(slot => new CandidateDisplayCell(slot.Column?.Title ?? "", slot.Width)).ToArray();
            var rows = table.Rows.Select(row => new CandidateDisplayRow(
                row.Id,
                slots.Select(slot => new CandidateDisplayCell(
                    slot.Column is { } column ? row.Cells.GetValueOrDefault(column.Id, "") : "",
                    slot.Width
                )).ToArray()
            )).ToArray();

            return new CandidateTableLayout(headings, rows);
        }
    }
}