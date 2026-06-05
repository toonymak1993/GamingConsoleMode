namespace SteamLoader.App.Hosting;

public sealed class SteamLoaderHostState
{
    private readonly object _gate = new();

    private bool _sharedContextAttached;
    private bool _quickAccessAttached;
    private string _serviceMessage = "Starting background host...";
    private string? _lastError;

    public SteamLoaderHostState()
    {
        StartedAtUtc = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset StartedAtUtc { get; }

    public void UpdateSharedContext(bool attached, string message)
    {
        lock (_gate)
        {
            _sharedContextAttached = attached;
            _serviceMessage = message;

            if (attached)
            {
                _lastError = null;
            }
        }
    }

    public void UpdateQuickAccess(bool attached, string message)
    {
        lock (_gate)
        {
            _quickAccessAttached = attached;
            _serviceMessage = message;

            if (attached)
            {
                _lastError = null;
            }
        }
    }

    public void UpdateMessage(string message)
    {
        lock (_gate)
        {
            _serviceMessage = message;
        }
    }

    public void UpdateError(string message)
    {
        lock (_gate)
        {
            _lastError = message;
            _serviceMessage = message;
        }
    }

    public SteamLoaderHostStatus Snapshot()
    {
        lock (_gate)
        {
            return new SteamLoaderHostStatus(
                StartedAtUtc,
                _sharedContextAttached,
                _quickAccessAttached,
                _serviceMessage,
                _lastError);
        }
    }
}

public sealed record SteamLoaderHostStatus(
    DateTimeOffset StartedAtUtc,
    bool SharedContextAttached,
    bool QuickAccessAttached,
    string ServiceMessage,
    string? LastError);
