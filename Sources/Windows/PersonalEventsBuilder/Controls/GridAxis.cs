using System;

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
            new PropertyMetadata(48.0, (sender, _) => ((GridAxis)sender).Changed?.Invoke())
        );

        public int Count {
            get => this.count;
            set {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                if (this.count != value) {
                    this.count = value;
                    this.FirstVisibleIndex = Math.Min(this.FirstVisibleIndex, Math.Max(0, value - 1));
                    this.Changed?.Invoke();
                }
            }
        }
        public int FirstVisibleIndex { get; private set; }

        private int count = 1;
        internal event Action? Changed;

        internal int Capacity(double length) => (int)Math.Min(this.Count, Math.Floor(length / this.Step));

        internal void Scroll(int delta, int capacity) {
            if (capacity == 0) {
                return;
            }
            int index = (int)Math.Clamp((long)this.FirstVisibleIndex + delta, 0, this.Count - capacity);
            if (index != this.FirstVisibleIndex) {
                this.FirstVisibleIndex = index;
                this.Changed?.Invoke();
            }
        }

        internal const double MinimumStep = 8;
    }
}