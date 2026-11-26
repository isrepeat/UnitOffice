using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
    public sealed partial class PlaceholderButton : Button {
        public static readonly DependencyProperty PlaceholderLines_LineWidthsProperty = DependencyProperty.Register(
            nameof(PlaceholderLines_LineWidths), typeof(string), typeof(PlaceholderButton), new PropertyMetadata("33"));

        public string PlaceholderLines_LineWidths {
            get => (string)this.GetValue(PlaceholderLines_LineWidthsProperty);
            set => this.SetValue(PlaceholderLines_LineWidthsProperty, value);
        }

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