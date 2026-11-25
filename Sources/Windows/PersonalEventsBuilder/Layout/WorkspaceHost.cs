using System;
using System.Linq;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace PersonalEventsBuilder.Layout {
    [ContentProperty(Name = nameof(Root))]
    public sealed class WorkspaceHost : UserControl {
        public Zone? Root { get; set; }

        public WorkspaceHost() {
            this.Loaded += (_, _) => {
                if (this.Content is null && this.Root is { } root) {
                    this.Content = Build(root);
                }
            };
        }

        private static FrameworkElement Build(Zone zone) {
            if (zone.Style is { } || zone.Background is { } || zone.Items.Count == 1 && zone.Items[0] is FrameworkElement) {
                var border = new Border { Style = zone.Style };
                if (zone.Background is { } background) {
                    border.Background = background;
                }
                border.Child = BuildContent(zone);
                return border;
            }
            return BuildContent(zone);
        }

        private static FrameworkElement BuildContent(Zone zone) {
            if (zone.Items.Count == 1 && zone.Items[0] is FrameworkElement content) {
                return content;
            }

            var grid = new Grid();
            var columns = zone.Direction == ZoneDirection.Columns;
            Splitter? pending = null;
            foreach (var item in zone.Items) {
                if (item is Splitter splitter) {
                    if (grid.Children.Count == 0 || pending is not null) {
                        throw new InvalidOperationException("A splitter must be placed between two zones.");
                    }
                    pending = splitter;
                    continue;
                }
                if (item is not Zone child) {
                    throw new InvalidOperationException("A zone accepts child zones or a single view.");
                }

                int index = grid.Children.Count;
                if (index > 0) {
                    AddTrack(grid, columns, new GridLength(zone.Spacing), 0, double.PositiveInfinity);
                    FrameworkElement separator;
                    if (pending is null) {
                        separator = new Border {
                            Background = (Brush)Application.Current.Resources["ZoneSplitterBrush"],
                            IsHitTestVisible = false,
                        };
                    } else {
                        var thumb = CreateSplitter(grid, columns, index);
                        if (pending.Style is { } style) {
                            thumb.Style = style;
                        }
                        separator = thumb;
                        pending = null;
                    }
                    Place(grid, separator, columns, index++);
                }

                AddTrack(grid, columns, child.Size, child.MinSize, child.MaxSize);
                Place(grid, Build(child), columns, index);
            }
            if (pending is not null) {
                throw new InvalidOperationException("A splitter must be followed by a zone.");
            }
            return grid;
        }

        private static void AddTrack(Grid grid, bool columns, GridLength size, double min, double max) {
            if (columns) {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = size, MinWidth = min, MaxWidth = max });
            } else {
                grid.RowDefinitions.Add(new RowDefinition { Height = size, MinHeight = min, MaxHeight = max });
            }
        }

        private static void Place(Grid grid, FrameworkElement element, bool columns, int index) {
            if (columns) {
                Grid.SetColumn(element, index);
            } else {
                Grid.SetRow(element, index);
            }
            grid.Children.Add(element);
        }

        private static Thumb CreateSplitter(Grid grid, bool columns, int index) {
            var thumb = new Thumb {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinWidth = 0,
                MinHeight = 0,
            };
            double[] initial = [];
            GridLength[] sizes = [];
            double offset = 0;
            thumb.DragStarted += (_, _) => {
                initial = columns ? grid.ColumnDefinitions.Select(d => d.ActualWidth).ToArray()
                                  : grid.RowDefinitions.Select(d => d.ActualHeight).ToArray();
                sizes = columns ? grid.ColumnDefinitions.Select(d => d.Width).ToArray()
                                : grid.RowDefinitions.Select(d => d.Height).ToArray();
                offset = 0;
            };
            thumb.DragDelta += (_, args) => {
                if (initial.Length == 0) {
                    return;
                }
                offset += columns ? args.HorizontalChange : args.VerticalChange;
                int left = index - 1, right = index + 1;
                double minLeft = columns ? grid.ColumnDefinitions[left].MinWidth : grid.RowDefinitions[left].MinHeight;
                double maxLeft = columns ? grid.ColumnDefinitions[left].MaxWidth : grid.RowDefinitions[left].MaxHeight;
                double minRight = columns ? grid.ColumnDefinitions[right].MinWidth : grid.RowDefinitions[right].MinHeight;
                double maxRight = columns ? grid.ColumnDefinitions[right].MaxWidth : grid.RowDefinitions[right].MaxHeight;
                var delta = Math.Clamp(offset, Math.Min(0, Math.Max(minLeft - initial[left], initial[right] - maxRight)),
                    Math.Max(0, Math.Min(maxLeft - initial[left], initial[right] - minRight)));

                // Сохраняем пропорции остальных звёздных треков при перераспределении места.
                for (int i = 0; i < sizes.Length; i++) {
                    if (i != left && i != right && !sizes[i].IsStar) {
                        continue;
                    }
                    double length = initial[i] + (i == left ? delta : i == right ? -delta : 0);
                    var size = new GridLength(length, sizes[i].IsStar ? GridUnitType.Star : GridUnitType.Pixel);
                    if (columns) {
                        grid.ColumnDefinitions[i].Width = size;
                    } else {
                        grid.RowDefinitions[i].Height = size;
                    }
                }
            };
            return thumb;
        }
    }
}