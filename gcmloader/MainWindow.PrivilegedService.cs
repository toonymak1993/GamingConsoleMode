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
            IsReady = true,
            IsRequired = false,
            Status = "Optional",
            Details = "WinPart shell handoff only runs when Windows already grants access."
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
        _cachedPrivilegedServiceHealth = null;
        _isPrivilegedServiceReady = true;
        _lastPrivilegedServiceCheckUtc = DateTimeOffset.UtcNow;
        _lastPrivilegedServiceMessage = "Local mode active";
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
