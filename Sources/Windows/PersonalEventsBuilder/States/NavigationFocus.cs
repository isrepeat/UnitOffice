using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PersonalEventsBuilder.States {
    internal static class NavigationFocus {
        internal static void SetActive(Border frame, bool active) =>
            frame.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }
}