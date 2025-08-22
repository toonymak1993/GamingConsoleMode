using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Tomlyn;
using Tomlyn.Model;

namespace GAMINGCONSOLEMODE
{
    public class AppSettings
    {
        private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.toml");

        private static readonly object _fileLock = new object();

        public static void FirstStart()
        {
            lock (_fileLock)
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                    Console.WriteLine($"Settings folder created at: {SettingsFolder}");
                }

                if (!File.Exists(SettingsFilePath))
                {
                    initialconfig();
                }
                else
                {
                    Console.WriteLine("Settings file already exists.");
                }
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
