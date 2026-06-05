using System.Text.Json;

namespace SteamLoader.App.Infrastructure.Hltb;

public sealed class HltbSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public HltbSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public HltbConfiguration Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaultConfiguration();
            }

            var json = File.ReadAllText(_settingsPath);
            var configuration = JsonSerializer.Deserialize<HltbConfiguration>(json, JsonOptions)
                ?? CreateDefaultConfiguration();

            Normalize(configuration);
            return configuration;
        }
        catch
        {
            return CreateDefaultConfiguration();
        }
    }

    public void Save(HltbConfiguration configuration)
    {
        Normalize(configuration);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(configuration, JsonOptions));
    }

    private static HltbConfiguration CreateDefaultConfiguration()
    {
        var configuration = new HltbConfiguration();
        Normalize(configuration);
        return configuration;
    }

    private static void Normalize(HltbConfiguration configuration)
    {
        configuration.Settings ??= new HltbSettingsConfiguration();
        configuration.Cache ??= new Dictionary<string, HltbCacheEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in configuration.Cache.Keys.ToArray())
        {
            configuration.Cache[key] ??= new HltbCacheEntry();
            configuration.Cache[key].RequestedTitle ??= string.Empty;
            configuration.Cache[key].MatchedTitle ??= string.Empty;
            configuration.Cache[key].DetailUrl ??= string.Empty;
        }
    }
}

public sealed class HltbConfiguration
{
    public HltbSettingsConfiguration Settings { get; set; } = new();

    public Dictionary<string, HltbCacheEntry> Cache { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HltbSettingsConfiguration
{
    public bool Enabled { get; set; } = true;

    public bool ShowMainStory { get; set; } = true;

    public bool ShowMainPlus { get; set; } = true;

    public bool ShowCompletionist { get; set; } = true;

    public bool ShowAllStyles { get; set; } = true;

    public bool ShowViewDetails { get; set; } = true;
}

public sealed class HltbCacheEntry
{
    public string RequestedTitle { get; set; } = string.Empty;

    public string MatchedTitle { get; set; } = string.Empty;

    public int? AppId { get; set; }

    public int? GameId { get; set; }

    public double? MainStoryHours { get; set; }

    public double? MainPlusHours { get; set; }

    public double? CompletionistHours { get; set; }

    public double? AllStylesHours { get; set; }

    public string DetailUrl { get; set; } = string.Empty;

    public bool Found { get; set; }

    public DateTimeOffset LastUpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
