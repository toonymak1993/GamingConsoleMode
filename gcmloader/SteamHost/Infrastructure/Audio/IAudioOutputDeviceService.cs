using System.Collections.Generic;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.Audio;

public interface IAudioOutputDeviceService
{
    IReadOnlyList<AudioOutputDeviceInfo> GetPlaybackDevices();

    void SetDefaultPlaybackDevice(string deviceId);

    AudioVolumeInfo GetDefaultPlaybackVolume();

    AudioVolumeInfo SetDefaultPlaybackVolume(double volume);

    AudioVolumeInfo AdjustDefaultPlaybackVolume(double delta);

    AudioVolumeInfo ToggleDefaultPlaybackMute();
}
