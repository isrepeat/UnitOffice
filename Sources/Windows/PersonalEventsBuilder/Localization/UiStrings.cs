using Microsoft.Windows.ApplicationModel.Resources;

namespace PersonalEventsBuilder.Localization {
    internal static class UiStrings {
        private static readonly ResourceLoader resourceLoader = new();

        public static string Get(string key) => UiStrings.resourceLoader.GetString(key);
    }
}