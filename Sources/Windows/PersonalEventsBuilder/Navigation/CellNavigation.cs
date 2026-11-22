using System.Linq;
using System.Collections.Generic;

namespace PersonalEventsBuilder.Navigation {
    internal enum CellDirection {
        Left,
        Right,
        Up,
        Down,
        Previous,
        Next,
    }

    internal readonly record struct CellPosition(int Row, int Zone, int Index) {
        public (int Zone, int Index) Column => (this.Zone, this.Index);
        public (int Row, int Zone, int Index) Order => (this.Row, this.Zone, this.Index);
    }

    internal static class CellNavigation {
        public static int? FindTarget(IReadOnlyList<CellPosition> positions, int currentIndex, CellDirection direction) {
            var current = positions[currentIndex];
            var ordered = positions.Select((position, index) => (Position: position, Index: (int?)index))
                .OrderBy(cell => cell.Position.Order);

            return direction switch {
                CellDirection.Left => ordered.LastOrDefault(cell => cell.Position.Row == current.Row
                    && cell.Position.Column.CompareTo(current.Column) < 0).Index,
                CellDirection.Right => ordered.FirstOrDefault(cell => cell.Position.Row == current.Row
                    && cell.Position.Column.CompareTo(current.Column) > 0).Index,
                CellDirection.Up => ordered.LastOrDefault(cell => cell.Position.Column == current.Column
                    && cell.Position.Row < current.Row).Index,
                CellDirection.Down => ordered.FirstOrDefault(cell => cell.Position.Column == current.Column
                    && cell.Position.Row > current.Row).Index,
                CellDirection.Previous => ordered.LastOrDefault(cell => cell.Position.Order.CompareTo(current.Order) < 0).Index,
                CellDirection.Next => ordered.FirstOrDefault(cell => cell.Position.Order.CompareTo(current.Order) > 0).Index,
                _ => null,
            };
        }
    }
}