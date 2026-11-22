using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class NewEventZone : UserControl {
        public NewEventZone() {
            InitializeComponent();
        }

        internal event Action? NewEventRequested;

        private void OnNewEventClick(object sender, RoutedEventArgs args) {
            this.NewEventRequested?.Invoke();
        }
    }
}