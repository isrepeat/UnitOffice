using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class FormInput : UserControl, IGridNavigationTarget {
        public GridArea Bounds { get; }
        public string Label { get; }

        public FormInput(int column, int row, int width, string label) {
            this.Bounds = new(column, row - 1, width, 2);
            this.Label = label;
            this.InitializeComponent();
            this.IsHitTestVisible = false;
        }

        public GridAddress FocusAddress(GridAddress address) => new(this.Bounds.Column, this.Bounds.Bottom - 1);
        public GridArea FocusArea(GridAddress address) => this.Bounds;

        public void SetActiveAddress(GridAddress? address) {
            this.IsHitTestVisible = address.HasValue;
            this.Editor.IsTabStop = address.HasValue;
            if (address.HasValue) {
                this.Editor.Focus(FocusState.Programmatic);
            }
        }
    }
}