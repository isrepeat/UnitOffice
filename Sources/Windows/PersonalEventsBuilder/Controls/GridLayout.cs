using System;

using Microsoft.UI.Xaml;

namespace PersonalEventsBuilder.Controls {
    public sealed class GridLayout : DependencyObject {
        public GridNavigation Navigation { get; set; } = new();
        public int NavigationOrder { get; set; }
        public GridAxis Columns {
            get => this.columns;
            set => this.Replace(ref this.columns, value);
        }
        public GridAxis Rows {
            get => this.rows;
            set => this.Replace(ref this.rows, value);
        }

        private GridAxis columns = new();
        private GridAxis rows = new();
        internal event Action? AxesChanged;

        private void Replace(ref GridAxis axis, GridAxis value) {
            ArgumentNullException.ThrowIfNull(value);
            if (axis != value) {
                axis = value;
                this.AxesChanged?.Invoke();
            }
        }
    }
}