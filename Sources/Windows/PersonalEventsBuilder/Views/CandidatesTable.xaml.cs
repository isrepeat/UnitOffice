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

        internal CandidatesTableParts.Controls.CandidateTableItem[] TableItems { get; } = [new()];

        public CandidatesTable() {
            this.InitializeComponent();
            this.Surface.ViewportChanged += this.UpdateButtons;
            this.UpdateButtons();
        }

        private void UpdateTable() {
            this.TableItems[0].Update(this.Columns ?? [], this.Rows ?? []);
        }

        private void UpdateButtons() {
            this.Previous.IsEnabled = this.Surface.CanScrollLeft;
            this.Next.IsEnabled = this.Surface.CanScrollRight;
        }

        private void OnPrevious(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(-1);

        private void OnNext(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(1);
    }
}