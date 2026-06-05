namespace SteamLoader.App.Models;

public sealed record StoreSyncSnapshot(
    SteamProfileInfo? SteamProfile,
    StoreSyncSettingsState Settings,
    IReadOnlyList<StoreSyncStoreState> Stores,
    StoreSyncLastSyncState? LastSync);
