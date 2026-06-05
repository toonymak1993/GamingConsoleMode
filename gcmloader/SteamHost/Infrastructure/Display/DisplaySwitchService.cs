using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamLoader.App.Infrastructure.Display;

public sealed class DisplaySwitchService
{
    private const int EnumCurrentSettings = -1;
    private const uint DisplayDeviceAttachedToDesktop = 0x00000001;
    private const uint DisplayDevicePrimaryDevice = 0x00000004;
    private const uint DmPelsWidth = 0x00080000;
    private const uint DmPelsHeight = 0x00100000;
    private const uint DmDisplayFrequency = 0x00400000;
    private const uint CdsTest = 0x00000002;
    private const uint CdsUpdateRegistry = 0x00000001;
    private const int DispChangeSuccessful = 0;

    private static readonly string DisplaySwitchPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "DisplaySwitch.exe");

    private static readonly ResolutionPreset[] ResolutionPresets =
    [
        new("full-hd", "Full HD", 1920, 1080),
        new("2k", "2K", 2560, 1440),
        new("4k", "4K", 3840, 2160)
    ];

    private static readonly int[] RefreshRatePresets = [60, 120];

    public DisplaySwitchResult SwitchToInternalDisplay()
    {
        RunDisplaySwitch("/internal");
        return new DisplaySwitchResult("internal", "Internal display mode requested.");
    }

    public DisplaySwitchResult SwitchToExternalDisplay()
    {
        RunDisplaySwitch("/external");
        return new DisplaySwitchResult("external", "External display mode requested.");
    }

    public DisplayModeSnapshot GetModeSnapshot()
    {
        var context = ReadDisplayContext();
        return BuildSnapshot(context);
    }

    public DisplayModeSnapshot SetResolutionPreset(string presetId)
    {
        var preset = ResolutionPresets.FirstOrDefault(entry =>
            string.Equals(entry.Id, presetId, StringComparison.OrdinalIgnoreCase));

        if (preset is null)
        {
            throw new InvalidOperationException("Unknown resolution preset.");
        }

        var context = ReadDisplayContext();
        var candidate = PickModeForResolution(context, preset)
            ?? throw new InvalidOperationException($"{preset.Title} is not available on the current display.");

        ApplyMode(context.Device, candidate);
        return GetModeSnapshot();
    }

    public DisplayModeSnapshot SetRefreshRatePreset(int refreshRate)
    {
        if (!RefreshRatePresets.Contains(refreshRate))
        {
            throw new InvalidOperationException("Unknown refresh rate preset.");
        }

        var context = ReadDisplayContext();
        var current = context.CurrentMode
            ?? throw new InvalidOperationException("The current display mode could not be detected.");

        var candidate = context.Modes
            .Where(mode =>
                mode.Width == current.Width &&
                mode.Height == current.Height &&
                IsRefreshRateMatch(mode.RefreshRate, refreshRate))
            .OrderBy(mode => Math.Abs(mode.RefreshRate - refreshRate))
            .ThenByDescending(mode => mode.RefreshRate)
            .FirstOrDefault();

        if (candidate is null)
        {
            throw new InvalidOperationException($"{refreshRate}Hz is not available at the current resolution.");
        }

        ApplyMode(context.Device, candidate);
        return GetModeSnapshot();
    }

    private static void RunDisplaySwitch(string arguments)
    {
        if (!File.Exists(DisplaySwitchPath))
        {
            throw new InvalidOperationException("DisplaySwitch.exe could not be found on this Windows installation.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = DisplaySwitchPath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The Windows display switch could not be started.");

        process.WaitForExit(5000);
    }

    private static DisplayModeSnapshot BuildSnapshot(DisplayContext context)
    {
        var current = context.CurrentMode;
        var currentResolution = current is null
            ? null
            : new DisplayResolutionState(
                current.Width,
                current.Height,
                $"{current.Width} x {current.Height}");

        var currentRefreshRate = current is null
            ? null
            : new DisplayRefreshRateState(
                current.RefreshRate,
                $"{current.RefreshRate}Hz");

        var resolutionStates = ResolutionPresets
            .Select(preset =>
            {
                var available = context.Modes.Any(mode => mode.Width == preset.Width && mode.Height == preset.Height);
                var selected = current?.Width == preset.Width && current?.Height == preset.Height;
                return new DisplayPresetState(
                    preset.Id,
                    preset.Title,
                    $"{preset.Width} x {preset.Height}",
                    available,
                    selected);
            })
            .ToArray();

        var refreshRateStates = RefreshRatePresets
            .Select(refreshRate =>
            {
                var available = current is not null && context.Modes.Any(mode =>
                    mode.Width == current.Width &&
                    mode.Height == current.Height &&
                    IsRefreshRateMatch(mode.RefreshRate, refreshRate));
                var selected = current is not null && IsRefreshRateMatch(current.RefreshRate, refreshRate);
                return new DisplayPresetState(
                    refreshRate.ToString(),
                    $"{refreshRate}Hz",
                    current is null
                        ? "Current resolution unavailable"
                        : $"Use {refreshRate}Hz at {current.Width} x {current.Height}",
                    available,
                    selected);
            })
            .ToArray();

        var status = current is null
            ? "Current display mode could not be detected."
            : $"Current mode: {current.Width} x {current.Height} @ {current.RefreshRate}Hz.";

        return new DisplayModeSnapshot(
            status,
            new DisplayDeviceState(context.Device.DeviceName, context.Device.DeviceString),
            currentResolution,
            currentRefreshRate,
            resolutionStates,
            refreshRateStates);
    }

    private static DisplayModeCandidate? PickModeForResolution(DisplayContext context, ResolutionPreset preset)
    {
        var modes = context.Modes
            .Where(mode => mode.Width == preset.Width && mode.Height == preset.Height)
            .ToArray();

        if (modes.Length == 0)
        {
            return null;
        }

        var currentRefreshRate = context.CurrentMode?.RefreshRate;
        if (currentRefreshRate is not null)
        {
            var currentRateMatch = modes
                .OrderBy(mode => Math.Abs(mode.RefreshRate - currentRefreshRate.Value))
                .FirstOrDefault(mode => IsRefreshRateMatch(mode.RefreshRate, currentRefreshRate.Value));

            if (currentRateMatch is not null)
            {
                return currentRateMatch;
            }
        }

        foreach (var preferredRate in RefreshRatePresets.OrderByDescending(rate => rate))
        {
            var preferredMatch = modes
                .OrderBy(mode => Math.Abs(mode.RefreshRate - preferredRate))
                .FirstOrDefault(mode => IsRefreshRateMatch(mode.RefreshRate, preferredRate));

            if (preferredMatch is not null)
            {
                return preferredMatch;
            }
        }

        return modes.OrderByDescending(mode => mode.RefreshRate).First();
    }

    private static DisplayContext ReadDisplayContext()
    {
        var device = GetPrimaryDisplayDevice();
        var currentMode = GetCurrentMode(device.DeviceName);
        var modes = GetDisplayModes(device.DeviceName);
        return new DisplayContext(device, currentMode, modes);
    }

    private static DisplayDeviceInfo GetPrimaryDisplayDevice()
    {
        DisplayDeviceInfo? fallbackDevice = null;

        for (uint index = 0; ; index++)
        {
            var device = CreateDisplayDevice();
            if (!EnumDisplayDevices(null, index, ref device, 0))
            {
                break;
            }

            var attached = (device.StateFlags & DisplayDeviceAttachedToDesktop) != 0;
            if (!attached)
            {
                continue;
            }

            var deviceInfo = new DisplayDeviceInfo(device.DeviceName, device.DeviceString);
            if ((device.StateFlags & DisplayDevicePrimaryDevice) != 0)
            {
                return deviceInfo;
            }

            fallbackDevice ??= deviceInfo;
        }

        return fallbackDevice
            ?? throw new InvalidOperationException("No active Windows display was found.");
    }

    private static DisplayModeCandidate? GetCurrentMode(string deviceName)
    {
        var mode = CreateDevMode();
        return EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode)
            ? new DisplayModeCandidate((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency, mode)
            : null;
    }

    private static IReadOnlyList<DisplayModeCandidate> GetDisplayModes(string deviceName)
    {
        var modes = new List<DisplayModeCandidate>();
        var seenModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var modeIndex = 0; ; modeIndex++)
        {
            var mode = CreateDevMode();
            if (!EnumDisplaySettings(deviceName, modeIndex, ref mode))
            {
                break;
            }

            if (mode.dmPelsWidth == 0 || mode.dmPelsHeight == 0 || mode.dmDisplayFrequency == 0)
            {
                continue;
            }

            var key = $"{mode.dmPelsWidth}x{mode.dmPelsHeight}@{mode.dmDisplayFrequency}";
            if (!seenModes.Add(key))
            {
                continue;
            }

            modes.Add(new DisplayModeCandidate(
                (int)mode.dmPelsWidth,
                (int)mode.dmPelsHeight,
                (int)mode.dmDisplayFrequency,
                mode));
        }

        return modes
            .OrderBy(mode => mode.Width)
            .ThenBy(mode => mode.Height)
            .ThenBy(mode => mode.RefreshRate)
            .ToArray();
    }

    private static void ApplyMode(DisplayDeviceInfo device, DisplayModeCandidate candidate)
    {
        var mode = candidate.NativeMode;
        mode.dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
        mode.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
        mode.dmPelsWidth = (uint)candidate.Width;
        mode.dmPelsHeight = (uint)candidate.Height;
        mode.dmDisplayFrequency = (uint)candidate.RefreshRate;

        var testResult = ChangeDisplaySettingsEx(device.DeviceName, ref mode, 0, CdsTest, 0);
        if (testResult != DispChangeSuccessful)
        {
            throw new InvalidOperationException(
                $"Windows rejected {candidate.Width} x {candidate.Height} @ {candidate.RefreshRate}Hz for this display.");
        }

        var applyResult = ChangeDisplaySettingsEx(device.DeviceName, ref mode, 0, CdsUpdateRegistry, 0);
        if (applyResult != DispChangeSuccessful)
        {
            throw new InvalidOperationException(
                $"Windows could not apply {candidate.Width} x {candidate.Height} @ {candidate.RefreshRate}Hz.");
        }
    }

    private static bool IsRefreshRateMatch(int actualRefreshRate, int requestedRefreshRate)
    {
        return Math.Abs(actualRefreshRate - requestedRefreshRate) <= 1;
    }

    private static DISPLAY_DEVICE CreateDisplayDevice()
    {
        return new DISPLAY_DEVICE
        {
            cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>(),
            DeviceName = string.Empty,
            DeviceString = string.Empty,
            DeviceID = string.Empty,
            DeviceKey = string.Empty
        };
    }

    private static DEVMODE CreateDevMode()
    {
        return new DEVMODE
        {
            dmDeviceName = string.Empty,
            dmSize = (ushort)Marshal.SizeOf<DEVMODE>(),
            dmFormName = string.Empty
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? deviceName, uint deviceIndex, ref DISPLAY_DEVICE displayDevice, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNumber, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string deviceName, ref DEVMODE devMode, nint windowHandle, uint flags, nint parameters);

    private sealed record ResolutionPreset(string Id, string Title, int Width, int Height);

    private sealed record DisplayDeviceInfo(string DeviceName, string DeviceString);

    private sealed record DisplayContext(
        DisplayDeviceInfo Device,
        DisplayModeCandidate? CurrentMode,
        IReadOnlyList<DisplayModeCandidate> Modes);

    private sealed record DisplayModeCandidate(
        int Width,
        int Height,
        int RefreshRate,
        DEVMODE NativeMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public uint StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}

public sealed record DisplaySwitchResult(string Mode, string Message);

public sealed record DisplayModeSnapshot(
    string StatusText,
    DisplayDeviceState Display,
    DisplayResolutionState? CurrentResolution,
    DisplayRefreshRateState? CurrentRefreshRate,
    IReadOnlyList<DisplayPresetState> ResolutionPresets,
    IReadOnlyList<DisplayPresetState> RefreshRatePresets);

public sealed record DisplayDeviceState(
    string DeviceName,
    string DeviceLabel);

public sealed record DisplayResolutionState(
    int Width,
    int Height,
    string Label);

public sealed record DisplayRefreshRateState(
    int RefreshRate,
    string Label);

public sealed record DisplayPresetState(
    string Id,
    string Title,
    string Description,
    bool Available,
    bool Selected);
