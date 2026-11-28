using System;
using System.IO;
using System.Security;
using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace PersonalEventsBuilder {
    public partial class App : Application {
        private Window? window;

        public App() {
            InitializeLogging();
            this.UnhandledException += (_, args) => LogError(args.Exception);
            Localization.UiLanguage.Initialize();
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args) {
            this.window = new MainWindow();
            this.window.Activate();
            if (st_loggingAvailable) {
                WinRt.EventBuilder.LogInfo("Main window activated.");
            }
        }

        private static bool st_loggingAvailable;

        private static void InitializeLogging() {
            try {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "UnitOffice", "PersonalEventsBuilder", "Logs");
                Directory.CreateDirectory(directory);
                st_loggingAvailable = WinRt.EventBuilder.InitializeLogging(Path.Combine(directory, "PersonalEventsBuilder.log"));
                if (st_loggingAvailable) {
                    WinRt.EventBuilder.LogInfo("Application starting.");
                }
            } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or System.Runtime.InteropServices.COMException) {
                Debug.WriteLine(exception);
            }
        }

        internal static void LogError(Exception exception) {
            if (st_loggingAvailable) {
                WinRt.EventBuilder.LogError(exception.ToString());
            } else {
                Debug.WriteLine(exception);
            }
        }
    }
}