using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class WordPreview : UserControl {
        public string? PreviewText {
            get => (string?)this.GetValue(PreviewTextProperty);
            set => this.SetValue(PreviewTextProperty, value);
        }
        public static readonly DependencyProperty PreviewTextProperty = DependencyProperty.Register(
            nameof(PreviewText), typeof(string), typeof(WordPreview), new PropertyMetadata(null));

        public WordPreview() {
            this.InitializeComponent();
        }
    }
}