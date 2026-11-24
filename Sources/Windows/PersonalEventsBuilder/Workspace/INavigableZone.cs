using System;

using Windows.System;

namespace PersonalEventsBuilder.Workspace {
    internal enum NavigationDirection {
        Next,
        Previous,
        Left,
        Right,
        Up,
        Down,
    }

    internal interface INavigableZone {
        int StartRow { get; }
        int RowCount { get; }
        int TargetCount { get; }
        GridArea GetTargetArea(int index);
        int? FindTargetAt(int row, int column);
        void ActivateTarget(int index);
        bool CanEdit => false;
        bool IsEditing => false;
        bool TryHandleEditorKey(VirtualKey key) => false;
        void LeaveTarget() { }

        GridArea GetWorkspaceArea(int index) {
            var gridArea = this.GetTargetArea(index);
            return gridArea with { Row = gridArea.Row + this.StartRow };
        }

        int? FindDirectionalTarget(GridArea origin, NavigationDirection direction) {
            int? result = null;
            var best = (Distance: int.MaxValue, Alignment: int.MaxValue);
            for (var index = 0; index < this.TargetCount; index++) {
                var score = Score(origin, this.GetWorkspaceArea(index), direction);
                if (score is { } candidate && candidate.CompareTo(best) < 0) {
                    best = candidate;
                    result = index;
                }
            }

            return result;
        }

        internal static (int Distance, int Alignment)? Score(GridArea origin, GridArea target, NavigationDirection direction) {
            var rowsOverlap = origin.Row < target.Bottom && target.Row < origin.Bottom;
            var columnsOverlap = origin.Column < target.Right && target.Column < origin.Right;
            return direction switch {
                NavigationDirection.Left when rowsOverlap && target.Right <= origin.Column =>
                    (origin.Column - target.Right, Math.Abs(origin.Row - target.Row)),
                NavigationDirection.Right when rowsOverlap && target.Column >= origin.Right =>
                    (target.Column - origin.Right, Math.Abs(origin.Row - target.Row)),
                NavigationDirection.Up when columnsOverlap && target.Bottom <= origin.Row =>
                    (origin.Row - target.Bottom, Math.Abs(origin.Column - target.Column)),
                NavigationDirection.Down when columnsOverlap && target.Row >= origin.Bottom =>
                    (target.Row - origin.Bottom, Math.Abs(origin.Column - target.Column)),
                _ => null,
            };
        }
    }
}