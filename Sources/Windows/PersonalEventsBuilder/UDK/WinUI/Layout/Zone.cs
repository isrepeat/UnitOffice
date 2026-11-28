using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;

namespace UDK.WinUI.Layout {
    public enum ZoneDirection { Rows, Columns }
    public sealed class Splitter : DependencyObject {
        public Style? Style { get; set; }
    }

    [ContentProperty(Name = nameof(Items))]
    public sealed class Zone : DependencyObject {
        public Style? Style { get; set; }

        public double MaxSize { get; set; } = double.PositiveInfinity;

        public double MinSize { get; set; }

        public double Spacing { get; set; } = 5;

        public Brush? Background { get; set; }

        public GridLength Size { get; set; } = new(1, GridUnitType.Star);

        public ZoneDirection Direction { get; set; }

        public bool SequentialScroll { get; set; }

        public string Title { get; set; } = string.Empty;

        public IList<DependencyObject> Items { get; } = new List<DependencyObject>();
    }
}