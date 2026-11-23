using Microsoft.Windows.Globalization;

namespace PersonalEventsBuilder.Localization {
    internal static class UiLanguage {
        public const string Ukrainian = "uk-UA";
        public const string Russian = "ru-RU";
        private const string LanguageValue = "Language";

        public static string Current => ApplicationLanguages.PrimaryLanguageOverride;

        public static bool IsSupported(string language) => language is Ukrainian or Russian;

        public static void Initialize() {
            var stored = Helpers.UserSettings.Read(LanguageValue, Ukrainian);
            ApplicationLanguages.PrimaryLanguageOverride = stored is Russian ? Russian : Ukrainian;
        }

        public static void Save(string language) {
            Helpers.UserSettings.Write(LanguageValue, language);
        }
    }
}