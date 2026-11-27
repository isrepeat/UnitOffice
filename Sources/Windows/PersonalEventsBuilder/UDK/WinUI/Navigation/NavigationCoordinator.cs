using System.Collections.Generic;
using Windows.System;

namespace UDK.WinUI.Navigation {
    internal interface INavigationSurface {
        Grid.GridArea NavigationBounds { get; }
        IEnumerable<Grid.GridArea> NavigationRegions { get; }
    }

    internal sealed class NavigationCoordinator {
        public Grid.GridAddress EntryCell { get; private set; }

        private int preferredIndex;
        private bool? lastMoveHorizontal;

        public void Remember(Grid.GridAddress address) {
            this.EntryCell = address;
            this.lastMoveHorizontal = null;
        }

        public void CommitMove(Grid.GridAddress hit, VirtualKey key) {
            bool horizontal = key is VirtualKey.Left or VirtualKey.Right;
            this.preferredIndex = this.DesiredIndex(horizontal);
            this.lastMoveHorizontal = horizontal;
            this.EntryCell = hit;
        }

        public Grid.GridAddress? FindNext(INavigationSurface surface, Grid.GridArea active, VirtualKey key) {
            var bounds = surface.NavigationBounds;
            var search = key switch {
                VirtualKey.Left => new Grid.GridArea(bounds.Column, bounds.Row,
                    active.Column - bounds.Column, bounds.Height),
                VirtualKey.Right => new Grid.GridArea(active.Right, bounds.Row,
                    bounds.Right - active.Right, bounds.Height),
                VirtualKey.Up => new Grid.GridArea(bounds.Column, bounds.Row,
                    bounds.Width, active.Row - bounds.Row),
                VirtualKey.Down => new Grid.GridArea(bounds.Column, active.Bottom,
                    bounds.Width, bounds.Bottom - active.Bottom),
                _ => default
            };
            return this.Find(surface, search, key is VirtualKey.Right or VirtualKey.Down, key is VirtualKey.Left or VirtualKey.Right);
        }

        public Grid.GridAddress? FindEntry(INavigationSurface surface, bool forward) =>
            this.Find(surface, surface.NavigationBounds, forward, alongColumns: false);

        private int DesiredIndex(bool horizontal) => this.lastMoveHorizontal == horizontal
            ? this.preferredIndex : horizontal ? this.EntryCell.Row : this.EntryCell.Column;

        private Grid.GridAddress? Find(INavigationSurface surface, Grid.GridArea search, bool forward, bool alongColumns) {
            if (search.IsEmpty) {
                return null;
            }
            int desired = this.DesiredIndex(alongColumns);
            Grid.GridAddress? best = null;
            (int Line, int Miss, int Index) bestScore = (int.MaxValue, int.MaxValue, int.MaxValue);
            foreach (var region in surface.NavigationRegions) {
                var area = region.Intersect(search);
                if (area.IsEmpty) {
                    continue;
                }

                // Сначала выбираем ближайшую строку (или колонку) по направлению движения.
                int line = alongColumns
                    ? (forward ? area.Column : area.Right - 1)
                    : (forward ? area.Row : area.Bottom - 1);
                int start = alongColumns ? area.Row : area.Column;
                int end = alongColumns ? area.Bottom : area.Right;

                // При поперечном переходе сохраняем индекс. Если места с таким индексом нет, берём первое.
                bool matches = desired >= start && desired < end;
                int index = matches ? desired : start;
                var score = (forward ? line : -line, matches ? 0 : 1, index);
                if (score.CompareTo(bestScore) < 0) {
                    bestScore = score;
                    best = alongColumns ? new(line, index) : new(index, line);
                }
            }
            return best;
        }
    }
}