using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalEventsBuilder.Helpers {
    public abstract class ObservableObjectBase : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void NotifyPropertiesChanged() {
            this.NotifyPropertyChanged(null);
        }
    }
}