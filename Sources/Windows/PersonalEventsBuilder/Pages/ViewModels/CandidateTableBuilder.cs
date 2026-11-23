using System.Linq;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Pages.ViewModels {
    public sealed record CandidateDisplayCell(string Text, double Width);

    public sealed record CandidateDisplayRow(string Id, CandidateDisplayCell[] Cells);

    public sealed record CandidateTableLayout(CandidateDisplayCell[] Headings, CandidateDisplayRow[] Rows);

    internal static class CandidateTableBuilder {
        private const double ColumnWidth = 152;

        internal static CandidateTableLayout Build(Workspace.CandidateTable table) {
            var headings = table.Columns.Select(column => new CandidateDisplayCell(column.Title, ColumnWidth)).ToArray();
            var rows = table.Rows.Select(row => new CandidateDisplayRow(
                row.Id,
                table.Columns.Select(column => new CandidateDisplayCell(
                    row.Cells.GetValueOrDefault(column.Id, ""),
                    ColumnWidth
                )).ToArray()
            )).ToArray();

            return new CandidateTableLayout(headings, rows);
        }
    }
}