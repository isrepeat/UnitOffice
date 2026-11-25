using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Navigation {
    internal sealed class PageManager {
        private readonly Frame frame;

        public PageManager(Frame frame) {
            this.frame = frame;
        }

        public bool CanGoBack => this.frame.CanGoBack;
        public Pages.WorkspacePage? CurrentWorkspace => this.frame.Content as Pages.WorkspacePage;

        public bool Navigate<TPage>() where TPage : Page {
            var pageType = typeof(TPage);
            return this.frame.CurrentSourcePageType != pageType && this.frame.Navigate(pageType);
        }

        public void GoBack() {
            if (this.CanGoBack) {
                this.frame.GoBack();
            }
        }
    }
}