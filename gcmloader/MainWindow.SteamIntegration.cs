using System.Net.Http;
using System.Diagnostics;
using SteamLoader.App.Hosting;
using SteamLoader.App.Infrastructure.Assets;
using SteamLoader.App.Infrastructure.Audio;
using SteamLoader.App.Infrastructure.Display;
using SteamLoader.App.Infrastructure.Hltb;
using SteamLoader.App.Infrastructure.Processes;
using SteamLoader.App.Infrastructure.Settings;
using SteamLoader.App.Infrastructure.Steam;
using SteamLoader.App.Infrastructure.StoreSync;
using SteamLoader.App.Infrastructure.Themes;
using SteamLoader.App.Models;
using SteamLoader.App.Services;

namespace gcmloader
{
    public sealed partial class MainWindow
    {
        private const string SteamStoreSyncEnabledKey = "steam_store_sync_enabled";
        private const string SteamStoreSyncArtworkKey = "steam_store_sync_artwork";
        private const string SteamStoreSyncEpicKey = "steam_store_sync_epic_enabled";
        private const string SteamStoreSyncGogKey = "steam_store_sync_gog_enabled";
        private const string SteamStoreSyncXboxKey = "steam_store_sync_xbox_enabled";
        private const string SteamPluginHostEnabledKey = "steam_plugin_host_enabled";
        private const string SteamPluginHostDeveloperModeKey = "steam_plugin_host_dev_mode";

        private static readonly Uri SteamDevToolsBaseUri = new("http://127.0.0.1:8080");
        private static readonly Uri SteamPluginApiBaseUri = new("http://127.0.0.1:47652/");

        private readonly object _steamIntegrationGate = new();
        private readonly List<string> _steamIntegrationLog = new();
        private StoreSyncService? _steamStoreSyncService;
        private HltbService? _steamHltbService;
        private ThemesService? _steamThemesService;
        private SteamLoaderHostState? _steamPluginHostState;
        private CancellationTokenSource? _steamPluginHostCancellation;
        private Task? _steamPluginHostTask;
        private string _steamDevToolsStatusText = "Not checked yet";
        private DateTimeOffset? _steamDevToolsLastCheckedAtUtc;

        private string SteamIntegrationRootPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "steamhost");

        private string SteamIntegrationDataPath => Path.Combine(SteamIntegrationRootPath, "data");

        private string SteamIntegrationThemesPath => Path.Combine(SteamIntegrationRootPath, "themes");

        private void EnsureSteamIntegrationDefaults()
        {
            EnsureBooleanSetting(SteamStoreSyncEnabledKey, true);
            EnsureBooleanSetting(SteamStoreSyncArtworkKey, true);
            EnsureBooleanSetting(SteamStoreSyncEpicKey, true);
            EnsureBooleanSetting(SteamStoreSyncGogKey, true);
            EnsureBooleanSetting(SteamStoreSyncXboxKey, true);
            EnsureBooleanSetting(SteamPluginHostEnabledKey, true);
            EnsureBooleanSetting(SteamPluginHostDeveloperModeKey, true);
        }

        private bool IsSteamStoreSyncEnabled() => GetSetting(SteamStoreSyncEnabledKey, true);

        private bool IsSteamPluginHostEnabled() => GetSetting(SteamPluginHostEnabledKey, true);

        private bool ShouldLaunchSteamInDeveloperMode() => GetSetting(SteamPluginHostDeveloperModeKey, true);

        private StoreSyncService EnsureSteamStoreSyncService()
        {
            if (_steamStoreSyncService == null)
            {
                string steamRoot = ResolveSteamRootPath();
                _steamStoreSyncService = new StoreSyncService(
                    new StoreSyncSettingsStore(Path.Combine(SteamIntegrationDataPath, "store-sync.json")),
                    new SteamShortcutFile(),
                    new SteamGridDbArtworkDownloader(),
                    steamRoot);
            }

            SyncGcmSteamSettingsIntoStoreSync(_steamStoreSyncService);
            return _steamStoreSyncService;
        }

