using System.Linq;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Views.EventFormParts {
    public sealed class FormColumn {
        public int Start { get; }
        public int Span { get; }
        public int? PrimaryIndex { get; }

        public FormColumn(int start, int span, int? primaryIndex) {
            this.Start = start;
            this.Span = span;
            this.PrimaryIndex = primaryIndex;
        }
    }

    public sealed class EventLayout {
        public int ColumnCount { get; }

        public IReadOnlyList<FormColumn> MainColumns { get; }

        public IReadOnlyList<PlacedFormField> Fields { get; }

        internal EventLayout(IReadOnlyList<PlacedFormField> fields, IReadOnlyList<FormColumn> mainColumns) {
            this.Fields = fields;
            this.MainColumns = mainColumns;
            this.ColumnCount = fields.Select(field => field.Bounds.Right)
                .Concat(mainColumns.Select(column => column.Start + column.Span)).DefaultIfEmpty().Max();
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