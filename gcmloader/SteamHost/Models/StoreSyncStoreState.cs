namespace SteamLoader.App.Models;

public sealed record StoreSyncStoreState(
    string Id,
    string Title,
    string Description,
    bool Enabled,
    bool IsReady,
    string StatusText,
    string DetailText,
    string PathValue,
    int DetectedTitleCount);
