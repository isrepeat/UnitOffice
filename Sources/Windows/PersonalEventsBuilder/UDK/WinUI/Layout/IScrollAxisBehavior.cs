using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace UDK.WinUI.Layout {
    public interface IScrollAxisBehavior {
        IDisposable Attach(ScrollAxisContext context);
    }

    public sealed class ScrollAxisContext {
        public ScrollBar ScrollBar { get; }
        private readonly UIElement content;
        private readonly UIElement container;

        internal ScrollAxisContext(ScrollBar scrollBar, UIElement content, UIElement container) {
            this.ScrollBar = scrollBar;
            this.content = content;
            this.container = container;
        }

        // В содержимом перехватываем колесо до ScrollViewer, в контейнере — над полосой и заголовками.
        public event PointerEventHandler Wheel {
            add {
                this.content.PointerWheelChanged += value;
                this.container.PointerWheelChanged += value;
            }
            remove {
                this.content.PointerWheelChanged -= value;
                this.container.PointerWheelChanged -= value;
            }
        }
    }
}