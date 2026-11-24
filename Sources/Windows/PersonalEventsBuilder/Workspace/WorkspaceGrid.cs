using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

namespace PersonalEventsBuilder.Workspace {
    internal readonly record struct GridArea(int Row, int Column, int RowSpan = 1, int ColumnSpan = 1) {
        internal int Bottom => this.Row + this.RowSpan;
        internal int Right => this.Column + this.ColumnSpan;

        internal bool Contains(int row, int column) =>
            row >= this.Row && row < this.Bottom && column >= this.Column && column < this.Right;
    }

    internal static class WorkspaceGrid {
        internal const int MinimumColumns = 8;
        internal const double ColumnWidth = 96;
        internal const double RowHeight = 28;

        internal static GeometryGroup CreateLines(
            GridArea viewport,
            Point offset,
            Func<int, int, bool> isInsideRowSpan,
            Func<int, int, bool> isInsideColumnSpan
        ) {
            var geometryGroup = new GeometryGroup();
            for (var row = viewport.Row; row <= viewport.Bottom; row++) {
                var y = row * RowHeight - offset.Y;
                AddBoundary(
                    geometryGroup,
                    viewport.Column,
                    viewport.Right,
                    column => isInsideRowSpan(row, column),
                    column => new Point(column * ColumnWidth - offset.X, y)
                );
            }

            for (var column = viewport.Column; column <= viewport.Right; column++) {
                var x = column * ColumnWidth - offset.X;
                AddBoundary(
                    geometryGroup,
                    viewport.Row,
                    viewport.Bottom,
                    row => isInsideColumnSpan(row, column),
                    row => new Point(x, row * RowHeight - offset.Y)
                );
            }

            return geometryGroup;
        }

        private static void AddBoundary(
            GeometryGroup geometryGroup,
            int start,
            int end,
            Func<int, bool> isInsideSpan,
            Func<int, Point> getPoint
        ) {
            var runStart = start;
            for (var track = start; track <= end; track++) {
                if (track == end || isInsideSpan(track)) {
                    if (track > runStart) {
                        geometryGroup.Children.Add(new LineGeometry { StartPoint = getPoint(runStart), EndPoint = getPoint(track) });
                    }

                    runStart = track + 1;
                }
            }
        }

        internal static void Place(FrameworkElement element, GridArea gridArea) {
            var bounds = GetBounds(gridArea);
            Canvas.SetLeft(element, bounds.X);
            Canvas.SetTop(element, bounds.Y);
            element.Width = bounds.Width;
            element.Height = bounds.Height;
        }

        internal static Rect GetBounds(GridArea gridArea) => new(
            gridArea.Column * ColumnWidth,
            gridArea.Row * RowHeight,
            gridArea.ColumnSpan * ColumnWidth,
            gridArea.RowSpan * RowHeight
        );
    }
}