using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Win32;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.StoreSync;

public sealed class StoreSyncService
{
    private const ulong SteamIdOffset = 76561197960265728UL;
    private const string ManagedShortcutMarker = "steamloader://managed";
    private static readonly string[] IgnoreCustomExecutableTokens =
    [
        "bootstrap",
        "bootstrapper",
        "launcher",
        "helper",
        "helpers",
        "tool",
        "tools",
        "service",
        "services",
        "sdk",
        "crash",
        "crashreporter",
        "crashpad",
        "report",
        "benchmark",
        "config",
        "configurator",
        "setup",
        "install",
        "unins",
        "uninstall",
        "patch",
        "update",
        "updater",
        "redist",
        "redistributable",
        "easyanticheat",
        "eadesktop",
        "ealauncher",
        "cefprocess",
        "prereq",
        "prerequisite",
        "editor",
        "engine",
        "unrealcefsubprocess",
        "unitycrashhandler",
        "shadercompileworker",
        "bugreport",
        "server",
        "dedicatedserver",
        "test"
    ];

    private static readonly string[] IgnoreCustomDirectoryTokens =
    [
        ".egstore",
        "__installer",
        "_redist",
        "engine",
        "engines",
        "redistributable",
        "redist",
        "prereq",
        "prerequisites",
        "launcher",
        "launchers",
        "support",
        "helper",
        "helpers",
        "tools",
        "tool",
        "editor",
        "editors",
        "sdk",
        "sdks",
        "modkit",
        "modkits",
        "commonredist",
        "steaminput"
    ];

    private static readonly string[] StructuralDirectoryTokens =
    [
        "bin",
        "bins",
        "binary",
        "binaries",
        "x64",
        "x86",
        "win64",
        "win32",
        "windows",
        "shipping",
        "release",
        "debug"
    ];

    private static readonly StoreDefinition[] StoreDefinitions =
    [
        new(
            "epic-games",
            "Epic Games",
            "Reads installed titles directly from `LauncherInstalled.dat` so you do not need to manage executables manually."),
        new(
            "gog-galaxy",
            "GOG Galaxy",
            "Checks GOG registry entries and imports classic Windows installs without another launcher."),
        new(
            "xbox-game-pass",
            "Xbox / Game Pass",
            "Scans common Xbox app library folders such as `XboxGames` and `ModifiableWindowsApps`."),
        new(
            "custom-locations",
            "Custom Locations",
            "Perfect for SSD library folders, emulator setups, or installs that do not belong to a launcher.",
            SupportsCustomPath: true),
    ];

    private readonly object _gate = new();
    private readonly StoreSyncSettingsStore _settingsStore;
    private readonly SteamShortcutFile _shortcutFile;
    private readonly SteamGridDbArtworkDownloader _artworkDownloader;
    private readonly string _steamRootPath;
    private Task? _activeSyncTask;

    internal StoreSyncService(
        StoreSyncSettingsStore settingsStore,
        SteamShortcutFile shortcutFile,
        SteamGridDbArtworkDownloader artworkDownloader,
        string steamRootPath)
    {
        _settingsStore = settingsStore;
        _shortcutFile = shortcutFile;
        _artworkDownloader = artworkDownloader;
        _steamRootPath = steamRootPath;
    }

