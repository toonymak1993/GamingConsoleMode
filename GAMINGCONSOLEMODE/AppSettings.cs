using DeckTop.Settings;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Tomlyn;

namespace GAMINGCONSOLEMODE
{
    /// <summary>
    /// WinUI entry points + forwards to <see cref="DeckTop.Settings.AppSettings"/> for shared TOML storage.
    /// </summary>
    public static class AppSettings
    {
        public static void Save(string key, object value) => DeckTop.Settings.AppSettings.Save(key, value);

        public static T Load<T>(string key) => DeckTop.Settings.AppSettings.Load<T>(key);

        public static void Delete(string key) => DeckTop.Settings.AppSettings.Delete(key);

        public static void initialconfig() => DeckTop.Settings.AppSettings.initialconfig();

        public static class MessageBoxHelper
        {
            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

            public static void Show(Window window, string text, string title)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                MessageBox(hwnd, text, title, 0);
            }
        }

        /// <summary>
        /// Hub startup: obsolete file cleanup, validate TOML, regenerate + restart if missing/invalid.
        /// </summary>
        public static void FirstStart(Window window)
        {
            try
            {
                string oldJsonPath = Path.Combine(DeckTop.Settings.AppSettings.ConfigurationDirectory, "settings.json");
                if (File.Exists(oldJsonPath))
                {
                    File.Delete(oldJsonPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting old settings.json: {ex.Message}");
            }

            try
            {
                string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string folderToDeletePath = Path.Combine(startMenuProgramsPath, "GCMcrew");
                if (Directory.Exists(folderToDeletePath))
                {
                    Directory.Delete(folderToDeletePath, true);
                    Trace.WriteLine($"Obsolete Start Menu folder deleted: {folderToDeletePath}");
                }
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show(window, $"An error occurred while deleting the old GCMcrew Start Menu folder:\n{ex.Message}", "Cleanup Error");
            }

            Directory.CreateDirectory(DeckTop.Settings.AppSettings.ConfigurationDirectory);

            string path = DeckTop.Settings.AppSettings.ConfigurationFilePath;
            if (!File.Exists(path))
            {
                RegenerateConfigAndRestart();
                return;
            }

            try
            {
                string content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new Exception("File is empty or contains only whitespace.");
                }

                _ = Toml.Parse(content).ToModel();
                Trace.WriteLine("Valid and parseable settings file found.");
            }
            catch (Exception ex)
            {
                MessageBoxHelper.Show(window, $"The settings file is corrupt or invalid and will be recreated.\n\nError: {ex.Message}", "Configuration Error");
                RegenerateConfigAndRestart();
            }
        }

        private static void RegenerateConfigAndRestart()
        {
            DeckTop.Settings.AppSettings.RegenerateConfiguration();
            RestartApplication();
        }

        public static void RestartApplication()
        {
            try
            {
                AppInstance.Restart(string.Empty);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Application restart failed: {ex.Message}");
            }
        }
    }
}
