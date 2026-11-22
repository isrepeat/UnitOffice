using Microsoft.UI.Xaml;

namespace PersonalEventsBuilder {
    public sealed partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
            this.MessageText.Text = new PersonalEventsBuilder.WinRt.EventBuilder().GetMessage();
        }
    }
}