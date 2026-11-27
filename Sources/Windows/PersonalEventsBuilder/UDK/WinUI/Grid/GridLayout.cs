using System;
using Microsoft.UI.Xaml;

namespace UDK.WinUI.Grid {
    public sealed class GridLayout : DependencyObject {
        public int NavigationOrder { get; set; }

        public GridAxis Rows {
            get => this.rows;
            set => this.Replace(ref this.rows, value);
        }

        public GridAxis Columns {
            get => this.columns;
            set => this.Replace(ref this.columns, value);
        }

        public GridNavigation Navigation { get; set; } = new();

        private GridAxis rows = new();
        private GridAxis columns = new();
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