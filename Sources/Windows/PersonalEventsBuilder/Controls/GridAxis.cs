using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;

namespace PersonalEventsBuilder.Controls {
    public sealed class GridAxis : DependencyObject {
        public double Step {
            get => (double)this.GetValue(StepProperty);
            set {
                if (!double.IsFinite(value) || value < MinimumStep) {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                this.SetValue(StepProperty, value);
            }
        }
        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step),
            typeof(double),
            typeof(GridAxis),
            new PropertyMetadata(48.0, (sender, _) => ((GridAxis)sender).UpdateViewport())
        );

        public int Count {
            get => this.count;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                if (this.count != value) {
                    this.count = value;
                    this.UpdateViewport();
                }
            }
        }
        public int FirstVisibleIndex { get; private set; }

        internal double Origin { get; private set; }

        private readonly Dictionary<object, (double Start, double End)> viewports = new();
        private int count = 1;
        internal event Action? Changed;

        internal void SetViewport(object owner, double length, double start = 0) {
            if (length <= 0) {
                if (!this.viewports.Remove(owner)) {
                    return;
                }
            } else {
                if (this.viewports.TryGetValue(owner, out var previous) && previous == (start, start + length)) {
                    return;
                }
                this.viewports[owner] = (start, start + length);
            }
            this.UpdateViewport();
        }

        private void UpdateViewport() {
            // Центрируем сетку один раз в общем диапазоне участвующих поверхностей.
            double start = this.viewports.Values.Select(viewport => viewport.Start).DefaultIfEmpty().Min();
            double end = this.viewports.Values.Select(viewport => viewport.End).DefaultIfEmpty().Max();
            double width = end - start;
            this.Origin = start + (width - this.Capacity(width, 0) * this.Step) / 2;
            this.FirstVisibleIndex = Math.Min(this.FirstVisibleIndex, Math.Max(0, this.Count - 1));
            this.Changed?.Invoke();
        }

        internal int Capacity(double length, double offset) =>
            (int)Math.Min(this.Count, Math.Floor(Math.Max(0, length - offset) / this.Step));

        internal int VisibleCount(double length, double offset) =>
            Math.Min(this.Count - this.FirstVisibleIndex, this.Capacity(length, offset));

        internal void Scroll(int delta, int capacity) {
            if (capacity == 0) {
                return;
            }
            int index = (int)Math.Clamp((long)this.FirstVisibleIndex + delta, 0, Math.Max(this.FirstVisibleIndex, this.Count - capacity));
            if (index != this.FirstVisibleIndex) {
                this.FirstVisibleIndex = index;
                this.Changed?.Invoke();
            }
        }

        internal const double MinimumStep = 8;
    }
}