        private HltbService EnsureSteamHltbService()
        {
            _steamHltbService ??= new HltbService(
                new HltbSettingsStore(Path.Combine(SteamIntegrationDataPath, "hltb.json")));

            return _steamHltbService;
        }

        private ThemesService EnsureSteamThemesService()
        {
            _steamThemesService ??= new ThemesService(
                new ThemesSettingsStore(Path.Combine(SteamIntegrationDataPath, "themes.json")),
                "Assets/themes-catalog.json",
                "Assets/themes-profiles-catalog.json",
                SteamIntegrationThemesPath);

            return _steamThemesService;
        }

        private void SyncGcmSteamSettingsIntoStoreSync(StoreSyncService storeSyncService)
        {
            StoreSyncSnapshot snapshot = storeSyncService.GetSnapshot();

            void EnsureDownloadArtworkSetting(bool desiredValue)
            {
                if (snapshot.Settings.DownloadArtwork != desiredValue)
                {
                    snapshot = storeSyncService.ToggleSetting("download-artwork");
                }
            }

            void EnsureStoreToggle(string storeId, bool desiredValue)
            {
                StoreSyncStoreState? storeState = snapshot.Stores.FirstOrDefault(store =>
                    string.Equals(store.Id, storeId, StringComparison.OrdinalIgnoreCase));

                if (storeState == null || storeState.Enabled == desiredValue)
                {
                    return;
                }

                snapshot = storeSyncService.SetStoreEnabled(storeId, desiredValue);
            }

            EnsureDownloadArtworkSetting(GetSetting(SteamStoreSyncArtworkKey, true));
            EnsureStoreToggle("epic-games", GetSetting(SteamStoreSyncEpicKey, true));
            EnsureStoreToggle("gog-galaxy", GetSetting(SteamStoreSyncGogKey, true));
            EnsureStoreToggle("xbox-game-pass", GetSetting(SteamStoreSyncXboxKey, true));

            string configuredSteamGridDbKey = GetSetting("steamgriddb_api_key", string.Empty, false).Trim();
            if (!string.IsNullOrWhiteSpace(configuredSteamGridDbKey))
            {
                string configuredPreview = new SteamGridDbArtworkDownloader().GetPreview(configuredSteamGridDbKey);
                if (!string.Equals(snapshot.Settings.SteamGridDbApiKeyPreview, configuredPreview, StringComparison.OrdinalIgnoreCase))
                {
                    storeSyncService.SetSteamGridDbApiKey(configuredSteamGridDbKey);
                }
            }
        }

