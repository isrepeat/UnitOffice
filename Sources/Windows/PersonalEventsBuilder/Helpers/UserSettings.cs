using Microsoft.Win32;

namespace PersonalEventsBuilder.Helpers {
    internal static class UserSettings {
        private const string SettingsKey = @"HKEY_CURRENT_USER\" + SettingsPath;
        private const string SettingsPath = @"Software\UnitOffice\PersonalEventsBuilder";

        internal static string[] Names(string folder) {
            using var key = Registry.CurrentUser.OpenSubKey($@"{SettingsPath}\{folder}");
            return key?.GetValueNames() ?? [];
        }

        internal static string Read(string folder, string name, string fallback) =>
            Registry.GetValue($@"{SettingsKey}\{folder}", name, fallback) as string ?? fallback;

        internal static void Write(string folder, string name, string value) =>
            Registry.SetValue($@"{SettingsKey}\{folder}", name, value);

        internal static T Read<T>(string name, T fallback) =>
            Registry.GetValue(SettingsKey, name, fallback) is T value ? value : fallback;

        internal static void Write(string name, object value) {
            Registry.SetValue(SettingsKey, name, value);
        }
    }
}