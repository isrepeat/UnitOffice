using System.Linq;
using System.Collections.Generic;
using Windows.System;
using Microsoft.UI.Xaml;

namespace UDK.WinUI.Grid {
    public sealed class GridNavigation : DependencyObject, UDK.WinUI.Navigation.IKeyboardNavigationArea {
        private Controls.GridCanvas? lastSurface;
        private Controls.GridCanvas? activeSurface;
        private readonly List<Controls.GridCanvas> surfaces = [];
        private readonly Navigation.NavigationCoordinator coordinator = new();

        public bool FocusArea() {
            if (this.lastSurface is { } previous && previous.GridBounds.Contains(this.coordinator.EntryCell)) {
                this.Select(previous, this.coordinator.EntryCell, restore: true);
                return this.activeSurface == previous;
            }
            foreach (var surface in this.surfaces.OrderBy(item => item.NavigationOrder)) {
                if (this.coordinator.FindEntry(surface, forward: true) is { } first) {
                    this.Select(surface, first);
                    return this.activeSurface == surface;
                }
            }
            return false;
        }

        public void MoveFocus(VirtualKey direction) {
            if (this.activeSurface is { } surface) {
                this.Move(surface, direction);
            } else {
                this.FocusArea();
            }
        }

        internal void Register(Controls.GridCanvas surface) => this.surfaces.Add(surface);

        internal void Remove(Controls.GridCanvas surface) {
            this.surfaces.Remove(surface);
            this.Release(surface);
            if (this.lastSurface == surface) {
                this.lastSurface = null;
            }
        }

        internal void Release(Controls.GridCanvas surface) {
            if (this.activeSurface == surface) {
                this.activeSurface = null;
                surface.SetActiveAddress(null);
            }
        }

        internal void Select(Controls.GridCanvas surface, GridAddress address, VirtualKey? move = null, bool restore = false) {
            if (!surface.Focus(FocusState.Programmatic)) {
                return;
            }
            if (this.activeSurface != surface) {
                this.activeSurface?.SetActiveAddress(null);
            }
            this.activeSurface = surface;
            this.lastSurface = surface;
            if (move is { } key) {
                this.coordinator.CommitMove(address, key);
            } else if (!restore) {
                this.coordinator.Remember(address);
            }
            surface.Reveal(address);
            surface.SetActiveAddress(surface.ResolveAddress(address));
        }

        internal bool Move(Controls.GridCanvas surface, VirtualKey key) {
            if (this.activeSurface != surface || key is not (VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down)) {
                return false;
            }
            var area = surface.FocusArea(this.coordinator.EntryCell);
            int direction = key is VirtualKey.Left or VirtualKey.Up ? -1 : 1;
            if (this.coordinator.FindNext(surface, area, key) is { } next) {
                this.Select(surface, next, key);
            } else if (key is VirtualKey.Up or VirtualKey.Down) {
                foreach (var candidate in this.surfaces.Where(item => (item.NavigationOrder - surface.NavigationOrder) * direction > 0)
                    .OrderBy(item => item.NavigationOrder * direction)) {
                    if (this.coordinator.FindEntry(candidate, direction > 0) is { } entry) {
                        this.Select(candidate, entry, key);
                        break;
                    }
                }
            }
            return true;
        }
    }
}