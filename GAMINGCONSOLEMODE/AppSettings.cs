using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Tomlyn;
using Tomlyn.Model;

namespace GAMINGCONSOLEMODE
{
    public class AppSettings
    {
        #region File Paths & Locking

        private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.toml");
        private static readonly object _fileLock = new object();

        #endregion

        #region Win32 MessageBox Helper

        /// <summary>
        /// A helper class to show a traditional Win32 MessageBox.
        /// </summary>
        public static class MessageBoxHelper
        {
            // Imports the user32.dll MessageBox function.
            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

            /// <summary>
            /// Shows a native Win32 message box.
            /// </summary>
            /// <param name="window">The parent WinUI 3 window.</param>
            /// <param name="text">The message to display.</param>
            /// <param name="title">The title of the message box.</param>
            public static void Show(Window window, string text, string title)
            {
                // Gets the window handle (HWND) from the WinUI 3 window object.
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                // Calls the Win32 function to show the message box (0 = MB_OK).
                MessageBox(hwnd, text, title, 0);
            }
        }

        #endregion

        #region First Start & Validation

        /// <summary>
        /// Ensures a valid and parseable configuration file exists on application startup.
        /// If the file does not exist, is empty, or syntactically invalid, it will be regenerated,
        /// and the application will be prompted to restart.
        /// Also handles cleanup of obsolete files from previous versions.
        /// </summary>
        /// <param name="window">The main application window, required for showing message boxes.</param>
        public static void FirstStart(Window window)
        {
            lock (_fileLock)
            {
                // --- Clean up obsolete settings.json from older versions ---
                try
                {
                    string oldJsonPath = Path.Combine(SettingsFolder, "settings.json");
                    if (File.Exists(oldJsonPath))
                    {
                        File.Delete(oldJsonPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting old settings.json: {ex.Message}");
                }

                // --- Clean up obsolete 'GCMcrew' start menu folder ---
                try
                {
                    string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    string folderToDeletePath = Path.Combine(startMenuProgramsPath, "GCMcrew");
                    if (Directory.Exists(folderToDeletePath))
                    {
                        Directory.Delete(folderToDeletePath, true);
                        Console.WriteLine($"Obsolete Start Menu folder deleted: {folderToDeletePath}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxHelper.Show(window, $"An error occurred while deleting the old GCMcrew Start Menu folder:\n{ex.Message}", "Cleanup Error");
                }

                // --- Ensure the settings directory exists ---
                Directory.CreateDirectory(SettingsFolder);

                // --- Validate the current settings.toml file ---
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
                        // Try to parse the file to ensure it's valid TOML.
                        var model = Toml.ToModel(content);
                        Console.WriteLine("Valid and parseable settings file found.");
                    }
                    catch (Exception ex)
                    {
                        MessageBoxHelper.Show(window, $"The settings file is corrupt or invalid and will be recreated.\n\nError: {ex.Message}", "Configuration Error");
                        RegenerateConfigAndRestart(window);
                    }
                }
            }
        }

        #endregion

        #region Configuration Management (Save, Load, Delete)

        /// <summary>
        /// Saves a key-value pair to the settings.toml file.
        /// If the file or key exists, it will be updated; otherwise, it will be created.
        /// </summary>
        public static void Save(string key, object value)
        {
            lock (_fileLock)
            {
                try
                {
                    TomlTable settings = File.Exists(SettingsFilePath)
                        ? Toml.Parse(File.ReadAllText(SettingsFilePath)).ToModel()
                        : new TomlTable();

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

        /// <summary>
        /// Loads a value from the settings.toml file by its key.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <returns>The value converted to the specified type.</returns>
        /// <exception cref="Exception">Throws if the key is not found or the type is incorrect.</exception>
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
                            // Direct cast if the type matches, otherwise try to convert.
                            return value is T typedValue ? typedValue : (T)Convert.ChangeType(value, typeof(T));
                        }
                    }
                    throw new Exception($"Key '{key}' not found in settings.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in Load method for key '{key}': {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Deletes a key from the settings.toml file.
        /// </summary>
        public static void Delete(string key)
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(SettingsFilePath)) return;

                    var tomlTable = Toml.Parse(File.ReadAllText(SettingsFilePath)).ToModel();

                    if (tomlTable.Remove(key))
                    {
                        File.WriteAllText(SettingsFilePath, Toml.FromModel(tomlTable));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to delete key '{key}' from AppSettings: {ex.Message}");
                }
            }
        }

        #endregion

        #region Default Configuration & Restart Logic

        /// <summary>
        /// Scalar and table defaults for a fresh settings.toml (kept in sync with gcmloader/AppSettings.cs).
        /// </summary>
        private static void AddDefaultSettingsKeys(TomlTable t)
        {
            t["uac"] = false; // REMOVED: UAC registry — backward compat only, no effect
            t["launcher"] = "steam";
            t["steamlauncherpath"] = "";
            t["playnitelauncherpath"] = "";
            t["customlauncherpath"] = "";
            t["gfnlauncherpath"] = "";
            t["onboarding"] = false;
            t["lossless"] = false;
            t["losslesspath"] = "";
            t["usewinpart"] = false;
            t["usewinpartstartapps"] = false;
            t["useboilr"] = false;
            t["usecssloader"] = false;
            t["usedeckyloader"] = false;
            t["usedisplayfusion"] = false;
            t["usepreaudio"] = false;
            t["usesteamstartupvideo"] = false;
            t["usestartupvideo"] = false;
            t["useseamlessswitchtogcm"] = false;
            t["usepreloadlist"] = false;
            t["preaudiostart"] = "";
            t["preaudioend"] = "";
            t["usedisplayfusion_start"] = "";
            t["usedisplayfusion_end"] = "";
            t["startupvideo_path"] = "";
            t["gcmwallpaper"] = false;
            t["gcmwallpaperpath"] = "";
            t["shortcutpopup"] = true;
            t["enable_taskbar"] = false;
            t["enable_startmenu"] = false;
            t["show_discord"] = true;
            t["handheldtouchlauncher"] = false;
            t["use_controller_navigation"] = false; // DeckTop default: desktop-first; set true for legacy gamepad UI + shortcuts
            t["steamgriddb_api_key"] = "";
            t["rog_m1_action"] = "";
            t["rog_m2_action"] = "";
            t["replace"] = false;

            t["winmode_shortcut"] = new TomlTable
            {
                ["key1"] = "",
                ["key2"] = "",
                ["enabled"] = false
            };

            for (int i = 1; i <= 5; i++)
            {
                t[$"button{i}link"] = "";
                t[$"button{i}image"] = "";
                t[$"button{i}args"] = "";
                t[$"button{i}workdir"] = "";
            }
        }

        /// <summary>
        /// Creates a new settings.toml file with default values and standard shortcuts.
        /// </summary>
        public static void initialconfig()
        {
            lock (_fileLock)
            {
                try
                {
                    // 1. Base defaults (all keys referenced by UI / gcmloader).
                    var defaultSettings = new TomlTable();
                    AddDefaultSettingsKeys(defaultSettings);

                    // 2. Create the list for default shortcuts in the new format.
                    // We now include "time" for hold duration to match our new logic.
                    var defaultShortcuts = new TomlTableArray
            {
                // Default Shortcut 1: GCM Taskmanager
                // Triggered by Back + X held for 1 second.
                new TomlTable
                {
                    ["key1"] = "Back",
                    ["key2"] = "X",
                    ["function"] = "taskmanager",
                    ["time"] = 1000,
                    ["enabled"] = true
                },
                // Default Shortcut 2: Show Overlay
                // Immediate trigger (0ms) as requested for a snappier feel.
                //new TomlTable
                //{
                 //   ["key1"] = "RightThumb",
                 //   ["key2"] = "DPadLeft",
                 //   ["function"] = "show overlay",
                 //   ["time"] = 0,
                 //  ["enabled"] = true
                //}
            };

                    // 3. Shortcut list (gamepad / chord shortcuts).
                    defaultSettings["shortcuts"] = defaultShortcuts;

                    // 4. Persist.
                    var tomlText = Toml.FromModel(defaultSettings);
                    File.WriteAllText(SettingsFilePath, tomlText);

                    Console.WriteLine($"[GCM] Default config created successfully with new shortcut format at: {SettingsFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GCM] Error creating initial config: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Helper method to delete, re-create the config, and restart the app, showing a message box.
        /// </summary>
        private static void RegenerateConfigAndRestart(Window window)
        {
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
            }
            initialconfig();
            RestartApplication();
        }

        /// <summary>
        /// Restarts the entire application.
        /// </summary>
        public static void RestartApplication()
        {
            try
            {
                // Use the AppInstance API to restart the application.
                AppInstance.Restart(string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application restart failed: {ex.Message}");
            }
        }

        #endregion
    }
}
