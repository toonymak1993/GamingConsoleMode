namespace SteamLoader.App.Models;

public sealed record AudioOutputDeviceInfo(
    string Id,
    string Name,
    bool IsDefault);
