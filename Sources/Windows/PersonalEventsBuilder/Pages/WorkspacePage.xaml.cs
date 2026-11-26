using System;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

using Windows.System;
using Windows.UI.Core;

namespace PersonalEventsBuilder.Pages {
    public sealed partial class WorkspacePage : Page {
        public WorkspacePage() {
            this.InitializeComponent();
            this.PreviewKeyDown += this.OnWorkspaceKeyDown;
        }

        private void OnOpenPalette(object sender, RoutedEventArgs args) {
            this.PaletteDrawer.OpenPaneLength = Math.Min(336, this.ActualWidth);
            this.PaletteDrawer.IsPaneOpen = true;
            this.Palette.FocusCloseButton();
        }

        private void OnClosePalette(object? sender, EventArgs args) => this.PaletteDrawer.IsPaneOpen = false;

        private void OnPaletteClosed(SplitView sender, object args) {
            this.PaletteButton.Focus(FocusState.Programmatic);
        }

        private void OnWorkspaceKeyDown(object sender, KeyRoutedEventArgs args) {
            if (this.PaletteDrawer.IsPaneOpen) {
                if (args.Key == VirtualKey.Escape) {
                    this.PaletteDrawer.IsPaneOpen = false;
                    args.Handled = true;
                }
                return;
            }
            if (args.Key is not (VirtualKey.Tab or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)) {
                return;
            }
            if (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down) ||
                InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down)) {
                return;
            }
            var grid = (UDK.WinUI.Grid.GridNavigation)this.Workspace.Resources["EventNavigation"];
            UDK.WinUI.Navigation.IKeyboardNavigationArea? area = null;
            var element = FocusManager.GetFocusedElement(this.XamlRoot) as DependencyObject;
            while (element is not null && element != this) {
                if (element == this.Commands) {
                    area = this.Commands;
                    break;
                }
                if (element is Views.EventForm or Views.CandidatesTable) {
                    area = grid;
                    break;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            if (args.Key == VirtualKey.Tab) {
                UDK.WinUI.Navigation.IKeyboardNavigationArea destination = ReferenceEquals(area, grid) ? this.Commands : grid;
                args.Handled = true;
                destination.FocusArea();
            } else if (area is not null) {
                args.Handled = true;
                area.MoveFocus(args.Key);
            }
        }
    }
}