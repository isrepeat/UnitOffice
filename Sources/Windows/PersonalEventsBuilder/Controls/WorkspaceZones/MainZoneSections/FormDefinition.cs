using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public sealed class FormDefinition : DependencyObject {
        public IList<FormGroup> Groups { get; } = new List<FormGroup>();

        internal EventFormLayout CreateLayout() {
            var primaryGroups = this.Groups.Where(formGroup => formGroup.Placement == FormGroupPlacement.Primary).ToArray();
            if (primaryGroups.Length != 1) {
                throw new InvalidOperationException("The form definition requires exactly one primary group.");
            }

            var extensions = this.Groups
                .Where(formGroup => formGroup.Placement != FormGroupPlacement.Primary)
                .Select(formGroup => new EventFormExtension(formGroup.CreateGroup(), formGroup.Placement, formGroup.AnchorFieldId))
                .ToArray();
            var primary = primaryGroups[0];
            return new EventFormLayout(primary.CreateGroup(), primary.Row, primary.Column, extensions);
        }
    }

    public sealed class FormGroup : DependencyObject {
        public FormGroupPlacement Placement { get; set; }
        public string? AnchorFieldId { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public Orientation Orientation { get; set; } = Orientation.Horizontal;
        public Style? BorderStyle { get; set; }
        public IList<FormField> Fields { get; } = new List<FormField>();

        internal EventFormGroup CreateGroup() {
            var fields = new List<EventField>();
            var row = 0;
            var column = 0;
            foreach (var definition in this.Fields) {
                var field = definition.CreateField(row, column);
                fields.Add(field);
                if (this.Orientation == Orientation.Vertical) {
                    row = Math.Max(field.Area.Bottom, field.LabelArea?.Bottom ?? 0);
                }
                else {
                    column = Math.Max(field.Area.Right, field.LabelArea?.Right ?? 0);
                }
            }
            return new EventFormGroup(fields.ToArray(), this.BorderStyle);
        }
    }

    public sealed class FormField : DependencyObject {
        public string Id { get; set; } = "";
        public string LabelKey { get; set; } = "";
        public int RowSpan { get; set; } = 1;
        public int ColumnSpan { get; set; } = 1;
        public LabelPlacement LabelPlacement { get; set; }

        internal EventField CreateField(int row, int column) => new(
            this.Id,
            new GridArea(
                row + (this.LabelPlacement == LabelPlacement.Above ? 1 : 0),
                column + (this.LabelPlacement == LabelPlacement.Left ? 1 : 0),
                this.RowSpan,
                this.ColumnSpan
            ),
            Localization.UiStrings.Get(this.LabelKey),
            LabelPlacement: this.LabelPlacement
        );
    }
}