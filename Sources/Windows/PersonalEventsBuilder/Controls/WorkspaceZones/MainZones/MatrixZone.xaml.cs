using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

using Windows.System;
using Windows.UI.Core;
using Windows.Foundation;

using MatrixAddress = PersonalEventsBuilder.Workspace.Matrix.MatrixAddress;
using MatrixCell = PersonalEventsBuilder.Workspace.Matrix.MatrixCell;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZones {
    public sealed partial class MatrixZone : UserControl {
        private const double RowHeight = 28;
        private const double ColumnWidth = 144;
        private Dictionary<MatrixAddress, MatrixCell> fields = new();
        private int rowCount;
        private int columnCount;
        private MatrixAddress? activeAddress;
        private bool isEditing;

        public MatrixZone() {
            this.InitializeComponent();
            this.LoadMatrix(4, 12, []);
        }

        internal void LoadMatrix(int rows, int columns, MatrixCell[] cells) {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
            var newFields = cells.ToDictionary(cell => cell.Address);
            foreach (var address in newFields.Keys) {
                if (address.Row < 0 || address.Row >= rows || address.Column < 0 || address.Column >= columns) {
                    throw new ArgumentOutOfRangeException(nameof(cells), address, "Cell address is outside the matrix.");
                }
            }

            this.FinishEditing(commit: false);
            this.activeAddress = null;
            this.SelectionOutline.Visibility = Visibility.Collapsed;
            this.fields = newFields;
            this.rowCount = rows;
            this.columnCount = columns;
            this.MatrixSurface.Width = columns * ColumnWidth;
            this.MatrixSurface.Height = rows * RowHeight;
            this.FieldLabels.Width = this.MatrixSurface.Width;
            this.FieldLabels.Height = this.MatrixSurface.Height;
            this.MatrixViewport.ChangeView(0, 0, null, disableAnimation: true);
            this.DrawViewport();
        }

        private bool IsInBounds(MatrixAddress address) =>
            address.Row >= 0 && address.Row < this.rowCount && address.Column >= 0 && address.Column < this.columnCount;

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs args) => this.DrawViewport();

        private void OnViewportChanged(object sender, ScrollViewerViewChangedEventArgs args) => this.DrawViewport();

        private void DrawViewport() {
            var viewport = this.MatrixViewport;
            var left = viewport.HorizontalOffset;
            var top = viewport.VerticalOffset;
            var right = Math.Min(this.MatrixSurface.Width, left + viewport.ViewportWidth);
            var bottom = Math.Min(this.MatrixSurface.Height, top + viewport.ViewportHeight);
            if (!(right > left && bottom > top)) {
                return;
            }

            var geometry = new GeometryGroup();
            var firstColumn = (int)Math.Ceiling(left / ColumnWidth);
            var lastColumn = (int)Math.Floor(right / ColumnWidth);
            for (var column = firstColumn; column <= lastColumn; column++) {
                var x = column * ColumnWidth - left;
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(x, 0), EndPoint = new Point(x, bottom - top) });
            }

            var firstRow = (int)Math.Ceiling(top / RowHeight);
            var lastRow = (int)Math.Floor(bottom / RowHeight);
            for (var row = firstRow; row <= lastRow; row++) {
                var y = row * RowHeight - top;
                geometry.Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(right - left, y) });
            }

            Canvas.SetLeft(this.GridLines, left);
            Canvas.SetTop(this.GridLines, top);
            this.GridLines.Data = geometry;

            this.FieldLabels.Children.Clear();
            var fieldStyle = (Style)this.Resources["MatrixFieldText"];
            foreach (var field in this.fields.Values) {
                var bounds = GetCellBounds(field.Address);
                if (bounds.Right <= left || bounds.Left >= right || bounds.Bottom <= top || bounds.Top >= bottom) {
                    continue;
                }

                var label = new TextBlock {
                    Style = fieldStyle,
                    Text = field.Text.Length == 0 ? field.Label : field.Text,
                };
                PlaceElement(label, bounds);
                this.FieldLabels.Children.Add(label);
            }
        }

        private static Rect GetCellBounds(MatrixAddress address) =>
            new(address.Column * ColumnWidth, address.Row * RowHeight, ColumnWidth, RowHeight);

        private static void PlaceElement(FrameworkElement element, Rect bounds) {
            Canvas.SetLeft(element, bounds.X);
            Canvas.SetTop(element, bounds.Y);
            element.Width = bounds.Width;
            element.Height = bounds.Height;
        }

        private MatrixAddress? GetFieldAt(Point point) {
            if (point.X < 0 || point.Y < 0 || point.X >= this.MatrixSurface.Width || point.Y >= this.MatrixSurface.Height) {
                return null;
            }

            var address = new MatrixAddress((int)(point.Y / RowHeight), (int)(point.X / ColumnWidth));
            return this.fields.ContainsKey(address) ? address : null;
        }

        private void OnMatrixTapped(object sender, TappedRoutedEventArgs args) {
            if (this.GetFieldAt(args.GetPosition(this.MatrixSurface)) is { } address
                && (!this.isEditing || this.activeAddress != address)) {
                this.SelectField(address);
            }
        }

        private void OnMatrixDoubleTapped(object sender, DoubleTappedRoutedEventArgs args) {
            if (!this.isEditing && this.GetFieldAt(args.GetPosition(this.MatrixSurface)) is { } address) {
                this.SelectField(address);
                this.BeginEditing();
            }
        }

        private void SelectField(MatrixAddress address) {
            this.FinishEditing(commit: true);
            this.activeAddress = address;
            var bounds = GetCellBounds(address);
            PlaceElement(this.SelectionOutline, bounds);
            this.SelectionOutline.Visibility = Visibility.Visible;
            this.Focus(FocusState.Programmatic);

            var viewport = this.MatrixViewport;
            var left = Math.Clamp(viewport.HorizontalOffset, Math.Min(bounds.Right - viewport.ViewportWidth, bounds.Left), bounds.Left);
            var top = Math.Clamp(viewport.VerticalOffset, Math.Min(bounds.Bottom - viewport.ViewportHeight, bounds.Top), bounds.Top);
            viewport.ChangeView(Math.Max(0, left), Math.Max(0, top), null, disableAnimation: true);
        }

        private void BeginEditing() {
            if (this.activeAddress is not { } address) {
                return;
            }

            this.isEditing = true;
            this.CellEditor.Text = this.fields[address].Text;
            PlaceElement(this.CellEditor, GetCellBounds(address));
            this.CellEditor.Visibility = Visibility.Visible;
            this.CellEditor.Focus(FocusState.Programmatic);
            this.CellEditor.SelectAll();
        }

        private void FinishEditing(bool commit) {
            if (!this.isEditing || this.activeAddress is not { } address) {
                return;
            }

            this.isEditing = false;
            if (commit) {
                this.fields[address] = this.fields[address] with { Text = this.CellEditor.Text };
                this.DrawViewport();
            }

            this.CellEditor.Visibility = Visibility.Collapsed;
        }

        private void OnEditorLostFocus(object sender, RoutedEventArgs args) => this.FinishEditing(commit: true);

        private void OnMatrixKeyDown(object sender, KeyRoutedEventArgs args) {
            if (this.isEditing && args.Key is VirtualKey.Enter or VirtualKey.Escape) {
                this.FinishEditing(commit: args.Key == VirtualKey.Enter);
                this.Focus(FocusState.Keyboard);
                args.Handled = true;
                return;
            }

            if (this.isEditing && args.Key != VirtualKey.Tab) {
                return;
            }

            if (!this.isEditing && this.activeAddress.HasValue && args.Key is VirtualKey.Enter or VirtualKey.F2) {
                this.BeginEditing();
                args.Handled = true;
                return;
            }

            var backwards = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
            if (this.FindNextField(args.Key, backwards) is { } target) {
                this.SelectField(target);
                args.Handled = true;
            } else if (args.Key == VirtualKey.Tab && this.isEditing) {
                this.FinishEditing(commit: true);
                this.Focus(FocusState.Keyboard);
            }
        }

        private MatrixAddress? FindNextField(VirtualKey key, bool backwards) {
            var addresses = this.fields.Keys.Where(this.IsInBounds);
            if (key == VirtualKey.Tab) {
                var ordered = addresses.OrderBy(address => (address.Row, address.Column));
                var candidates = backwards ? ordered.Reverse() : ordered;
                foreach (var address in candidates) {
                    if (this.activeAddress is not { } current
                        || (address.Row, address.Column).CompareTo((current.Row, current.Column)) * (backwards ? -1 : 1) > 0) {
                        return address;
                    }
                }

                return null;
            }

            if (this.activeAddress is not { } active) {
                return null;
            }

            var next = key switch {
                VirtualKey.Left => addresses.Where(address => address.Row == active.Row && address.Column < active.Column)
                    .OrderByDescending(address => address.Column),
                VirtualKey.Right => addresses.Where(address => address.Row == active.Row && address.Column > active.Column)
                    .OrderBy(address => address.Column),
                VirtualKey.Up => addresses.Where(address => address.Column == active.Column && address.Row < active.Row)
                    .OrderByDescending(address => address.Row),
                VirtualKey.Down => addresses.Where(address => address.Column == active.Column && address.Row > active.Row)
                    .OrderBy(address => address.Row),
                _ => Enumerable.Empty<MatrixAddress>(),
            };

            return next.Select(address => (MatrixAddress?)address).FirstOrDefault();
        }
    }
}