using System.Net;
using System.Text;
using System.Text.Json;
using SteamLoader.App.Infrastructure.Audio;
using SteamLoader.App.Infrastructure.Display;
using SteamLoader.App.Infrastructure.Hltb;
using SteamLoader.App.Infrastructure.Processes;
using SteamLoader.App.Infrastructure.Settings;
using SteamLoader.App.Infrastructure.StoreSync;
using SteamLoader.App.Infrastructure.Steam;
using SteamLoader.App.Infrastructure.Themes;
using SteamLoader.App.Models;
using SteamLoader.App.Services;

namespace SteamLoader.App.Hosting;

public sealed class SteamLoaderApiServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAudioOutputDeviceService _audioOutputDeviceService;
    private readonly DisplaySwitchService _displaySwitchService;
    private readonly ProcessWindowService _processWindowService;
    private readonly HltbService _hltbService;
    private readonly StoreSyncService _storeSyncService;
    private readonly ThemesService _themesService;
    private readonly SteamLoaderSettingsService _steamLoaderSettingsService;
    private readonly PowerActionService _powerActionService;
    private readonly SteamFrontendComponentService _frontendComponentService;
    private readonly SteamLoaderHostState _hostState;
    private readonly HttpListener _listener;
    private readonly Action _requestShutdown;
    private readonly Action? _requestOpenManager;
    private Task? _acceptLoopTask;

    public SteamLoaderApiServer(
        IAudioOutputDeviceService audioOutputDeviceService,
        DisplaySwitchService displaySwitchService,
        ProcessWindowService processWindowService,
        HltbService hltbService,
        StoreSyncService storeSyncService,
        ThemesService themesService,
        SteamLoaderSettingsService steamLoaderSettingsService,
        PowerActionService powerActionService,
        SteamFrontendComponentService frontendComponentService,
        Uri baseUri,
        SteamLoaderHostState hostState,
        Action requestShutdown,
        Action? requestOpenManager = null)
    {
        _audioOutputDeviceService = audioOutputDeviceService;
        _displaySwitchService = displaySwitchService;
        _processWindowService = processWindowService;
        _hltbService = hltbService;
        _storeSyncService = storeSyncService;
        _themesService = themesService;
        _steamLoaderSettingsService = steamLoaderSettingsService;
        _powerActionService = powerActionService;
        _frontendComponentService = frontendComponentService;
        _hostState = hostState;
        _requestShutdown = requestShutdown;
        _requestOpenManager = requestOpenManager;
        _listener = new HttpListener();
        _listener.Prefixes.Add(baseUri.ToString());
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        if (_acceptLoopTask is not null)
        {
            await _acceptLoopTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _listener.Close();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (!_listener.IsListening)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        response.Headers["Cache-Control"] = "no-store";

        if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = (int)HttpStatusCode.NoContent;
            response.Close();
            return;
        }

        try
        {
            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/health")
            {
                await WriteTextAsync(response, HttpStatusCode.OK, "ok", cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/control/status")
            {
                await WriteJsonAsync(response, HttpStatusCode.OK, _hostState.Snapshot(), cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/frontend/components")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    await _frontendComponentService.GetSnapshotAsync(cancellationToken),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/control/shutdown")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    new { message = "Shutdown requested." },
                    cancellationToken);

                _requestShutdown();
                return;
            }

            if (TryResolvePluginId(request.Url?.AbsolutePath, out var pluginId) &&
                !_steamLoaderSettingsService.IsPluginEnabled(pluginId))
            {
                await WriteDisabledPluginResponseAsync(
                    response,
                    request.Url?.AbsolutePath,
                    pluginId,
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/audio/devices")
            {
                var devices = await StaThread.RunAsync(
                    () => _audioOutputDeviceService.GetPlaybackDevices(),
                    cancellationToken);

                await WriteJsonAsync(response, HttpStatusCode.OK, devices, cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/audio/default")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetDefaultDeviceRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.DeviceId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "Device ID is required." },
                        cancellationToken);
                    return;
                }

                await StaThread.RunAsync(
                    () =>
                    {
                        _audioOutputDeviceService.SetDefaultPlaybackDevice(payload.DeviceId);
                        return true;
                    },
                    cancellationToken);

                var devices = await StaThread.RunAsync(
                    () => _audioOutputDeviceService.GetPlaybackDevices(),
                    cancellationToken);

                await WriteJsonAsync(response, HttpStatusCode.OK, devices, cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/audio/volume")
            {
                var volumeInfo = await StaThread.RunAsync(
                    () => _audioOutputDeviceService.GetDefaultPlaybackVolume(),
                    cancellationToken);

                await WriteJsonAsync(response, HttpStatusCode.OK, volumeInfo, cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/audio/volume")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetVolumeRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || double.IsNaN(payload.Volume) || double.IsInfinity(payload.Volume))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A valid volume value is required." },
                        cancellationToken);
                    return;
                }

                var volumeInfo = await StaThread.RunAsync(
                    () => _audioOutputDeviceService.SetDefaultPlaybackVolume(payload.Volume),
                    cancellationToken);

                await WriteJsonAsync(response, HttpStatusCode.OK, volumeInfo, cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/audio/volume/adjust")
            {
                var payload = await JsonSerializer.DeserializeAsync<AdjustVolumeRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || double.IsNaN(payload.Delta) || double.IsInfinity(payload.Delta))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A valid volume delta is required." },
                        cancellationToken);
                    return;
                }

                var volumeInfo = await StaThread.RunAsync(
                    () => _audioOutputDeviceService.AdjustDefaultPlaybackVolume(payload.Delta),
                    cancellationToken);

                await WriteJsonAsync(response, HttpStatusCode.OK, volumeInfo, cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/audio/volume/toggle-mute")
            {
                var volumeInfo = await StaThread.RunAsync(
                    () => _audioOutputDeviceService.ToggleDefaultPlaybackMute(),
                    cancellationToken);

                await WriteJsonAsync(response, HttpStatusCode.OK, volumeInfo, cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/settings/state")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _steamLoaderSettingsService.GetSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/settings/autostart")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetBooleanValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null)
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A boolean value is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _steamLoaderSettingsService.SetRunOnWindowsSignIn(payload.Value),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/settings/hide-windows-shell")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetBooleanValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null)
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A boolean value is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _steamLoaderSettingsService.SetHideWindowsShellInConsoleMode(payload.Value),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/settings/plugins/enabled")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetPluginEnabledRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.PluginId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A plugin ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _steamLoaderSettingsService.SetPluginEnabled(payload.PluginId, payload.Enabled),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/settings/open-manager")
            {
                _requestOpenManager?.Invoke();

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _steamLoaderSettingsService.GetSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/display/internal")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _displaySwitchService.SwitchToInternalDisplay(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/display/external")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _displaySwitchService.SwitchToExternalDisplay(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/display/modes")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _displaySwitchService.GetModeSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/display/resolution")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetTextValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Value))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A resolution preset is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _displaySwitchService.SetResolutionPreset(payload.Value),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/display/refresh-rate")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetIntegerValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null)
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A refresh rate is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _displaySwitchService.SetRefreshRatePreset(payload.Value),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/processes/windows")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _processWindowService.GetSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/processes/activate")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetTextValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Value))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A window handle is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _processWindowService.ActivateWindow(payload.Value),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/hltb/state")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _hltbService.GetSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/hltb/game")
            {
                var title = request.QueryString["title"] ?? string.Empty;
                var appIdValue = request.QueryString["appId"];
                int? appId = int.TryParse(appIdValue, out var parsedAppId) && parsedAppId > 0
                    ? parsedAppId
                    : null;

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    await _hltbService.GetGameAsync(title, appId, cancellationToken),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/state")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.GetSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/state")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.GetSnapshot(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/resolve-css")
            {
                var title = request.QueryString["title"];
                var url = request.QueryString["url"];

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    new
                    {
                        css = _themesService.ResolveCssForTarget(title, url)
                    },
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/catalog/refresh")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.RefreshCatalog(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/settings/toggle")
            {
                var payload = await JsonSerializer.DeserializeAsync<ToggleSettingRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Key))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A setting key is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.ToggleSetting(payload.Key),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/themes/install")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetThemeInstalledRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ThemeId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A theme ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.SetThemeInstalled(payload.ThemeId, payload.Installed),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/themes/enabled")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetThemeEnabledRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ThemeId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A theme ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.SetThemeEnabled(payload.ThemeId, payload.Enabled),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/themes/option/toggle")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetThemeOptionRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ThemeId) || string.IsNullOrWhiteSpace(payload.OptionId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A theme ID and option ID are required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.ToggleThemeOption(payload.ThemeId, payload.OptionId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/themes/option/choice")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetThemeChoiceRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null ||
                    string.IsNullOrWhiteSpace(payload.ThemeId) ||
                    string.IsNullOrWhiteSpace(payload.OptionId) ||
                    string.IsNullOrWhiteSpace(payload.ChoiceId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A theme ID, option ID, and choice ID are required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.SetThemeChoice(payload.ThemeId, payload.OptionId, payload.ChoiceId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/themes/option/range/adjust")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetThemeRangeRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ThemeId) || string.IsNullOrWhiteSpace(payload.OptionId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A theme ID and option ID are required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.AdjustThemeRange(payload.ThemeId, payload.OptionId, payload.Delta),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/themes/option/range/reset")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetThemeOptionRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ThemeId) || string.IsNullOrWhiteSpace(payload.OptionId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A theme ID and option ID are required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.ResetThemeRange(payload.ThemeId, payload.OptionId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/profiles/create")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetTextValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.CreateProfile(payload?.Value ?? string.Empty),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/profiles/install")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetProfileRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ProfileId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A profile ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.InstallProfile(payload.ProfileId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/profiles/apply")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetProfileRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ProfileId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A profile ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.ApplyProfile(payload.ProfileId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/profiles/update")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetProfileRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ProfileId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A profile ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.UpdateProfile(payload.ProfileId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/themes/profiles/remove")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetProfileRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.ProfileId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A profile ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _themesService.RemoveProfile(payload.ProfileId),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/hltb/settings/toggle")
            {
                var payload = await JsonSerializer.DeserializeAsync<ToggleSettingRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Key))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A setting key is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _hltbService.ToggleSetting(payload.Key),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/hltb/cache/clear")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _hltbService.ClearCache(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/hltb/open-details")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetTextValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Value))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A detail URL is required." },
                        cancellationToken);
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = payload.Value,
                    UseShellExecute = true,
                });

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    new { message = "Opened the HowLongToBeat detail page." },
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/power/start-desktop")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _powerActionService.StartWindowsDesktop(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/power/restart-steam")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _powerActionService.RestartSteam(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/power/restart-steam-tools")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _powerActionService.RestartSteamTools(),
                    cancellationToken);

                _requestShutdown();
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/power/sleep")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _powerActionService.SleepWindows(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/power/restart-windows")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _powerActionService.RestartWindows(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/power/shutdown-windows")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _powerActionService.ShutDownWindows(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/settings/toggle")
            {
                var payload = await JsonSerializer.DeserializeAsync<ToggleSettingRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Key))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A setting key is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.ToggleSetting(payload.Key),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/settings/api-key")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetTextValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.SetSteamGridDbApiKey(payload?.Value ?? string.Empty),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/stores/enabled")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetStoreEnabledRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                if (payload is null || string.IsNullOrWhiteSpace(payload.StoreId))
                {
                    await WriteJsonAsync(
                        response,
                        HttpStatusCode.BadRequest,
                        new { message = "A store ID is required." },
                        cancellationToken);
                    return;
                }

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.SetStoreEnabled(payload.StoreId, payload.Enabled),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/stores/custom-path")
            {
                var payload = await JsonSerializer.DeserializeAsync<SetTextValueRequest>(
                    request.InputStream,
                    JsonOptions,
                    cancellationToken);

                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.SetCustomScanPath(payload?.Value ?? string.Empty),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/stores/custom-path/clear")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.ClearCustomScanPath(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/sync")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.RunSync(),
                    cancellationToken);
                return;
            }

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                request.Url?.AbsolutePath == "/api/store-sync/startup-sync")
            {
                await WriteJsonAsync(
                    response,
                    HttpStatusCode.OK,
                    _storeSyncService.RunStartupSync(),
                    cancellationToken);
                return;
            }

            await WriteJsonAsync(
                response,
                HttpStatusCode.NotFound,
                new { message = "Route not found." },
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            await WriteJsonAsync(
                response,
                HttpStatusCode.BadRequest,
                new { message = exception.Message },
                cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteJsonAsync(
                response,
                HttpStatusCode.InternalServerError,
                new { message = exception.Message },
                cancellationToken);
        }
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";

        await using var output = response.OutputStream;
        await JsonSerializer.SerializeAsync(output, payload, JsonOptions, cancellationToken);
    }

    private static async Task WriteTextAsync(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string text,
        CancellationToken cancellationToken)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/plain; charset=utf-8";

        var bytes = Encoding.UTF8.GetBytes(text);
        await using var output = response.OutputStream;
        await output.WriteAsync(bytes, cancellationToken);
    }

    private async Task WriteDisabledPluginResponseAsync(
        HttpListenerResponse response,
        string? path,
        string pluginId,
        CancellationToken cancellationToken)
    {
        if (string.Equals(path, "/api/themes/resolve-css", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(response, HttpStatusCode.OK, new { css = string.Empty }, cancellationToken);
            return;
        }

        if (string.Equals(path, "/api/hltb/game", StringComparison.OrdinalIgnoreCase))
        {
            var settings = new HltbSettingsState(false, false, false, false, false, false, 0);
            await WriteJsonAsync(
                response,
                HttpStatusCode.OK,
                new HltbGameSnapshot(
                    RequestedTitle: string.Empty,
                    MatchedTitle: string.Empty,
                    AppId: null,
                    GameId: null,
                    MainStory: string.Empty,
                    MainPlus: string.Empty,
                    Completionist: string.Empty,
                    AllStyles: string.Empty,
                    DetailUrl: string.Empty,
                    Found: false,
                    Cached: false,
                    Settings: settings,
                    ErrorMessage: "HLTB is disabled in Tools for Steam settings."),
                cancellationToken);
            return;
        }

        await WriteJsonAsync(
            response,
            HttpStatusCode.Forbidden,
            new { message = $"{pluginId} is disabled in Tools for Steam settings." },
            cancellationToken);
    }

    private static bool TryResolvePluginId(string? path, out string pluginId)
    {
        pluginId = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.TrimEnd('/');
        var pluginPrefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/api/audio"] = "audio",
            ["/api/display"] = "display",
            ["/api/processes"] = "processes",
            ["/api/hltb"] = "hltb",
            ["/api/store-sync"] = "store-sync",
            ["/api/themes"] = "themes",
            ["/api/power"] = "power"
        };

        foreach (var (prefix, id) in pluginPrefixes)
        {
            if (normalizedPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                pluginId = id;
                return true;
            }
        }

        return false;
    }

    private sealed record SetDefaultDeviceRequest(string DeviceId);

    private sealed record SetVolumeRequest(double Volume);

    private sealed record AdjustVolumeRequest(double Delta);

    private sealed record ToggleSettingRequest(string Key);

    private sealed record SetTextValueRequest(string Value);

    private sealed record SetBooleanValueRequest(bool Value);

    private sealed record SetIntegerValueRequest(int Value);

    private sealed record SetPluginEnabledRequest(string PluginId, bool Enabled);

    private sealed record SetStoreEnabledRequest(string StoreId, bool Enabled);

    private sealed record SetThemeInstalledRequest(string ThemeId, bool Installed);

    private sealed record SetThemeEnabledRequest(string ThemeId, bool Enabled);

    private sealed record SetThemeOptionRequest(string ThemeId, string OptionId);

    private sealed record SetThemeChoiceRequest(string ThemeId, string OptionId, string ChoiceId);

    private sealed record SetThemeRangeRequest(string ThemeId, string OptionId, int Delta);

    private sealed record SetProfileRequest(string ProfileId);
}
