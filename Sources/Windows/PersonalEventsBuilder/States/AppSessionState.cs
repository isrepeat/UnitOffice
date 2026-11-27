using System;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.States {
    internal sealed class AppSessionState {
        public Navigation.PageManager PageManager => this.pageManager
            ?? throw new InvalidOperationException("Application navigation has not been initialized.");

        private Navigation.PageManager? pageManager;

        public void InitializeNavigation(Frame frame) {
            if (this.pageManager is not null) {
                throw new InvalidOperationException("Application navigation has already been initialized.");
            }

            this.pageManager = new Navigation.PageManager(frame);
        }
    }
}