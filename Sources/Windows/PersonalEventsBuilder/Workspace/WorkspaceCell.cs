namespace PersonalEventsBuilder.Workspace {
    internal enum CellTextStyle {
        Value,
        Heading,
    }

    public sealed class WorkspaceCell {
        internal GridArea Area { get; }
        internal string Text { get; }
        internal CellTextStyle TextStyle { get; }

        internal WorkspaceCell(GridArea area, string text, CellTextStyle textStyle = CellTextStyle.Value) {
            this.Area = area;
            this.Text = text;
            this.TextStyle = textStyle;
        }
    }
}