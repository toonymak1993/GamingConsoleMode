namespace SteamLoader.App.Models;

public sealed record SteamProfileInfo(
    string PersonaName,
    string AccountName,
    string SteamId64,
    string AccountId,
    string ShortcutsPath);
