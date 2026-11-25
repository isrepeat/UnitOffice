using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class GridCanvas : UserControl {
        public GridScale? GridScale {
            get => (GridScale?)this.GetValue(GridScaleProperty);
            set => this.SetValue(GridScaleProperty, value);
        }
        public static readonly DependencyProperty GridScaleProperty = DependencyProperty.Register(
            nameof(GridScale),
            typeof(GridScale),
            typeof(GridCanvas),
            new PropertyMetadata(null, OnScaleChanged)
        );

        private GridScale? observedScale;
        private long stepToken;

        private uint? pointerId;
        private bool vertical;
        private double lineIndex;
        private double grabOffset;

        public GridCanvas() {
            this.InitializeComponent();
            this.Surface.SizeChanged += (_, _) => this.Draw();
            this.GridScale = new GridScale();
            this.Loaded += (_, _) => this.Observe(this.GridScale);
            this.Unloaded += (_, _) => this.Observe(null);
            this.Surface.PointerPressed += this.StartDrag;
            this.Surface.PointerMoved += this.MoveDrag;
            this.Surface.PointerReleased += (_, args) => this.Surface.ReleasePointerCapture(args.Pointer);
            this.Surface.PointerCaptureLost += (_, args) => {
                if (this.pointerId == args.Pointer.PointerId) {
                    this.pointerId = null;
                }
            };
        }

        private static void OnScaleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var gridCanvas = (GridCanvas)sender;
            if (gridCanvas.IsLoaded) {
                gridCanvas.Observe(gridCanvas.GridScale);
            }
        }

        private void Observe(GridScale? scale) {
            this.observedScale?.UnregisterPropertyChangedCallback(GridScale.StepProperty, this.stepToken);
            this.observedScale = scale;
            if (scale is not null) {
                this.stepToken = scale.RegisterPropertyChangedCallback(GridScale.StepProperty, (_, _) => this.Draw());
            }
            this.Draw();
        }

        private void Draw() {
            this.Lines.Data = null;
            var geometry = new GeometryGroup();
            double width = this.Surface.ActualWidth;
            double height = this.Surface.ActualHeight;
            double step = this.observedScale?.Step ?? 0;
            if (!double.IsFinite(step) || step < GridScale.MinimumStep) {
                return;
            }
            for (double x = step; x < width; x += step) {
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, height) });
            }
            for (double y = step; y < height; y += step) {
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(width, y) });
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
            double step = this.observedScale?.Step ?? 0;
            if (!double.IsFinite(step) || step < GridScale.MinimumStep) {
                return;
            }
            double column = Math.Round(point.X / step);
            double row = Math.Round(point.Y / step);
            double dx = column >= 1 && column * step < this.Surface.ActualWidth ? Math.Abs(point.X - column * step) : double.PositiveInfinity;
            double dy = row >= 1 && row * step < this.Surface.ActualHeight ? Math.Abs(point.Y - row * step) : double.PositiveInfinity;
            if (Math.Min(dx, dy) > 4 || !this.Surface.CapturePointer(args.Pointer)) {
                return;
            }

            this.pointerId = args.Pointer.PointerId;
            this.vertical = dx <= dy;
            this.lineIndex = this.vertical ? column : row;
            this.grabOffset = (this.vertical ? point.X : point.Y) - this.lineIndex * step;
            args.Handled = true;
        }

        private void MoveDrag(object sender, PointerRoutedEventArgs args) {
            if (this.pointerId != args.Pointer.PointerId || this.observedScale is not { } scale) {
                return;
            }
            var point = args.GetCurrentPoint(this.Surface).Position;
            double coordinate = this.vertical ? point.X : point.Y;
            scale.Step = Math.Max(GridScale.MinimumStep, (coordinate - this.grabOffset) / this.lineIndex);
            args.Handled = true;
        }
    }
}