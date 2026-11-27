using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace PersonalEventsBuilder.Views.EventFormParts.Controls {
    public sealed partial class FormInput : UserControl, UDK.WinUI.Grid.IGridNavigationTarget {
        public UDK.WinUI.Grid.GridArea Bounds { get; }

        public string Label { get; }
        public Style FieldStyle { get; }

        public FormInput(UDK.WinUI.Grid.GridArea gridArea, string label, Style fieldStyle) {
            this.Bounds = gridArea;
            this.Label = label;
            this.FieldStyle = fieldStyle;
            this.InitializeComponent();
            AutomationProperties.SetName(this, label);
            this.PointerEntered += (_, _) => this.HoverLayer.Opacity = 1;
            this.PointerExited += (_, _) => this.HoverLayer.Opacity = 0;
        }

        public UDK.WinUI.Grid.GridAddress FocusAddress(UDK.WinUI.Grid.GridAddress address) => new(this.Bounds.Column, this.Bounds.Bottom - 1);
        public UDK.WinUI.Grid.GridArea FocusArea(UDK.WinUI.Grid.GridAddress address) => this.Bounds;

        public void SetActiveAddress(UDK.WinUI.Grid.GridAddress? address) {
            States.NavigationFocus.SetActive(this.FocusFrame, address.HasValue);
            this.IsTabStop = address.HasValue;
            if (address.HasValue) {
                this.Focus(FocusState.Programmatic);
            }
        }
    }
}