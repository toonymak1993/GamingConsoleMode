namespace SteamLoader.App.Models;

public sealed record StoreSyncLastSyncState(
    bool Succeeded,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Message,
    int ImportedCount,
    int RemovedCount,
    int SkippedCount);
