using System.Collections.Generic;

namespace PersonalEventsBuilder.Workspace {
    public sealed class EventFormLayout {
        public EventFormLayout(
            IReadOnlyList<FormField> left,
            IReadOnlyList<FormField> primaryRow,
            IReadOnlyList<FormField> below,
            IReadOnlyList<FormField> right
        ) {
            this.Left = left;
            this.PrimaryRow = primaryRow;
            FormField[] center = [.. primaryRow, .. below];
            this.Center = center;
            this.Right = right;
        }

        public IReadOnlyList<FormField> Left { get; }
        public IReadOnlyList<FormField> PrimaryRow { get; }
        public IReadOnlyList<FormField> Center { get; }
        public IReadOnlyList<FormField> Right { get; }
    }
}