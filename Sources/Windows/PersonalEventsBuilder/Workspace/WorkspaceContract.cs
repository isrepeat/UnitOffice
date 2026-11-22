using System;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Workspace {
    public enum ModeCategory {
        Arrival,
        Departure,
        StatusChange,
    }

    public enum CoreField {
        Person,
        Reporter,
        Rank,
        IncomingNumber,
        IncomingDate,
        DocumentType,
        DocumentNumber,
        EventDate,
    }

    public enum FieldKind {
        Text,
        Date,
        Choice,
        CheckBox,
    }

    public enum FieldPlacement {
        Left,
        Right,
        Below,
    }

    public sealed record EventMode(string Id, string Title, bool IsDefault = false);

    public sealed record ModeGroup(string Title, IReadOnlyList<EventMode> Modes);

    public sealed record FieldValue(string? Text = null, DateTimeOffset? Date = null, bool? Checked = null);

    public sealed record CoreFieldState(CoreField Field, bool IsEnabled = true, FieldValue? InitialValue = null, IReadOnlyList<string>? Options = null);

    public sealed record ExtensionField(string Id, string Title, FieldKind Kind, FieldPlacement Placement,
        CoreField? Anchor = null, bool IsEnabled = true, FieldValue? InitialValue = null, IReadOnlyList<string>? Options = null);

    public sealed record WorkspaceAction(string Id, string Title, bool IsEnabled = true);

    public sealed record CandidateColumn(string Id, string Title);

    public sealed record CandidateRow(string Id, IReadOnlyDictionary<string, string> Cells);

    public sealed record CandidateTable(IReadOnlyList<CandidateColumn> Columns, IReadOnlyList<CandidateRow> Rows);

    public sealed record FormValues(IReadOnlyDictionary<CoreField, FieldValue> Core, IReadOnlyDictionary<string, FieldValue> Extensions);

    public sealed record ModeWorkspace(string Preview, IReadOnlyList<CoreFieldState> CoreFields,
        IReadOnlyList<ExtensionField> Extensions, IReadOnlyList<WorkspaceAction> Actions, CandidateTable Candidates);

    public interface IWorkspaceSource {
        IReadOnlyList<EventMode> GetModes(ModeCategory category);
        ModeWorkspace OpenMode(string modeId);
        IReadOnlyDictionary<CoreField, FieldValue> ApplyCandidate(string modeId, string candidateId, FormValues values);
        string ExecuteAction(string? modeId, string actionId, FormValues values);
    }

    internal static class WorkspaceActions {
        public const string LoadDatabase = "load-database";
        public const string ReloadConfiguration = "reload-configuration";
        public const string SaveEvent = "save-event";
    }
}