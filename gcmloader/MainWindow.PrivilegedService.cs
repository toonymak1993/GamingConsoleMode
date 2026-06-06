using Microsoft.Win32;

namespace gcmloader;

public sealed partial class MainWindow
{
    private GcmServiceHealthSnapshot? _cachedPrivilegedServiceHealth;
    private bool _isPrivilegedServiceReady;
    private DateTimeOffset? _lastPrivilegedServiceCheckUtc;
    private string _lastPrivilegedServiceMessage = "Local mode not checked yet";
    private readonly Dictionary<string, GcmSubsystemHealth> _localRuntimeSubsystems = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Winlogon shell access"] = new()
        {
            Name = "Winlogon shell access",
            IsReady = false,
            IsRequired = true,
            Status = "Not checked yet",
            Details = "GCM keeps shell ownership in the current-user Winlogon shell entry, matching the SteamLoader shell model."
        },
        ["UAC policy access"] = new()
        {
            Name = "UAC policy access",
            IsReady = true,
            IsRequired = false,
            Status = "Optional",
            Details = "UAC policy changes are skipped unless Windows already allows them."
        },
        ["Keyboard redirect access"] = new()
        {
            Name = "Keyboard redirect access",
            IsReady = true,
            IsRequired = false,
            Status = "Ready",
            Details = "Keyboard redirect stays local and falls back cleanly when elevation is unavailable."
        },
        ["Touch keyboard control"] = new()
        {
            Name = "Touch keyboard control",
            IsReady = true,
            IsRequired = false,
            Status = "Ready",
            Details = "Touch keyboard startup is attempted directly from GCM without a helper service."
        }
    };

    private Task<bool> EnsurePrivilegedServiceReadyAsync(bool failFast, CancellationToken cancellationToken = default)
    {
        App.StartupTrace("Checking local runtime mode. No privileged service is required.");
        RefreshLocalRuntimeSubsystems();
        _cachedPrivilegedServiceHealth = null;
        _isPrivilegedServiceReady = true;
        _lastPrivilegedServiceCheckUtc = DateTimeOffset.UtcNow;
        _lastPrivilegedServiceMessage = BuildLocalRuntimeSummary();
        DispatcherQueue.TryEnqueue(RefreshSettingsOverlayValues);
        return Task.FromResult(true);
    }

    private async Task RefreshPrivilegedServiceStatusAsync(bool notifyUser = true, CancellationToken cancellationToken = default)
    {
        await EnsurePrivilegedServiceReadyAsync(failFast: false, cancellationToken);

        if (notifyUser)
        {
            DispatcherQueue.TryEnqueue(() => ShowInAppNotification("GCM local mode ready."));
        }

        DispatcherQueue.TryEnqueue(RefreshSettingsOverlayValues);
    }

    private void SchedulePrivilegedServiceHealthRefresh()
    {
        // Local mode: there is no background service to probe.
    }

    private static bool ShouldFailFastWhenPrivilegedServiceIsMissing()
    {
        return false;
    }

    private static void TryStartInstalledPrivilegedService()
    {
        // Intentionally unused in local mode.
    }

    private static void TryStopInstalledPrivilegedService()
    {
        // Intentionally unused in local mode.
    }

    private static bool TryGetPrivilegedServiceStatus(out string statusMessage)
    {
        statusMessage = "Local mode active";
        return true;
    }

    private Task<bool> TrySetWinlogonShellViaServiceAsync(string shellPath)
    {
        return Task.FromResult(false);
    }

    private Task<bool> TrySetUacModeViaServiceAsync(bool enablePrompts)
    {
        return Task.FromResult(false);
    }

    private Task<bool> TryConfigureKeyboardRedirectViaServiceAsync(bool enabled)
    {
        return Task.FromResult(false);
    }

    private Task<bool> TryEnsureTouchKeyboardServiceViaServiceAsync()
    {
        return Task.FromResult(false);
    }

    private Task<bool> TrySetWindowsServiceStartupModeViaServiceAsync(string serviceName, string startupMode)
    {
        return Task.FromResult(false);
    }

    private Task<bool> TryStopWindowsServiceViaServiceAsync(string serviceName)
    {
        return Task.FromResult(false);
    }

    private string BuildPrivilegedServiceStatusSummary()
    {
        if (_lastPrivilegedServiceCheckUtc == null)
        {
            return _lastPrivilegedServiceMessage;
        }

        return $"Local mode | {_lastPrivilegedServiceCheckUtc.Value.ToLocalTime():HH:mm:ss}";
    }

    private string BuildPrivilegedServiceLogSummary()
    {
        return ShortenForUi(_lastPrivilegedServiceMessage, "Local mode active");
    }

    private GcmSubsystemHealth? FindPrivilegedSubsystem(string name)
    {
        return _localRuntimeSubsystems.TryGetValue(name, out GcmSubsystemHealth? subsystem)
            ? subsystem
            : null;
    }

    private void RefreshLocalRuntimeSubsystems()
    {
        if (_localRuntimeSubsystems.TryGetValue("Winlogon shell access", out GcmSubsystemHealth? shellSubsystem))
        {
            string configuredShell = ReadConfiguredWinlogonShell();
            string shellExecutable = ExtractExecutablePath(configuredShell);

            if (IsGcmShellExecutable(shellExecutable))
            {
                shellSubsystem.IsReady = true;
                shellSubsystem.Status = "Registered";
                shellSubsystem.Details = $"Windows is configured to boot into {Path.GetFileName(shellExecutable)}.";
            }
            else if (string.IsNullOrWhiteSpace(shellExecutable))
            {
                shellSubsystem.IsReady = false;
                shellSubsystem.Status = "Missing";
                shellSubsystem.Details = "No Winlogon shell entry could be read. Reinstall or repair GCM if shell mode should be active.";
            }
            else if (string.Equals(shellExecutable, "explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                shellSubsystem.IsReady = false;
                shellSubsystem.Status = "Explorer owns boot";
                shellSubsystem.Details = "Windows is currently configured to boot into Explorer. Repair or reinstall GCM to claim shell ownership again.";
            }
            else
            {
                shellSubsystem.IsReady = false;
                shellSubsystem.Status = "Custom shell";
                shellSubsystem.Details = $"Winlogon currently points to {Path.GetFileName(shellExecutable)} instead of GCM.";
            }
        }
    }

    private static string ReadConfiguredWinlogonShell()
    {
        try
        {
            const string winlogonPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

            using RegistryKey? localMachineKey = Registry.LocalMachine.OpenSubKey(winlogonPath, false);
            string machineShell = localMachineKey?.GetValue("Shell")?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(machineShell))
            {
                return machineShell;
            }

            using RegistryKey? currentUserKey = Registry.CurrentUser.OpenSubKey(winlogonPath, false);
            return currentUserKey?.GetValue("Shell")?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractExecutablePath(string shellValue)
    {
        if (string.IsNullOrWhiteSpace(shellValue))
        {
            return string.Empty;
        }

        string trimmed = shellValue.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            int endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return trimmed.Substring(1, endQuote - 1);
            }
        }

        int exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return trimmed.Substring(0, exeIndex + 4).Trim();
        }

        return trimmed;
    }

    private static bool IsGcmShellExecutable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(executablePath), "gcmloader.exe", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildLocalRuntimeSummary()
    {
        GcmSubsystemHealth? shellSubsystem = FindPrivilegedSubsystem("Winlogon shell access");
        if (shellSubsystem == null)
        {
            return "Local mode active";
        }

        return $"Local mode | {shellSubsystem.Status}";
    }
}

internal sealed class GcmServiceHealthSnapshot
{
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ServiceVersion { get; set; } = string.Empty;
    public List<GcmSubsystemHealth> Subsystems { get; set; } = [];
}

internal sealed class GcmSubsystemHealth
{
    public string Name { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public bool IsRequired { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
