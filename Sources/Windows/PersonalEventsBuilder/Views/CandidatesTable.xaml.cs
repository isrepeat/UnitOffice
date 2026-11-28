using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class CandidatesTable : UserControl {
        public UDK.WinUI.Grid.GridLayout? GridLayout {
            get => (UDK.WinUI.Grid.GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(UDK.WinUI.Grid.GridLayout),
            typeof(CandidatesTable),
            new PropertyMetadata(null)
        );

        public IReadOnlyList<EventFormParts.FormColumn>? Columns {
            get => (IReadOnlyList<EventFormParts.FormColumn>?)this.GetValue(ColumnsProperty);
            set => this.SetValue(ColumnsProperty, value);
        }
        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
            nameof(Columns), typeof(IReadOnlyList<EventFormParts.FormColumn>), typeof(CandidatesTable),
            new PropertyMetadata(null, (sender, _) => ((CandidatesTable)sender).UpdateTable()));

        public IReadOnlyList<IReadOnlyList<string>>? Rows {
            get => (IReadOnlyList<IReadOnlyList<string>>?)this.GetValue(RowsProperty);
            set => this.SetValue(RowsProperty, value);
        }
        public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
            nameof(Rows), typeof(IReadOnlyList<IReadOnlyList<string>>), typeof(CandidatesTable),
            new PropertyMetadata(null, (sender, _) => ((CandidatesTable)sender).UpdateTable()));

        private readonly CandidatesTableParts.Controls.CandidateGrid region;

        public CandidatesTable() {
            this.InitializeComponent();
            // Один CandidateGrid на весь срок жизни представления: вместе с ним между режимами сохраняется пул ячеек.
            this.region = (CandidatesTableParts.Controls.CandidateGrid)((DataTemplate)this.Resources["TableTemplate"]).LoadContent();
            this.Surface.ViewportChanged += this.UpdateButtons;
            this.UpdateButtons();
        }

        private void UpdateTable() {
            var region = this.region;
            if (this.Columns is { Count: > 0 } columns) {
                region.Configure(columns, this.Rows ?? []);
                this.Surface.ReplaceRegions(new[] { region });
            } else {
                this.Surface.ReplaceRegions<CandidatesTableParts.Controls.CandidateGrid>([]);
            }
        }

        private void UpdateButtons() {
            this.Previous.IsEnabled = this.Surface.CanScrollLeft;
            this.Next.IsEnabled = this.Surface.CanScrollRight;
        }

        private void OnPrevious(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(-1);

        private void OnNext(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(1);
    }
}