using System.Collections.Generic;

namespace PersonalEventsBuilder.Views.EventFormParts {
    public sealed record ColumnRange(int Start, int Span);

    public sealed class EventLayout {
        public ColumnRange MainColumnRange { get; }

        public IReadOnlyList<PlacedFormField> Fields { get; }

        internal EventLayout(IReadOnlyList<PlacedFormField> fields, ColumnRange mainColumnRange) {
            this.Fields = fields;
            this.MainColumnRange = mainColumnRange;
        }
    }
    public readonly struct PlacedFormField {
        public readonly Scheme.FormField Field;
        public readonly Scheme.FormGroup Group;
        public readonly UDK.WinUI.Grid.GridArea Bounds;

        public PlacedFormField(Scheme.FormGroup formGroup, Scheme.FormField formField, UDK.WinUI.Grid.GridArea gridArea) {
            this.Group = formGroup;
            this.Field = formField;
            this.Bounds = gridArea;
        }
    }
}