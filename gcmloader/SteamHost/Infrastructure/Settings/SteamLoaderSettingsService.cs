using System.Reflection;
using System.Text.Json;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.Settings;

public sealed class SteamLoaderSettingsService
{
    private static readonly SteamLoaderPluginDefinition[] PluginDefinitions =
    [
        new("processes", "Processes", "Window switcher for visible app windows.", true),
        new("store-sync", "Store Sync", "Launcher sync, Steam shortcuts, and artwork updates.", true),
        new("audio", "Audio", "Output device switching and system volume controls.", true),
        new("display", "Display", "Display switching, resolution, and refresh rate controls.", true),
        new("hltb", "HLTB", "HowLongToBeat game page estimates.", true),
        new("themes", "Themes", "Theme engine, theme store, and profiles.", true),
        new("power", "Power", "Recovery and power actions. This stays available for safety.", false),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly string _installPath;
    private readonly object _gate = new();

    public SteamLoaderSettingsService(string settingsPath, string installPath)
    {
        _settingsPath = settingsPath;
        _installPath = installPath;
    }

    public SteamLoaderGeneralSettingsSnapshot GetSnapshot()
    {
        var settings = LoadSettings();
        return new SteamLoaderGeneralSettingsSnapshot(
            RunOnWindowsSignIn: false,
            HideWindowsShellInConsoleMode: false,
            FirstRunCompleted: settings.FirstRunCompleted == true,
            ConsoleModeDefaultApplied: true,
            ProductVersion: GetProductVersion(),
            InstallPath: _installPath,
            Plugins: BuildPluginStates(settings));
    }

    public SteamLoaderGeneralSettingsSnapshot SetRunOnWindowsSignIn(bool enabled)
    {
        var settings = LoadSettings() with
        {
            RunOnWindowsSignIn = false,
            RunOnWindowsSignInUserConfigured = true,
            ConsoleModeDefaultApplied = true,
        };

        SaveSettings(settings);
        return GetSnapshot();
    }

    public SteamLoaderGeneralSettingsSnapshot SetHideWindowsShellInConsoleMode(bool enabled)
    {
        var settings = LoadSettings() with
        {
            HideWindowsShellInConsoleMode = false,
        };

        SaveSettings(settings);
        return GetSnapshot();
    }

    public SteamLoaderGeneralSettingsSnapshot SetPluginEnabled(string pluginId, bool enabled)
    {
        var definition = PluginDefinitions.FirstOrDefault(plugin =>
            string.Equals(plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            throw new InvalidOperationException("Unknown plugin.");
        }

        if (!definition.CanDisable && !enabled)
        {
            throw new InvalidOperationException("This plugin cannot be disabled.");
        }

        var settings = LoadSettings();
        var pluginStates = NormalizePluginStates(settings.PluginEnabled);
        pluginStates[definition.Id] = enabled || !definition.CanDisable;

        SaveSettings(settings with
        {
            PluginEnabled = pluginStates,
        });

        return GetSnapshot();
    }

    public bool IsPluginEnabled(string pluginId)
    {
        if (string.Equals(pluginId, "settings", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var definition = PluginDefinitions.FirstOrDefault(plugin =>
            string.Equals(plugin.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (definition is null || !definition.CanDisable)
        {
            return true;
        }

        var settings = LoadSettings();
        var pluginStates = NormalizePluginStates(settings.PluginEnabled);
        return pluginStates.TryGetValue(definition.Id, out var enabled) ? enabled : true;
    }

    public SteamLoaderGeneralSettingsSnapshot CompleteFirstRunSetup()
    {
        var settings = LoadSettings() with
        {
            FirstRunCompleted = true,
            FirstRunCompletedAtUtc = DateTimeOffset.UtcNow,
        };

        SaveSettings(settings);
        return GetSnapshot();
    }

    private SteamLoaderSettingsData LoadSettings()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return new SteamLoaderSettingsData();
                }

                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<SteamLoaderSettingsData>(json, JsonOptions)
                    ?? new SteamLoaderSettingsData();
            }
            catch
            {
                return new SteamLoaderSettingsData();
            }
        }
    }

    private void SaveSettings(SteamLoaderSettingsData settings)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
    }

    private static string GetProductVersion()
    {
        return typeof(SteamLoaderSettingsService)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.0.0-dev";
    }

    private static IReadOnlyList<SteamLoaderPluginSettingsState> BuildPluginStates(SteamLoaderSettingsData settings)
    {
        var pluginStates = NormalizePluginStates(settings.PluginEnabled);

        return PluginDefinitions
            .Select(plugin => new SteamLoaderPluginSettingsState(
                plugin.Id,
                plugin.Title,
                plugin.Description,
                plugin.CanDisable ? pluginStates[plugin.Id] : true,
                plugin.CanDisable))
            .ToArray();
    }

    private static Dictionary<string, bool> NormalizePluginStates(Dictionary<string, bool>? savedStates)
    {
        var normalized = PluginDefinitions.ToDictionary(
            plugin => plugin.Id,
            _ => true,
            StringComparer.OrdinalIgnoreCase);

        if (savedStates is null)
        {
            return normalized;
        }

        foreach (var plugin in PluginDefinitions)
        {
            if (!plugin.CanDisable)
            {
                normalized[plugin.Id] = true;
                continue;
            }

            if (savedStates.TryGetValue(plugin.Id, out var enabled))
            {
                normalized[plugin.Id] = enabled;
            }
        }

        return normalized;
    }

    private sealed record SteamLoaderPluginDefinition(
        string Id,
        string Title,
        string Description,
        bool CanDisable);

    private sealed record SteamLoaderSettingsData
    {
        public bool? RunOnWindowsSignIn { get; init; }

        public bool? ConsoleModeDefaultApplied { get; init; }

        public bool? RunOnWindowsSignInUserConfigured { get; init; }

        public bool? HideWindowsShellInConsoleMode { get; init; }

        public bool? FirstRunCompleted { get; init; }

        public DateTimeOffset? FirstRunCompletedAtUtc { get; init; }

        public Dictionary<string, bool>? PluginEnabled { get; init; }
    }
}
