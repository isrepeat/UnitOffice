using System;

using Windows.Foundation;

namespace PersonalEventsBuilder.Workspace {
    internal readonly record struct NavigationTarget(INavigableZone Zone, int Index) {
        internal GridArea Area => this.Zone.GetWorkspaceArea(this.Index);
    }

    internal sealed class WorkspaceNavigation {
        private readonly INavigableZone[] navigableZones;

        internal WorkspaceNavigation(params INavigableZone[] navigableZones) {
            this.navigableZones = navigableZones;
        }

        internal NavigationTarget? FindAt(int row, int column) {
            foreach (var navigableZone in this.navigableZones) {
                if (row < navigableZone.StartRow || row >= navigableZone.StartRow + navigableZone.RowCount) {
                    continue;
                }

                var index = navigableZone.FindTargetAt(row - navigableZone.StartRow, column);
                if (index.HasValue) {
                    return new(navigableZone, index.Value);
                }
            }

            return null;
        }

        internal NavigationTarget? FindNearest(Point position) {
            var row = (int)(position.Y / WorkspaceGrid.RowHeight);
            var column = (int)(position.X / WorkspaceGrid.ColumnWidth);
            if (this.FindAt(row, column) is { } directTarget) {
                return directTarget;
            }

            NavigationTarget? nearestTarget = null;
            var bestDistance = double.PositiveInfinity;
            foreach (var navigableZone in this.navigableZones) {
                for (var index = 0; index < navigableZone.TargetCount; index++) {
                    var navigationTarget = new NavigationTarget(navigableZone, index);
                    var bounds = WorkspaceGrid.GetBounds(navigationTarget.Area);
                    var dx = position.X - Math.Clamp(position.X, bounds.Left, bounds.Right);
                    var dy = position.Y - Math.Clamp(position.Y, bounds.Top, bounds.Bottom);
                    var distance = dx * dx + dy * dy;
                    if (distance < bestDistance) {
                        bestDistance = distance;
                        nearestTarget = navigationTarget;
                    }
                }
            }

            return nearestTarget;
        }

        internal NavigationTarget? FindNext(NavigationTarget? current, NavigationDirection direction) {
            if (direction is NavigationDirection.Next or NavigationDirection.Previous) {
                return this.FindSequentialTarget(current, direction == NavigationDirection.Next ? 1 : -1);
            }

            if (current is not { } target) {
                return this.FindSequentialTarget(null, 1);
            }

            if (target.Zone.FindDirectionalTarget(target.Area, direction) is { } insideIndex) {
                return new(target.Zone, insideIndex);
            }

            NavigationTarget? result = null;
            var best = (Distance: int.MaxValue, Alignment: int.MaxValue);
            foreach (var navigableZone in this.navigableZones) {
                if (navigableZone == target.Zone || navigableZone.FindDirectionalTarget(target.Area, direction) is not { } index) {
                    continue;
                }

                var candidate = new NavigationTarget(navigableZone, index);
                var score = INavigableZone.Score(target.Area, candidate.Area, direction);
                if (score is { } value && value.CompareTo(best) < 0) {
                    best = value;
                    result = candidate;
                }
            }

            return result;
        }

        private NavigationTarget? FindSequentialTarget(NavigationTarget? current, int step) {
            var zoneIndex = step > 0 ? 0 : this.navigableZones.Length - 1;
            if (current is { } target) {
                var nextIndex = target.Index + step;
                if (nextIndex >= 0 && nextIndex < target.Zone.TargetCount) {
                    return new(target.Zone, nextIndex);
                }

                zoneIndex = System.Array.IndexOf(this.navigableZones, target.Zone) + step;
            }

            for (; zoneIndex >= 0 && zoneIndex < this.navigableZones.Length; zoneIndex += step) {
                var navigableZone = this.navigableZones[zoneIndex];
                if (navigableZone.TargetCount > 0) {
                    return new(navigableZone, step > 0 ? 0 : navigableZone.TargetCount - 1);
                }
            }

            return null;
        }
    }
}