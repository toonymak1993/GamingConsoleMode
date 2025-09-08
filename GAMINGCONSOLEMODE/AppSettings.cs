using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Tomlyn;
using Tomlyn.Model;

namespace GAMINGCONSOLEMODE
{
    public class AppSettings
    {
        private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.toml");

        private static readonly object _fileLock = new object();
        // This is a helper class to show a traditional Win32 MessageBox.
        // It's simpler for synchronous, blocking messages than using a ContentDialog.
        public static class MessageBoxHelper
        {
            // Imports the user32.dll MessageBox function
            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

            // Shows the message box
            public static void Show(Window window, string text, string title)
            {
                // Gets the window handle (HWND) from the WinUI 3 window object
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

                // Calls the Win32 function
                MessageBox(hwnd, text, title, 0); // 0 = MB_OK
            }
        }

        /// <summary>
        /// Ensures that a valid configuration file exists and cleans up obsolete files.
        /// If the configuration needs to be created, it will trigger an application restart afterwards.
        /// </summary>
        /// <summary>
        /// Ensures a valid and parseable configuration file exists.
        /// If the file does not exist, is empty, whitespace, or syntactically invalid,
        /// it will be deleted and regenerated, followed by an application restart.
        /// </summary>
        public static void FirstStart(Window window) // <-- MODIFIED: Takes a Window parameter
        {
            lock (_fileLock)
            {
                // --- Clean up obsolete settings.json ---
                try
                {
                    string oldJsonPath = Path.Combine(SettingsFolder, "settings.json");
                    if (File.Exists(oldJsonPath))
                    {
                        File.Delete(oldJsonPath);
                        // No message box needed here, this is a silent cleanup
                    }
                }
                catch (Exception ex)
                {
                    //"An error occurred while deleting the old settings.json:\n{ex.Message}", "Cleanup Error");
                }
                //--- Clean up gcmcrew startmenu folder
                try
                {
                    // Get the path to the user's Start Menu Programs folder dynamically
                    string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

                    // Combine it with the folder name you want to delete
                    string folderToDeletePath = Path.Combine(startMenuProgramsPath, "GCMcrew");

                    // Check if the folder exists
                    if (Directory.Exists(folderToDeletePath))
                    {
                        // Delete the folder and all its contents (recursive: true)
                        Directory.Delete(folderToDeletePath, true);
                        Console.WriteLine($"Obsolete Start Menu folder deleted: {folderToDeletePath}");
                    }
                }
                catch (Exception ex)
                {
                    // Show an error if deletion fails (e.g., due to permissions)
                    MessageBoxHelper.Show(window, $"An error occurred while deleting the GCMcrew Start Menu folder:\n{ex.Message}", "Cleanup Error");
                }

                // --- Ensure the settings directory exists ---
                Directory.CreateDirectory(SettingsFolder);

                // --- Main Validation Logic ---
                if (!File.Exists(SettingsFilePath))
                {
                    RegenerateConfigAndRestart(window); 
                }
                else
                {
                    try
                    {
                        string content = File.ReadAllText(SettingsFilePath);
                        if (string.IsNullOrWhiteSpace(content))
                        {
                            throw new Exception("File is empty or contains only whitespace.");
                        }
                        var model = Toml.ToModel(content);
                        Console.WriteLine("Valid and parseable settings file found.");
                    }
                    catch (Exception ex)
                    {
                        MessageBoxHelper.Show(window, $"The settings file is corrupt or invalid and will be recreated.\n\nError: {ex.Message}", "Configuration Error");
                        RegenerateConfigAndRestart(window); // <-- MODIFIED: Passes the window
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to handle the process of deleting, re-creating, and restarting.
        /// </summary>
        private static void RegenerateConfigAndRestart(Window window) // <-- MODIFIED: Takes a Window parameter
        {
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
            }

            initialconfig();

            // --- Restart after initial configuration with a MessageBox ---
            MessageBoxHelper.Show(window, "The initial setup is complete. The application will now restart to apply the new settings.", "Restart Required");
            RestartApplication();
        }

        /// <summary>
        /// Helper method to handle the process of deleting, re-creating, and restarting.
        /// </summary>
        private static void RegenerateConfigAndRestart()
        {
            // Ensure the file is deleted before creating a new one.
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
            }

            // Create the default configuration.
            initialconfig();

            // --- Restart after initial configuration ---
            Console.WriteLine("Initial setup is complete. The application will now restart.");
            RestartApplication();
        }

        /// <summary>
        /// Startet die Anwendung neu.
        /// </summary>
        public static void RestartApplication()
        {
            try
            {
                // Startet die Anwendung neu und beendet die aktuelle Instanz.
                // Der Parameter kann für Startargumente verwendet werden, wir brauchen hier aber keine.
                AppInstance.Restart(string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"restart error {ex.Message}");
                // Optional: Dem Benutzer eine Fehlermeldung anzeigen.
                // WICHTIG: Da wir hier keine Referenz auf das UI-Fenster haben, 
                // ist eine MessageBox schwierig. Console-Logging ist hier sicherer.
            }
        }
        public static void Save(string key, object value)
        {
            lock (_fileLock)
            {
                try
                {
                    TomlTable settings;

                    if (File.Exists(SettingsFilePath))
                    {
                        var tomlText = File.ReadAllText(SettingsFilePath);
                        settings = Toml.Parse(tomlText).ToModel();
                    }
                    else
                    {
                        settings = new TomlTable();
                    }

                    settings[key] = value;

                    var newToml = Toml.FromModel(settings);
                    File.WriteAllText(SettingsFilePath, newToml);

                    Console.WriteLine($"Saved: {key} = {value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in Save method: {ex.Message}");
                }
            }
        }

        public static T Load<T>(string key)
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        var tomlText = File.ReadAllText(SettingsFilePath);
                        var settings = Toml.Parse(tomlText).ToModel();

                        if (settings.ContainsKey(key))
                        {
                            var value = settings[key];

                            if (value is T typedValue)
                            {
                                return typedValue;
                            }
                            else
                            {
                                // Convert if possible
                                return (T)Convert.ChangeType(value, typeof(T));
                            }
                        }
                    }
                    throw new Exception($"Key '{key}' not found in the settings.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in Load method: {ex.Message}");
                    throw;
                }
            }
        }
        public static void Delete(string key)
        {
            try
            {
                // Prüfen, ob die Einstellungsdatei existiert.
                if (!File.Exists(SettingsFilePath))
                {
                    return;
                }

                // Lese das gesamte TOML-Dokument.
                var tomlString = File.ReadAllText(SettingsFilePath);
                var tomlTable = Toml.ToModel(tomlString);

                // Prüfe, ob der Schlüssel vorhanden ist und entferne ihn, wenn ja.
                if (tomlTable.ContainsKey(key))
                {
                    tomlTable.Remove(key);

                    // Schreibe die aktualisierte Tabelle zurück in die Datei.
                    File.WriteAllText(SettingsFilePath, Toml.FromModel(tomlTable));
                }
            }
            catch (Exception ex)
            {
                // Logge den Fehler, um bei der Fehlersuche zu helfen.
                Debug.WriteLine($"[ERROR] Fehler beim Löschen des Schlüssels '{key}' aus den AppSettings: {ex.Message}");
            }
        }
        public static void initialconfig()
        {
            lock (_fileLock)
            {
                try
                {
                    // 1. Deine bestehenden Standard-Einstellungen
                    var defaultSettings = new TomlTable
                    {
                        ["launcher"] = "steam",
                        ["steamlauncherpath"] = @"C:\Program Files (x86)\Steam\steam.exe",
                        ["onboarding"] = false
                    };

                    // === ANFANG DER NEUEN LOGIK ===

                    // 2. Erstelle die Liste (TomlTableArray) für die Standard-Shortcuts
                    var defaultShortcuts = new TomlTableArray
            {
                // Standard-Shortcut 1: Task Manager
                new TomlTable
                {
                    ["key1"] = "Back",
                    ["key2"] = "Start",
                    ["function"] = "taskmanager",
                    ["enabled"] = true
                },
                // Standard-Shortcut 2: Show Overlay
                new TomlTable
                {
                    ["key1"] = "RightThumb",
                    ["key2"] = "DPadLeft",
                    ["function"] = "show overlay",
                    ["enabled"] = true
                }
            };

                    // 3. Füge die Shortcut-Liste zu den Haupteinstellungen hinzu
                    defaultSettings["shortcuts"] = defaultShortcuts;

                    // === ENDE DER NEUEN LOGIK ===

                    // 4. Speichere die kompletten Einstellungen (inkl. Shortcuts) in die Datei
                    var tomlText = Toml.FromModel(defaultSettings);
                    File.WriteAllText(SettingsFilePath, tomlText);

                    Console.WriteLine($"Default settings file with shortcuts created at: {SettingsFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in initialconfig method: {ex.Message}");
                }
            }
        }
    }
}
