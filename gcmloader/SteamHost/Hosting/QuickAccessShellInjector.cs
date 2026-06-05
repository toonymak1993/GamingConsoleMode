using SteamLoader.App.Infrastructure.Steam;
using SteamLoader.App.Services;

namespace SteamLoader.App.Hosting;

public sealed class QuickAccessShellInjector
{
    private readonly SteamDevToolsClient _devToolsClient;
    private readonly SteamLoaderHostState _hostState;
    private readonly Uri _apiBaseUri;
    private readonly SteamClientLaunchService _steamClientLaunchService;
    private readonly string _sharedScriptTemplate;
    private readonly string _popupScriptTemplate;
    private readonly string _themeSurfaceScriptTemplate;
    private bool _sharedReadyLogged;
    private bool _popupReadyLogged;
    private bool _themeSurfaceReadyLogged;
    private string? _sharedTargetId;
    private string? _quickAccessTargetId;
    private readonly HashSet<string> _themeSurfaceTargetIds = new(StringComparer.Ordinal);

    public QuickAccessShellInjector(
        SteamDevToolsClient devToolsClient,
        Uri apiBaseUri,
        SteamClientLaunchService steamClientLaunchService,
        string sharedScriptTemplate,
        string popupScriptTemplate,
        string themeSurfaceScriptTemplate,
        SteamLoaderHostState hostState)
    {
        _devToolsClient = devToolsClient;
        _apiBaseUri = apiBaseUri;
        _steamClientLaunchService = steamClientLaunchService;
        _sharedScriptTemplate = sharedScriptTemplate;
        _popupScriptTemplate = popupScriptTemplate;
        _themeSurfaceScriptTemplate = themeSurfaceScriptTemplate;
        _hostState = hostState;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EnsureInjectedAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ResetAttachedTargets($"Injector error: {exception.Message}");
                _hostState.UpdateError($"Injector error: {exception.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    private async Task EnsureInjectedAsync(CancellationToken cancellationToken)
    {
        var launchState = await _steamClientLaunchService.EnsureDevToolsReadyAsync(cancellationToken);
        if (!launchState.DevToolsReady)
        {
            ResetAttachedTargets(launchState.Message);
            return;
        }

        var sharedTarget = await _devToolsClient.GetSharedJsContextTargetAsync(cancellationToken);
        if (sharedTarget is null)
        {
            _sharedReadyLogged = false;
            _sharedTargetId = null;
            _hostState.UpdateSharedContext(false, "Waiting for Steam SharedJSContext.");
        }
        else if (!_sharedReadyLogged || !string.Equals(_sharedTargetId, sharedTarget.Id, StringComparison.Ordinal))
        {
            _sharedReadyLogged = await InjectIntoTargetAsync(
                sharedTarget,
                _sharedScriptTemplate,
                "SharedJSContext attached.",
                _sharedReadyLogged,
                (message) => _hostState.UpdateSharedContext(true, message),
                cancellationToken);

            if (_sharedReadyLogged)
            {
                _sharedTargetId = sharedTarget.Id;
            }
        }
        else
        {
            _hostState.UpdateSharedContext(true, "SharedJSContext attached.");
        }

        var quickAccessTarget = await _devToolsClient.GetQuickAccessTargetAsync(cancellationToken);
        if (quickAccessTarget is null)
        {
            _popupReadyLogged = false;
            _quickAccessTargetId = null;
            _hostState.UpdateQuickAccess(false, "Waiting for the Quick Access popup.");
        }
        else if (!_popupReadyLogged || !string.Equals(_quickAccessTargetId, quickAccessTarget.Id, StringComparison.Ordinal))
        {
            _popupReadyLogged = await InjectIntoTargetAsync(
                quickAccessTarget,
                _popupScriptTemplate,
                "Quick Access attached.",
                _popupReadyLogged,
                (message) => _hostState.UpdateQuickAccess(true, message),
                cancellationToken);

            if (_popupReadyLogged)
            {
                _quickAccessTargetId = quickAccessTarget.Id;
            }
        }
        else
        {
            _hostState.UpdateQuickAccess(true, "Quick Access attached.");
        }

        var themeSurfaceTargets = await _devToolsClient.GetThemeSurfaceTargetsAsync(cancellationToken);
        if (themeSurfaceTargets.Count == 0)
        {
            _themeSurfaceReadyLogged = false;
            _themeSurfaceTargetIds.Clear();
        }
        else
        {
            var activeThemeTargetIds = themeSurfaceTargets
                .Select(target => target.Id)
                .ToHashSet(StringComparer.Ordinal);

            _themeSurfaceTargetIds.RemoveWhere(id => !activeThemeTargetIds.Contains(id));

            foreach (var themeSurfaceTarget in themeSurfaceTargets)
            {
                if (_themeSurfaceTargetIds.Contains(themeSurfaceTarget.Id))
                {
                    continue;
                }

                var injected = await InjectIntoTargetAsync(
                    themeSurfaceTarget,
                    _themeSurfaceScriptTemplate,
                    "Theme surface attached.",
                    _themeSurfaceReadyLogged,
                    (_) => { },
                    cancellationToken);

                if (injected)
                {
                    _themeSurfaceReadyLogged = true;
                    _themeSurfaceTargetIds.Add(themeSurfaceTarget.Id);
                }
            }
        }
    }

    private async Task<bool> InjectIntoTargetAsync(
        SteamDevToolsTarget target,
        string scriptTemplate,
        string readyMessage,
        bool readyLogged,
        Action<string> setReadyState,
        CancellationToken cancellationToken)
    {
        var script = scriptTemplate.Replace("__STEAMLOADER_API_BASE__", _apiBaseUri.ToString(), StringComparison.Ordinal);

        var result = await _devToolsClient.EvaluateAsync(
            target.WebSocketDebuggerUrl,
            script,
            cancellationToken);

        if (!result.Success)
        {
            _hostState.UpdateError($"Injection failed: {result.ErrorMessage}");
            return readyLogged;
        }

        if (string.Equals(result.Value?.ToString(), "injected", StringComparison.Ordinal))
        {
            if (!readyLogged)
            {
                setReadyState(readyMessage);
                readyLogged = true;
            }
            else
            {
                setReadyState(readyMessage);
            }
        }

        return readyLogged;
    }

    private void ResetAttachedTargets(string message)
    {
        _sharedReadyLogged = false;
        _popupReadyLogged = false;
        _themeSurfaceReadyLogged = false;
        _sharedTargetId = null;
        _quickAccessTargetId = null;
        _themeSurfaceTargetIds.Clear();
        _hostState.UpdateSharedContext(false, message);
        _hostState.UpdateQuickAccess(false, message);
    }
}
