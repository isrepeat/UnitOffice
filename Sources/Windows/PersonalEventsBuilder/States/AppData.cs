namespace PersonalEventsBuilder.States {
    internal static class AppData {
        internal const string SettingsKey = @"HKEY_CURRENT_USER\Software\UnitOffice\PersonalEventsBuilder";

        public static AppSessionState AppSessionState { get; } = new();
    }
}