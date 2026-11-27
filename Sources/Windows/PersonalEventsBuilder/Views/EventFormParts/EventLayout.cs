using System.Collections.Generic;

namespace PersonalEventsBuilder.Views.EventFormParts {
    public sealed record FormColumn(int Start, int Span, int? PrimaryIndex);

    public sealed class EventLayout {
        public IReadOnlyList<FormColumn> MainColumns { get; }

        public IReadOnlyList<PlacedFormField> Fields { get; }

        internal EventLayout(IReadOnlyList<PlacedFormField> fields, IReadOnlyList<FormColumn> mainColumns) {
            this.Fields = fields;
            this.MainColumns = mainColumns;
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