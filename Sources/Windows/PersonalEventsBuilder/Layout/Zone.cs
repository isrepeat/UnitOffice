using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;

namespace PersonalEventsBuilder.Layout {
    public enum ZoneDirection { Rows, Columns }

    [ContentProperty(Name = nameof(Items))]
    public sealed class Zone : DependencyObject {
        public Style? Style { get; set; }
        public Style? LeadingSplitterStyle { get; set; }
        public Brush? Background { get; set; }
        public GridLength Size { get; set; } = new(1, GridUnitType.Star);
        public double MinSize { get; set; }
        public double MaxSize { get; set; } = double.PositiveInfinity;
        public ZoneDirection Direction { get; set; }
        public bool AllowResize { get; set; }
        public double Spacing { get; set; } = 5;
        public IList<DependencyObject> Items { get; } = new List<DependencyObject>();
    }
}