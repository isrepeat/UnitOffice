using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed class CandidateRowView : ContentControl {
        public CandidateRowView() {
            this.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            this.DataContextChanged += this.OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) {
            var columns = new EqualWidthColumnsPanel();
            if (args.NewValue is IReadOnlyList<string> cells) {
                foreach (var cell in cells) {
                    columns.Children.Add(new TextBlock {
                        Text = cell,
                        FontSize = 12,
                        Margin = new Thickness(6, 4, 6, 4),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                }
            }

            this.Content = columns;
        }
    }
}