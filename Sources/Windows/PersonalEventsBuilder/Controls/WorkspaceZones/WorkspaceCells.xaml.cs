using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using CellTextStyle = PersonalEventsBuilder.Workspace.CellTextStyle;
using WorkspaceCell = PersonalEventsBuilder.Workspace.WorkspaceCell;
using WorkspaceGrid = PersonalEventsBuilder.Workspace.WorkspaceGrid;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class WorkspaceCells : UserControl {
        public WorkspaceCell[]? Cells {
            get => (WorkspaceCell[]?)this.GetValue(CellsProperty);
            set => this.SetValue(CellsProperty, value);
        }

        public static readonly DependencyProperty CellsProperty = DependencyProperty.Register(
            nameof(Cells), typeof(WorkspaceCell[]), typeof(WorkspaceCells), new PropertyMetadata(null, OnCellsChanged)
        );

        public WorkspaceCells() {
            this.InitializeComponent();
        }

        private static void OnCellsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            ((WorkspaceCells)sender).DrawCells();
        }

        private void DrawCells() {
            this.CellCanvas.Children.Clear();
            if (this.Cells is not { } workspaceCells) {
                return;
            }

            var resources = Application.Current.Resources;
            var valueStyle = (Style)resources["WorkspaceCellText"];
            var headingStyle = (Style)resources["WorkspaceHeaderText"];
            foreach (var workspaceCell in workspaceCells) {
                var textBlock = new TextBlock {
                    Text = workspaceCell.Text,
                    Style = workspaceCell.TextStyle == CellTextStyle.Heading ? headingStyle : valueStyle,
                };
                WorkspaceGrid.Place(textBlock, workspaceCell.Area);
                this.CellCanvas.Children.Add(textBlock);
            }
        }
    }
}