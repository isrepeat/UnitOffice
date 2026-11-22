using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls.WorkspaceZones {
    public sealed partial class WorkspaceActionsZone : UserControl {
        public WorkspaceActionsZone() {
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