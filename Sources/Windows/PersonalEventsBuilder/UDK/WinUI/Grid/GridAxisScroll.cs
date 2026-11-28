using System;

using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;

using UDK.WinUI.Layout;

namespace UDK.WinUI.Grid {
    public sealed class GridAxisScroll : IScrollAxisBehavior {
        public GridAxis? Axis { get; set; }

        public IDisposable Attach(ScrollAxisContext context) =>
            new Connection(this.Axis ?? throw new InvalidOperationException("GridAxisScroll requires an Axis."), context);

        private sealed class Connection : IDisposable {
            private readonly GridAxis axis;
            private readonly ScrollAxisContext context;
            private int wheelRemainder;

            public Connection(GridAxis axis, ScrollAxisContext context) {
                this.axis = axis;
                this.context = context;
                this.UpdateBar();
                axis.Changed += this.UpdateBar;
                context.ScrollBar.Scroll += this.OnScroll;
                context.Wheel += this.OnWheel;
            }

            public void Dispose() {
                this.axis.Changed -= this.UpdateBar;
                this.context.ScrollBar.Scroll -= this.OnScroll;
                this.context.Wheel -= this.OnWheel;
            }

            private void UpdateBar() {
                var bar = this.context.ScrollBar;
                bar.Maximum = this.axis.MaximumOffset;
                bar.ViewportSize = this.axis.ViewportCapacity;
                bar.LargeChange = Math.Max(1, this.axis.ViewportCapacity);
                bar.Value = this.axis.FirstVisibleIndex;
                bar.Visibility = this.axis.ViewportCapacity > 0 && this.axis.MaximumOffset > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            private void OnScroll(object sender, ScrollEventArgs args) => this.axis.SetGlobalOffset((int)Math.Round(args.NewValue));

            private void OnWheel(object sender, PointerRoutedEventArgs args) {
                var modifiers = args.KeyModifiers;
                if (modifiers != VirtualKeyModifiers.Shift && modifiers != (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)) {
                    this.wheelRemainder = 0;
                    return;
                }
                args.Handled = true;
                var pointer = args.GetCurrentPoint(this.context.ScrollBar).Properties;
                int delta = pointer.IsHorizontalMouseWheel ? pointer.MouseWheelDelta : -pointer.MouseWheelDelta;
                if (this.context.ScrollBar.Visibility != Visibility.Visible ||
                    (this.axis.FirstVisibleIndex == 0 && delta < 0) || (this.axis.FirstVisibleIndex == this.axis.MaximumOffset && delta > 0)) {
                    this.wheelRemainder = 0;
                    return;
                }
                // У колеса высокого разрешения один шаг может прийти несколькими событиями.
                const int wheelDeltaPerColumn = 120;
                this.wheelRemainder += delta;
                this.axis.SetGlobalOffset(this.axis.FirstVisibleIndex + this.wheelRemainder / wheelDeltaPerColumn);
                this.wheelRemainder %= wheelDeltaPerColumn;
            }
        }
    }
}