        private string ResolveSteamRootPath()
        {
            string steamExecutablePath = ResolveSteamExecutablePath();
            string? steamRoot = Path.GetDirectoryName(steamExecutablePath);

            if (!string.IsNullOrWhiteSpace(steamRoot) && Directory.Exists(steamRoot))
            {
                return steamRoot;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        }

        private async Task EnsureSteamPluginHostRunningAsync(bool notifyIfStarting)
        {
            Task? runningTask;
            lock (_steamIntegrationGate)
            {
                runningTask = _steamPluginHostTask;
                if (runningTask is { IsCompleted: false })
                {
                    return;
                }

                _steamPluginHostCancellation?.Dispose();
                _steamPluginHostCancellation = new CancellationTokenSource();
                _steamPluginHostState = new SteamLoaderHostState();
                _steamPluginHostTask = RunSteamPluginHostLoopAsync(_steamPluginHostCancellation.Token);
            }

            if (notifyIfStarting)
            {
                QueueSteamIntegrationNotification("Starting Steam plugin host...");
            }

            _ = RefreshSteamDevToolsStatusAsync(logResult: false);
            await Task.CompletedTask;
        }

        private async Task StopSteamPluginHostAsync(bool notifyIfStopping)
        {
            CancellationTokenSource? cancellationSource;
            Task? runningTask;

            lock (_steamIntegrationGate)
            {
                cancellationSource = _steamPluginHostCancellation;
                runningTask = _steamPluginHostTask;
                _steamPluginHostCancellation = null;
            }

            if (cancellationSource == null)
            {
                return;
            }

            if (notifyIfStopping)
            {
                QueueSteamIntegrationNotification("Stopping Steam plugin host...");
            }

            try
            {
                cancellationSource.Cancel();
                if (runningTask != null)
                {
                    await runningTask;
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                cancellationSource.Dispose();
                lock (_steamIntegrationGate)
                {
                    _steamPluginHostTask = null;
                    _steamPluginHostState = null;
                }
            }
        }

        private async Task RunSteamPluginHostLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(SteamIntegrationDataPath);
                Directory.CreateDirectory(SteamIntegrationThemesPath);

                StoreSyncService storeSyncService = EnsureSteamStoreSyncService();
                HltbService hltbService = EnsureSteamHltbService();
                ThemesService themesService = EnsureSteamThemesService();

                using HttpClient httpClient = new()
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };

                ProcessWindowService processWindowService = new();
                SteamDevToolsClient devToolsClient = new(httpClient, SteamDevToolsBaseUri);
                SteamFrontendComponentService frontendComponentService = new(devToolsClient);
                SteamClientLaunchService steamClientLaunchService = new(
                    httpClient,
                    SteamDevToolsBaseUri,
                    ResolveSteamRootPath());
                PowerActionService powerActionService = new(
                    steamClientLaunchService,
                    ScheduleSteamDesktopReturnFromHost,
                    ScheduleSteamPluginHostRestartFromHost);
                SteamLoaderSettingsService settingsService = new(
                    Path.Combine(SteamIntegrationDataPath, "tfs.json"),
                    AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
                string sharedScript = EmbeddedAssetReader.ReadText("Assets/quickaccess-shell.js");
                string popupScript = string.Join(
                    Environment.NewLine,
                    EmbeddedAssetReader.ReadText("Assets/st-frontend-lib.js"),
                    EmbeddedAssetReader.ReadText("Assets/quickaccess-popup.js"));
                string themeSurfaceScript = string.Join(
                    Environment.NewLine,
                    EmbeddedAssetReader.ReadText("Assets/theme-surface.js"),
                    EmbeddedAssetReader.ReadText("Assets/hltb-surface.js"));

                await using SteamLoaderApiServer apiServer = new(
                    new CoreAudioOutputDeviceService(),
                    new DisplaySwitchService(),
                    processWindowService,
                    hltbService,
                    storeSyncService,
                    themesService,
                    settingsService,
                    powerActionService,
                    frontendComponentService,
                    SteamPluginApiBaseUri,
                    _steamPluginHostState ?? new SteamLoaderHostState(),
                    () => _steamPluginHostCancellation?.Cancel(),
                    OpenToolsForSteamManagerFromHost);

                QuickAccessShellInjector injector = new(
                    devToolsClient,
                    SteamPluginApiBaseUri,
                    steamClientLaunchService,
                    sharedScript,
                    popupScript,
                    themeSurfaceScript,
                    _steamPluginHostState ?? new SteamLoaderHostState());

                _steamPluginHostState?.UpdateMessage("Steam plugin host is waiting for Steam.");
                await apiServer.StartAsync(cancellationToken);
                AppendSteamIntegrationLog("Steam plugin host API is running on 127.0.0.1:47652.");

                try
                {
                    await injector.RunAsync(cancellationToken);
                }
                finally
                {
                    await apiServer.StopAsync();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _steamPluginHostState?.UpdateError(ex.Message);
                QueueSteamIntegrationNotification($"Steam plugin host error: {ex.Message}");
            }
            finally
            {
                lock (_steamIntegrationGate)
                {
                    _steamPluginHostTask = null;
                }
            }
        }

        private async Task<bool> IsSteamDevToolsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpClient httpClient = new()
                {
                    Timeout = TimeSpan.FromSeconds(2)
                };

                using HttpResponseMessage response = await httpClient.GetAsync(new Uri(SteamDevToolsBaseUri, "/json/list"), cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                string payload = await response.Content.ReadAsStringAsync(cancellationToken);
                return !string.IsNullOrWhiteSpace(payload);
            }
            catch
            {
                return false;
            }
        }

