using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed class PlaceholderLines : UserControl {
        public static readonly DependencyProperty LineWidthsProperty = DependencyProperty.Register(
            nameof(LineWidths), typeof(string), typeof(PlaceholderLines),
            new PropertyMetadata("33", (sender, _) => ((PlaceholderLines)sender).Rebuild()));

        private readonly StackPanel lines = new() { Spacing = 6 };

        public string LineWidths {
            get => (string)this.GetValue(LineWidthsProperty);
            set => this.SetValue(LineWidthsProperty, value);
        }

        public PlaceholderLines() {
            this.IsTabStop = false;
            this.IsHitTestVisible = false;
            this.VerticalAlignment = VerticalAlignment.Center;
            this.Opacity = 0.32;
            this.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WorkspaceTextBrush"];
            this.Content = this.lines;
            this.Rebuild();
        }

        private void Rebuild() {
            this.lines.Children.Clear();
            foreach (string token in (this.LineWidths ?? "").Split(',')) {
                if (!int.TryParse(token, out int width) || width is < 1 or > 100) {
                    continue;
                }
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - width, GridUnitType.Star) });
                var stroke = new Rectangle { Height = 3 };
                stroke.SetBinding(Shape.FillProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
                row.Children.Add(stroke);
                this.lines.Children.Add(row);
            }
        }
    }
}