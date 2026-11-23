using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class ActionsZone : UserControl {
        public ActionsZone() {
            InitializeComponent();
        }

        internal event Action<string>? ActionRequested;

        private void OnActionClick(object sender, RoutedEventArgs args) {
            if (sender is Button { Tag: string actionId }) {
                this.ActionRequested?.Invoke(actionId);
            }
        }
    }
}