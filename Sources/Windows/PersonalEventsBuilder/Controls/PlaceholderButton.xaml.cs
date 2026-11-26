using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class PlaceholderButton : Button {
        public PlaceholderButton() {
            this.InitializeComponent();
            this.Click += (_, _) => this.Focus(FocusState.Programmatic);
        }

        private Border? focusFrame;
        private bool isActive;

        protected override void OnApplyTemplate() {
            base.OnApplyTemplate();
            this.focusFrame = this.GetTemplateChild("FocusFrame") as Border;
            this.SetActive(this.isActive);
        }

        internal void SetActive(bool active) {
            this.isActive = active;
            if (this.focusFrame is { } frame) {
                States.NavigationFocus.SetActive(frame, active);
            }
        }
    }
}