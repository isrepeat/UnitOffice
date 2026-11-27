using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace PersonalEventsBuilder.Views.EventFormParts.Controls {
    public sealed partial class FormInput : UserControl, UDK.WinUI.Grid.IGridNavigationTarget {
        //
        // IGridNavigationTarget
        //
        public UDK.WinUI.Grid.GridArea Bounds => this.Item.Bounds;

        public bool IsSide => this.Item.Group?.Placement is Scheme.FormPlacement.Left or Scheme.FormPlacement.Right;

        public bool IsPrimary => this.Item.Group?.Placement == Scheme.FormPlacement.Primary;

        private PlacedFormField Item => this.DataContext is PlacedFormField item ? item : default;

        public FormInput() {
            this.InitializeComponent();
            this.DataContextChanged += (_, args) => {
                this.Bindings.Update();
                if (args.NewValue is PlacedFormField item) {
                    AutomationProperties.SetName(this, $"{Localization.UiStrings.Get(item.Group.LabelKey)} {item.Field.Id}");
                }
            };
            this.PointerEntered += (_, _) => this.HoverLayer.Opacity = 1;
            this.PointerExited += (_, _) => this.HoverLayer.Opacity = 0;
        }

        //
        // IGridNavigationTarget
        //
        public void SetActiveAddress(UDK.WinUI.Grid.GridAddress? address) {
            States.NavigationFocus.SetActive(this.FocusFrame, address.HasValue);
            this.IsTabStop = address.HasValue;
            if (address.HasValue) {
                this.Focus(FocusState.Programmatic);
            }
        }

        public UDK.WinUI.Grid.GridArea FocusArea(UDK.WinUI.Grid.GridAddress address) => this.Bounds;

        public UDK.WinUI.Grid.GridAddress FocusAddress(UDK.WinUI.Grid.GridAddress address) => new(this.Bounds.Column, this.Bounds.Bottom - 1);
    }
}