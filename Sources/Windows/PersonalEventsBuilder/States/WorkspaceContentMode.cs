using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace PersonalEventsBuilder.States {
    public sealed class WorkspaceContentMode : INotifyPropertyChanged {
        //
        // INotifyPropertyChanged
        //
        public event PropertyChangedEventHandler? PropertyChanged;

        public Visibility DataVisibility => this.ShowPlaceholders ? Visibility.Collapsed : Visibility.Visible;
        public Visibility PlaceholderVisibility => this.ShowPlaceholders ? Visibility.Visible : Visibility.Collapsed;

        public bool ShowPlaceholders {
            get => this.showPlaceholders;
            set {
                if (Helpers.PropertyChange.Set(this, ref this.showPlaceholders, value, this.PropertyChanged)) {
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DataVisibility)));
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderVisibility)));
                }
            }
        }

        private bool showPlaceholders = true;
    }
}