using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace GAMINGCONSOLEMODE
{
    public class AppSettings
    {
        // Store configuration in %AppData%\gcmsettings\
        private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

        // Lock object for thread safety
        private static readonly object _fileLock = new object();

        // Ensures that the folder and default configuration file exist
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

        // Saves a specific key-value pair to the configuration file
        public static void Save(string key, object value)
        {
            lock (_fileLock)
            {
                try
                {
                    JObject settings;

                    // Load existing JSON file or create a new JObject if the file is empty or missing
                    if (File.Exists(SettingsFilePath))
                    {
                        var json = File.ReadAllText(SettingsFilePath);
                        settings = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                    }
                    else
                    {
                        settings = new JObject();
                    }

                    // Update or add the key-value pair
                    settings[key] = JToken.FromObject(value);

                    // Write the updated JSON back to the file (synchronous; can be replaced with async methods if needed)
                    File.WriteAllText(SettingsFilePath, settings.ToString(Formatting.Indented));
                    Console.WriteLine($"Saved: {key} = {value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in Save method: {ex.Message}");
                }
            }
        }

        // Loads a specific key from the configuration file and converts it to the specified type
        public static T Load<T>(string key)
        {
            lock (_fileLock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        var json = File.ReadAllText(SettingsFilePath);
                        var settings = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);

                        if (settings.ContainsKey(key))
                        {
                            return settings[key].ToObject<T>();
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

        // Initializes default configuration on first start
        public static void initialconfig()
        {
            lock (_fileLock)
            {
                try
                {
                    var defaultSettings = new JObject
                    {
                        ["launcher"] = "steam",
                        ["steamlauncherpath"] = @"C:\Program Files (x86)\Steam\steam.exe"
                    };

                    File.WriteAllText(SettingsFilePath, defaultSettings.ToString(Formatting.Indented));
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
