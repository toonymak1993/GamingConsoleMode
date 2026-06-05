namespace SteamLoader.App.Models;

public sealed record ProcessWindowInfo(
    string Handle,
    string Title,
    string ProcessName,
    int ProcessId,
    bool IsMinimized,
    bool IsForeground);
