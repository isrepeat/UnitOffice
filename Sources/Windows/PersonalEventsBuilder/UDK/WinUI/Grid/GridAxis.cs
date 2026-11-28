using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UDK.WinUI.Grid {
    public sealed class GridAxis : DependencyObject {
        internal readonly struct ColumnWindow {
            public int First { get; }
            public int Count { get; }
            public double Left { get; }
            public int Slot { get; }
            public int Slots { get; }

            public ColumnWindow(int first, int count, double left, int slot, int slots) {
                this.First = first;
                this.Count = count;
                this.Left = left;
                this.Slot = slot;
                this.Slots = slots;
            }
        }
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
            new PropertyMetadata(48.0, (sender, _) => ((GridAxis)sender).UpdateViewport(stepChanged: true))
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

        internal int ViewportCapacity => this.previousCapacity;

        internal int MaximumOffset => Math.Max(0, this.Count - Math.Max(1, this.ViewportCapacity));

        internal double Origin { get; private set; }

        internal double DisplayIndex { get; private set; }

        private int count = 1;
        private int previousCapacity;
        private double animationStart;
        private double scrollPosition;
        internal const double MinimumStep = 8;
        internal event Action? Changed;
        private readonly Stopwatch animation = new();
        private readonly Dictionary<object, (double Start, double End, bool CenterOnStepChange)> viewports = new();

        internal void SetViewport(object owner, double length, double start = 0, bool centerOnStepChange = false) {
            if (length <= 0) {
                if (!this.viewports.Remove(owner)) {
                    return;
                }
            } else {
                var viewport = (start, start + length, centerOnStepChange);
                if (this.viewports.TryGetValue(owner, out var previous) && previous == viewport) {
                    return;
                }
                this.viewports[owner] = viewport;
            }
            this.UpdateViewport();
        }

        internal int Capacity(double length, double offset) =>
            (int)Math.Min(this.Count, Math.Floor(Math.Max(0, length - offset) / this.Step));

        internal ColumnWindow GetColumns(double start, double width) {
            // Положение полосы на экране и индекс колонки в данных меняются независимо.
            double origin = this.Origin - start;
            int slot = (int)Math.Clamp(Math.Ceiling(-origin / this.Step), 0, this.previousCapacity);
            double left = origin + slot * this.Step;
            int slots = Math.Min(this.previousCapacity - slot, this.Capacity(width, left));
            int first = this.FirstVisibleIndex + slot;
            return new ColumnWindow(first, slots, left, slot, slots);
        }

        internal void SetGlobalOffset(int index) => this.SetScrollIndex(Math.Clamp(index, 0, this.MaximumOffset));

        internal int VisibleCount(double length, double offset) =>
            Math.Min(this.Count - this.FirstVisibleIndex, this.Capacity(length, offset));

        internal void Scroll(int delta, int capacity) {
            if (capacity == 0) {
                return;
            }
            int index = (int)Math.Clamp((long)this.FirstVisibleIndex + delta, 0, Math.Max(this.FirstVisibleIndex, this.Count - capacity));
            this.SetScrollIndex(index);
        }

        private void UpdateViewport(bool stepChanged = false) {
            // Центрируем сетку один раз в общем диапазоне участвующих поверхностей.
            double start = this.viewports.Values.Select(viewport => viewport.Start).DefaultIfEmpty().Min();
            double end = this.viewports.Values.Select(viewport => viewport.End).DefaultIfEmpty().Max();
            double width = end - start;
            int capacity = this.Capacity(width, 0);
            this.Origin = start + (width - capacity * this.Step) / 2;
            // Сохраняем половину ячейки между соседними шагами перетаскивания.
            if (stepChanged && this.previousCapacity > 0 && capacity > 0 && this.viewports.Values.Any(viewport => viewport.CenterOnStepChange)) {
                this.scrollPosition += (this.previousCapacity - capacity) / 2.0;
            }
            this.scrollPosition = Math.Clamp(this.scrollPosition, 0, Math.Max(0, this.Count - Math.Max(1, capacity)));
            this.FirstVisibleIndex = (int)Math.Floor(this.scrollPosition);
            this.StopAnimation();
            this.previousCapacity = capacity;
            this.Changed?.Invoke();
        }

        private void SetScrollIndex(int index) {
            if (index != this.FirstVisibleIndex) {
                this.scrollPosition = index;
                this.FirstVisibleIndex = index;
                this.animationStart = this.DisplayIndex;
                if (!this.animation.IsRunning) {
                    CompositionTarget.Rendering += this.OnAnimationFrame;
                }
                this.animation.Restart();
                this.Changed?.Invoke();
            }
        }

        private void OnAnimationFrame(object? sender, object args) {
            double progress = Math.Min(1, this.animation.Elapsed.TotalMilliseconds / 150);
            this.DisplayIndex = this.FirstVisibleIndex + (this.animationStart - this.FirstVisibleIndex) * Math.Pow(1 - progress, 3);
            if (progress >= 1) {
                this.StopAnimation();
            }
            this.Changed?.Invoke();
        }

        private void StopAnimation() {
            if (this.animation.IsRunning) {
                CompositionTarget.Rendering -= this.OnAnimationFrame;
                this.animation.Reset();
            }
            this.DisplayIndex = this.FirstVisibleIndex;
        }
    }
}