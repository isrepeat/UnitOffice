using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace PersonalEventsBuilder.Controls {
    internal interface IGridViewportContent {
        void ArrangeCells(GridArea visible, double width, double height);
    }

    internal sealed class CandidateGrid : UserControl, IGridNavigationTarget, IGridViewportContent {
        private readonly Canvas cells = new();
        private readonly string[][] values;
        private readonly Dictionary<GridAddress, CandidateCell> visibleCells = [];
        private readonly Stack<CandidateCell> recycledCells = new();
        private GridAddress? active;
        public GridArea Bounds { get; }

        internal CandidateGrid(int column, int row, string[][] values) {
            this.values = values;
            this.Bounds = new(column, row, values.Length == 0 ? 0 : values[0].Length, values.Length);
            this.Content = this.cells;
            this.IsTabStop = false;
        }

        public GridAddress FocusAddress(GridAddress address) => address;
        public GridArea FocusArea(GridAddress address) => new(address.Column, address.Row, 1, 1);

        public void SetActiveAddress(GridAddress? address) {
            if (this.active is { } previous && this.visibleCells.TryGetValue(previous, out var oldCell)) {
                oldCell.SetSelected(false);
            }
            this.active = address;
            this.IsTabStop = address.HasValue;
            if (address is { } selected) {
                AutomationProperties.SetName(this, this.values[selected.Row - this.Bounds.Row][selected.Column - this.Bounds.Column]);
                this.Focus(FocusState.Programmatic);
            }
            if (address is { } current && this.visibleCells.TryGetValue(current, out var cell)) {
                cell.SetSelected(true);
            }
        }

        public void ArrangeCells(GridArea visible, double width, double height) {
            foreach (var address in this.visibleCells.Keys.Where(address => !visible.Contains(address)).ToArray()) {
                var released = this.visibleCells[address];
                this.visibleCells.Remove(address);
                this.cells.Children.Remove(released);
                released.DataContext = null;
                this.recycledCells.Push(released);
            }
            for (int row = visible.Row; row < visible.Bottom; row++) {
                for (int column = visible.Column; column < visible.Right; column++) {
                    var address = new GridAddress(column, row);
                    if (!this.visibleCells.TryGetValue(address, out var cell)) {
                        cell = this.recycledCells.TryPop(out var recycled) ? recycled : new CandidateCell();
                        cell.DataContext = this.values[row - this.Bounds.Row][column - this.Bounds.Column];
                        cell.SetSelected(address == this.active);
                        this.visibleCells.Add(address, cell);
                        this.cells.Children.Add(cell);
                    }
                    cell.Width = width;
                    cell.Height = height;
                    Canvas.SetLeft(cell, (column - this.Bounds.Column) * width);
                    Canvas.SetTop(cell, (row - this.Bounds.Row) * height);
                }
            }
        }
    }
}