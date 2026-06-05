using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.Audio;

public sealed class CoreAudioOutputDeviceService : IAudioOutputDeviceService
{
    private readonly CoreAudioController _controller = new();

    public IReadOnlyList<AudioOutputDeviceInfo> GetPlaybackDevices()
    {
        return _controller
            .GetPlaybackDevices(DeviceState.Active)
            .Select(device => new AudioOutputDeviceInfo(
                device.Id.ToString(),
                device.FullName,
                device.IsDefaultDevice))
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public void SetDefaultPlaybackDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device ID is required.", nameof(deviceId));
        }

        var device = _controller.GetDevice(Guid.Parse(deviceId));
        if (device is null)
        {
            throw new InvalidOperationException("The requested device was not found.");
        }

        // Windows separates default playback devices by role.
        // Setting both keeps the switch consistent across the usual paths.
        device.SetAsDefault();
        device.SetAsDefaultCommunications();
    }

    public AudioVolumeInfo GetDefaultPlaybackVolume()
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device is null)
        {
            throw new InvalidOperationException("No default playback device is available.");
        }

        return CreateVolumeInfo(device);
    }

    public AudioVolumeInfo SetDefaultPlaybackVolume(double volume)
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device is null)
        {
            throw new InvalidOperationException("No default playback device is available.");
        }

        device.Volume = Math.Clamp(volume, 0d, 100d);
        return CreateVolumeInfo(device);
    }

    public AudioVolumeInfo AdjustDefaultPlaybackVolume(double delta)
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device is null)
        {
            throw new InvalidOperationException("No default playback device is available.");
        }

        device.Volume = Math.Clamp(device.Volume + delta, 0d, 100d);
        return CreateVolumeInfo(device);
    }

    public AudioVolumeInfo ToggleDefaultPlaybackMute()
    {
        var device = _controller.DefaultPlaybackDevice;
        if (device is null)
        {
            throw new InvalidOperationException("No default playback device is available.");
        }

        device.ToggleMute();
        return CreateVolumeInfo(device);
    }

    private static AudioVolumeInfo CreateVolumeInfo(CoreAudioDevice device)
    {
        return new AudioVolumeInfo(
            device.Id.ToString(),
            device.FullName,
            device.Volume,
            device.IsMuted);
    }
}
