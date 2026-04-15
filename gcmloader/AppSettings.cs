using System;
using System.IO;
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

        /// <summary>
        /// Scalar and table defaults for a fresh settings.toml (kept in sync with GAMINGCONSOLEMODE/AppSettings.cs).
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

        public static void initialconfig()
        {
            lock (_fileLock)
            {
                try
                {
                    var defaultSettings = new TomlTable();
                    AddDefaultSettingsKeys(defaultSettings);

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
