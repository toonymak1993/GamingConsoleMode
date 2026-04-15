using System;
using System.Diagnostics;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace DeckTop.Settings;

/// <summary>
/// Single implementation for %AppData%\gcmsettings\settings.toml shared by gcmloader and GAMINGCONSOLEMODE.
/// </summary>
public static class AppSettings
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "gcmsettings");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.toml");

    private static readonly object _fileLock = new object();

    public static string ConfigurationDirectory => SettingsFolder;

    public static string ConfigurationFilePath => SettingsFilePath;

    /// <summary>
    /// gcmloader / tools: ensure folder exists and create default TOML when missing (no restart).
    /// </summary>
    public static void EnsureConfigurationFile()
    {
        lock (_fileLock)
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
                Trace.WriteLine($"Settings folder created at: {SettingsFolder}");
            }

            if (!File.Exists(SettingsFilePath))
            {
                WriteDefaultConfigurationUnlocked();
            }
        }
    }

    public static void Save(string key, object value)
    {
        lock (_fileLock)
        {
            try
            {
                TomlTable settings = ReadTomlTableOrEmpty();

                settings[key] = value;

                File.WriteAllText(SettingsFilePath, Toml.FromModel(settings));
                Trace.WriteLine($"Saved: {key} = {value}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error in Save method: {ex.Message}");
            }
        }
    }

    public static T Load<T>(string key)
    {
        lock (_fileLock)
        {
            try
            {
                var settings = ReadTomlTableOrEmpty();

                if (settings.ContainsKey(key))
                {
                    var value = settings[key];
                    return value is T typedValue ? typedValue : (T)Convert.ChangeType(value, typeof(T))!;
                }

                throw new Exception($"Key '{key}' not found in settings.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error in Load method for key '{key}': {ex.Message}");
                throw;
            }
        }
    }

    public static void Delete(string key)
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return;
                }

                var tomlTable = ReadTomlTableOrEmpty();

                if (tomlTable.Remove(key))
                {
                    File.WriteAllText(SettingsFilePath, Toml.FromModel(tomlTable));
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[ERROR] Failed to delete key '{key}' from AppSettings: {ex.Message}");
            }
        }
    }

    /// <summary>Must be called while holding <see cref="_fileLock"/>.</summary>
    private static TomlTable ReadTomlTableOrEmpty()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new TomlTable();
        }

        return Toml.Parse(File.ReadAllText(SettingsFilePath)).ToModel();
    }

    /// <summary>
    /// Deletes settings.toml if present and writes full defaults (including shortcuts table).
    /// </summary>
    public static void RegenerateConfiguration()
    {
        lock (_fileLock)
        {
            if (File.Exists(SettingsFilePath))
            {
                File.Delete(SettingsFilePath);
            }

            WriteDefaultConfigurationUnlocked();
        }
    }

    public static void initialconfig()
    {
        lock (_fileLock)
        {
            WriteDefaultConfigurationUnlocked();
        }
    }

    private static void WriteDefaultConfigurationUnlocked()
    {
        try
        {
            if (!Directory.Exists(SettingsFolder))
            {
                Directory.CreateDirectory(SettingsFolder);
            }

            var defaultSettings = new TomlTable();
            AddDefaultSettingsKeys(defaultSettings);

            var defaultShortcuts = new TomlTableArray
            {
                new TomlTable
                {
                    ["key1"] = "Back",
                    ["key2"] = "X",
                    ["function"] = "taskmanager",
                    ["time"] = 1000,
                    ["enabled"] = true
                },
            };

            defaultSettings["shortcuts"] = defaultShortcuts;

            File.WriteAllText(SettingsFilePath, Toml.FromModel(defaultSettings));
            Trace.WriteLine($"[DeckTop] Default config created at: {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[DeckTop] Error creating initial config: {ex.Message}");
        }
    }

    /// <summary>
    /// Scalar and table defaults for a fresh settings.toml (single source for both apps).
    /// </summary>
    private static void AddDefaultSettingsKeys(TomlTable t)
    {
        t["uac"] = false;
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
        t["use_controller_navigation"] = false;
        t["steamgriddb_api_key"] = "";
        t["rog_m1_action"] = "";
        t["rog_m2_action"] = "";
        t["replace"] = false;
        t["github_update_check"] = true;

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
}
