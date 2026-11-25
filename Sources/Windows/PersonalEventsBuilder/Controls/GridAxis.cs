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

        internal double CenterOffset { get; private set; }

        private readonly Dictionary<object, double> lengths = new();
        private int count = 1;
        internal event Action? Changed;

        internal void SetViewport(object owner, double length) {
            if (length <= 0) {
                if (!this.lengths.Remove(owner)) {
                    return;
                }
            } else {
                if (this.lengths.TryGetValue(owner, out var previous) && previous == length) {
                    return;
                }
                this.lengths[owner] = length;
            }
            this.UpdateViewport();
        }

        private void UpdateViewport() {
            // Выравниваем начало по широкой поверхности. Узкие показывают окно на той же сетке.
            double width = this.lengths.Values.DefaultIfEmpty().Max();
            this.CenterOffset = (width - this.Capacity(width, 0) * this.Step) / 2;
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