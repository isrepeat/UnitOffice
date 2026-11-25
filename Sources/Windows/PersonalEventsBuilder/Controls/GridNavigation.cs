using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;

using Windows.System;

namespace PersonalEventsBuilder.Controls {
    internal sealed record GridCell(int Column, int Row);

    public sealed class GridNavigation : DependencyObject {
        private readonly List<GridCanvas> surfaces = [];
        private GridCanvas? activeSurface;
        private GridCell? activeCell;
        private int preferredColumn;

        internal void Register(GridCanvas surface) => this.surfaces.Add(surface);

        internal void Remove(GridCanvas surface) {
            this.surfaces.Remove(surface);
            this.Release(surface);
        }

        internal void Release(GridCanvas surface) {
            if (this.activeSurface == surface) {
                this.activeSurface = null;
                this.activeCell = null;
                surface.UpdateTargets();
            }
        }

        internal bool IsActive(GridCanvas surface, GridCell cell) => this.activeSurface == surface && this.activeCell == cell;

        internal void Select(GridCanvas surface, GridCell cell, bool preserveColumn = false) {
            if (!surface.Focus(FocusState.Programmatic)) {
                return;
            }
            var previous = this.activeSurface;
            this.activeSurface = surface;
            this.activeCell = cell;
            if (!preserveColumn) {
                this.preferredColumn = cell.Column;
            }
            if (previous != surface) {
                previous?.UpdateTargets();
            }
            surface.Reveal(cell);
            surface.UpdateTargets();
        }

        internal bool Move(GridCanvas surface, VirtualKey key) {
            if (this.activeSurface != surface || this.activeCell is not { } cell) {
                return false;
            }

            bool horizontal = key is VirtualKey.Left or VirtualKey.Right;
            if (!horizontal && key is not (VirtualKey.Up or VirtualKey.Down)) {
                return false;
            }
            int direction = key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            if (horizontal) {
                var next = surface.ReachableCells.Where(target => target.Row == cell.Row && (target.Column - cell.Column) * direction > 0)
                    .OrderBy(target => target.Column * direction).FirstOrDefault();
                if (next is not null) {
                    this.Select(surface, next);
                }
                return true;
            }

            // Сначала ищем занятую строку на текущей поверхности, затем переходим к соседней.
            foreach (var candidate in this.surfaces.OrderBy(item => item.NavigationOrder * direction)) {
                if (candidate != surface && (candidate.NavigationOrder - surface.NavigationOrder) * direction <= 0) {
                    continue;
                }
                var next = candidate.ReachableCells.Where(target => candidate != surface || (target.Row - cell.Row) * direction > 0)
                    .OrderBy(target => target.Row * direction)
                    .ThenBy(target => Math.Abs(target.Column - this.preferredColumn))
                    .ThenBy(target => target.Column).FirstOrDefault();
                if (next is not null) {
                    this.Select(candidate, next, preserveColumn: true);
                    break;
                }
            }
            return true;
        }
    }
}