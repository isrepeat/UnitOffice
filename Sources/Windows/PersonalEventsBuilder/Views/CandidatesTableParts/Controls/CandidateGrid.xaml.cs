using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace PersonalEventsBuilder.Views.CandidatesTableParts.Controls {
    public sealed class CandidateTableItem {
        internal event System.EventHandler? Changed;
        internal IReadOnlyList<EventFormParts.FormColumn> Columns { get; private set; } = [];
        internal IReadOnlyList<IReadOnlyList<string>> Rows { get; private set; } = [];

        internal void Update(IReadOnlyList<EventFormParts.FormColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows) {
            this.Columns = columns;
            this.Rows = rows;
            this.Changed?.Invoke(this, System.EventArgs.Empty);
        }
    }

    internal sealed partial class CandidateGrid : UserControl, UDK.WinUI.Grid.IGridNavigationTarget, UDK.WinUI.Grid.IGridViewportContent, UDK.WinUI.Grid.INotifyGridRegionChanged {
        public event System.EventHandler? RegionChanged;

        public CandidateTableItem? TableItem {
            get => (CandidateTableItem?)this.GetValue(TableItemProperty);
            set => this.SetValue(TableItemProperty, value);
        }
        public static readonly DependencyProperty TableItemProperty = DependencyProperty.Register(
            nameof(TableItem), typeof(CandidateTableItem), typeof(CandidateGrid), new PropertyMetadata(null, (sender, args) => {
                var grid = (CandidateGrid)sender;
                if (args.OldValue is CandidateTableItem previous) {
                    previous.Changed -= grid.OnTableChanged;
                }
                if (args.NewValue is CandidateTableItem current) {
                    current.Changed += grid.OnTableChanged;
                }
                grid.OnTableChanged(sender, System.EventArgs.Empty);
            }));
        //
        // IGridNavigationTarget
        //
        public UDK.WinUI.Grid.GridArea Bounds { get; private set; }

        private IReadOnlyList<EventFormParts.FormColumn> columns = [];
        private IReadOnlyList<IReadOnlyList<string>> values = [];
        private UDK.WinUI.Grid.GridAddress? active;
        private readonly Stack<CandidateCell> recycledCells = new();
        private readonly Dictionary<UDK.WinUI.Grid.GridAddress, CandidateCell> visibleCells = [];

        public CandidateGrid() {
            this.InitializeComponent();
        }

        //
        // IGridNavigationTarget
        //
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

        public UDK.WinUI.Grid.GridArea FocusArea(UDK.WinUI.Grid.GridAddress address) {
            int index = this.ColumnIndex(address.Column);
            var column = this.columns[index];
            return new(column.Start, address.Row, column.Span, 1);
        }

        public UDK.WinUI.Grid.GridAddress FocusAddress(UDK.WinUI.Grid.GridAddress address) =>
            new(this.FocusArea(address).Column, address.Row);

        //
        // IGridViewportContent
        //
        public void ArrangeCells(UDK.WinUI.Grid.GridArea visible, double width, double height) {
            // Широкая ячейка привязана к началу столбца и остаётся видимой, даже если в окно попадает только его правый край.
            var visibleStarts = this.columns.Where(column => column.Start < visible.Right && column.Start + column.Span > visible.Column)
                .Select(column => column.Start).ToHashSet();
            // Сначала освобождаем исчезнувшие адреса, чтобы их ячейки можно было сразу использовать для новых столбцов.
            foreach (var address in this.visibleCells.Keys.Where(address => address.Row < visible.Row || address.Row >= visible.Bottom || !visibleStarts.Contains(address.Column)).ToArray()) {
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
                        // Загружаем XAML-шаблон только при нехватке готовых ячеек в пуле.
                        cell = this.recycledCells.TryPop(out var recycled) ? recycled
                            : (CandidateCell)((DataTemplate)this.Resources["CandidateCellTemplate"]).LoadContent();
                        cell.SetSelected(address == this.active);
                        this.visibleCells.Add(address, cell);
                        this.Cells.Children.Add(cell);
                    }
                    cell.SetContent(this.ValueAt(address), row == this.Bounds.Row, row - this.Bounds.Row < 3);
                    cell.Width = width * area.Width;
                    cell.Height = height;
                    Canvas.SetLeft(cell, (area.Column - this.Bounds.Column) * width);
                    Canvas.SetTop(cell, (row - this.Bounds.Row) * height);
                    column = area.Right;
                }
            }
        }

        private void OnTableChanged(object? sender, System.EventArgs args) {
            this.values = this.TableItem?.Rows ?? [];
            this.columns = this.TableItem?.Columns ?? [];
            this.Bounds = this.columns.Count == 0 ? default : new(this.columns[0].Start, 0,
                this.columns[^1].Start + this.columns[^1].Span - this.columns[0].Start, System.Math.Max(8, this.values.Count));
            this.RegionChanged?.Invoke(this, System.EventArgs.Empty);
        }

        private int ColumnIndex(int column) => Enumerable.Range(0, this.columns.Count).First(index => column < this.columns[index].Start + this.columns[index].Span);

        private string ValueAt(UDK.WinUI.Grid.GridAddress address) {
            int row = address.Row - this.Bounds.Row;
            int? index = this.columns[this.ColumnIndex(address.Column)].PrimaryIndex;
            return row < this.values.Count && index is int dataIndex && dataIndex < this.values[row].Count
                ? this.values[row][dataIndex] : string.Empty;
        }
    }
}