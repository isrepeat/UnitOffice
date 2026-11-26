using System.ComponentModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Views.EventFormParts {
    public sealed class FormInputViewModel : INotifyPropertyChanged {
        public bool IsActive {
            get => this.isActive;
            internal set {
                if (this.isActive != value) {
                    this.isActive = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private bool isActive;
    }

    public sealed partial class FormInput : UserControl, UDK.WinUI.Grid.IGridNavigationTarget {
        public FormInputViewModel ViewModel { get; } = new();
        public UDK.WinUI.Grid.GridArea Bounds { get; }

        public string Label { get; }
        public Style FieldStyle { get; }

        public FormInput(UDK.WinUI.Grid.GridArea gridArea, string label, Style fieldStyle) {
            this.Bounds = gridArea;
            this.Label = label;
            this.FieldStyle = fieldStyle;
            this.InitializeComponent();
            AutomationProperties.SetName(this, label);
            this.IsHitTestVisible = false;
        }

        public UDK.WinUI.Grid.GridAddress FocusAddress(UDK.WinUI.Grid.GridAddress address) => new(this.Bounds.Column, this.Bounds.Bottom - 1);
        public UDK.WinUI.Grid.GridArea FocusArea(UDK.WinUI.Grid.GridAddress address) => this.Bounds;

        public void SetActiveAddress(UDK.WinUI.Grid.GridAddress? address) {
            this.ViewModel.IsActive = address.HasValue;
            this.IsHitTestVisible = address.HasValue;
            this.IsTabStop = address.HasValue;
            if (address.HasValue) {
                this.Focus(FocusState.Programmatic);
            }
        }
    }
}