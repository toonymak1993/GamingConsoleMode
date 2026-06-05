using SteamLoader.App.Infrastructure.Assets;
using SteamLoader.App.Infrastructure.Audio;
using SteamLoader.App.Infrastructure.Display;
using SteamLoader.App.Infrastructure.Hltb;
using SteamLoader.App.Infrastructure.Processes;
using SteamLoader.App.Infrastructure.Settings;
using SteamLoader.App.Infrastructure.StoreSync;
using SteamLoader.App.Infrastructure.Steam;
using SteamLoader.App.Infrastructure.Themes;
using SteamLoader.App.Services;

namespace SteamLoader.App.Hosting;

public sealed class SteamLoaderBackgroundHost
{
    private static readonly Uri DebugEndpoint = new("http://127.0.0.1:8080");
    private static readonly Uri ApiBaseUri = new("http://127.0.0.1:47652/");

    private readonly SteamLoaderHostState _hostState;

    public SteamLoaderBackgroundHost(SteamLoaderHostState hostState)
    {
        _hostState = hostState;
    }

    public async Task RunAsync(CancellationToken cancellationToken, Action requestShutdown)
    {
        _hostState.UpdateMessage("Background host is running.");
        var rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "steamhost");
        var dataPath = Path.Combine(rootPath, "data");

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var audioOutputDeviceService = new CoreAudioOutputDeviceService();
        var displaySwitchService = new DisplaySwitchService();
        var processWindowService = new ProcessWindowService();
        var hltbService = new HltbService(
            new HltbSettingsStore(Path.Combine(dataPath, "hltb.json")));
        var storeSyncSettingsStore = new StoreSyncSettingsStore(
            Path.Combine(dataPath, "store-sync.json"));
        var storeSyncService = new StoreSyncService(
            storeSyncSettingsStore,
            new SteamShortcutFile(),
            new SteamGridDbArtworkDownloader(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var themesService = new ThemesService(
            new ThemesSettingsStore(Path.Combine(dataPath, "themes.json")),
            "Assets/themes-catalog.json",
            "Assets/themes-profiles-catalog.json",
            Path.Combine(rootPath, "themes"));
        var steamLoaderSettingsService = new SteamLoaderSettingsService(
            Path.Combine(dataPath, "tfs.json"),
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var devToolsClient = new SteamDevToolsClient(httpClient, DebugEndpoint);
        var frontendComponentService = new SteamFrontendComponentService(devToolsClient);
        var steamClientLaunchService = new SteamClientLaunchService(
            httpClient,
            DebugEndpoint,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var powerActionService = new PowerActionService(
            steamClientLaunchService,
            startWindowsDesktop: null,
            restartSteamTools: requestShutdown);
        var sharedScript = EmbeddedAssetReader.ReadText("Assets/quickaccess-shell.js");
        var popupScript = string.Join(
            Environment.NewLine,
            EmbeddedAssetReader.ReadText("Assets/st-frontend-lib.js"),
            EmbeddedAssetReader.ReadText("Assets/quickaccess-popup.js"));
        var themeSurfaceScript = string.Join(
            Environment.NewLine,
            EmbeddedAssetReader.ReadText("Assets/theme-surface.js"),
            EmbeddedAssetReader.ReadText("Assets/hltb-surface.js"));

        await using var apiServer = new SteamLoaderApiServer(
            audioOutputDeviceService,
            displaySwitchService,
            processWindowService,
            hltbService,
            storeSyncService,
            themesService,
            steamLoaderSettingsService,
            powerActionService,
            frontendComponentService,
            ApiBaseUri,
            _hostState,
            requestShutdown);

        var injector = new QuickAccessShellInjector(
            devToolsClient,
            ApiBaseUri,
            steamClientLaunchService,
            sharedScript,
            popupScript,
            themeSurfaceScript,
            _hostState);

        await apiServer.StartAsync(cancellationToken);

        try
        {
            await injector.RunAsync(cancellationToken);
        }
        finally
        {
            _hostState.UpdateMessage("Background host stopped.");
            await apiServer.StopAsync();
        }
    }
}
