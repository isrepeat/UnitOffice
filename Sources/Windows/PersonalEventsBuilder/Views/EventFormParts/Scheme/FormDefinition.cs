using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.EventFormParts.Scheme {
    [Flags]
    public enum FormFeature {
        None = 0,
        Document = 1,
        Treatment = 2
    }

    public enum FormPlacement {
        Primary,
        Absolute,
        Left,
        Right,
        BelowField
    }

    public sealed class FormModeDefinition {
        public string Mode { get; set; } = "";
        public FormFeature Features { get; set; }
    }

    [ContentProperty(Name = nameof(Fields))]
    public sealed class FormGroup : DependencyObject {
        public int Row { get; set; }

        public int Column { get; set; }

        public string LabelKey { get; set; } = "";

        public string AnchorFieldId { get; set; } = "";

        public FormFeature Feature { get; set; }

        public Orientation Orientation { get; set; } = Orientation.Horizontal;

        public FormPlacement Placement { get; set; }

        public IList<FormField> Fields { get; } = new List<FormField>();
    }

    public sealed class FormField : DependencyObject {
        public string? Value {
            get => (string?)this.GetValue(ValueProperty);
            set => this.SetValue(ValueProperty, value);
        }
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(FormField), new PropertyMetadata(null));

        public int Width { get; set; } = 1;

        public int Height { get; set; } = 2;

        public string Id { get; set; } = "";
    }

    [ContentProperty(Name = nameof(Groups))]
    public class FormDefinition : DependencyObject {
        public IList<FormGroup> Groups { get; } = new List<FormGroup>();
        public IList<FormModeDefinition> Modes { get; } = new List<FormModeDefinition>();

        internal static EventLayout CreateLayout(IReadOnlyList<FormGroup> groups) {
            if (groups.Count == 0) {
                return new EventLayout(Array.Empty<PlacedFormField>(), Array.Empty<FormColumn>());
            }

            var primary = groups.Single(group => group.Placement == FormPlacement.Primary);
            var layout = new List<PlacedFormField>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var anchors = new Dictionary<string, UDK.WinUI.Grid.GridArea>(StringComparer.Ordinal);
            var nextRows = new Dictionary<string, int>(StringComparer.Ordinal);
            int left = primary.Column;
            int right = left;
            // Сначала резервируем заданные позиции, затем размещаем нижние расширения в порядке XAML.
            foreach (var group in groups.Where(group => group != primary).OrderBy(group => group.Placement == FormPlacement.BelowField).Prepend(primary)) {
                if (group.Fields.Count == 0 || group.Fields.Any(field => field.Width <= 0 || field.Height <= 0)) {
                    throw new InvalidOperationException("A group requires fields with positive dimensions.");
                }
                bool horizontal = group.Orientation == Orientation.Horizontal;
                int width = horizontal ? group.Fields.Sum(field => field.Width) : group.Fields.Max(field => field.Width);
                int column = group.Column;
                int row = group.Row;
                switch (group.Placement) {
                    case FormPlacement.Primary:
                        right = checked(column + width);
                        break;
                    case FormPlacement.Left:
                        left = checked(left - width);
                        column = left;
                        row = primary.Row;
                        break;
                    case FormPlacement.Right:
                        column = right;
                        right = checked(right + width);
                        row = primary.Row;
                        break;
                    case FormPlacement.BelowField:
                        if (!anchors.TryGetValue(group.AnchorFieldId, out var anchor)) {
                            throw new InvalidOperationException($"Unknown primary field '{group.AnchorFieldId}'.");
                        }
                        column = anchor.Column;
                        row = nextRows.GetValueOrDefault(group.AnchorFieldId, anchor.Bottom);
                        int height = horizontal ? group.Fields.Max(field => field.Height) : group.Fields.Sum(field => field.Height);
                        row = FindAvailableRow(layout, column, row, width, height);
                        nextRows[group.AnchorFieldId] = checked(row + height);
                        break;
                }

                foreach (var field in group.Fields) {
                    var bounds = new UDK.WinUI.Grid.GridArea(column, row, field.Width, field.Height);
                    if (string.IsNullOrWhiteSpace(field.Id) || !ids.Add(field.Id)) {
                        throw new InvalidOperationException($"Missing or duplicate field ID '{field.Id}'.");
                    }
                    if (column < 0 || row < 0 || layout.Any(item => !bounds.Intersect(item.Bounds).IsEmpty)) {
                        throw new InvalidOperationException($"Invalid placement for field '{field.Id}'.");
                    }
                    layout.Add(new PlacedFormField(group, field, bounds));
                    if (group == primary) {
                        anchors.Add(field.Id, bounds);
                    }
                    if (horizontal) {
                        column = bounds.Right;
                    }
                    else {
                        row = bounds.Bottom;
                    }
                }
            }
            // Use the widest field at each horizontal position and exclude lower extensions.
            var mainColumns = layout.Where(item => item.Group.Placement != FormPlacement.BelowField)
                .GroupBy(item => item.Bounds.Column)
                .OrderBy(group => group.Key)
                .Select(group => group.OrderByDescending(item => item.Bounds.Width).First())
                .Select(item => new FormColumn(item.Bounds.Column, item.Bounds.Width,
                    item.Group == primary ? primary.Fields.IndexOf(item.Field) : null))
                .ToArray();
            return new EventLayout(layout.AsReadOnly(), Array.AsReadOnly(mainColumns));
        }

        private static int FindAvailableRow(List<PlacedFormField> layout, int column, int row, int width, int height) {
            while (true) {
                var area = new UDK.WinUI.Grid.GridArea(column, row, width, height);
                int nextRow = row;
                foreach (var placedFormField in layout) {
                    if (!area.Intersect(placedFormField.Bounds).IsEmpty) {
                        nextRow = Math.Max(nextRow, placedFormField.Bounds.Bottom);
                    }
                }
                if (nextRow == row) {
                    return row;
                }
                row = nextRow;
            }
        }
    }
}