    public StoreSyncSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return BuildSnapshot(_settingsStore.Load());
        }
    }

    public StoreSyncSnapshot ToggleSetting(string key)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();

            switch (key)
            {
                case "download-artwork":
                    configuration.DownloadArtwork = !configuration.DownloadArtwork;
                    break;
                case "prefer-animated-artwork":
                    configuration.PreferAnimatedArtwork = !configuration.PreferAnimatedArtwork;
                    break;
                case "close-steam-before-sync":
                    configuration.CloseSteamBeforeSync = !configuration.CloseSteamBeforeSync;
                    break;
                case "backup-shortcuts":
                    configuration.BackupShortcuts = !configuration.BackupShortcuts;
                    break;
                case "launch-big-picture-after-sync":
                    configuration.LaunchBigPictureAfterSync = !configuration.LaunchBigPictureAfterSync;
                    break;
                default:
                    throw new InvalidOperationException("Unknown Store Sync setting.");
            }

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public StoreSyncSnapshot SetSteamGridDbApiKey(string value)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            configuration.SteamGridDbApiKey = value.Trim();
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public StoreSyncSnapshot SetStoreEnabled(string storeId, bool enabled)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            var storeConfiguration = GetStoreConfiguration(configuration, storeId);
            storeConfiguration.Enabled = enabled;
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public StoreSyncSnapshot SetCustomScanPath(string path)
    {
        lock (_gate)
        {
            var trimmedPath = path.Trim();
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                throw new InvalidOperationException("A folder path is required.");
            }

            var fullPath = Path.GetFullPath(trimmedPath);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException("The custom folder does not exist.");
            }

            var configuration = _settingsStore.Load();
            var storeConfiguration = GetStoreConfiguration(configuration, "custom-locations");
            storeConfiguration.ScanPath = fullPath;
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public StoreSyncSnapshot ClearCustomScanPath()
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            var storeConfiguration = GetStoreConfiguration(configuration, "custom-locations");
            storeConfiguration.ScanPath = string.Empty;
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public StoreSyncSnapshot RunSync()
    {
        lock (_gate)
        {
            if (_activeSyncTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("A Store Sync run is already in progress.");
            }

            var configuration = _settingsStore.Load();
            var profile = ResolveSteamProfile();
            if (profile is null)
            {
                throw new InvalidOperationException("Steam profile data could not be resolved.");
            }

            var startedAt = DateTimeOffset.UtcNow;
            configuration.LastSync = new StoreSyncLastSyncState(
                Succeeded: true,
                StartedAtUtc: startedAt,
                CompletedAtUtc: startedAt,
                Message: "Tools for Steam is closing Steam, syncing your shortcuts, and preparing the restart.",
                ImportedCount: 0,
                RemovedCount: 0,
                SkippedCount: 0);

            _settingsStore.Save(configuration);
            _activeSyncTask = Task.Run(() => RunSyncInBackgroundAsync(startedAt, launchSteamWhenFinished: false));
            return BuildSnapshot(configuration);
        }
    }

    public StoreSyncSnapshot RunStartupSync()
    {
        lock (_gate)
        {
            if (_activeSyncTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("A Store Sync run is already in progress.");
            }

            var configuration = _settingsStore.Load();
            var profile = ResolveSteamProfile();
            if (profile is null)
            {
                throw new InvalidOperationException("Steam profile data could not be resolved.");
            }

            var startedAt = DateTimeOffset.UtcNow;
            configuration.LastSync = new StoreSyncLastSyncState(
                Succeeded: true,
                StartedAtUtc: startedAt,
                CompletedAtUtc: startedAt,
                Message: "Tools for Steam is syncing your launchers before starting Steam.",
                ImportedCount: 0,
                RemovedCount: 0,
                SkippedCount: 0);

            _settingsStore.Save(configuration);
            _activeSyncTask = Task.Run(() => RunSyncInBackgroundAsync(startedAt, launchSteamWhenFinished: true));
            return BuildSnapshot(configuration);
        }
    }

    public async Task<StoreSyncSnapshot> RunIntegratedSyncAsync(
        bool launchSteamWhenFinished,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset startedAt;

        lock (_gate)
        {
            if (_activeSyncTask is { IsCompleted: false })
            {
                throw new InvalidOperationException("A Store Sync run is already in progress.");
            }

            var configuration = _settingsStore.Load();
            var profile = ResolveSteamProfile();
            if (profile is null)
            {
                throw new InvalidOperationException("Steam profile data could not be resolved.");
            }

            startedAt = DateTimeOffset.UtcNow;
            configuration.LastSync = new StoreSyncLastSyncState(
                Succeeded: true,
                StartedAtUtc: startedAt,
                CompletedAtUtc: startedAt,
                Message: launchSteamWhenFinished
                    ? "Tools for Steam is syncing your launchers before starting Steam."
                    : "Tools for Steam is syncing your launchers right now.",
                ImportedCount: 0,
                RemovedCount: 0,
                SkippedCount: 0);

            _settingsStore.Save(configuration);
            _activeSyncTask = Task.CompletedTask;
        }

        await RunSyncCoreAsync(startedAt, launchSteamWhenFinished, cancellationToken);

        lock (_gate)
        {
            return BuildSnapshot(_settingsStore.Load());
        }
    }

    private StoreSyncSnapshot BuildSnapshot(StoreSyncConfiguration configuration)
    {
        var profile = ResolveSteamProfile();
        var storeSnapshots = BuildStoreSnapshots(configuration);

        return new StoreSyncSnapshot(
            profile,
            new StoreSyncSettingsState(
                SteamGridDbApiKeyConfigured: !string.IsNullOrWhiteSpace(_artworkDownloader.GetEffectiveApiKey(configuration.SteamGridDbApiKey)),
                SteamGridDbApiKeyPreview: _artworkDownloader.GetPreview(configuration.SteamGridDbApiKey),
                DownloadArtwork: configuration.DownloadArtwork,
                PreferAnimatedArtwork: configuration.PreferAnimatedArtwork,
                CloseSteamBeforeSync: configuration.CloseSteamBeforeSync,
                BackupShortcuts: configuration.BackupShortcuts,
                LaunchBigPictureAfterSync: configuration.LaunchBigPictureAfterSync),
            storeSnapshots.Select(snapshot => new StoreSyncStoreState(
                snapshot.Definition.Id,
                snapshot.Definition.Title,
                snapshot.Definition.Description,
                snapshot.Configuration.Enabled,
                snapshot.Scan.IsReady,
                snapshot.Scan.StatusText,
                snapshot.Scan.DetailText,
                snapshot.Configuration.ScanPath,
                snapshot.Scan.Games.Count)).ToList(),
            configuration.LastSync);
    }

    private List<StoreSnapshot> BuildStoreSnapshots(StoreSyncConfiguration configuration)
    {
        return StoreDefinitions
            .Select(definition =>
            {
                var storeConfiguration = GetStoreConfiguration(configuration, definition.Id);
                var scan = ScanStore(definition, storeConfiguration);
                return new StoreSnapshot(definition, storeConfiguration, scan);
            })
            .ToList();
    }

    private StoreScanResult ScanStore(StoreDefinition definition, StoreSyncStoreConfiguration configuration)
    {
        return definition.Id switch
        {
            "epic-games" => ScanEpicGames(),
            "gog-galaxy" => ScanGogGames(),
            "xbox-game-pass" => ScanXboxGames(),
            "custom-locations" => ScanCustomLocations(configuration.ScanPath),
            _ => new StoreScanResult(false, "Unknown store", "The store definition is not supported.", []),
        };
    }

    private StoreScanResult ScanEpicGames()
    {
        var manifestPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "UnrealEngineLauncher",
            "LauncherInstalled.dat");

        if (!File.Exists(manifestPath))
        {
            return new StoreScanResult(false, "Not installed", "Epic Games Launcher was not detected.", []);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("InstallationList", out var installationList) ||
                installationList.ValueKind != JsonValueKind.Array)
            {
                return new StoreScanResult(true, "Ready", "Epic metadata is available, but no installed titles were found.", []);
            }

            var games = new List<StoreGameEntry>();

            foreach (var item in installationList.EnumerateArray())
            {
                var installLocation = GetJsonString(item, "InstallLocation");
                var title = GetJsonString(item, "DisplayName");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = GetJsonString(item, "AppName");
                }

                var launchExecutable = GetJsonString(item, "LaunchExecutable");
                var executablePath = ResolveExecutablePath(installLocation ?? string.Empty, launchExecutable);
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                var launchOptions = GetJsonString(item, "LaunchCommand") ?? string.Empty;
                games.Add(new StoreGameEntry("epic-games", title, executablePath, Path.GetDirectoryName(executablePath) ?? installLocation ?? string.Empty, launchOptions));
            }

            return new StoreScanResult(
                true,
                "Ready",
                games.Count > 0
                    ? $"{games.Count} installed title{(games.Count == 1 ? string.Empty : "s")} detected."
                    : "Epic metadata is available, but no installed titles were found.",
                games);
        }
        catch (Exception exception)
        {
            return new StoreScanResult(false, "Error", exception.Message, []);
        }
    }

    private StoreScanResult ScanGogGames()
    {
        var games = new List<StoreGameEntry>();
        var roots = new[]
        {
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games"),
            Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\Games"),
        };

        try
        {
            foreach (var root in roots.Where(root => root is not null))
            {
                using (root)
                {
                    foreach (var subKeyName in root!.GetSubKeyNames())
                    {
                        using var gameKey = root.OpenSubKey(subKeyName);
                        if (gameKey is null)
                        {
                            continue;
                        }

                        var title =
                            gameKey.GetValue("gameName") as string
                            ?? gameKey.GetValue("GAMENAME") as string
                            ?? gameKey.GetValue("DisplayName") as string
                            ?? subKeyName;

                        var installPath =
                            gameKey.GetValue("path") as string
                            ?? gameKey.GetValue("PATH") as string
                            ?? string.Empty;

                        var executableHint =
                            gameKey.GetValue("exe") as string
                            ?? gameKey.GetValue("gameExe") as string
                            ?? gameKey.GetValue("launchCommand") as string
                            ?? string.Empty;

                        var executablePath = ResolveExecutablePath(installPath, executableHint);
                        if (string.IsNullOrWhiteSpace(executablePath))
                        {
                            continue;
                        }

                        games.Add(new StoreGameEntry(
                            "gog-galaxy",
                            title,
                            executablePath,
                            Path.GetDirectoryName(executablePath) ?? installPath,
                            string.Empty));
                    }
                }
            }

            return games.Count > 0
                ? new StoreScanResult(true, "Ready", $"{games.Count} installed title{(games.Count == 1 ? string.Empty : "s")} detected.", games)
                : new StoreScanResult(false, "Not installed", "No GOG library entries were detected on this system.", []);
        }
        catch (Exception exception)
        {
            return new StoreScanResult(false, "Error", exception.Message, []);
        }
    }

    private StoreScanResult ScanXboxGames()
    {
        try
        {
            var libraryRoots = DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
                .SelectMany(drive => new[]
                {
                    Path.Combine(drive.RootDirectory.FullName, "XboxGames"),
                    Path.Combine(drive.RootDirectory.FullName, "ModifiableWindowsApps"),
                })
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (libraryRoots.Count == 0)
            {
                return new StoreScanResult(false, "Not installed", "No Xbox library folders were found.", []);
            }

            var games = new List<StoreGameEntry>();
            foreach (var libraryRoot in libraryRoots)
            {
                foreach (var folder in Directory.GetDirectories(libraryRoot))
                {
                    var executablePath = FindBestExecutable(folder);
                    if (string.IsNullOrWhiteSpace(executablePath))
                    {
                        continue;
                    }

                    games.Add(new StoreGameEntry(
                        "xbox-game-pass",
                        Path.GetFileName(folder),
                        executablePath,
                        Path.GetDirectoryName(executablePath) ?? folder,
                        string.Empty));
                }
            }

            return new StoreScanResult(
                true,
                "Ready",
                games.Count > 0
                    ? $"{games.Count} installed title{(games.Count == 1 ? string.Empty : "s")} detected."
                    : "Xbox libraries were found, but no launchable executables were detected.",
                games);
        }
        catch (Exception exception)
        {
            return new StoreScanResult(false, "Error", exception.Message, []);
        }
    }

    private StoreScanResult ScanCustomLocations(string scanPath)
    {
        if (string.IsNullOrWhiteSpace(scanPath))
        {
            return new StoreScanResult(false, "Path required", "Choose a folder before syncing custom locations.", []);
        }

        if (!Directory.Exists(scanPath))
        {
            return new StoreScanResult(false, "Missing folder", "The saved custom folder does not exist anymore.", []);
        }

        try
        {
            var bestCandidatesByRoot = new Dictionary<string, ExecutableCandidate>(StringComparer.OrdinalIgnoreCase);

            foreach (var executablePath in EnumerateCustomExecutableCandidates(scanPath, maximumDepth: 6))
            {
                var candidateRoot = ResolveCustomGameRoot(scanPath, executablePath);
                if (string.IsNullOrWhiteSpace(candidateRoot)
                    || !Directory.Exists(candidateRoot)
                    || ShouldSkipCustomCandidateDirectory(candidateRoot))
                {
                    continue;
                }

                var score = ScoreCustomExecutable(candidateRoot, executablePath);
                if (score <= 0)
                {
                    continue;
                }

                var candidate = new ExecutableCandidate(executablePath, score);
                if (!bestCandidatesByRoot.TryGetValue(candidateRoot, out var currentBest)
                    || candidate.Score > currentBest.Score)
                {
                    bestCandidatesByRoot[candidateRoot] = candidate;
                }
            }

            var games = bestCandidatesByRoot
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new StoreGameEntry(
                    "custom-locations",
                    BuildDetectedTitle(entry.Key, entry.Value.Path),
                    entry.Value.Path,
                    Path.GetDirectoryName(entry.Value.Path) ?? entry.Key,
                    string.Empty))
                .ToList();

            return new StoreScanResult(
                true,
                "Ready",
                games.Count > 0
                    ? $"{games.Count} launchable title{(games.Count == 1 ? string.Empty : "s")} detected."
                    : "The folder is valid, but no likely game executables were found.",
                games);
        }
        catch (Exception exception)
        {
            return new StoreScanResult(false, "Error", exception.Message, []);
        }
    }

    private SteamProfileInfo? ResolveSteamProfile()
    {
        var userdataPath = Path.Combine(_steamRootPath, "userdata");
        if (!Directory.Exists(userdataPath))
        {
            return null;
        }

        var loginUsersPath = Path.Combine(_steamRootPath, "config", "loginusers.vdf");
        if (File.Exists(loginUsersPath))
        {
            var text = File.ReadAllText(loginUsersPath);
            var match = Regex.Matches(
                    text,
                    "\"(?<steamId64>\\d{17})\"\\s*\\{(?<body>.*?)\\}",
                    RegexOptions.Singleline)
                .Select(result => new
                {
                    SteamId64 = result.Groups["steamId64"].Value,
                    Body = result.Groups["body"].Value
                })
                .OrderByDescending(entry => GetVdfField(entry.Body, "MostRecent") == "1")
                .ThenByDescending(entry => ParseLong(GetVdfField(entry.Body, "Timestamp")))
                .FirstOrDefault();

            if (match is not null && ulong.TryParse(match.SteamId64, out var steamId64Value))
            {
                var accountIdValue = steamId64Value >= SteamIdOffset
                    ? (steamId64Value - SteamIdOffset).ToString()
                    : match.SteamId64;

                return new SteamProfileInfo(
                    PersonaName: GetVdfField(match.Body, "PersonaName") ?? accountIdValue,
                    AccountName: GetVdfField(match.Body, "AccountName") ?? accountIdValue,
                    SteamId64: match.SteamId64,
                    AccountId: accountIdValue,
                    ShortcutsPath: BuildShortcutsPath(accountIdValue));
            }
        }

        var accountDirectory = Directory.GetDirectories(userdataPath)
            .Select(Path.GetFileName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

        return accountDirectory is null
            ? null
            : new SteamProfileInfo(
                PersonaName: accountDirectory,
                AccountName: accountDirectory,
                SteamId64: string.Empty,
                AccountId: accountDirectory,
                ShortcutsPath: BuildShortcutsPath(accountDirectory));
    }

    private async Task RunSyncInBackgroundAsync(DateTimeOffset startedAt, bool launchSteamWhenFinished)
    {
        // The request returns first, then the background host takes over so Steam can close safely.
        await Task.Delay(1000);
        await RunSyncCoreAsync(startedAt, launchSteamWhenFinished, CancellationToken.None);
    }

    private async Task RunSyncCoreAsync(
        DateTimeOffset startedAt,
        bool launchSteamWhenFinished,
        CancellationToken cancellationToken)
    {
        var steamWasRunning = false;

        try
        {
            var configuration = _settingsStore.Load();
            var profile = ResolveSteamProfile();
            if (profile is null)
            {
                throw new InvalidOperationException("Steam profile data could not be resolved.");
            }

            var discoveredGames = BuildStoreSnapshots(configuration)
                .Where(snapshot => snapshot.Configuration.Enabled && snapshot.Scan.IsReady)
                .SelectMany(snapshot => snapshot.Scan.Games)
                .GroupBy(game => NormalizeKey($"{game.Title}|{game.ExecutablePath}"))
                .Select(group => group.First())
                .ToList();

            var existingEntries = _shortcutFile.Read(profile.ShortcutsPath).ToList();
            var existingManagedEntries = existingEntries
                .Where(SteamShortcutFile.HasManagedTag)
                .ToList();
            var unmanagedEntries = existingEntries
                .Where(entry => !SteamShortcutFile.HasManagedTag(entry))
                .ToList();
            var managedEntries = discoveredGames
                .Select(CreateShortcutEntry)
                .OrderBy(entry => entry.Game.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var managedEntriesChanged = !AreManagedEntriesEquivalent(existingManagedEntries, managedEntries);
            if (managedEntriesChanged)
            {
                steamWasRunning = IsSteamRunning();
                if (steamWasRunning)
                {
                    CloseSteamForSync();
                }

                if (configuration.BackupShortcuts)
                {
                    BackupShortcuts(profile.ShortcutsPath, startedAt);
                }

                unmanagedEntries.AddRange(managedEntries.Select(entry => entry.Entry));
                _shortcutFile.Write(profile.ShortcutsPath, unmanagedEntries);
            }

            StoreSyncArtworkSummary? artworkSummary = null;
            if (managedEntriesChanged && configuration.DownloadArtwork)
            {
                var gridDirectory = BuildGridDirectory(profile);
                var apiKey = _artworkDownloader.GetEffectiveApiKey(configuration.SteamGridDbApiKey);
                artworkSummary = await _artworkDownloader.DownloadAsync(
                    gridDirectory,
                    managedEntries
                        .Select(entry => new StoreSyncArtworkTarget(entry.Game.Title, entry.AppId))
                        .ToList(),
                    apiKey,
                    configuration.PreferAnimatedArtwork,
                    cancellationToken);
            }

            if (managedEntriesChanged && (steamWasRunning || launchSteamWhenFinished))
            {
                LaunchSteam(configuration.LaunchBigPictureAfterSync);
            }

            configuration.LastSync = new StoreSyncLastSyncState(
                Succeeded: true,
                StartedAtUtc: startedAt,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                Message: managedEntriesChanged
                    ? BuildSyncMessage(managedEntries.Count, existingManagedEntries.Count, artworkSummary, configuration.DownloadArtwork)
                    : BuildNoChangesMessage(managedEntries.Count),
                ImportedCount: managedEntries.Count,
                RemovedCount: managedEntriesChanged ? existingManagedEntries.Count : 0,
                SkippedCount: managedEntriesChanged ? 0 : managedEntries.Count);
            _settingsStore.Save(configuration);
        }
        catch (Exception exception)
        {
            try
            {
                var configuration = _settingsStore.Load();
                if ((steamWasRunning || launchSteamWhenFinished) && !IsSteamRunning())
                {
                    LaunchSteam(configuration.LaunchBigPictureAfterSync);
                }

                configuration.LastSync = new StoreSyncLastSyncState(
                    Succeeded: false,
                    StartedAtUtc: startedAt,
                    CompletedAtUtc: DateTimeOffset.UtcNow,
                    Message: exception.Message,
                    ImportedCount: 0,
                    RemovedCount: 0,
                    SkippedCount: 0);
                _settingsStore.Save(configuration);
            }
            catch
            {
            }
        }
        finally
        {
            lock (_gate)
            {
                _activeSyncTask = null;
            }
        }
    }

    private void BackupShortcuts(string shortcutsPath, DateTimeOffset startedAt)
    {
        var sourceDirectory = Path.GetDirectoryName(shortcutsPath);
        if (sourceDirectory is null)
        {
            return;
        }

        Directory.CreateDirectory(sourceDirectory);
        if (!File.Exists(shortcutsPath))
        {
            return;
        }

        var backupDirectory = Path.Combine(sourceDirectory, "steamloader-backups");
        Directory.CreateDirectory(backupDirectory);

        var backupName = $"shortcuts-{startedAt:yyyyMMdd-HHmmss}.vdf";
        File.Copy(shortcutsPath, Path.Combine(backupDirectory, backupName), overwrite: true);
    }

    private void CloseSteamForSync()
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

        var steamExePath = Path.Combine(_steamRootPath, "steam.exe");
        if (File.Exists(steamExePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamExePath,
                    Arguments = "-shutdown",
                    WorkingDirectory = _steamRootPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })?.Dispose();
            }
            catch
            {
            }
        }

        var timeoutAt = DateTime.UtcNow.AddSeconds(20);
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
        if (IsSteamRunning())
        {
            throw new InvalidOperationException("Tools for Steam could not close Steam completely before syncing.");
        }
    }

    private void LaunchSteam(bool launchBigPicture)
    {
        var steamExePath = Path.Combine(_steamRootPath, "steam.exe");
        if (!File.Exists(steamExePath))
        {
            return;
        }

        if (launchBigPicture)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "steam://open/bigpicture",
                    UseShellExecute = true,
                })?.Dispose();
                return;
            }
            catch
            {
            }
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = steamExePath,
            Arguments = launchBigPicture ? "-start steam://open/bigpicture" : string.Empty,
            WorkingDirectory = _steamRootPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.Dispose();
    }

    private static StoreSyncStoreConfiguration GetStoreConfiguration(StoreSyncConfiguration configuration, string storeId)
    {
        if (!configuration.Stores.TryGetValue(storeId, out var storeConfiguration) || storeConfiguration is null)
        {
            storeConfiguration = new StoreSyncStoreConfiguration();
            configuration.Stores[storeId] = storeConfiguration;
        }

        storeConfiguration.ScanPath ??= string.Empty;
        return storeConfiguration;
    }

    private static string BuildShortcutsPath(string accountId)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam",
            "userdata",
            accountId,
            "config",
            "shortcuts.vdf");
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string ResolveExecutablePath(string installPath, string? executableHint)
    {
        if (!string.IsNullOrWhiteSpace(executableHint))
        {
            var combinedPath = executableHint;
            if (!Path.IsPathRooted(combinedPath) && !string.IsNullOrWhiteSpace(installPath))
            {
                combinedPath = Path.Combine(installPath, combinedPath);
            }

            if (File.Exists(combinedPath))
            {
                return Path.GetFullPath(combinedPath);
            }
        }

        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return string.Empty;
        }

        return FindBestExecutable(installPath) ?? string.Empty;
    }

    private static string? FindBestExecutable(string directoryPath)
    {
        return FindExecutableCandidates(directoryPath, maximumDepth: 2)
            .OrderBy(path => ScoreExecutable(path))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static IEnumerable<string> FindExecutableCandidates(string directoryPath, int maximumDepth)
    {
        var rootDirectory = new DirectoryInfo(directoryPath);
        if (!rootDirectory.Exists)
        {
            yield break;
        }

        foreach (var file in EnumerateFiles(rootDirectory, maximumDepth))
        {
            if (!file.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsIgnoredExecutable(file.Name))
            {
                continue;
            }

            yield return file.FullName;
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo rootDirectory, int maximumDepth)
    {
        var queue = new Queue<(DirectoryInfo Directory, int Depth)>();
        queue.Enqueue((rootDirectory, 0));

        while (queue.Count > 0)
        {
            var (directory, depth) = queue.Dequeue();

            FileInfo[] files;
            try
            {
                files = directory.GetFiles();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (depth >= maximumDepth)
            {
                continue;
            }

            DirectoryInfo[] directories;
            try
            {
                directories = directory.GetDirectories();
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in directories)
            {
                queue.Enqueue((childDirectory, depth + 1));
            }
        }
    }

    private static bool IsIgnoredExecutable(string fileName)
    {
        var lowerName = fileName.ToLowerInvariant();

        return lowerName.Contains("unins")
            || lowerName.Contains("uninstall")
            || lowerName.Contains("crashreport")
            || lowerName.Contains("vc_redist")
            || lowerName.Contains("eosbootstrapper")
            || lowerName.Equals("setup.exe", StringComparison.OrdinalIgnoreCase)
            || lowerName.Equals("updater.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static int ScoreExecutable(string path)
    {
        var lowerPath = path.ToLowerInvariant();
        var score = 0;

        if (lowerPath.Contains("shipping"))
        {
            score -= 3;
        }

        if (lowerPath.Contains("\\content\\"))
        {
            score -= 2;
        }

        if (lowerPath.Contains("launcher"))
        {
            score += 4;
        }

        return score;
    }

    private static IEnumerable<string> EnumerateCustomExecutableCandidates(string rootDirectory, int maximumDepth)
    {
        if (!Directory.Exists(rootDirectory))
        {
            yield break;
        }

        var pendingDirectories = new Stack<(string Directory, int Depth)>();
        pendingDirectories.Push((rootDirectory, 0));

        while (pendingDirectories.Count > 0)
        {
            var (directory, depth) = pendingDirectories.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.exe");
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (IgnoreCustomExecutableTokens.Any(token =>
                        fileName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                yield return file;
            }

            if (depth >= maximumDepth)
            {
                continue;
            }

            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(directory);
            }
            catch
            {
                continue;
            }

            foreach (var subDirectory in subDirectories)
            {
                var directoryName = Path.GetFileName(subDirectory);
                if (IgnoreCustomDirectoryTokens.Any(token =>
                        directoryName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                pendingDirectories.Push((subDirectory, depth + 1));
            }
        }
    }

    private static bool ShouldSkipCustomCandidateDirectory(string candidatePath)
    {
        var directoryName = Path.GetFileName(candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return false;
        }

        return IgnoreCustomDirectoryTokens.Any(token =>
            directoryName.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveCustomGameRoot(string rootPath, string executablePath)
    {
        var currentDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            return rootPath;
        }

        var normalizedRoot = NormalizePath(rootPath);
        while (!string.IsNullOrWhiteSpace(currentDirectory)
               && NormalizePath(currentDirectory).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            var directoryName = Path.GetFileName(currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!IsStructuralDirectory(directoryName))
            {
                return currentDirectory;
            }

            var parent = Directory.GetParent(currentDirectory);
            if (parent is null)
            {
                break;
            }

            currentDirectory = parent.FullName;
        }

        return rootPath;
    }

    private static bool IsStructuralDirectory(string? directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            return false;
        }

        var normalizedDirectoryName = NormalizeToken(directoryName);
        return StructuralDirectoryTokens.Any(token =>
            string.Equals(normalizedDirectoryName, NormalizeToken(token), StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreCustomExecutable(string rootDirectory, string executablePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        if (IgnoreCustomExecutableTokens.Any(token =>
                fileName.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return -100;
        }

        var normalizedRoot = NormalizeToken(Path.GetFileName(rootDirectory));
        var normalizedFile = NormalizeToken(fileName);
        var normalizedParent = NormalizeToken(Path.GetFileName(Path.GetDirectoryName(executablePath) ?? string.Empty));

        var score = 20;
        if (normalizedRoot == normalizedFile)
        {
            score += 90;
        }
        else if (!string.IsNullOrWhiteSpace(normalizedRoot)
                 && !string.IsNullOrWhiteSpace(normalizedFile)
                 && (normalizedFile.Contains(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                     || normalizedRoot.Contains(normalizedFile, StringComparison.OrdinalIgnoreCase)))
        {
            score += 55;
        }

        if (normalizedParent == normalizedRoot)
        {
            score += 25;
        }

        if (executablePath.Contains(@"\Binaries\Win64\", StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (executablePath.Contains(@"\Win64\", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (executablePath.Contains("Shipping", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (executablePath.Contains(@"\Engine\", StringComparison.OrdinalIgnoreCase)
            || executablePath.Contains(@"\Editor\", StringComparison.OrdinalIgnoreCase)
            || executablePath.Contains(@"\Support\", StringComparison.OrdinalIgnoreCase))
        {
            score -= 120;
        }

        try
        {
            var fileInfo = new FileInfo(executablePath);
            score += (int)Math.Min(18, fileInfo.Length / (20 * 1024 * 1024));
        }
        catch
        {
        }

        return score;
    }

    private static string BuildDetectedTitle(string candidateRoot, string executablePath)
    {
        var metadataTitle = TryReadExecutableTitle(executablePath);
        if (!string.IsNullOrWhiteSpace(metadataTitle))
        {
            return PrettifyTitle(metadataTitle);
        }

        return PrettifyTitle(Path.GetFileName(candidateRoot));
    }

    private static string? TryReadExecutableTitle(string executablePath)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            foreach (var value in new[] { versionInfo.ProductName, versionInfo.FileDescription })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var trimmed = value.Trim();
                if (IgnoreCustomExecutableTokens.Any(token =>
                        trimmed.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return trimmed;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string NormalizeToken(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return (path ?? string.Empty)
                .Trim()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string PrettifyTitle(string value)
    {
        var cleaned = value.Replace('_', ' ').Replace('-', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? value : cleaned;
    }

    private static string NormalizeKey(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static ManagedShortcutEntry CreateShortcutEntry(StoreGameEntry game)
    {
        var appId = SteamShortcutIds.ComputeAppId(game.Title, game.ExecutablePath);
        var entry = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["appid"] = unchecked((int)appId),
            ["appname"] = game.Title,
            ["Exe"] = QuotePath(game.ExecutablePath),
            ["StartDir"] = QuotePath(game.StartDirectory),
            ["icon"] = game.ExecutablePath,
            ["ShortcutPath"] = ManagedShortcutMarker,
            ["LaunchOptions"] = game.LaunchOptions,
            ["IsHidden"] = 0,
            ["AllowDesktopConfig"] = 1,
            ["AllowOverlay"] = 1,
            ["OpenVR"] = 0,
            ["Devkit"] = 0,
            ["DevkitGameID"] = string.Empty,
            ["DevkitOverrideAppID"] = 0,
            ["LastPlayTime"] = 0,
            ["FlatpakAppID"] = string.Empty,
            ["tags"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["0"] = "Tools for Steam",
                ["1"] = "Store Sync",
                ["2"] = game.StoreId,
            },
        };

        return new ManagedShortcutEntry(game, appId, entry);
    }

    private static bool AreManagedEntriesEquivalent(
        IReadOnlyList<Dictionary<string, object?>> existingManagedEntries,
        IReadOnlyList<ManagedShortcutEntry> desiredManagedEntries)
    {
        if (existingManagedEntries.Count != desiredManagedEntries.Count)
        {
            return false;
        }

        var existingSignatures = existingManagedEntries
            .Select(BuildManagedEntrySignature)
            .OrderBy(signature => signature, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var desiredSignatures = desiredManagedEntries
            .Select(entry => BuildManagedEntrySignature(entry.Entry))
            .OrderBy(signature => signature, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < existingSignatures.Length; index++)
        {
            if (!string.Equals(existingSignatures[index], desiredSignatures[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildManagedEntrySignature(Dictionary<string, object?> entry)
    {
        static string ReadValue(Dictionary<string, object?> values, string key)
        {
            return values.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        }

        return string.Join(
            "|",
            ReadValue(entry, "appname"),
            ReadValue(entry, "Exe"),
            ReadValue(entry, "StartDir"),
            ReadValue(entry, "LaunchOptions"),
            ReadValue(entry, "ShortcutPath"));
    }

    private static string QuotePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : $"\"{path}\"";
    }

    private static string BuildSyncMessage(
        int importedCount,
        int removedCount,
        StoreSyncArtworkSummary? artworkSummary,
        bool artworkEnabled)
    {
        if (importedCount == 0)
        {
            return "No launchable third-party titles were found during this sync.";
        }

        if (artworkEnabled && artworkSummary is not null && artworkSummary.UpdatedTitleCount > 0)
        {
            return $"Synced {importedCount} title{(importedCount == 1 ? string.Empty : "s")} into Steam, refreshed {removedCount} previous Tools for Steam shortcut{(removedCount == 1 ? string.Empty : "s")}, and updated artwork for {artworkSummary.UpdatedTitleCount} title{(artworkSummary.UpdatedTitleCount == 1 ? string.Empty : "s")}.";
        }

        return $"Synced {importedCount} title{(importedCount == 1 ? string.Empty : "s")} into Steam and refreshed {removedCount} previous Tools for Steam shortcut{(removedCount == 1 ? string.Empty : "s")}.";
    }

    private static string BuildNoChangesMessage(int discoveredCount)
    {
        return discoveredCount == 0
            ? "Tools for Steam did not find any launchable third-party titles to sync."
            : $"Tools for Steam checked {discoveredCount} title{(discoveredCount == 1 ? string.Empty : "s")} and found no shortcut changes.";
    }

    private static IEnumerable<Process> GetSteamProcesses()
    {
        return Process.GetProcessesByName("steam")
            .Concat(Process.GetProcessesByName("steamwebhelper"));
    }

    private static bool IsSteamRunning()
    {
        return GetSteamProcesses().Any(process =>
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

    private static string BuildGridDirectory(SteamProfileInfo profile)
    {
        var shortcutsDirectory = Path.GetDirectoryName(profile.ShortcutsPath)
            ?? throw new InvalidOperationException("The Steam shortcuts folder could not be resolved.");

        return Path.Combine(shortcutsDirectory, "grid");
    }

    private static string? GetVdfField(string body, string fieldName)
    {
        var match = Regex.Match(body, $"\"{Regex.Escape(fieldName)}\"\\s+\"(?<value>.*?)\"");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static long ParseLong(string? value)
    {
        return long.TryParse(value, out var parsedValue) ? parsedValue : 0;
    }

    private sealed record StoreDefinition(
        string Id,
        string Title,
        string Description,
        bool SupportsCustomPath = false);

    private sealed record StoreScanResult(
        bool IsReady,
        string StatusText,
        string DetailText,
        IReadOnlyList<StoreGameEntry> Games);

    private sealed record StoreSnapshot(
        StoreDefinition Definition,
        StoreSyncStoreConfiguration Configuration,
        StoreScanResult Scan);

    private sealed record StoreGameEntry(
        string StoreId,
        string Title,
        string ExecutablePath,
        string StartDirectory,
        string LaunchOptions);

    private readonly record struct ExecutableCandidate(string Path, int Score);

    private sealed record ManagedShortcutEntry(
        StoreGameEntry Game,
        uint AppId,
        Dictionary<string, object?> Entry);
}
