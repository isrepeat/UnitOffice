namespace UDK.WinUI.Grid {
    internal interface INotifyGridRegionChanged {
        event System.EventHandler? RegionChanged;
    }

    internal interface IGridViewportContent {
        void ArrangeCells(GridArea visible, double width, double height);
    }
}