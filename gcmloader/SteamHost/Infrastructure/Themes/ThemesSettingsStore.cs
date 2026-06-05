using System.Text.Json;

namespace SteamLoader.App.Infrastructure.Themes;

public sealed class ThemesSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public ThemesSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public ThemesConfiguration Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaultConfiguration();
            }

            var json = File.ReadAllText(_settingsPath);
            var configuration = JsonSerializer.Deserialize<ThemesConfiguration>(json, JsonOptions)
                ?? CreateDefaultConfiguration();

            Normalize(configuration);
            return configuration;
        }
        catch
        {
            return CreateDefaultConfiguration();
        }
    }

    public void Save(ThemesConfiguration configuration)
    {
        Normalize(configuration);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(configuration, JsonOptions));
    }

    private static ThemesConfiguration CreateDefaultConfiguration()
    {
        var configuration = new ThemesConfiguration();
        Normalize(configuration);
        return configuration;
    }

    private static void Normalize(ThemesConfiguration configuration)
    {
        configuration.Settings ??= new ThemesSettingsConfiguration();
        configuration.Themes ??= new Dictionary<string, ThemeInstallationConfiguration>(StringComparer.OrdinalIgnoreCase);
        configuration.Profiles ??= new Dictionary<string, ThemeProfileConfiguration>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in configuration.Themes.Keys.ToArray())
        {
            configuration.Themes[key] ??= new ThemeInstallationConfiguration();
            configuration.Themes[key].Values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var key in configuration.Profiles.Keys.ToArray())
        {
            configuration.Profiles[key] ??= new ThemeProfileConfiguration();
            configuration.Profiles[key].Themes ??=
                new Dictionary<string, ThemeProfileThemeConfiguration>(StringComparer.OrdinalIgnoreCase);

            foreach (var themeKey in configuration.Profiles[key].Themes.Keys.ToArray())
            {
                configuration.Profiles[key].Themes[themeKey] ??= new ThemeProfileThemeConfiguration();
                configuration.Profiles[key].Themes[themeKey].Values ??=
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}

public sealed class ThemesConfiguration
{
    public ThemesSettingsConfiguration Settings { get; set; } = new();

    public Dictionary<string, ThemeInstallationConfiguration> Themes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ThemeProfileConfiguration> Profiles { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string? SelectedProfileId { get; set; }
}

public sealed class ThemesSettingsConfiguration
{
    public bool ThemeEngineEnabled { get; set; } = true;

    public bool ShowCommunityThemes { get; set; } = true;

    public bool SingleThemeMode { get; set; }

    public bool AutoEnableOnInstall { get; set; }
}

public sealed class ThemeInstallationConfiguration
{
    public bool Installed { get; set; }

    public bool Enabled { get; set; }

    public Dictionary<string, string> Values { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ThemeProfileConfiguration
{
    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = "Tools for Steam";

    public string Description { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";

    public string SourceLabel { get; set; } = "Local";

    public int DownloadCount { get; set; }

    public Dictionary<string, ThemeProfileThemeConfiguration> Themes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ThemeProfileThemeConfiguration
{
    public bool Installed { get; set; } = true;

    public bool Enabled { get; set; }

    public Dictionary<string, string> Values { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
