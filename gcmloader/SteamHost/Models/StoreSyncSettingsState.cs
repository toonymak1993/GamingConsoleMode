namespace SteamLoader.App.Models;

public sealed record StoreSyncSettingsState(
    bool SteamGridDbApiKeyConfigured,
    string SteamGridDbApiKeyPreview,
    bool DownloadArtwork,
    bool PreferAnimatedArtwork,
    bool CloseSteamBeforeSync,
    bool BackupShortcuts,
    bool LaunchBigPictureAfterSync);
