namespace SteamLoader.App.Models;

public sealed record AudioVolumeInfo(
    string DeviceId,
    string DeviceName,
    double Volume,
    bool IsMuted);
