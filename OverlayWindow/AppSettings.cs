using System;
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

        public static void initialconfig()
        {
            lock (_fileLock)
            {
                try
                {
                    var defaultSettings = new TomlTable
                    {
                        ["launcher"] = "steam",
                        ["steamlauncherpath"] = @"C:\Program Files (x86)\Steam\steam.exe",
                        ["onboarding"] = false
                    };

                    var tomlText = Toml.FromModel(defaultSettings);
                    File.WriteAllText(SettingsFilePath, tomlText);

                    Console.WriteLine($"Default settings file created at: {SettingsFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in initialconfig method: {ex.Message}");
                }
            }
        }
    }
}
