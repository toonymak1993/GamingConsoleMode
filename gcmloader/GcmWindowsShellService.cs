using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace gcmloader;

internal static class GcmWindowsShellService
{
    private const string WinlogonKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string ShellValueName = "Shell";
    private const string DefaultShellCommand = "explorer.exe";
    private const int ExplorerShellReadyAttempts = 80;
    private const int ExplorerShellReadyDelayMs = 125;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    public static string ResolveStableExecutablePath(string executablePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return executablePath;
            }

            string normalizedPath = executablePath.Trim().Trim('"');
            string appxSegment = $"{Path.DirectorySeparatorChar}AppX{Path.DirectorySeparatorChar}";
            int appxIndex = normalizedPath.IndexOf(appxSegment, StringComparison.OrdinalIgnoreCase);
            if (appxIndex < 0)
            {
                return normalizedPath;
            }

            string stableCandidate = normalizedPath.Replace(
                appxSegment,
                $"{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);

            return File.Exists(stableCandidate) ? stableCandidate : normalizedPath;
        }
        catch
        {
            return executablePath;
        }
    }

    public static void SetEnabled(string executablePath, string? arguments, bool enabled)
    {
        string desiredCommand = BuildCommand(executablePath, arguments);
        if (enabled)
        {
            SetShellCommand(desiredCommand);
            return;
        }

        SetShellCommand(DefaultShellCommand);
    }

    public static void PrepareCurrentSession(string executablePath, string? arguments)
    {
        string desiredCommand = BuildCommand(executablePath, arguments);

        try
        {
            SetShellCommand(DefaultShellCommand);
        }
        catch
        {
        }

        try
        {
            SetShellCommand(desiredCommand);
        }
        catch
        {
        }
    }

    public static async Task<bool> StartWindowsShellForCurrentSessionAsync(
        string executablePath,
        string? arguments,
        bool restartExplorerIfFileWindowOnly = true)
    {
        string desiredCommand = BuildCommand(executablePath, arguments);

        try
        {
            SetShellCommand(DefaultShellCommand);
            await Task.Delay(150);

            if (!IsWindowsShellReady())
            {
                if (!IsExplorerRunning())
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = DefaultShellCommand,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    })?.Dispose();
                }
            }

            bool shellReady = await WaitForWindowsShellAsync(ExplorerShellReadyAttempts, ExplorerShellReadyDelayMs);
            await Task.Delay(500);
            return shellReady;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                SetShellCommand(desiredCommand);
            }
            catch
            {
            }
        }
    }

    public static void StartWindowsShellIfNeeded()
    {
        try
        {
            if (IsExplorerRunning())
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
            })?.Dispose();
        }
        catch
        {
        }
    }

    public static bool IsExplorerRunning()
    {
        return Process.GetProcessesByName("explorer")
            .Any(process =>
            {
                try
                {
                    return !process.HasExited;
                }
                catch
                {
                    return false;
                }
            });
    }

    public static bool IsWindowsShellReady()
    {
        return FindWindow("Shell_TrayWnd", null) != IntPtr.Zero;
    }

    public static async Task<bool> WaitForWindowsShellAsync(int maxAttempts, int delayMs)
    {
        if (IsWindowsShellReady())
        {
            return true;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            await Task.Delay(delayMs);
            if (IsWindowsShellReady())
            {
                return true;
            }
        }

        return false;
    }

    public static string GetShellCommand()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath, writable: false);
        return key?.GetValue(ShellValueName) as string ?? DefaultShellCommand;
    }

    private static void SetShellCommand(string command)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(WinlogonKeyPath, writable: true);
        key?.SetValue(
            ShellValueName,
            string.IsNullOrWhiteSpace(command) ? DefaultShellCommand : command,
            RegistryValueKind.String);

        try
        {
            using RegistryKey? currentUserKey = Registry.CurrentUser.OpenSubKey(WinlogonKeyPath, writable: true);
            currentUserKey?.DeleteValue(ShellValueName, throwOnMissingValue: false);
        }
        catch
        {
        }
    }

    private static string BuildCommand(string executablePath, string? arguments)
    {
        string stableExecutablePath = ResolveStableExecutablePath(executablePath);
        return string.IsNullOrWhiteSpace(arguments)
            ? $"\"{stableExecutablePath}\""
            : $"\"{stableExecutablePath}\" {arguments}";
    }
}
