using Microsoft.Win32;

namespace PersonalEventsBuilder.Helpers {
    internal static class UserSettings {
        private const string SettingsKey = @"HKEY_CURRENT_USER\Software\UnitOffice\PersonalEventsBuilder";

        internal static T Read<T>(string name, T fallback) =>
            Registry.GetValue(SettingsKey, name, fallback) is T value ? value : fallback;

        internal static void Write(string name, object value) {
            Registry.SetValue(SettingsKey, name, value);
        }
    }
}