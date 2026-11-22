using System;

using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class EqualWidthColumnsPanel : Panel {
        private const double ColumnSpacing = 4;

        public EqualWidthColumnsPanel() {
            InitializeComponent();
        }

        protected override Size MeasureOverride(Size availableSize) {
            var children = this.Children;
            if (children.Count == 0) {
                return new Size();
            }

            var gapWidth = (children.Count - 1) * ColumnSpacing;
            var columnWidth = Math.Max(0, (availableSize.Width - gapWidth) / children.Count);
            var desiredWidth = 0.0;
            var desiredHeight = 0.0;
            foreach (var child in children) {
                child.Measure(new Size(columnWidth, availableSize.Height));
                desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
                desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
            }

            return new Size(desiredWidth * children.Count + gapWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize) {
            var children = this.Children;
            if (children.Count == 0) {
                return finalSize;
            }

            var columnWidth = Math.Max(0, (finalSize.Width - (children.Count - 1) * ColumnSpacing) / children.Count);
            for (var index = 0; index < children.Count; index++) {
                var x = index * (columnWidth + ColumnSpacing);
                children[index].Arrange(new Rect(x, 0, columnWidth, finalSize.Height));
            }

            return finalSize;
        }
    }
}