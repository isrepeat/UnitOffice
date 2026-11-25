using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class GridCanvas : UserControl {
        public GridLayout? GridLayout {
            get => (GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(GridLayout),
            typeof(GridCanvas),
            new PropertyMetadata(null, OnLayoutChanged)
        );

        public DataTemplate? TargetTemplate { get; set; }

        public bool CanScrollLeft => this.columnWindow.Slots > 0 && this.LeftColumn > 0;
        public bool CanScrollRight => this.columns is { } axis && this.columnWindow.Slots > 0 && this.LeftColumn + this.visibleColumns < axis.Count;

        internal event Action? ViewportChanged;
        private readonly object columnViewport = new();
        private readonly object rowViewport = new();
        private GridLayout? observedLayout;
        private GridAxis? columns;
        private GridAxis? rows;
        private double gridHeight;

        private double surfaceX;
        private GridAxis.ColumnWindow columnWindow;
        private double gridLeft => this.columnWindow.Left;
        private int visibleColumns => this.columnWindow.Count;
        private int visibleRows;
        internal GridCell[] Cells { get; set; } = [];
        internal int NavigationOrder => this.observedLayout?.NavigationOrder ?? 0;
        private int LeftColumn => this.columnWindow.First;
        internal IEnumerable<GridCell> ReachableCells => this.Cells.Where(cell =>
            this.visibleRows > 0 && this.visibleColumns > 0 && cell.Column >= 0 &&
            cell.Column < this.columns!.Count && cell.Row >= 0 && cell.Row < this.rows!.Count);

        private IEnumerable<GridCell> VisibleCells => this.ReachableCells.Where(cell =>
            cell.Column >= this.LeftColumn && cell.Column < this.LeftColumn + this.visibleColumns &&
            cell.Row >= this.rows!.FirstVisibleIndex && cell.Row < this.rows.FirstVisibleIndex + this.visibleRows);
        private uint? pointerId;
        private bool vertical;
        private double lineIndex;
        private double grabOffset;

        public GridCanvas() {
            this.InitializeComponent();
            this.Surface.LayoutUpdated += (_, _) => {
                if (this.columns is not null && this.Surface.XamlRoot is not null) {
                    double x = this.Surface.TransformToVisual(null).TransformPoint(new Point()).X;
                    if (x != this.surfaceX) {
                        this.surfaceX = x;
                        this.UpdateViewport();
                    }
                }
            };
            this.Surface.SizeChanged += (_, _) => this.UpdateViewport();
            this.GridLayout = new GridLayout();
            this.Loaded += (_, _) => this.Observe(this.GridLayout);
            this.Unloaded += (_, _) => this.Observe(null);
            this.Surface.Tapped += this.SelectNearest;
            this.Surface.PointerPressed += this.StartDrag;
            this.KeyDown += (_, args) => {
                if (this.observedLayout?.Navigation.Move(this, args.Key) == true) {
                    args.Handled = true;
                }
            };
            this.LostFocus += (_, _) => {
                if (this.FocusState != FocusState.Unfocused) {
                    return;
                }
                this.observedLayout?.Navigation.Release(this);
            };
            this.Surface.PointerWheelChanged += (_, args) => {
                int delta = args.GetCurrentPoint(this.Surface).Properties.MouseWheelDelta;
                if (delta != 0 && this.rows is { } axis) {
                    int previous = axis.FirstVisibleIndex;
                    this.ScrollRows(-Math.Sign(delta));
                    args.Handled = previous != axis.FirstVisibleIndex;
                }
            };
            this.Surface.PointerMoved += this.MoveDrag;
            this.Surface.PointerReleased += (_, args) => {
                if (this.pointerId == args.Pointer.PointerId) {
                    this.Surface.ReleasePointerCapture(args.Pointer);
                }
            };
            this.Surface.PointerCaptureLost += (_, args) => {
                if (this.pointerId == args.Pointer.PointerId) {
                    this.pointerId = null;
                }
            };
        }

        private static void OnLayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var gridCanvas = (GridCanvas)sender;
            if (gridCanvas.IsLoaded) {
                gridCanvas.Observe(gridCanvas.GridLayout);
            }
        }

        public void ScrollColumns(int delta) => this.columns?.ScrollColumns(delta, this.columnWindow);

        public void ScrollRows(int delta) => this.rows?.Scroll(delta, this.rows.Capacity(this.Surface.ActualHeight, 0));

        private void Observe(GridLayout? layout) {
            if (this.observedLayout is { } previous) {
                previous.Navigation.Remove(this);
                previous.AxesChanged -= this.ObserveAxes;
            }
            this.observedLayout = layout;
            if (layout is not null) {
                layout.Navigation.Register(this);
                layout.AxesChanged += this.ObserveAxes;
            }
            this.ObserveAxes();
        }

        private void ObserveAxes() {
            if (this.columns is not null) {
                this.columns.Changed -= this.Draw;
                this.columns.SetViewport(this.columnViewport, 0);
            }
            if (this.rows is not null) {
                this.rows.Changed -= this.Draw;
                this.rows.SetViewport(this.rowViewport, 0);
            }
            this.columns = this.observedLayout?.Columns;
            this.rows = this.observedLayout?.Rows;
            if (this.columns is not null && this.rows is not null) {
                this.columns.Changed += this.Draw;
                this.rows.Changed += this.Draw;
            }
            this.UpdateViewport();
        }

        private void UpdateViewport() {
            this.columns?.SetViewport(this.columnViewport, this.Surface.ActualWidth, this.surfaceX);
            this.rows?.SetViewport(this.rowViewport, this.Surface.ActualHeight);
            this.Draw();
        }

        private void Draw() {
            this.Lines.Data = null;
            this.Labels.Children.Clear();
            this.columnWindow = default;
            this.visibleRows = 0;
            var geometry = new GeometryGroup();
            if (this.columns is not { } columns || this.rows is not { } rows) {
                this.UpdateTargets();
                this.ViewportChanged?.Invoke();
                return;
            }
            this.columnWindow = columns.GetColumns(this.surfaceX, this.Surface.ActualWidth);
            int visibleRows = rows.VisibleCount(this.Surface.ActualHeight, 0);
            this.visibleRows = visibleRows;
            this.gridHeight = visibleRows * rows.Step;
            this.UpdateTargets();
            this.ViewportChanged?.Invoke();
            if (this.visibleColumns == 0 || this.gridHeight <= 0) {
                return;
            }
            double step = columns.Step;
            double gridWidth = this.visibleColumns * step;
            double right = this.gridLeft + gridWidth;
            this.Labels.Clip = new RectangleGeometry { Rect = new Rect(this.gridLeft, 0, gridWidth, this.gridHeight) };
            for (int i = 0; i < this.visibleColumns; i++) {
                var label = new TextBlock { Text = (this.LeftColumn + i + 1).ToString() };
                Canvas.SetLeft(label, this.gridLeft + i * step + 3);
                this.Labels.Children.Add(label);
            }

            for (int i = 0; i <= this.visibleColumns; i++) {
                double x = this.gridLeft + i * step;
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, this.gridHeight) });
            }
            for (int i = 1; i <= visibleRows; i++) {
                double y = i * rows.Step;
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(this.gridLeft, y), EndPoint = new Point(right, y) });
            }
            this.Lines.Data = geometry;
        }

        internal void UpdateTargets() {
            var cells = this.VisibleCells.ToArray();
            int count = cells.Length;
            var children = this.Targets.Children;
            while (children.Count > count) {
                children.RemoveAt(children.Count - 1);
            }
            if (count == 0 || this.columns is not { } columns || this.rows is not { } rows) {
                return;
            }

            for (int i = 0; i < count; i++) {
                if (i == children.Count) {
                    var item = this.TargetTemplate is null ? new GridPoint() : this.TargetTemplate.LoadContent();
                    if (item is not FrameworkElement element || element is not IGridNavigationTarget) {
                        throw new InvalidOperationException("The grid target template must contain a FrameworkElement implementing IGridNavigationTarget.");
                    }
                    children.Add(element);
                }

                var target = (FrameworkElement)children[i];
                var cell = cells[i];
                int x = cell.Column - this.LeftColumn;
                int y = cell.Row - rows.FirstVisibleIndex;
                target.Width = columns.Step;
                target.Height = rows.Step;
                Canvas.SetLeft(target, this.gridLeft + x * columns.Step);
                Canvas.SetTop(target, y * rows.Step);
                ((IGridNavigationTarget)target).UpdateNavigationState(cell.Column, cell.Row, this.observedLayout!.Navigation.IsActive(this, cell));
            }
        }

        private void SelectNearest(object sender, TappedRoutedEventArgs args) {
            var point = args.GetPosition(this.Surface);
            if (this.visibleColumns == 0 || this.visibleRows == 0
                || this.columns is not { } columns || this.rows is not { } rows) {
                return;
            }

            var nearest = this.VisibleCells.MinBy(cell => {
                double dx = this.gridLeft + (cell.Column - this.LeftColumn + 0.5) * columns.Step - point.X;
                double dy = (cell.Row - rows.FirstVisibleIndex + 0.5) * rows.Step - point.Y;
                return dx * dx + dy * dy;
            });
            if (nearest is not null) {
                args.Handled = true;
                this.observedLayout!.Navigation.Select(this, nearest);
            }
        }

        internal void Reveal(GridCell cell) {
            int left = this.LeftColumn;
            int top = this.rows!.FirstVisibleIndex;
            this.ScrollColumns(cell.Column - Math.Clamp(cell.Column, left, left + this.visibleColumns - 1));
            this.ScrollRows(cell.Row - Math.Clamp(cell.Row, top, top + this.visibleRows - 1));
        }

        private void StartDrag(object sender, PointerRoutedEventArgs args) {
            var pointer = args.GetCurrentPoint(this.Surface);
            if (this.pointerId.HasValue || !pointer.Properties.IsLeftButtonPressed) {
                return;
            }

            // Ищем ближайшую видимую линию по обеим осям.
            var point = pointer.Position;
            if (this.columns is not { } columns || this.rows is not { } rows || point.Y > this.gridHeight) {
                return;
            }
            double step = columns.Step;
            double localX = point.X - this.gridLeft;
            if (this.visibleColumns == 0 || localX < 0 || localX > this.visibleColumns * step) {
                return;
            }
            double column = Math.Round(localX / step);
            double row = Math.Round(point.Y / rows.Step);
            double dx = column >= 1 && column <= this.visibleColumns ? Math.Abs(localX - column * step) : double.PositiveInfinity;
            double dy = row >= 1 && row * rows.Step <= this.gridHeight ? Math.Abs(point.Y - row * rows.Step) : double.PositiveInfinity;
            if (Math.Min(dx, dy) > 4 || !this.Surface.CapturePointer(args.Pointer)) {
                return;
            }

            this.pointerId = args.Pointer.PointerId;
            this.vertical = dx <= dy;
            this.lineIndex = this.vertical ? this.LeftColumn - columns.FirstVisibleIndex + column : row;
            this.grabOffset = (this.vertical ? point.X : point.Y) - this.lineIndex * (this.vertical ? columns.Step : rows.Step);
            args.Handled = true;
        }

        private void MoveDrag(object sender, PointerRoutedEventArgs args) {
            if (this.pointerId != args.Pointer.PointerId || (this.vertical ? this.columns : this.rows) is not { } axis) {
                return;
            }
            var point = args.GetCurrentPoint(this.Surface).Position;
            double coordinate = this.vertical ? point.X : point.Y;
            axis.Step = Math.Max(GridAxis.MinimumStep, (coordinate - this.grabOffset) / this.lineIndex);
            args.Handled = true;
        }
    }
}