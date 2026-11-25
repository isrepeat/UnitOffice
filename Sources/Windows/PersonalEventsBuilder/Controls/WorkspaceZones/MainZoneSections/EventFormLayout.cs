using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public enum FormGroupPlacement {
        Primary,
        Left,
        Right,
        BelowField,
    }

    internal sealed record EventFormGroup(EventField[] Fields, Style? BorderStyle);
    internal sealed record FormGroupBorder(GridArea Area, Style Style);

    internal sealed record EventFormExtension(
        EventFormGroup Group,
        FormGroupPlacement Placement,
        string? AnchorFieldId = null
    );

    internal sealed class EventFormLayout {
        internal IReadOnlyList<EventField> Fields { get; }
        internal IReadOnlyList<FormGroupBorder> GroupBorders => this.groupBorders;

        private readonly Dictionary<string, EventField> fieldsById = new(StringComparer.Ordinal);
        private readonly List<FormGroupBorder> groupBorders = new();

        internal EventFormLayout(EventFormGroup primaryGroup, int startRow, int startColumn, params EventFormExtension[] extensions) {
            ArgumentOutOfRangeException.ThrowIfNegative(startRow);
            ArgumentOutOfRangeException.ThrowIfNegative(startColumn);

            var fields = new List<EventField>();
            this.AddGroup(fields, primaryGroup, row: startRow, column: startColumn);
            var primaryFields = new Dictionary<string, EventField>(this.fieldsById, StringComparer.Ordinal);
            var left = startColumn;
            var right = startColumn + GetWidth(primaryGroup);
            var nextRowByAnchor = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var eventFormExtension in extensions) {
                switch (eventFormExtension.Placement) {
                    case FormGroupPlacement.Left:
                        left -= GetWidth(eventFormExtension.Group);
                        this.AddGroup(fields, eventFormExtension.Group, row: startRow, column: left);
                        break;
                    case FormGroupPlacement.Right:
                        this.AddGroup(fields, eventFormExtension.Group, row: startRow, column: right);
                        right += GetWidth(eventFormExtension.Group);
                        break;
                    case FormGroupPlacement.BelowField:
                        if (eventFormExtension.AnchorFieldId is not { } id || !primaryFields.TryGetValue(id, out var anchorField)) {
                            throw new ArgumentException("A lower extension needs an anchor in the primary group.", nameof(extensions));
                        }

                        var anchorBottom = Math.Max(anchorField.Area.Bottom, anchorField.LabelArea?.Bottom ?? 0);
                        var row = nextRowByAnchor.GetValueOrDefault(id, anchorBottom);
                        this.AddGroup(fields, eventFormExtension.Group, row, anchorField.Area.Column);
                        nextRowByAnchor[id] = row + GetHeight(eventFormExtension.Group);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(extensions));
                }
            }

            this.Fields = fields.OrderBy(eventField => eventField.Area.Row).ThenBy(eventField => eventField.Area.Column).ToArray();
        }

        internal GridArea GetFieldArea(string fieldId) => this.fieldsById[fieldId].Area;

        private void AddGroup(List<EventField> fields, EventFormGroup eventFormGroup, int row, int column) {
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            foreach (var eventField in eventFormGroup.Fields) {
                var resolvedField = eventField with {
                    Area = eventField.Area with { Row = eventField.Area.Row + row, Column = eventField.Area.Column + column },
                };
                foreach (var area in GetAreas(eventField)) {
                    if (area.Row < 0 || area.Column < 0 || area.RowSpan <= 0 || area.ColumnSpan <= 0) {
                        throw new ArgumentException("Field and label coordinates must fit inside the group.", nameof(eventFormGroup));
                    }
                }

                foreach (var placedField in fields) {
                    if (GetAreas(placedField).Any(placedArea => GetAreas(resolvedField).Any(area => Intersects(placedArea, area)))) {
                        throw new ArgumentException("Two form fields or labels occupy the same cells.", nameof(eventFormGroup));
                    }
                }

                this.fieldsById.Add(resolvedField.Id, resolvedField);
                fields.Add(resolvedField);
            }

            if (eventFormGroup.Fields.Length > 0 && eventFormGroup.BorderStyle is { } borderStyle) {
                this.groupBorders.Add(new FormGroupBorder(
                    new GridArea(row, column, GetHeight(eventFormGroup), GetWidth(eventFormGroup)),
                    borderStyle
                ));
            }
        }

        private static int GetWidth(EventFormGroup eventFormGroup) =>
            eventFormGroup.Fields.SelectMany(GetAreas).Select(area => area.Right).DefaultIfEmpty().Max();

        private static int GetHeight(EventFormGroup eventFormGroup) =>
            eventFormGroup.Fields.SelectMany(GetAreas).Select(area => area.Bottom).DefaultIfEmpty().Max();

        private static IEnumerable<GridArea> GetAreas(EventField eventField) {
            yield return eventField.Area;
            if (eventField.LabelArea is { } labelArea) {
                yield return labelArea;
            }
        }

        private static bool Intersects(GridArea first, GridArea second) =>
            first.Row < second.Bottom && second.Row < first.Bottom
                && first.Column < second.Right && second.Column < first.Right;
    }
}