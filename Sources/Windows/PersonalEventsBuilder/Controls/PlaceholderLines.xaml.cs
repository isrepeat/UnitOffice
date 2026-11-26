using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class PlaceholderLines : UserControl {
        public static readonly DependencyProperty LineWidthsProperty = DependencyProperty.Register(
            nameof(LineWidths), typeof(string), typeof(PlaceholderLines),
            new PropertyMetadata("33", (sender, _) => ((PlaceholderLines)sender).Rebuild()));

        public string LineWidths {
            get => (string)this.GetValue(LineWidthsProperty);
            set => this.SetValue(LineWidthsProperty, value);
        }

        public PlaceholderLines() {
            this.InitializeComponent();
            this.Rebuild();
        }

        private void Rebuild() {
            this.Lines.Children.Clear();
            foreach (string token in (this.LineWidths ?? "").Split(',')) {
                if (!int.TryParse(token, out int width) || width is < 1 or > 100) {
                    continue;
                }
                var row = (Grid)((DataTemplate)this.Resources["LineTemplate"]).LoadContent();
                row.ColumnDefinitions[0].Width = new GridLength(width, GridUnitType.Star);
                row.ColumnDefinitions[1].Width = new GridLength(100 - width, GridUnitType.Star);
                row.SetBinding(DataContextProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
                this.Lines.Children.Add(row);
            }
        }
    }
}