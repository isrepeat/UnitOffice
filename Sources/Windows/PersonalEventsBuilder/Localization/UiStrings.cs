using Microsoft.Windows.ApplicationModel.Resources;

namespace PersonalEventsBuilder.Localization {
    internal static class UiStrings {
        private static readonly ResourceLoader st_resourceLoader = new();

        public static string Get(string key) => UiStrings.st_resourceLoader.GetString(key);
    }
}