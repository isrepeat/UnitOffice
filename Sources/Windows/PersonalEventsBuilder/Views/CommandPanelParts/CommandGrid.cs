using System.Linq;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.System;

namespace PersonalEventsBuilder.Views.CommandPanelParts {
    public sealed class CommandGrid : Grid, UDK.WinUI.Navigation.IKeyboardNavigationArea, UDK.WinUI.Navigation.INavigationSurface {
        private System.Collections.Generic.IEnumerable<Controls.PlaceholderButton> AvailableButtons =>
            this.Children.OfType<Controls.PlaceholderButton>().Where(button => button.IsEnabled && button.Visibility == Visibility.Visible);

        UDK.WinUI.Grid.GridArea UDK.WinUI.Navigation.INavigationSurface.NavigationBounds =>
            new(0, 0, this.ColumnDefinitions.Count, this.RowDefinitions.Count);

        System.Collections.Generic.IEnumerable<UDK.WinUI.Grid.GridArea> UDK.WinUI.Navigation.INavigationSurface.NavigationRegions =>
            this.AvailableButtons.Select(BoundsOf);

        private Controls.PlaceholderButton? lastButton;
        private readonly UDK.WinUI.Navigation.NavigationCoordinator coordinator = new();
        private bool moving;

        public CommandGrid() {
            this.AddHandler(PointerPressedEvent, new PointerEventHandler(this.OnButtonPressed), true);
            this.GotFocus += (_, _) => {
                if (FocusManager.GetFocusedElement(this.XamlRoot) is Controls.PlaceholderButton button && this.Children.Contains(button)) {
                    this.Select(button);
                }
            };
            this.LosingFocus += (_, args) => {
                if (args.NewFocusedElement is UIElement next && !this.Children.Contains(next)) {
                    this.lastButton?.SetActive(false);
                }
            };
        }

        public bool FocusArea() {
            var button = this.AvailableButtons.FirstOrDefault(item => item == this.lastButton) ?? this.AvailableButtons.FirstOrDefault();
            return button?.Focus(FocusState.Keyboard) == true;
        }

        public void MoveFocus(VirtualKey direction) {
            if (FocusManager.GetFocusedElement(this.XamlRoot) is not Button current || !this.Children.Contains(current)) {
                this.FocusArea();
                return;
            }
            var area = BoundsOf(current);
            if (this.coordinator.FindNext(this, area, direction) is not { } hit) {
                return;
            }
            var next = this.AvailableButtons.First(button => BoundsOf(button).Contains(hit));
            this.moving = true;
            try {
                if (next.Focus(FocusState.Keyboard)) {
                    this.Select(next);
                    this.coordinator.CommitMove(hit, direction);
                    next.StartBringIntoView();
                }
            } finally {
                this.moving = false;
            }
        }

        private static UDK.WinUI.Grid.GridArea BoundsOf(FrameworkElement button) =>
            new(GetColumn(button), GetRow(button), GetColumnSpan(button), GetRowSpan(button));

        private void Select(Controls.PlaceholderButton button) {
            if (button != this.lastButton) {
                this.lastButton?.SetActive(false);
                if (!this.moving) {
                    this.coordinator.Remember(new(GetColumn(button), GetRow(button)));
                }
                this.lastButton = button;
            }
            button.SetActive(true);
        }

        private void OnButtonPressed(object sender, PointerRoutedEventArgs args) {
            var point = args.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) {
                return;
            }
            var source = args.OriginalSource as DependencyObject;
            while (source is not null && source != this && source is not Controls.PlaceholderButton) {
                source = VisualTreeHelper.GetParent(source);
            }
            if (source is not Controls.PlaceholderButton button || !this.Children.Contains(button)) {
                return;
            }

            // Учитываем фактические размеры строк и столбцов, включая интервалы между ними.
            double x = this.Padding.Left + this.BorderThickness.Left;
            int column = 0;
            while (column + 1 < this.ColumnDefinitions.Count && point.Position.X >= x + this.ColumnDefinitions[column].ActualWidth + this.ColumnSpacing / 2) {
                x += this.ColumnDefinitions[column++].ActualWidth + this.ColumnSpacing;
            }
            double y = this.Padding.Top + this.BorderThickness.Top;
            int row = 0;
            while (row + 1 < this.RowDefinitions.Count && point.Position.Y >= y + this.RowDefinitions[row].ActualHeight + this.RowSpacing / 2) {
                y += this.RowDefinitions[row++].ActualHeight + this.RowSpacing;
            }
            this.Select(button);
            this.coordinator.Remember(BoundsOf(button).Closest(new(column, row)));
        }
    }
}