using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalEventsBuilder.Helpers {
    internal static class PropertyChange {
        public static bool Set<T>(
            object sender,
            ref T field,
            T value,
            PropertyChangedEventHandler? handler,
            [CallerMemberName] string? propertyName = null
        ) {
            if (EqualityComparer<T>.Default.Equals(field, value)) {
                return false;
            }

            field = value;
            handler?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}