using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class CandidatesTable : UserControl {
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(UDK.WinUI.Grid.GridLayout),
            typeof(CandidatesTable),
            new PropertyMetadata(null)
        );

        public static readonly DependencyProperty ColumnRangeProperty = DependencyProperty.Register(
            nameof(ColumnRange), typeof(EventFormParts.ColumnRange), typeof(CandidatesTable),
            new PropertyMetadata(null, (sender, args) => {
                if (!Equals(args.OldValue, args.NewValue)) {
                    ((CandidatesTable)sender).UpdateRange();
                }
            }));

        public UDK.WinUI.Grid.GridLayout? GridLayout {
            get => (UDK.WinUI.Grid.GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }

        public EventFormParts.ColumnRange? ColumnRange {
            get => (EventFormParts.ColumnRange?)this.GetValue(ColumnRangeProperty);
            set => this.SetValue(ColumnRangeProperty, value);
        }

        private readonly string[][] table;

        public CandidatesTable() {
            this.InitializeComponent();
            this.table = [
                [Localization.UiStrings.Get("Tr_WorkspacePerson"), Localization.UiStrings.Get("Tr_WorkspaceRank"),
                    Localization.UiStrings.Get("Tr_WorkspaceDocumentNumber"), Localization.UiStrings.Get("Tr_WorkspaceDocumentDate")],
                [Localization.UiStrings.Get("Tr_WorkspaceCandidateOne"), Localization.UiStrings.Get("Tr_WorkspaceRankOne"), "123/26", "20.11.2026"],
                [Localization.UiStrings.Get("Tr_WorkspaceCandidateTwo"), Localization.UiStrings.Get("Tr_WorkspaceRankTwo"), "124/26", "21.11.2026"],
            ];
            this.Surface.ViewportChanged += this.UpdateButtons;
            this.UpdateButtons();
        }

        private void UpdateRange() {
            CandidatesTableParts.Controls.CandidateGrid[] regions = this.ColumnRange is { Span: > 0 } range
                ? [new CandidatesTableParts.Controls.CandidateGrid(range.Start, range.Span, this.table)] : [];
            this.Surface.ReplaceRegions(regions);
        }

        private void UpdateButtons() {
            this.Previous.IsEnabled = this.Surface.CanScrollLeft;
            this.Next.IsEnabled = this.Surface.CanScrollRight;
        }

        private void OnPrevious(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(-1);

        private void OnNext(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(1);
    }
}