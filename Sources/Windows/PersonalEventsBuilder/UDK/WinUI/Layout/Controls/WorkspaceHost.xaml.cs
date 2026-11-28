using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

using XamlGrid = Microsoft.UI.Xaml.Controls.Grid;

namespace UDK.WinUI.Layout.Controls {
    [ContentProperty(Name = nameof(Root))]
    public sealed partial class WorkspaceHost : UserControl {
        public Zone? Root { get; set; }

        public WorkspaceHost() {
            this.InitializeComponent();
            this.Loaded += (_, _) => {
                if (this.ZonePresenter.Child is null && this.Root is { } root) {
                    this.ZonePresenter.Child = this.Build(root);
                }
            };
        }

        private FrameworkElement Build(Zone zone) {
            if (zone.Style is { } || zone.Background is { } || zone.Items.Count == 1 && zone.Items[0] is FrameworkElement) {
                var border = this.CreateElement<Border>("ZoneFrame");
                if (zone.Style is { } style) {
                    border.Style = style;
                }
                if (zone.Background is { } background) {
                    border.Background = background;
                }
                border.Child = this.BuildContent(zone);
                return border;
            }
            return this.BuildContent(zone);
        }

        private FrameworkElement BuildContent(Zone zone) {
            if (zone.Items.Count == 1 && zone.Items[0] is FrameworkElement content) {
                return content;
            }

            var grid = this.CreateElement<XamlGrid>("ZoneGrid");
            var columns = zone.Direction == ZoneDirection.Columns;
            Splitter? pending = null;
            var sections = new List<(Zone Zone, int Track)>();
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
                        separator = this.CreateElement<Border>("Gap");
                    } else {
                        var thumb = this.CreateSplitter(grid, columns, index);
                        if (pending.Style is { } style) {
                            thumb.Style = style;
                        }
                        separator = thumb;
                        pending = null;
                    }
                    Place(grid, separator, columns, index++);
                }

                AddTrack(grid, columns, child.Size, child.MinSize, child.MaxSize);
                if (zone.SequentialScroll && !columns) {
                    sections.Add((child, index));
                }
                Place(grid, this.Build(child), columns, index);
            }
            if (pending is not null) {
                throw new InvalidOperationException("A splitter must be followed by a zone.");
            }
            return sections.Count > 1 ? CreateScrollingRows(grid, sections) : grid;
        }

        private FrameworkElement CreateScrollingRows(XamlGrid grid, List<(Zone Zone, int Track)> sections) {
            var container = this.CreateElement<XamlGrid>("ScrollingRows");
            var viewer = (ScrollViewer)container.Children[0];
            var headers = (StackPanel)container.Children[1];
            viewer.Content = grid;
            double TopOf(int track) => grid.RowDefinitions.Take(track).Sum(row => row.ActualHeight);

            // Последнюю зону тоже можно довести до верхнего края области просмотра.
            var last = sections[^1];
            container.SizeChanged += (_, args) => grid.RowDefinitions[last.Track].MinHeight =
                Math.Clamp(args.NewSize.Height, last.Zone.MinSize, last.Zone.MaxSize);
            foreach (var (zone, track) in sections) {
                var button = this.CreateElement<Button>("SectionHeader");
                button.Content = zone.Title;
                int previousHeaders = headers.Children.Count;
                button.Click += (_, _) => {
                    double covered = headers.Children.Take(previousHeaders).Cast<Button>().Sum(header => header.ActualHeight);
                    viewer.ChangeView(null, Math.Max(0, TopOf(track) - covered), null, true);
                };
                headers.Children.Add(button);
            }

            void UpdateHeaders() {
                double covered = 0;
                for (int i = 0; i < sections.Count; i++) {
                    var row = grid.RowDefinitions[sections[i].Track];
                    var visibility = row.ActualHeight > 0 && TopOf(sections[i].Track) + row.ActualHeight <= viewer.VerticalOffset + covered
                        ? Visibility.Visible : Visibility.Collapsed;
                    if (headers.Children[i].Visibility != visibility) {
                        headers.Children[i].Visibility = visibility;
                    }
                    if (visibility == Visibility.Visible) {
                        covered += ((Button)headers.Children[i]).ActualHeight;
                    }
                }
            }

            viewer.ViewChanged += (_, _) => UpdateHeaders();
            grid.LayoutUpdated += (_, _) => UpdateHeaders();
            return container;
        }

        private static void AddTrack(XamlGrid grid, bool columns, GridLength size, double min, double max) {
            if (columns) {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = size, MinWidth = min, MaxWidth = max });
            } else {
                grid.RowDefinitions.Add(new RowDefinition { Height = size, MinHeight = min, MaxHeight = max });
            }
        }

        private static void Place(XamlGrid grid, FrameworkElement element, bool columns, int index) {
            if (columns) {
                XamlGrid.SetColumn(element, index);
            } else {
                XamlGrid.SetRow(element, index);
            }
            grid.Children.Add(element);
        }

        private T CreateElement<T>(string key) where T : FrameworkElement =>
            (T)((DataTemplate)this.ZonePresenter.Resources[key]).LoadContent();

        private Thumb CreateSplitter(XamlGrid grid, bool columns, int index) {
            var thumb = this.CreateElement<Thumb>("DragHandle");
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