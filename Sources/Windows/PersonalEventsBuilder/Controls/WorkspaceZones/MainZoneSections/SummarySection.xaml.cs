using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Foundation;

using WorkspaceGrid = PersonalEventsBuilder.Workspace.WorkspaceGrid;

namespace PersonalEventsBuilder.Controls.WorkspaceZones.MainZoneSections {
    public sealed partial class SummarySection : UserControl {
        public SummarySectionViewModel? ViewModel {
            get => (SummarySectionViewModel?)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SummarySectionViewModel),
            typeof(SummarySection),
            new PropertyMetadata(null)
        );

        public SummarySection() {
            this.InitializeComponent();
        }

        protected override Size MeasureOverride(Size availableSize) {
            var size = base.MeasureOverride(availableSize);
            var rowCount = Math.Max(2, (int)Math.Ceiling(size.Height / WorkspaceGrid.RowHeight));
            this.ViewModel?.SetMeasuredRowCount(rowCount);
            return new Size(size.Width, rowCount * WorkspaceGrid.RowHeight);
        }
    }
}