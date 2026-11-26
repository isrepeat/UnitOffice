using System.Linq;
using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Windows.System;

namespace UDK.WinUI.Grid {
    public sealed class GridNavigation : DependencyObject {
        private readonly List<GridCanvas> surfaces = [];
        private GridCanvas? activeSurface;
        private GridAddress activeAddress;
        private int preferredColumn;

        internal void Register(GridCanvas surface) => this.surfaces.Add(surface);

        internal void Remove(GridCanvas surface) {
            this.surfaces.Remove(surface);
            this.Release(surface);
        }

        internal void Release(GridCanvas surface) {
            if (this.activeSurface == surface) {
                this.activeSurface = null;
                surface.SetActiveAddress(null);
            }
        }

        internal void Select(GridCanvas surface, GridAddress address, bool preserveColumn = false) {
            if (!surface.Focus(FocusState.Programmatic)) {
                return;
            }
            if (this.activeSurface != surface) {
                this.activeSurface?.SetActiveAddress(null);
            }
            this.activeSurface = surface;
            this.activeAddress = address;
            if (!preserveColumn) {
                this.preferredColumn = address.Column;
            }
            surface.Reveal(address);
            surface.SetActiveAddress(address);
        }

        internal bool Move(GridCanvas surface, VirtualKey key) {
            if (this.activeSurface != surface || key is not (VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)) {
                return false;
            }
            var area = surface.FocusArea(this.activeAddress);
            var grid = surface.GridBounds;
            bool horizontal = key is VirtualKey.Left or VirtualKey.Right;
            int direction = key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            var search = key switch {
                VirtualKey.Left => new GridArea(0, this.activeAddress.Row, area.Column, 1),
                VirtualKey.Right => new GridArea(area.Right, this.activeAddress.Row, grid.Right - area.Right, 1),
                VirtualKey.Up => new GridArea(0, 0, grid.Width, area.Row),
                _ => new GridArea(0, area.Bottom, grid.Width, grid.Bottom - area.Bottom)
            };
            var desired = new GridAddress(horizontal ? this.activeAddress.Column : this.preferredColumn, this.activeAddress.Row);
            if (surface.FindAddress(search, desired, byRow: !horizontal) is { } next) {
                this.Select(surface, next, preserveColumn: !horizontal);
            } else if (!horizontal) {
                foreach (var candidate in this.surfaces.Where(item => (item.NavigationOrder - surface.NavigationOrder) * direction > 0)
                    .OrderBy(item => item.NavigationOrder * direction)) {
                    var bounds = candidate.GridBounds;
                    var edge = new GridAddress(this.preferredColumn, direction > 0 ? 0 : bounds.Bottom - 1);
                    if (candidate.FindAddress(bounds, edge, byRow: true) is { } entry) {
                        this.Select(candidate, entry, preserveColumn: true);
                        break;
                    }
                }
            }
            return true;
        }
    }
}