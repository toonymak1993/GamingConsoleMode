namespace SteamLoader.App.Models;

public sealed record ProcessesSnapshot(
    IReadOnlyList<ProcessWindowInfo> Windows,
    string StatusText);
