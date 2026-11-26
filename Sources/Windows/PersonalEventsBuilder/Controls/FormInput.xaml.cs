using System.ComponentModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.Controls {
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

    public sealed partial class FormInput : UserControl, IGridNavigationTarget {
        public FormInputViewModel ViewModel { get; } = new();
        public GridArea Bounds { get; }

        public FormInput(int column, int row, int width, string label) {
            this.Bounds = new(column, row - 1, width, 2);
            this.InitializeComponent();
            AutomationProperties.SetName(this, label);
            this.IsHitTestVisible = false;
        }

        public GridAddress FocusAddress(GridAddress address) => new(this.Bounds.Column, this.Bounds.Bottom - 1);
        public GridArea FocusArea(GridAddress address) => this.Bounds;

        public void SetActiveAddress(GridAddress? address) {
            this.ViewModel.IsActive = address.HasValue;
            this.IsHitTestVisible = address.HasValue;
            this.IsTabStop = address.HasValue;
            if (address.HasValue) {
                this.Focus(FocusState.Programmatic);
            }
        }
    }
}