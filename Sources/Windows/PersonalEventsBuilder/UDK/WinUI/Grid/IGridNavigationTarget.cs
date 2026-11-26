namespace UDK.WinUI.Grid {
    public interface IGridNavigationTarget {
        GridArea Bounds { get; }
        GridAddress FocusAddress(GridAddress address);
        GridArea FocusArea(GridAddress address);
        void SetActiveAddress(GridAddress? address);
    }
}