using System.Diagnostics;
using Microsoft.Win32;

namespace SteamLoader.App.Services;

public sealed class SteamClientLaunchService
{
    private static readonly TimeSpan ActionCooldown = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RestartGracePeriod = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(18);

    private readonly HttpClient _httpClient;
    private readonly Uri _debugEndpoint;
    private readonly string _fallbackSteamRootPath;

    private DateTimeOffset? _firstUnavailableAt;
    private DateTimeOffset _lastActionAt = DateTimeOffset.MinValue;
    private bool _restartedExistingSteam;

    public SteamClientLaunchService(HttpClient httpClient, Uri debugEndpoint, string fallbackSteamRootPath)
    {
        _httpClient = httpClient;
        _debugEndpoint = debugEndpoint;
        _fallbackSteamRootPath = fallbackSteamRootPath;
    }

    public async Task<SteamClientLaunchState> EnsureDevToolsReadyAsync(CancellationToken cancellationToken)
    {
        if (await IsDebugEndpointAvailableAsync(cancellationToken))
        {
            _firstUnavailableAt = null;
            _restartedExistingSteam = false;
            return new SteamClientLaunchState(true, "Steam DevTools endpoint is ready.");
        }

        var now = DateTimeOffset.UtcNow;
        _firstUnavailableAt ??= now;

        if (now - _lastActionAt < ActionCooldown)
        {
            return new SteamClientLaunchState(
                false,
                "Steam is starting with the DevTools endpoint enabled.");
        }

        var steamExePath = ResolveSteamExecutablePath();
        if (steamExePath is null)
        {
            return new SteamClientLaunchState(
                false,
                "Tools for Steam could not find steam.exe. Install Steam or start it once manually.");
        }

        var steamRunning = IsSteamRunning();
        if (!steamRunning)
        {
            LaunchSteam(steamExePath);
            _lastActionAt = now;
            return new SteamClientLaunchState(
                false,
                "Starting Steam in Gamepad UI with DevTools enabled.");
        }

        if (!_restartedExistingSteam && now - _firstUnavailableAt.Value >= RestartGracePeriod)
        {
            RestartSteamForDevTools(steamExePath);
            _lastActionAt = now;
            _restartedExistingSteam = true;
            return new SteamClientLaunchState(
                false,
                "Restarting Steam once so Tools for Steam can attach to the DevTools endpoint.");
        }

        return new SteamClientLaunchState(
            false,
            "Steam is running without the DevTools endpoint. Preparing a controlled restart.");
    }

    public static string BuildSteamLaunchArguments(bool launchBigPicture)
    {
        return launchBigPicture
            ? "-gamepadui -dev -cef-enable-debugging"
            : "-dev -cef-enable-debugging";
    }

    public SteamClientLaunchState RestartSteamForSteamTools()
    {
        var steamExePath = ResolveSteamExecutablePath();
        if (steamExePath is null)
        {
            return new SteamClientLaunchState(
                false,
                "Tools for Steam could not find steam.exe. Install Steam or start it once manually.");
        }

        RestartSteamForDevTools(steamExePath);
        _lastActionAt = DateTimeOffset.UtcNow;
        _firstUnavailableAt = _lastActionAt;
        _restartedExistingSteam = true;

        return new SteamClientLaunchState(
            false,
            "Steam is restarting in Gamepad UI with DevTools enabled.");
    }

    private async Task<bool> IsDebugEndpointAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_debugEndpoint, "/json/list"));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void RestartSteamForDevTools(string steamExePath)
    {
        CloseSteam();
        LaunchSteam(steamExePath);
    }

    private static void LaunchSteam(string steamExePath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = steamExePath,
            Arguments = BuildSteamLaunchArguments(launchBigPicture: true),
            WorkingDirectory = Path.GetDirectoryName(steamExePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.Dispose();
    }

    private void CloseSteam()
    {
        var steamProcesses = GetSteamProcesses().ToList();
        if (steamProcesses.Count == 0)
        {
            return;
        }

        foreach (var process in steamProcesses)
        {
            try
            {
                if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                {
                    process.CloseMainWindow();
                }
            }
            catch
            {
            }
        }

        var steamExePath = ResolveSteamExecutablePath();
        if (steamExePath is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamExePath,
                    Arguments = "-shutdown",
                    WorkingDirectory = Path.GetDirectoryName(steamExePath) ?? AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })?.Dispose();
            }
            catch
            {
            }
        }

        var timeoutAt = DateTime.UtcNow.Add(ShutdownTimeout);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (!IsSteamRunning())
            {
                return;
            }

            Thread.Sleep(300);
        }

        foreach (var process in GetSteamProcesses())
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }

        Thread.Sleep(800);
    }

    private string? ResolveSteamExecutablePath()
    {
        var candidates = new[]
        {
            GetRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamExe"),
            CombineRegistryPath(GetRegistryString(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath")),
            CombineRegistryPath(GetRegistryString(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath")),
            CombineRegistryPath(GetRegistryString(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath")),
            Path.Combine(_fallbackSteamRootPath, "steam.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steam.exe"),
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string? CombineRegistryPath(string? steamPath)
    {
        return string.IsNullOrWhiteSpace(steamPath)
            ? null
            : Path.Combine(steamPath, "steam.exe");
    }

    private static string? GetRegistryString(RegistryKey root, string keyPath, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath, writable: false);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSteamRunning()
    {
        return GetSteamProcesses().Any();
    }

    private static IEnumerable<Process> GetSteamProcesses()
    {
        return Process.GetProcesses()
            .Where(process =>
            {
                try
                {
                    return !process.HasExited &&
                        (string.Equals(process.ProcessName, "steam", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(process.ProcessName, "steamwebhelper", StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            });
    }
}

public sealed record SteamClientLaunchState(
    bool DevToolsReady,
    string Message);
