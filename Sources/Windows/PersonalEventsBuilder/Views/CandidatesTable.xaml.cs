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

        public CandidatesTable() {
            this.InitializeComponent();
            string[][] table = [
                [Localization.UiStrings.Get("Tr_WorkspacePerson"), Localization.UiStrings.Get("Tr_WorkspaceRank"),
                    Localization.UiStrings.Get("Tr_WorkspaceDocumentNumber"), Localization.UiStrings.Get("Tr_WorkspaceDocumentDate")],
                [Localization.UiStrings.Get("Tr_WorkspaceCandidateOne"), Localization.UiStrings.Get("Tr_WorkspaceRankOne"), "123/26", "20.11.2026"],
                [Localization.UiStrings.Get("Tr_WorkspaceCandidateTwo"), Localization.UiStrings.Get("Tr_WorkspaceRankTwo"), "124/26", "21.11.2026"],
            ];
            this.Surface.Register(new CandidatesTableParts.CandidateGrid(2, 0, table));
            this.Surface.ViewportChanged += this.UpdateButtons;
            this.UpdateButtons();
        }

        private void UpdateButtons() {
            this.Previous.IsEnabled = this.Surface.CanScrollLeft;
            this.Next.IsEnabled = this.Surface.CanScrollRight;
        }

        private void OnPrevious(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(-1);

        private void OnNext(object sender, RoutedEventArgs args) => this.Surface.ScrollColumns(1);
    }
}