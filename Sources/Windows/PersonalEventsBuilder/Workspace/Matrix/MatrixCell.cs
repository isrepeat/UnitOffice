namespace PersonalEventsBuilder.Workspace.Matrix {
    internal readonly record struct MatrixAddress(int Row, int Column);

    internal sealed record MatrixCell(MatrixAddress Address, string Label, string Text = "");
}