        private async Task RefreshSteamDevToolsStatusAsync(bool logResult)
        {
            try
            {
                using HttpClient httpClient = new()
                {
                    Timeout = TimeSpan.FromSeconds(3)
                };

                SteamDevToolsClient devToolsClient = new(httpClient, SteamDevToolsBaseUri);
                IReadOnlyList<SteamDevToolsTarget> targets = await devToolsClient.GetTargetsAsync(CancellationToken.None);
                bool hasShared = targets.Any(target =>
                    string.Equals(target.Title, "SharedJSContext", StringComparison.OrdinalIgnoreCase));
                bool hasQuickAccess = targets.Any(target =>
                    target.Title.StartsWith("QuickAccess", StringComparison.OrdinalIgnoreCase));

                _steamDevToolsLastCheckedAtUtc = DateTimeOffset.UtcNow;
                _steamDevToolsStatusText = hasShared && hasQuickAccess
                    ? $"Online | Shared + Quick Access | {targets.Count} target(s)"
                    : hasShared
                        ? $"Partial | Shared only | {targets.Count} target(s)"
                        : hasQuickAccess
                            ? $"Partial | Quick Access only | {targets.Count} target(s)"
                            : $"Online | No Steam UI targets | {targets.Count} target(s)";

                if (logResult)
                {
                    AppendSteamIntegrationLog($"DevTools probe: {_steamDevToolsStatusText}.");
                }
            }
            catch (Exception ex)
            {
                _steamDevToolsLastCheckedAtUtc = DateTimeOffset.UtcNow;
                _steamDevToolsStatusText = $"Offline | {ShortenForUi(ex.Message, "Port unreachable")}";

                if (logResult)
                {
                    AppendSteamIntegrationLog($"DevTools probe failed: {ex.Message}");
                }
            }

            DispatcherQueue.TryEnqueue(() => RefreshSettingsOverlayValues());
        }

        private async Task RunSteamStoreSyncBeforeLaunchAsync()
        {
            try
            {
                QueueSteamIntegrationNotification("Syncing your launchers into Steam...");
                StoreSyncSnapshot snapshot = await EnsureSteamStoreSyncService().RunIntegratedSyncAsync(false);
                QueueSteamIntegrationNotification(snapshot.LastSync?.Message ?? "Steam sync finished.");
                RefreshSettingsOverlayValues();
            }
            catch (Exception ex)
            {
                QueueSteamIntegrationNotification($"Steam sync failed: {ex.Message}");
                Debug.WriteLine($"[SteamSync] {ex}");
            }
        }

        private string BuildSteamLaunchArguments()
        {
            List<string> arguments = ["-gamepadui"];

            if (IsSteamPluginHostEnabled() && ShouldLaunchSteamInDeveloperMode())
            {
                arguments.Add("-dev");
                arguments.Add("-cef-enable-debugging");
            }

            return string.Join(' ', arguments);
        }

