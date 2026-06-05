using System.Text.Json;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.StoreSync;

public sealed class StoreSyncSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public StoreSyncSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public StoreSyncConfiguration Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return CreateDefaultConfiguration();
            }

            var json = File.ReadAllText(_settingsPath);
            var configuration = JsonSerializer.Deserialize<StoreSyncConfiguration>(json, JsonOptions)
                ?? CreateDefaultConfiguration();

            Normalize(configuration);
            return configuration;
        }
        catch
        {
            return CreateDefaultConfiguration();
        }
    }

    public void Save(StoreSyncConfiguration configuration)
    {
        Normalize(configuration);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(configuration, JsonOptions));
    }

    private static StoreSyncConfiguration CreateDefaultConfiguration()
    {
        var configuration = new StoreSyncConfiguration();
        Normalize(configuration);
        return configuration;
    }

    private static void Normalize(StoreSyncConfiguration configuration)
    {
        configuration.SteamGridDbApiKey ??= string.Empty;
        configuration.Stores ??= new Dictionary<string, StoreSyncStoreConfiguration>(StringComparer.OrdinalIgnoreCase);

        foreach (var storeId in new[] { "epic-games", "gog-galaxy", "xbox-game-pass", "custom-locations" })
        {
            if (!configuration.Stores.TryGetValue(storeId, out var storeConfiguration) || storeConfiguration is null)
            {
                configuration.Stores[storeId] = new StoreSyncStoreConfiguration();
                continue;
            }

            storeConfiguration.ScanPath ??= string.Empty;
        }
    }
}

public sealed class StoreSyncConfiguration
{
    public string SteamGridDbApiKey { get; set; } = string.Empty;

    public bool DownloadArtwork { get; set; } = true;

    public bool PreferAnimatedArtwork { get; set; }

    public bool CloseSteamBeforeSync { get; set; } = true;

    public bool BackupShortcuts { get; set; } = true;

    public bool LaunchBigPictureAfterSync { get; set; } = true;

    public Dictionary<string, StoreSyncStoreConfiguration> Stores { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public StoreSyncLastSyncState? LastSync { get; set; }
}

public sealed class StoreSyncStoreConfiguration
{
    public bool Enabled { get; set; } = true;

    public string ScanPath { get; set; } = string.Empty;
}
