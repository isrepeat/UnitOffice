using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace UDK.WinUI.Grid.Controls {
    public sealed partial class GridCanvas : UserControl, Navigation.INavigationSurface {
        //
        // INavigationSurface
        //
        GridArea Navigation.INavigationSurface.NavigationBounds => this.GridBounds;

        IEnumerable<GridArea> Navigation.INavigationSurface.NavigationRegions => this.regions.Count == 0
            ? new[] { this.GridBounds } : this.regions.Select(region => region.Bounds);

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

        public IEnumerable? ItemsSource {
            get => (IEnumerable?)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(GridCanvas),
            new PropertyMetadata(null, (sender, _) => ((GridCanvas)sender).RebuildRegions()));

        public DataTemplate? ItemTemplate {
            get => (DataTemplate?)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }
        public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
            nameof(ItemTemplate), typeof(DataTemplate), typeof(GridCanvas),
            new PropertyMetadata(null, (sender, _) => ((GridCanvas)sender).RebuildRegions(reuse: false)));

        private int LeftColumn => this.columnWindow.First;

        public bool CanScrollLeft => this.columnWindow.Slots > 0 && this.LeftColumn > 0;

        public bool CanScrollRight => this.columns is { } axis && this.columnWindow.Slots > 0 && this.LeftColumn + this.visibleColumns < axis.Count;

        private int visibleColumns => this.columnWindow.Count;

        internal int NavigationOrder => this.observedLayout?.NavigationOrder ?? 0;

        private double gridLeft => this.columnWindow.Left;

        private GridArea VisibleArea {
            get {
                int left = (int)Math.Floor(this.displayColumn);
                int top = (int)Math.Floor(this.displayRow);
                int right = (int)Math.Ceiling(this.displayColumn + this.columnWindow.Slots);
                int bottom = (int)Math.Ceiling(this.displayRow + this.drawingBounds.Height / this.rows!.Step);
                return this.GridBounds.Intersect(new(left, top, right - left, bottom - top));
            }
        }

        internal GridArea GridBounds => this.visibleColumns > 0 && this.visibleRows > 0
            ? new(0, 0, this.columns!.Count, this.rows!.Count) : default;

        public Visibility GridLinesVisibility {
            get => this.Lines.Visibility;
            set => this.Lines.Visibility = value;
        }

        private int visibleRows;
        private bool vertical;
        private Rect drawingBounds;
        private uint? pointerId;
        private double surfaceX;
        private double lineIndex;
        private double displayRow;
        private double grabOffset;
        private double gridHeight;
        private double displayColumn;
        private GridAxis? rows;
        private GridAxis? columns;
        private GridLayout? observedLayout;
        private GridAddress? activeAddress;
        internal event Action? ViewportChanged;
        private readonly object rowViewport = new();
        private readonly object columnViewport = new();
        private GridAxis.ColumnWindow columnWindow;
        private IGridNavigationTarget? activeTarget;
        private readonly List<IGridNavigationTarget> regions = [];

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

        public bool ScrollRows(int delta) {
            if (this.rows is not { } rows) {
                return false;
            }
            int previous = rows.FirstVisibleIndex;
            rows.Scroll(delta, rows.Capacity(this.Surface.ActualHeight, 0));
            return previous != rows.FirstVisibleIndex;
        }

        private void OnRowScroll(object sender, Microsoft.UI.Xaml.Controls.Primitives.ScrollEventArgs args) {
            if (this.rows is { } rows) {
                this.ScrollRows((int)Math.Round(args.NewValue) - rows.FirstVisibleIndex);
            }
        }

        internal void Register<T>(T region) where T : FrameworkElement, IGridNavigationTarget {
            if (this.regions.Any(other => !region.Bounds.Intersect(other.Bounds).IsEmpty)) {
                throw new ArgumentException("Grid regions must not overlap.", nameof(region));
            }
            this.regions.Add(region);
            if (region is INotifyGridRegionChanged notifying) {
                notifying.RegionChanged += this.OnRegionChanged;
            }
            this.Targets.Children.Add(region);
            this.UpdateTargets();
        }

        internal void ReplaceRegions<T>(IReadOnlyList<T> replacements) where T : FrameworkElement {
            var address = this.activeAddress;
            // Общие для двух раскладок контролы остаются в визуальном дереве вместе со своим состоянием фокуса.
            var nextElements = new HashSet<FrameworkElement>(replacements);
            if (this.activeTarget is FrameworkElement activeElement && !nextElements.Contains(activeElement)) {
                this.SetActiveAddress(null);
            }
            for (int index = this.Targets.Children.Count - 1; index >= 0; index--) {
                if (this.Targets.Children[index] is FrameworkElement element && !nextElements.Contains(element)) {
                    if (element is INotifyGridRegionChanged notifying) {
                        notifying.RegionChanged -= this.OnRegionChanged;
                    }
                    this.Targets.Children.RemoveAt(index);
                }
            }
            this.regions.Clear();
            foreach (var region in replacements) {
                this.regions.Add((IGridNavigationTarget)region);
                if (!this.Targets.Children.Contains(region)) {
                    if (region is INotifyGridRegionChanged notifying) {
                        notifying.RegionChanged += this.OnRegionChanged;
                    }
                    this.Targets.Children.Add(region);
                }
            }
            this.UpdateTargets();
            if (address is { } previous) {
                var resolved = this.ResolveAddress(previous);
                if (this.activeAddress != resolved || this.activeTarget != this.Owner(resolved)) {
                    this.SetActiveAddress(resolved);
                }
            }
        }

        internal GridAddress ResolveAddress(GridAddress hit) => this.Owner(hit)?.FocusAddress(hit) ?? hit;

        private void OnRegionChanged(object? sender, EventArgs args) {
            this.UpdateTargets();
            if (this.activeAddress is { } previous) {
                this.SetActiveAddress(this.ResolveAddress(previous));
            }
        }

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

        private void RebuildRegions(bool reuse = true) {
            var elements = new List<FrameworkElement>();
            if (this.ItemTemplate is { } template && this.ItemsSource is { } source) {
                // Каждый совпавший элемент используем один раз. При смене шаблона всю раскладку создаём из нового шаблона.
                var previous = reuse ? this.regions.OfType<FrameworkElement>().ToList() : [];
                foreach (var item in source) {
                    int index = previous.FindIndex(element => Equals(element.DataContext, item));
                    if (index >= 0) {
                        elements.Add(previous[index]);
                        previous.RemoveAt(index);
                        continue;
                    }
                    if (template.LoadContent() is not FrameworkElement element || element is not IGridNavigationTarget) {
                        throw new InvalidOperationException("ItemTemplate must create a FrameworkElement implementing IGridNavigationTarget.");
                    }
                    element.DataContext = item;
                    elements.Add(element);
                }
            }
            this.ReplaceRegions(elements);
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
            int capacity = rows.Capacity(this.Surface.ActualHeight, 0);
            this.RowScroll.Maximum = Math.Max(0, rows.Count - capacity);
            this.RowScroll.ViewportSize = capacity;
            this.RowScroll.LargeChange = Math.Max(1, capacity);
            this.RowScroll.Value = rows.FirstVisibleIndex;
            this.RowScroll.Visibility = capacity > 0 && capacity < rows.Count ? Visibility.Visible : Visibility.Collapsed;
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
            for (int row = top; row <= bottom; row++) {
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