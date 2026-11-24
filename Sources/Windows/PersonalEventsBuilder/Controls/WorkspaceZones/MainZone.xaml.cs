using System;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

using Windows.System;
using Windows.UI.Core;
using Windows.Foundation;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;
using NavigationDirection = PersonalEventsBuilder.Workspace.NavigationDirection;
using NavigationTarget = PersonalEventsBuilder.Workspace.NavigationTarget;
using WorkspaceGrid = PersonalEventsBuilder.Workspace.WorkspaceGrid;
using WorkspaceLayout = PersonalEventsBuilder.Workspace.WorkspaceLayout;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed record GridDrawing(Geometry Geometry, double Left, double Top);

    public sealed partial class MainZone : UserControl {
        public WorkspaceLayout? Layout {
            get => (WorkspaceLayout?)this.GetValue(LayoutProperty);
            set => this.SetValue(LayoutProperty, value);
        }
        public GridDrawing? GridDrawing {
            get => (GridDrawing?)this.GetValue(GridDrawingProperty);
            private set => this.SetValue(GridDrawingProperty, value);
        }

        private NavigationTarget? activeTarget;
        private double preferredColumn;

        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
            nameof(Layout),
            typeof(WorkspaceLayout),
            typeof(MainZone),
            new PropertyMetadata(null, OnLayoutChanged)
        );

        public static readonly DependencyProperty GridDrawingProperty = DependencyProperty.Register(
            nameof(GridDrawing),
            typeof(GridDrawing),
            typeof(MainZone),
            new PropertyMetadata(null)
        );

        public MainZone() {
            this.InitializeComponent();
            this.UpdateSurfaceSize();
        }

        private static void OnLayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var mainZone = (MainZone)sender;
            if (args.OldValue is WorkspaceLayout oldLayout) {
                oldLayout.ExtentChanged -= mainZone.OnSectionExtentChanged;
            }

            mainZone.activeTarget?.Zone.LeaveTarget();
            mainZone.activeTarget = null;
            mainZone.FocusOutline.Visibility = Visibility.Collapsed;
            if (args.NewValue is WorkspaceLayout workspaceLayout) {
                workspaceLayout.ExtentChanged += mainZone.OnSectionExtentChanged;
            }

            mainZone.UpdateSurfaceSize();
            mainZone.DrawViewport();
        }

        private void OnWorkspaceSurfaceSizeChanged(object sender, SizeChangedEventArgs args) => this.DrawViewport();

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs args) => this.UpdateSurfaceSize();

        private void OnViewportChanged(object sender, ScrollViewerViewChangedEventArgs args) => this.DrawViewport();

        private void UpdateSurfaceSize() {
            var viewportWidth = this.WorkspaceViewport.ActualWidth;
            var viewportHeight = this.WorkspaceViewport.ActualHeight;
            var minimumColumns = Math.Max(WorkspaceGrid.MinimumColumns, this.Layout?.ColumnCount ?? 0);
            var columnCount = Math.Max(minimumColumns, (int)(viewportWidth / WorkspaceGrid.ColumnWidth));
            this.WorkspaceSurface.Width = columnCount * WorkspaceGrid.ColumnWidth;
            this.WorkspaceSurface.MinHeight = Math.Floor(viewportHeight / WorkspaceGrid.RowHeight) * WorkspaceGrid.RowHeight;
        }

        private void OnSectionExtentChanged() {
            this.UpdateSurfaceSize();
            if (this.activeTarget is { } navigationTarget) {
                if (this.Layout?.IsVisible(navigationTarget.Zone) == true) {
                    WorkspaceGrid.Place(this.FocusOutline, navigationTarget.Area);
                } else {
                    navigationTarget.Zone.LeaveTarget();
                    this.activeTarget = null;
                    this.FocusOutline.Visibility = Visibility.Collapsed;
                }
            }

            this.DrawViewport();
        }

        private void DrawViewport() {
            var viewport = this.WorkspaceViewport;
            var left = viewport.HorizontalOffset;
            var top = viewport.VerticalOffset;
            var right = Math.Min(this.WorkspaceSurface.ActualWidth, left + viewport.ViewportWidth);
            var bottom = Math.Min(this.WorkspaceSurface.ActualHeight, top + viewport.ViewportHeight);
            if (!(right > left && bottom > top)) {
                return;
            }

            var firstColumn = (int)(left / WorkspaceGrid.ColumnWidth);
            var lastColumn = (int)Math.Ceiling(right / WorkspaceGrid.ColumnWidth);
            var firstRow = (int)(top / WorkspaceGrid.RowHeight);
            var lastRow = (int)Math.Ceiling(bottom / WorkspaceGrid.RowHeight);
            var visibleArea = new GridArea(firstRow, firstColumn, lastRow - firstRow, lastColumn - firstColumn);
            var geometry = WorkspaceGrid.CreateLines(
                visibleArea,
                new Point(left, top),
                this.IsInsideRowSpan,
                this.IsInsideColumnSpan
            );

            this.GridDrawing = new GridDrawing(geometry, left, top);
            this.Layout?.UpdateViewport(visibleArea);
        }

        private bool IsInsideRowSpan(int row, int column) =>
            this.Layout?.IsInsideRowSpan(row, column) == true;

        private bool IsInsideColumnSpan(int row, int column) =>
            column > 0 && column * WorkspaceGrid.ColumnWidth < this.WorkspaceSurface.ActualWidth
                && this.Layout?.IsInsideColumnSpan(row, column) == true;

        private NavigationTarget? FindTargetAt(Point position) {
            if (position.X < 0 || position.Y < 0) {
                return null;
            }

            return this.Layout?.WorkspaceNavigation.FindAt(
                (int)(position.Y / WorkspaceGrid.RowHeight),
                (int)(position.X / WorkspaceGrid.ColumnWidth)
            );
        }

        private void OnWorkspacePointerPressed(object sender, PointerRoutedEventArgs args) {
            var pointerPoint = args.GetCurrentPoint(this.WorkspaceSurface);
            if (!pointerPoint.Properties.IsLeftButtonPressed
                || this.FindTargetAt(pointerPoint.Position).HasValue
                || this.Layout?.WorkspaceNavigation.FindNearest(pointerPoint.Position) is not { } navigationTarget) {
                return;
            }

            args.Handled = true;
            this.FocusPointerTarget(navigationTarget, pointerPoint.Position);
        }

        private void OnWorkspaceTapped(object sender, TappedRoutedEventArgs args) {
            var position = args.GetPosition(this.WorkspaceSurface);
            if (this.Layout?.WorkspaceNavigation.FindNearest(position) is { } navigationTarget
                && (this.activeTarget?.Zone.IsEditing != true || this.activeTarget != navigationTarget)) {
                this.FocusPointerTarget(navigationTarget, position);
            }
        }

        private void OnWorkspaceDoubleTapped(object sender, DoubleTappedRoutedEventArgs args) {
            var position = args.GetPosition(this.WorkspaceSurface);
            if (this.activeTarget?.Zone.IsEditing != true
                && this.FindTargetAt(position) is { } navigationTarget) {
                this.FocusPointerTarget(navigationTarget, position);
                navigationTarget.Zone.ActivateTarget(navigationTarget.Index);
            }
        }

        private void FocusPointerTarget(NavigationTarget navigationTarget, Point position) {
            var column = position.X / WorkspaceGrid.ColumnWidth;
            this.FocusTarget(navigationTarget, Math.Clamp(column, navigationTarget.Area.Column, navigationTarget.Area.Right));
        }

        private void FocusTarget(NavigationTarget navigationTarget, double? preferredColumn = null) {
            this.activeTarget?.Zone.LeaveTarget();
            this.activeTarget = navigationTarget;
            this.preferredColumn = preferredColumn ?? navigationTarget.Area.Column + navigationTarget.Area.ColumnSpan / 2.0;
            WorkspaceGrid.Place(this.FocusOutline, navigationTarget.Area);
            this.FocusOutline.Visibility = Visibility.Visible;
            this.Focus(FocusState.Programmatic);

            var bounds = WorkspaceGrid.GetBounds(navigationTarget.Area);
            var viewport = this.WorkspaceViewport;
            var left = GetVisibleOffset(viewport.HorizontalOffset, viewport.ViewportWidth, bounds.Left, bounds.Right);
            var top = GetVisibleOffset(viewport.VerticalOffset, viewport.ViewportHeight, bounds.Top, bounds.Bottom);
            viewport.ChangeView(left, top, null, disableAnimation: true);
        }

        private static double GetVisibleOffset(double offset, double viewportSize, double start, double end) =>
            Math.Max(0, Math.Clamp(offset, Math.Min(end - viewportSize, start), start));

        private void OnWorkspaceGotFocus(object sender, RoutedEventArgs args) {
            if (!ReferenceEquals(args.OriginalSource, this) || this.activeTarget.HasValue) {
                return;
            }

            var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            var direction = shift ? NavigationDirection.Previous : NavigationDirection.Next;
            if (this.Layout?.WorkspaceNavigation.FindNext(null, direction) is { } navigationTarget) {
                this.FocusTarget(navigationTarget);
            }
        }

        private void OnWorkspaceKeyDown(object sender, KeyRoutedEventArgs args) {
            if (this.activeTarget is { } target) {
                if (target.Zone.TryHandleEditorKey(args.Key)) {
                    this.Focus(FocusState.Keyboard);
                    args.Handled = true;
                    return;
                }

                if (target.Zone.IsEditing && args.Key != VirtualKey.Tab) {
                    return;
                }

                if (args.Key == VirtualKey.Enter || (args.Key == VirtualKey.F2 && target.Zone.CanEdit)) {
                    target.Zone.ActivateTarget(target.Index);
                    args.Handled = true;
                    return;
                }
            }

            var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            NavigationDirection? direction = args.Key switch {
                VirtualKey.Tab => shift ? NavigationDirection.Previous : NavigationDirection.Next,
                VirtualKey.Left => NavigationDirection.Left,
                VirtualKey.Right => NavigationDirection.Right,
                VirtualKey.Up => NavigationDirection.Up,
                VirtualKey.Down => NavigationDirection.Down,
                _ => null,
            };
            double? preferredColumn = this.activeTarget.HasValue && direction is NavigationDirection.Up or NavigationDirection.Down
                ? this.preferredColumn
                : null;
            if (direction.HasValue
                && this.Layout?.WorkspaceNavigation.FindNext(this.activeTarget, direction.Value, preferredColumn) is { } navigationTarget) {
                this.FocusTarget(navigationTarget, preferredColumn);
                args.Handled = true;
            } else if (args.Key == VirtualKey.Tab) {
                if (this.activeTarget?.Zone.IsEditing == true) {
                    this.activeTarget.Value.Zone.LeaveTarget();
                    this.Focus(FocusState.Keyboard);
                }

                this.activeTarget = null;
                this.FocusOutline.Visibility = Visibility.Collapsed;
            }
        }
    }
}