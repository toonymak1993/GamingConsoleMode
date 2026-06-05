using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamLoader.App.Services;

public sealed class PowerActionService
{
    private readonly SteamClientLaunchService _steamClientLaunchService;
    private readonly Action? _startWindowsDesktop;
    private readonly Action? _restartSteamTools;

    public PowerActionService(
        SteamClientLaunchService steamClientLaunchService,
        Action? startWindowsDesktop,
        Action? restartSteamTools)
    {
        _steamClientLaunchService = steamClientLaunchService;
        _startWindowsDesktop = startWindowsDesktop;
        _restartSteamTools = restartSteamTools;
    }

    public PowerActionResult StartWindowsDesktop()
    {
        _startWindowsDesktop?.Invoke();
        return new PowerActionResult("Windows desktop is starting.");
    }

    public PowerActionResult RestartSteam()
    {
        return new PowerActionResult(_steamClientLaunchService.RestartSteamForSteamTools().Message);
    }

    public PowerActionResult RestartSteamTools()
    {
        _restartSteamTools?.Invoke();
        return new PowerActionResult("Tools for Steam background host is restarting.");
    }

    public PowerActionResult SleepWindows()
    {
        SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false);
        return new PowerActionResult("Windows is going to sleep.");
    }

    public PowerActionResult RestartWindows()
    {
        StartShutdownCommand("/r /t 0");
        return new PowerActionResult("Windows restart requested.");
    }

    public PowerActionResult ShutDownWindows()
    {
        StartShutdownCommand("/s /t 0");
        return new PowerActionResult("Windows shutdown requested.");
    }

    private static void StartShutdownCommand(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        })?.Dispose();
    }

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
}

public sealed record PowerActionResult(string Message);
