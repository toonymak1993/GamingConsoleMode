namespace SteamLoader.App.Models;

public sealed record HltbSettingsState(
    bool Enabled,
    bool ShowMainStory,
    bool ShowMainPlus,
    bool ShowCompletionist,
    bool ShowAllStyles,
    bool ShowViewDetails,
    int CacheEntryCount);

public sealed record HltbSnapshot(
    HltbSettingsState Settings,
    string StatusText);

public sealed record HltbGameSnapshot(
    string RequestedTitle,
    string MatchedTitle,
    int? AppId,
    int? GameId,
    string MainStory,
    string MainPlus,
    string Completionist,
    string AllStyles,
    string DetailUrl,
    bool Found,
    bool Cached,
    HltbSettingsState Settings,
    string? ErrorMessage);
