using System.Collections.Generic;

namespace PersonalEventsBuilder.Workspace {
    public enum ModeCategory {
        Arrival,
        Departure,
        StatusChange,
    }

    public sealed record EventMode(string Id, string Title, bool IsDefault = false);

    public sealed record ModeGroup(string Title, IReadOnlyList<EventMode> Modes);

    public sealed record WorkspaceAction(string Id, string Title, bool IsEnabled = true);

    public sealed record CandidateColumn(string Id, string Title);

    public sealed record CandidateRow(string Id, IReadOnlyDictionary<string, string> Cells);

    public sealed record CandidateTable(IReadOnlyList<CandidateColumn> Columns, IReadOnlyList<CandidateRow> Rows);

    public sealed record ModeWorkspace(string Preview, IReadOnlyList<WorkspaceAction> Actions, CandidateTable Candidates);

    public interface IWorkspaceSource {
        IReadOnlyList<EventMode> GetModes(ModeCategory category);
        ModeWorkspace OpenMode(string modeId);
        string ExecuteAction(string? modeId, string actionId);
    }

    internal static class WorkspaceActions {
        public const string LoadDatabase = "load-database";
        public const string ReloadConfiguration = "reload-configuration";
        public const string SaveEvent = "save-event";
    }
}