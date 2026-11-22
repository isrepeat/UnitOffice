using System;
using System.Linq;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class EventFormZone : UserControl {
        public EventFormZone() {
            this.InitializeComponent();
        }

        internal NavigationMatrix Matrix => this.FormMatrix;

        internal double LeftOverflow => Math.Max(0, this.LeftColumn.ActualWidth - this.LeftColumn.MinWidth);
        internal double CenterInset => this.LeftColumn.ActualWidth + this.FieldColumns.ColumnSpacing;
        internal double RequiredWidth => this.FieldColumns.ColumnDefinitions.Sum(column => column.ActualWidth)
            + (this.FieldColumns.ColumnDefinitions.Count - 1) * this.FieldColumns.ColumnSpacing
            + this.FormBorder.Padding.Left + this.FormBorder.Padding.Right
            + this.FormBorder.BorderThickness.Left + this.FormBorder.BorderThickness.Right;

        internal void SetHeadingInset(double inset) {
            this.FormHeading.Margin = new Thickness(inset, 0, 0, 0);
        }
    }
}