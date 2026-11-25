using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class GridPoint : UserControl, IGridNavigationTarget {
        public GridPoint() {
            this.InitializeComponent();
        }

        public void UpdateNavigationState(int column, int row, bool isActive) {
            this.ActivePoint.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}