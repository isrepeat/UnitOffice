using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace PersonalEventsBuilder.Views.CandidatesTableParts.Controls {
    internal sealed partial class CandidateGrid : UserControl, UDK.WinUI.Grid.IGridNavigationTarget, UDK.WinUI.Grid.IGridViewportContent {
        public UDK.WinUI.Grid.GridArea Bounds { get; }

        private readonly string[][] values;
        private readonly int[] columnEdges;
        private readonly Dictionary<UDK.WinUI.Grid.GridAddress, CandidateCell> visibleCells = [];
        private readonly Stack<CandidateCell> recycledCells = new();
        private UDK.WinUI.Grid.GridAddress? active;

        internal CandidateGrid(int column, int span, string[][] values) {
            this.values = values;
            int columns = values.Length == 0 ? 0 : values[0].Length;
            this.Bounds = new(column, 0, span, System.Math.Max(8, values.Length));
            this.columnEdges = new int[columns + 1];
            this.columnEdges[0] = column;
            for (int index = 0; index < columns; index++) {
                int width = span / columns + (index < span % columns ? 1 : 0);
                this.columnEdges[index + 1] = this.columnEdges[index] + width;
            }
            this.InitializeComponent();
        }

        public UDK.WinUI.Grid.GridArea FocusArea(UDK.WinUI.Grid.GridAddress address) {
            int index = this.ColumnIndex(address.Column);
            int start = this.columnEdges[index];
            return new(start, address.Row, this.columnEdges[index + 1] - start, 1);
        }

        public UDK.WinUI.Grid.GridAddress FocusAddress(UDK.WinUI.Grid.GridAddress address) =>
            new(this.FocusArea(address).Column, address.Row);

        public void SetActiveAddress(UDK.WinUI.Grid.GridAddress? address) {
            if (this.active is { } previous && this.visibleCells.TryGetValue(previous, out var oldCell)) {
                oldCell.SetSelected(false);
            }
            this.active = address;
            this.IsTabStop = address.HasValue;
            if (address is { } selected) {
                AutomationProperties.SetName(this, this.ValueAt(selected));
                this.Focus(FocusState.Programmatic);
            }
            if (address is { } current && this.visibleCells.TryGetValue(current, out var cell)) {
                cell.SetSelected(true);
            }
        }

        public void ArrangeCells(UDK.WinUI.Grid.GridArea visible, double width, double height) {
            foreach (var address in this.visibleCells.Keys.Where(address => visible.Intersect(this.FocusArea(address)).IsEmpty).ToArray()) {
                var released = this.visibleCells[address];
                this.visibleCells.Remove(address);
                this.Cells.Children.Remove(released);
                released.DataContext = null;
                this.recycledCells.Push(released);
            }
            for (int row = visible.Row; row < visible.Bottom; row++) {
                for (int column = visible.Column; column < visible.Right;) {
                    var area = this.FocusArea(new(column, row));
                    var address = new UDK.WinUI.Grid.GridAddress(area.Column, row);
                    if (!this.visibleCells.TryGetValue(address, out var cell)) {
                        cell = this.recycledCells.TryPop(out var recycled) ? recycled : new CandidateCell();
                        cell.SetContent(this.ValueAt(address), row == this.Bounds.Row);
                        cell.SetSelected(address == this.active);
                        this.visibleCells.Add(address, cell);
                        this.Cells.Children.Add(cell);
                    }
                    cell.Width = width * area.Width;
                    cell.Height = height;
                    Canvas.SetLeft(cell, (area.Column - this.Bounds.Column) * width);
                    Canvas.SetTop(cell, (row - this.Bounds.Row) * height);
                    column = area.Right;
                }
            }
        }

        private int ColumnIndex(int column) => System.Array.FindIndex(this.columnEdges, edge => edge > column) - 1;

        private string ValueAt(UDK.WinUI.Grid.GridAddress address) {
            int row = address.Row - this.Bounds.Row;
            return row < this.values.Length ? this.values[row][this.ColumnIndex(address.Column)] : string.Empty;
        }
    }
}