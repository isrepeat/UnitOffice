using System;
using System.Linq;
using System.Collections.Generic;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace UDK.WinUI.Grid.Controls {
    public sealed partial class GridCanvas : UserControl, Navigation.INavigationSurface {
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(GridLayout),
            typeof(GridCanvas),
            new PropertyMetadata(null, OnLayoutChanged)
        );

        public GridLayout? GridLayout {
            get => (GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }

        public bool CanScrollLeft => this.columnWindow.Slots > 0 && this.LeftColumn > 0;
        public Visibility GridLinesVisibility {
            get => this.Lines.Visibility;
            set => this.Lines.Visibility = value;
        }

        public bool CanScrollRight => this.columns is { } axis && this.columnWindow.Slots > 0 && this.LeftColumn + this.visibleColumns < axis.Count;
        private double gridLeft => this.columnWindow.Left;
        private int visibleColumns => this.columnWindow.Count;
        internal int NavigationOrder => this.observedLayout?.NavigationOrder ?? 0;
        private int LeftColumn => this.columnWindow.First;
        internal GridArea GridBounds => this.visibleColumns > 0 && this.visibleRows > 0
            ? new(0, 0, this.columns!.Count, this.rows!.Count) : default;

        private GridArea VisibleArea {
            get {
                int left = (int)Math.Floor(this.displayColumn);
                int top = (int)Math.Floor(this.displayRow);
                int right = (int)Math.Ceiling(this.displayColumn + this.columnWindow.Slots);
                int bottom = (int)Math.Ceiling(this.displayRow + this.drawingBounds.Height / this.rows!.Step);
                return this.GridBounds.Intersect(new(left, top, right - left, bottom - top));
            }
        }

        GridArea Navigation.INavigationSurface.NavigationBounds => this.GridBounds;
        IEnumerable<GridArea> Navigation.INavigationSurface.NavigationRegions => this.regions.Count == 0
            ? new[] { this.GridBounds } : this.regions.Select(region => region.Bounds);

        internal event Action? ViewportChanged;
        private readonly object columnViewport = new();
        private readonly object rowViewport = new();
        private GridLayout? observedLayout;
        private GridAxis? columns;
        private GridAxis? rows;
        private double gridHeight;
        private double surfaceX;
        private GridAxis.ColumnWindow columnWindow;
        private int visibleRows;
        private readonly List<IGridNavigationTarget> regions = [];
        private GridAddress? activeAddress;
        private IGridNavigationTarget? activeTarget;
        private Rect drawingBounds;
        private double displayColumn;
        private double displayRow;
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
            this.Surface.SizeChanged += (_, args) => {
                this.Targets.Width = args.NewSize.Width;
                this.Targets.Height = args.NewSize.Height;
                this.UpdateViewport();
            };
            this.GridLayout = new GridLayout();
            this.Loaded += (_, _) => this.Observe(this.GridLayout);
            this.Unloaded += (_, _) => this.Observe(null);
            this.Surface.Tapped += this.SelectNearest;
            this.Surface.PointerPressed += this.StartDrag;
            this.PreviewKeyDown += (_, args) => {
                if (this.observedLayout?.Navigation.Move(this, args.Key) == true) {
                    args.Handled = true;
                }
            };
            this.LostFocus += (_, _) => {
                var focused = this.XamlRoot is null ? null : FocusManager.GetFocusedElement(this.XamlRoot) as DependencyObject;
                while (focused is not null) {
                    if (focused == this) {
                        return;
                    }
                    focused = VisualTreeHelper.GetParent(focused);
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

        public void ScrollColumns(int delta) => this.columns?.ScrollColumns(delta, this.columnWindow);

        public void ScrollRows(int delta) => this.rows?.Scroll(delta, this.rows.Capacity(this.Surface.ActualHeight, 0));

        internal void Register<T>(T region) where T : FrameworkElement, IGridNavigationTarget {
            if (this.regions.Any(other => !region.Bounds.Intersect(other.Bounds).IsEmpty)) {
                throw new ArgumentException("Grid regions must not overlap.", nameof(region));
            }
            this.regions.Add(region);
            this.Targets.Children.Add(region);
            this.UpdateTargets();
        }

        internal void ReplaceRegions<T>(IReadOnlyList<T> replacements) where T : FrameworkElement, IGridNavigationTarget {
            var address = this.activeAddress;
            this.SetActiveAddress(null);
            this.Targets.Children.Clear();
            this.regions.Clear();
            foreach (var region in replacements) {
                this.regions.Add(region);
                this.Targets.Children.Add(region);
            }
            this.UpdateTargets();
            if (address is { } previous) {
                this.SetActiveAddress(this.ResolveAddress(previous));
            }
        }

        internal GridAddress ResolveAddress(GridAddress hit) => this.Owner(hit)?.FocusAddress(hit) ?? hit;

        internal GridArea FocusArea(GridAddress address) => this.Owner(address)?.FocusArea(address) ?? new(address.Column, address.Row, 1, 1);

        internal void SetActiveAddress(GridAddress? address) {
            var target = address is { } cell ? this.Owner(cell) : null;
            if (this.activeTarget != target) {
                this.activeTarget?.SetActiveAddress(null);
            }
            this.activeAddress = address;
            this.activeTarget = target;
            target?.SetActiveAddress(address);
            this.UpdateCursor();
        }

        internal void Reveal(GridAddress address) {
            var area = this.FocusArea(address);
            int left = this.LeftColumn;
            int top = this.rows!.FirstVisibleIndex;
            int firstRow = Math.Max(area.Row, address.Row - this.visibleRows + 1);
            int lastColumn = Math.Min(area.Right, area.Column + this.visibleColumns) - 1;
            this.ScrollColumns(area.Column < left ? area.Column - left : Math.Max(0, lastColumn - (left + this.visibleColumns - 1)));
            this.ScrollRows(firstRow < top ? firstRow - top : Math.Max(0, address.Row - (top + this.visibleRows - 1)));
        }

        private static void OnLayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var gridCanvas = (GridCanvas)sender;
            if (gridCanvas.IsLoaded) {
                gridCanvas.Observe(gridCanvas.GridLayout);
            }
        }

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
            this.displayColumn = columns.DisplayIndex + this.columnWindow.Slot;
            this.displayRow = rows.DisplayIndex;
            double step = columns.Step;
            this.drawingBounds = new Rect(columns.Origin - this.surfaceX + this.columnWindow.Slot * step, 0,
                this.columnWindow.Slots * step, rows.Capacity(this.Surface.ActualHeight, 0) * rows.Step);
            this.ViewportClip.Rect = this.drawingBounds;
            this.UpdateTargets();
            this.ViewportChanged?.Invoke();
            if (this.visibleColumns == 0 || this.gridHeight <= 0) {
                return;
            }
            int first = Math.Max(0, (int)Math.Floor(this.displayColumn));
            int end = Math.Min(columns.Count, (int)Math.Ceiling(this.displayColumn + this.columnWindow.Slots));
            double left = this.drawingBounds.X + (first - this.displayColumn) * step;
            double right = left + (end - first) * step;
            int top = (int)Math.Floor(this.displayRow);
            int bottom = Math.Min(rows.Count, (int)Math.Ceiling(this.displayRow + this.drawingBounds.Height / rows.Step));
            for (int column = first; column <= end; column++) {
                double x = left + (column - first) * step;
                if (column < end && this.regions.Count == 0) {
                    var label = (TextBlock)((DataTemplate)this.Surface.Resources["ColumnLabelTemplate"]).LoadContent();
                    label.DataContext = (column + 1).ToString();
                    Canvas.SetLeft(label, x + 3);
                    this.Labels.Children.Add(label);
                }
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, (bottom - this.displayRow) * rows.Step) });
            }
            for (int row = top + 1; row <= bottom; row++) {
                double y = (row - this.displayRow) * rows.Step;
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(left, y), EndPoint = new Point(right, y) });
            }
            this.Lines.Data = geometry;
        }

        private IGridNavigationTarget? Owner(GridAddress address) => this.regions.FirstOrDefault(region => region.Bounds.Contains(address));

        private GridAddress? FindNearestCell(GridArea search, GridAddress desired) {
            search = search.Intersect(this.GridBounds);
            if (search.IsEmpty) {
                return null;
            }
            if (this.regions.Count == 0) {
                return search.Closest(desired);
            }

            GridAddress? best = null;
            (double, double, int) bestScore = (double.PositiveInfinity, double.PositiveInfinity, int.MaxValue);
            foreach (var region in this.regions) {
                var intersection = search.Intersect(region.Bounds);
                if (intersection.IsEmpty) {
                    continue;
                }
                var hit = intersection.Closest(desired);
                var address = this.ResolveAddress(hit);
                if (!this.GridBounds.Contains(address)) {
                    continue;
                }
                double dx = Math.Abs(hit.Column - desired.Column);
                double dy = Math.Abs(hit.Row - desired.Row);
                var score = (dx * dx + dy * dy, dy, hit.Column);
                if (score.CompareTo(bestScore) < 0) {
                    bestScore = score;
                    best = hit;
                }
            }
            return best;
        }

        private void UpdateCursor() {
            this.Cursor.Visibility = Visibility.Collapsed;
            if (this.activeTarget is null && this.activeAddress is { } address && this.columns is { } columns && this.rows is { } rows) {
                this.Place(this.Cursor, new(address.Column, address.Row, 1, 1), columns.Step, rows.Step);
                this.Cursor.Visibility = Visibility.Visible;
            }
        }

        private void Place(FrameworkElement element, GridArea area, double width, double height) {
            element.Width = width * area.Width;
            element.Height = height * area.Height;
            Canvas.SetLeft(element, this.drawingBounds.X + (area.Column - this.displayColumn) * width);
            Canvas.SetTop(element, (area.Row - this.displayRow) * height);
        }

        private void UpdateTargets() {
            if (this.columns is not { } columns || this.rows is not { } rows) {
                return;
            }
            var visible = this.VisibleArea;
            foreach (var region in this.regions) {
                this.Place((FrameworkElement)region, region.Bounds, columns.Step, rows.Step);
                if (region is IGridViewportContent content) {
                    content.ArrangeCells(visible.Intersect(region.Bounds), columns.Step, rows.Step);
                }
            }
            this.UpdateCursor();
        }

        private void SelectNearest(object sender, TappedRoutedEventArgs args) {
            if (this.GridBounds.IsEmpty) {
                return;
            }
            var point = args.GetPosition(this.Surface);
            var hit = new GridAddress(
                (int)Math.Floor(this.displayColumn + (point.X - this.drawingBounds.X) / this.columns!.Step),
                (int)Math.Floor(this.displayRow + point.Y / this.rows!.Step)
            );
            if (this.FindNearestCell(this.VisibleArea, hit) is { } address) {
                args.Handled = true;
                this.observedLayout!.Navigation.Select(this, address);
            }
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