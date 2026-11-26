using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views {
    public sealed partial class EventForm : UserControl {
        public UDK.WinUI.Grid.GridLayout? GridLayout {
            get => (UDK.WinUI.Grid.GridLayout?)this.GetValue(GridLayoutProperty);
            set => this.SetValue(GridLayoutProperty, value);
        }
        public static readonly DependencyProperty GridLayoutProperty = DependencyProperty.Register(
            nameof(GridLayout),
            typeof(UDK.WinUI.Grid.GridLayout),
            typeof(EventForm),
            new PropertyMetadata(null)
        );

        public EventForm() {
            this.InitializeComponent();
            this.Surface.Register(new EventFormParts.FormInput(0, 1, 3, Localization.UiStrings.Get("Tr_WorkspacePerson")));
            this.Surface.Register(new EventFormParts.FormInput(3, 1, 2, Localization.UiStrings.Get("Tr_WorkspaceRank")));
            this.Surface.Register(new EventFormParts.FormInput(5, 1, 2, Localization.UiStrings.Get("Tr_WorkspaceDocumentNumber")));
            this.Surface.Register(new EventFormParts.FormInput(7, 1, 2, Localization.UiStrings.Get("Tr_WorkspaceDocumentDate")));
            this.Surface.Register(new EventFormParts.FormInput(0, 3, 3, Localization.UiStrings.Get("Tr_WorkspaceDestination")));
            this.Surface.Register(new EventFormParts.FormInput(5, 3, 2, Localization.UiStrings.Get("Tr_WorkspaceDepartureDate")));
            this.Surface.Register(new EventFormParts.FormInput(0, 5, 6, Localization.UiStrings.Get("Tr_WorkspaceNotes")));
        }
    }
}