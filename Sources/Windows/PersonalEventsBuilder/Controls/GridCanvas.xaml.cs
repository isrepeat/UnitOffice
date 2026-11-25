using System;

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

        public bool CanScrollLeft => this.columns is { FirstVisibleIndex: > 0 } && this.visibleColumns > 0;
        public bool CanScrollRight => this.columns is { } axis && this.visibleColumns > 0 && axis.FirstVisibleIndex + this.visibleColumns < axis.Count;

        internal event Action? ViewportChanged;
        private readonly object columnViewport = new();
        private readonly object rowViewport = new();
        private GridLayout? observedLayout;
        private GridAxis? columns;
        private GridAxis? rows;
        private double gridHeight;

        private double gridLeft;
        private int visibleColumns;
        private uint? pointerId;
        private bool vertical;
        private double lineIndex;
        private double grabOffset;

        public GridCanvas() {
            this.InitializeComponent();
            this.Surface.SizeChanged += (_, _) => this.UpdateViewport();
            this.GridLayout = new GridLayout();
            this.Loaded += (_, _) => this.Observe(this.GridLayout);
            this.Unloaded += (_, _) => this.Observe(null);
            this.Surface.PointerPressed += this.StartDrag;
            this.Surface.PointerWheelChanged += (_, args) => {
                int delta = args.GetCurrentPoint(this.Surface).Properties.MouseWheelDelta;
                if (delta != 0 && this.rows is { } axis) {
                    int previous = axis.FirstVisibleIndex;
                    this.ScrollRows(-Math.Sign(delta));
                    args.Handled = previous != axis.FirstVisibleIndex;
                }
            };
            this.Surface.PointerMoved += this.MoveDrag;
            this.Surface.PointerReleased += (_, args) => this.Surface.ReleasePointerCapture(args.Pointer);
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

        public void ScrollColumns(int delta) => this.columns?.Scroll(delta, this.columns.Capacity(this.Surface.ActualWidth, this.columns.CenterOffset));

        public void ScrollRows(int delta) => this.rows?.Scroll(delta, this.rows.Capacity(this.Surface.ActualHeight, 0));

        private void Observe(GridLayout? layout) {
            if (this.observedLayout is { } previous) {
                previous.AxesChanged -= this.ObserveAxes;
            }
            this.observedLayout = layout;
            if (layout is not null) {
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
            this.columns?.SetViewport(this.columnViewport, this.Surface.ActualWidth);
            this.rows?.SetViewport(this.rowViewport, this.Surface.ActualHeight);
            this.Draw();
        }

        private void Draw() {
            this.Lines.Data = null;
            this.Labels.Children.Clear();
            this.visibleColumns = 0;
            var geometry = new GeometryGroup();
            if (this.columns is not { } columns || this.rows is not { } rows) {
                this.ViewportChanged?.Invoke();
                return;
            }
            this.gridLeft = columns.CenterOffset;
            this.visibleColumns = columns.VisibleCount(this.Surface.ActualWidth, this.gridLeft);
            int visibleRows = rows.VisibleCount(this.Surface.ActualHeight, 0);
            this.gridHeight = visibleRows * rows.Step;
            this.ViewportChanged?.Invoke();
            if (this.visibleColumns == 0 || this.gridHeight <= 0) {
                return;
            }
            double step = columns.Step;
            double gridWidth = this.visibleColumns * step;
            double right = this.gridLeft + gridWidth;
            this.Labels.Clip = new RectangleGeometry { Rect = new Rect(this.gridLeft, 0, gridWidth, this.gridHeight) };
            for (int i = 0; i < this.visibleColumns; i++) {
                var label = new TextBlock { Text = (columns.FirstVisibleIndex + i + 1).ToString() };
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
            this.lineIndex = this.vertical ? column : row;
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