        private void ScheduleSteamDesktopReturnFromHost()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() => BackToWindows());
            }
            catch (Exception ex)
            {
                AppendSteamIntegrationLog($"Desktop return scheduling failed: {ex.Message}");
            }
        }

        private void ScheduleSteamPluginHostRestartFromHost()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() => _ = RestartSteamPluginHostFromHostAsync());
            }
            catch (Exception ex)
            {
                AppendSteamIntegrationLog($"Host restart scheduling failed: {ex.Message}");
            }
        }

        private async Task RestartSteamPluginHostFromHostAsync()
        {
            AppendSteamIntegrationLog("Restarting Tools for Steam host...");
            await Task.Delay(250);
            await StopSteamPluginHostAsync(notifyIfStopping: false);
            await EnsureSteamPluginHostRunningAsync(notifyIfStarting: false);
            await RefreshSteamPluginHostStatusAsync();
        }

        private void OpenToolsForSteamManagerFromHost()
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    BringTaskManagerToFrontAndFocus();
                    OpenSettingsOverlay();

                    int steamCategoryIndex = _settingsCategories.FindIndex(category =>
                        string.Equals(category.Id, "steam", StringComparison.OrdinalIgnoreCase));

                    if (steamCategoryIndex >= 0)
                    {
                        SelectSettingsCategory(steamCategoryIndex);
                    }

                    _settingsPane = SettingsPane.Categories;
                    UpdateVisualFocus();
                    RefreshSettingsOverlayValues();
                });
            }
            catch (Exception ex)
            {
                AppendSteamIntegrationLog($"Open manager request failed: {ex.Message}");
            }
        }

        private async Task<bool> ShouldForceSteamRestartForPluginHostAsync(bool steamIsRunning)
        {
            if (!steamIsRunning || !IsSteamPluginHostEnabled() || !ShouldLaunchSteamInDeveloperMode())
            {
                return false;
            }

            return !await IsSteamDevToolsAvailableAsync();
        }

        private void ToggleSteamStoreSyncEnabledSetting()
        {
            AppSettings.Save(SteamStoreSyncEnabledKey, !GetSetting(SteamStoreSyncEnabledKey, true));
            RefreshSettingsOverlayValues();
        }

        private void ToggleSteamStoreSyncArtworkSetting()
        {
            StoreSyncSnapshot snapshot = EnsureSteamStoreSyncService().ToggleSetting("download-artwork");
            AppSettings.Save(SteamStoreSyncArtworkKey, snapshot.Settings.DownloadArtwork);
            RefreshSettingsOverlayValues();
        }

        private void ToggleSteamStoreEnabledSetting(string storeId, string settingsKey)
        {
            StoreSyncSnapshot snapshot = EnsureSteamStoreSyncService().GetSnapshot();
            bool currentValue = snapshot.Stores.FirstOrDefault(store =>
                    string.Equals(store.Id, storeId, StringComparison.OrdinalIgnoreCase))
                ?.Enabled ?? true;

            snapshot = EnsureSteamStoreSyncService().SetStoreEnabled(storeId, !currentValue);
            bool updatedValue = snapshot.Stores.FirstOrDefault(store =>
                    string.Equals(store.Id, storeId, StringComparison.OrdinalIgnoreCase))
                ?.Enabled ?? !currentValue;
            AppSettings.Save(settingsKey, updatedValue);
            RefreshSettingsOverlayValues();
        }

        private async Task RunSteamStoreSyncNowAsync()
        {
            try
            {
                StoreSyncSnapshot snapshot = await EnsureSteamStoreSyncService().RunIntegratedSyncAsync(false);
                QueueSteamIntegrationNotification(snapshot.LastSync?.Message ?? "Steam sync finished.");
                RefreshSettingsOverlayValues();
            }
            catch (Exception ex)
            {
                QueueSteamIntegrationNotification($"Steam sync failed: {ex.Message}");
            }
        }

        private async Task ToggleSteamPluginHostEnabledSettingAsync()
        {
            bool enabled = !GetSetting(SteamPluginHostEnabledKey, true);
            AppSettings.Save(SteamPluginHostEnabledKey, enabled);

            if (enabled)
            {
                await EnsureSteamPluginHostRunningAsync(notifyIfStarting: true);
            }
            else
            {
                await StopSteamPluginHostAsync(notifyIfStopping: true);
            }

            RefreshSettingsOverlayValues();
        }

        private void ToggleSteamPluginDeveloperModeSetting()
        {
            AppSettings.Save(SteamPluginHostDeveloperModeKey, !GetSetting(SteamPluginHostDeveloperModeKey, true));
            RefreshSettingsOverlayValues();
        }

        private async Task RefreshSteamPluginHostStatusAsync()
        {
            if (IsSteamPluginHostEnabled())
            {
                await EnsureSteamPluginHostRunningAsync(notifyIfStarting: false);
            }

            await RefreshSteamDevToolsStatusAsync(logResult: true);
            RefreshSettingsOverlayValues();
        }

        private void RefreshSteamIntegrationSettingsValues()
        {
            StoreSyncSnapshot snapshot = EnsureSteamStoreSyncService().GetSnapshot();

            SetSettingsRowValue("steam-store-sync-enabled", FormatEnabled(IsSteamStoreSyncEnabled()));
            SetSettingsRowValue("steam-store-sync-artwork", FormatEnabled(snapshot.Settings.DownloadArtwork));
            SetSettingsRowValue("steam-store-sync-epic", FormatSteamStoreState(snapshot, "epic-games"));
            SetSettingsRowValue("steam-store-sync-gog", FormatSteamStoreState(snapshot, "gog-galaxy"));
            SetSettingsRowValue("steam-store-sync-xbox", FormatSteamStoreState(snapshot, "xbox-game-pass"));
            SetSettingsRowValue("steam-store-sync-run", snapshot.LastSync?.Message ?? "Run now");

            SetSettingsRowValue("steam-plugin-host-enabled", FormatEnabled(IsSteamPluginHostEnabled()));
            SetSettingsRowValue("steam-plugin-host-devmode", FormatEnabled(ShouldLaunchSteamInDeveloperMode()));
            SetSettingsRowValue("steam-plugin-host-status", BuildSteamPluginHostStatusSummary());
            SetSettingsRowValue("steam-plugin-devtools-status", BuildSteamDevToolsStatusSummary());
            SetSettingsRowValue("steam-plugin-log", BuildSteamPluginLogSummary());

            string syncSummary = IsSteamStoreSyncEnabled() ? "Sync on" : "Sync off";
            string hostSummary = IsSteamPluginHostEnabled() ? "Host on" : "Host off";
            SetSettingsCategorySummary("steam", $"{syncSummary} | {hostSummary}");
        }

        private string FormatSteamStoreState(StoreSyncSnapshot snapshot, string storeId)
        {
            StoreSyncStoreState? store = snapshot.Stores.FirstOrDefault(item =>
                string.Equals(item.Id, storeId, StringComparison.OrdinalIgnoreCase));

            if (store == null)
            {
                return "Unknown";
            }

            return $"{FormatEnabled(store.Enabled)} | {store.StatusText}";
        }

        private string BuildSteamPluginHostStatusSummary()
        {
            if (!IsSteamPluginHostEnabled())
            {
                return "Disabled";
            }

            SteamLoaderHostStatus? snapshot = _steamPluginHostState?.Snapshot();
            if (snapshot == null)
            {
                return "Ready on next Steam launch";
            }

            if (!string.IsNullOrWhiteSpace(snapshot.LastError))
            {
                return $"Error | {ShortenForUi(snapshot.LastError, "Error")}";
            }

            if (snapshot.SharedContextAttached && snapshot.QuickAccessAttached)
            {
                return "Attached to Steam";
            }

            if (snapshot.SharedContextAttached)
            {
                return "SharedJSContext ready";
            }

            return snapshot.ServiceMessage.NullIfEmpty("Waiting for Steam");
        }

        private string BuildSteamDevToolsStatusSummary()
        {
            if (_steamDevToolsLastCheckedAtUtc == null)
            {
                return _steamDevToolsStatusText;
            }

            DateTime localTime = _steamDevToolsLastCheckedAtUtc.Value.ToLocalTime().DateTime;
            return $"{_steamDevToolsStatusText} | {localTime:HH:mm:ss}";
        }

        private string BuildSteamPluginLogSummary()
        {
            lock (_steamIntegrationGate)
            {
                return _steamIntegrationLog.Count == 0
                    ? "No service log yet"
                    : _steamIntegrationLog[^1];
            }
        }

        private void AppendSteamIntegrationLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (_steamIntegrationGate)
            {
                _steamIntegrationLog.Add($"{DateTime.Now:HH:mm:ss} | {message}");
                while (_steamIntegrationLog.Count > 12)
                {
                    _steamIntegrationLog.RemoveAt(0);
                }
            }
        }

        private void QueueSteamIntegrationNotification(string message)
        {
            AppendSteamIntegrationLog(message);

            try
            {
                DispatcherQueue.TryEnqueue(() => ShowInAppNotification(message));
            }
            catch
            {
            }
        }
    }
}
