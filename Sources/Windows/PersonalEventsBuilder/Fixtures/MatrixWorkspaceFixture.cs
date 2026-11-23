using MatrixAddress = PersonalEventsBuilder.Workspace.Matrix.MatrixAddress;
using MatrixZone = PersonalEventsBuilder.Controls.WorkspaceZones.MainZones.MatrixZone;

namespace PersonalEventsBuilder.Fixtures {
    internal static class MatrixWorkspaceFixture {
        internal static void ApplyTo(MatrixZone matrixZone) {
            matrixZone.LoadMatrix(10_000, 100, [
                new(new MatrixAddress(1, 1), Localization.UiStrings.Get("Tr_MatrixPerson")),
                new(new MatrixAddress(1, 5), Localization.UiStrings.Get("Tr_MatrixRank")),
                new(new MatrixAddress(8, 5), Localization.UiStrings.Get("Tr_MatrixDocumentNumber")),
            ]);
        }
    }
}