namespace PersonalEventsBuilder.Controls {
    public interface IGridNavigationTarget {
        void UpdateNavigationState(int column, int row, bool isActive);
    }
}