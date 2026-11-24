using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using GridArea = PersonalEventsBuilder.Workspace.GridArea;
using WorkspaceGrid = PersonalEventsBuilder.Workspace.WorkspaceGrid;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public sealed partial class CandidatesSection : UserControl {
        public CandidatesSectionViewModel? ViewModel {
            get => (CandidatesSectionViewModel?)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(CandidatesSectionViewModel),
            typeof(CandidatesSection),
            new PropertyMetadata(null, OnViewModelChanged)
        );

        public CandidatesSection() {
            this.InitializeComponent();
        }

        private static void OnViewModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var candidatesSection = (CandidatesSection)sender;
            if (args.OldValue is CandidatesSectionViewModel oldViewModel) {
                oldViewModel.ViewportChanged -= candidatesSection.DrawVisibleCells;
            }

            if (args.NewValue is CandidatesSectionViewModel candidatesSectionViewModel) {
                candidatesSectionViewModel.ViewportChanged += candidatesSection.DrawVisibleCells;
            }

            candidatesSection.DrawVisibleCells();
        }

        private void DrawVisibleCells() {
            this.CellLabels.Children.Clear();
            if (this.ViewModel is not { } candidatesSectionViewModel) {
                return;
            }

            var area = candidatesSectionViewModel.VisibleArea;
            for (var row = area.Row; row < area.Bottom; row++) {
                for (var column = 0; column < candidatesSectionViewModel.Columns.Length; column++) {
                    var candidateColumn = candidatesSectionViewModel.Columns[column];
                    if (candidateColumn.Column >= area.Right || candidateColumn.Column + candidateColumn.ColumnSpan <= area.Column) {
                        continue;
                    }

                    var textBlock = new TextBlock {
                        Style = (Style)Application.Current.Resources[row == 0 ? "WorkspaceHeaderText" : "WorkspaceCellText"],
                        Text = row == 0 ? candidateColumn.Title : candidatesSectionViewModel.Rows[row - 1].Cells[column],
                    };
                    WorkspaceGrid.Place(textBlock, new GridArea(row, candidateColumn.Column, ColumnSpan: candidateColumn.ColumnSpan));
                    this.CellLabels.Children.Add(textBlock);
                }
            }
        }
    }
}