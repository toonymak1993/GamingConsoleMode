using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SteamLoader.App.Infrastructure.Assets;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.Themes;

public sealed class ThemesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex CssLoaderModuleClassRegex = new(
        @"\.([_A-Za-z][-_A-Za-z0-9]*)",
        RegexOptions.Compiled);
    private static readonly (string Prefix, string Replacement)[] CssLoaderSemanticClassMappings =
    [
        ("sharedappdetailsheader_TopCapsule_", ".steamloader-theme-game-detail-topcapsule"),
        ("sharedappdetailsheader_HeaderBackgroundImage_", ".steamloader-theme-game-detail-hero"),
        ("sharedappdetailsheader_ImgSrc_", ".steamloader-theme-game-detail-hero-image"),
        ("sharedappdetailsheader_TitleSection_", ".steamloader-theme-game-detail-title-section"),
        ("sharedappdetailsheader_SVGTitle_", ".steamloader-theme-game-detail-logo"),
        ("sharedappdetailsheader_BoxSizer_", ".steamloader-theme-game-detail-logo-box"),
        ("basicappdetailssectionstyler_PlaySection_", ".steamloader-theme-game-detail-playbar"),
        ("appdetailsplaysection_CloudStatusRow_", ".steamloader-theme-game-detail-cloud-status"),
        ("appdetailsplaysection_CloudStatusLabel_", ".steamloader-theme-game-detail-cloud-label"),
        ("appdetailsplaysection_CloudStatusIcon_", ".steamloader-theme-game-detail-cloud-icon"),
        ("appdetailsplaysection_CloudSyncProblem_", ".steamloader-theme-game-detail-cloud-problem")
    ];
    private static readonly string[] CleanGameviewSeedFiles =
    [
        "theme.json",
        "shared.css",
        "transparent.css",
        "blur.css",
        "connected.css",
        "steamcloud.css",
        "lockedlogo.css",
        "alignments/left.css",
        "alignments/right.css",
        "alignments/contain.css",
        "alignments/stretch.css"
    ];

    private readonly object _gate = new();
    private readonly ThemesSettingsStore _settingsStore;
    private readonly string _catalogAssetPath;
    private readonly string _profilesCatalogAssetPath;
    private readonly string _localThemesFolder;
    private List<ThemeManifest> _catalog;
    private List<ThemeProfileCatalogEntry> _profileCatalog;

    public ThemesService(
        ThemesSettingsStore settingsStore,
        string catalogAssetPath,
        string profilesCatalogAssetPath,
        string localThemesFolder)
    {
        _settingsStore = settingsStore;
        _catalogAssetPath = catalogAssetPath;
        _profilesCatalogAssetPath = profilesCatalogAssetPath;
        _localThemesFolder = localThemesFolder;

        Directory.CreateDirectory(_localThemesFolder);
        EnsureBundledThemeSeeds();
        _catalog = LoadCatalog();
        _profileCatalog = LoadProfileCatalog();
    }

    public ThemesSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            if (EnsureCatalogDefaults(configuration))
            {
                _settingsStore.Save(configuration);
            }

            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot RefreshCatalog()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_localThemesFolder);
            EnsureBundledThemeSeeds();
            _catalog = LoadCatalog();
            _profileCatalog = LoadProfileCatalog();

            var configuration = _settingsStore.Load();
            if (EnsureCatalogDefaults(configuration))
            {
                _settingsStore.Save(configuration);
            }

            return BuildSnapshot(configuration);
        }
    }

    public string ResolveCssForTarget(string? title, string? url)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            if (EnsureCatalogDefaults(configuration))
            {
                _settingsStore.Save(configuration);
            }

            return BuildActiveCss(configuration, new ThemeRenderTarget(title ?? string.Empty, url ?? string.Empty));
        }
    }

    public ThemesSnapshot ToggleSetting(string key)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);

            switch (key)
            {
                case "theme-engine-enabled":
                    configuration.Settings.ThemeEngineEnabled = !configuration.Settings.ThemeEngineEnabled;
                    break;
                case "show-community-themes":
                    configuration.Settings.ShowCommunityThemes = !configuration.Settings.ShowCommunityThemes;
                    break;
                case "single-theme-mode":
                    configuration.Settings.SingleThemeMode = !configuration.Settings.SingleThemeMode;
                    if (configuration.Settings.SingleThemeMode)
                    {
                        KeepOnlyFirstEnabledTheme(configuration);
                    }
                    break;
                case "auto-enable-on-install":
                    configuration.Settings.AutoEnableOnInstall = !configuration.Settings.AutoEnableOnInstall;
                    break;
                default:
                    throw new InvalidOperationException("Unknown Themes setting.");
            }

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot SetThemeInstalled(string themeId, bool installed)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);
            var manifest = FindManifest(themeId);
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);

            themeConfiguration.Installed = installed;
            if (!installed)
            {
                themeConfiguration.Enabled = false;
            }
            else if (configuration.Settings.AutoEnableOnInstall)
            {
                if (configuration.Settings.SingleThemeMode)
                {
                    DisableAllThemes(configuration);
                }

                themeConfiguration.Enabled = true;
            }

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot SetThemeEnabled(string themeId, bool enabled)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);
            var manifest = FindManifest(themeId);
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);

            if (!themeConfiguration.Installed)
            {
                throw new InvalidOperationException("Install the theme before enabling it.");
            }

            if (enabled && configuration.Settings.SingleThemeMode)
            {
                DisableAllThemes(configuration);
            }

            themeConfiguration.Enabled = enabled;
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot ToggleThemeOption(string themeId, string optionId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);
            var manifest = FindManifest(themeId);
            var option = FindOption(manifest, optionId);
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);

            if (!themeConfiguration.Installed)
            {
                throw new InvalidOperationException("Install the theme before changing its options.");
            }

            if (!string.Equals(option.Type, "toggle", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This theme option is not a toggle.");
            }

            var currentValue = ParseBoolValue(GetStoredOptionValue(option, themeConfiguration));
            themeConfiguration.Values[option.Id] = (!currentValue).ToString().ToLowerInvariant();

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot SetThemeChoice(string themeId, string optionId, string choiceId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);
            var manifest = FindManifest(themeId);
            var option = FindOption(manifest, optionId);
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);

            if (!themeConfiguration.Installed)
            {
                throw new InvalidOperationException("Install the theme before changing its options.");
            }

            if (!string.Equals(option.Type, "choice", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This theme option is not a choice.");
            }

            if (option.Choices.All(choice => !string.Equals(choice.Id, choiceId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The selected option value is not available.");
            }

            themeConfiguration.Values[option.Id] = choiceId;
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot AdjustThemeRange(string themeId, string optionId, int delta)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);
            var manifest = FindManifest(themeId);
            var option = FindOption(manifest, optionId);
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);

            if (!themeConfiguration.Installed)
            {
                throw new InvalidOperationException("Install the theme before changing its options.");
            }

            if (!string.Equals(option.Type, "range", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This theme option is not a range.");
            }

            var currentValue = ParseRangeValue(option, GetStoredOptionValue(option, themeConfiguration));
            var nextValue = ClampRangeValue(option, currentValue + (option.Step * delta));
            themeConfiguration.Values[option.Id] = nextValue.ToString(CultureInfo.InvariantCulture);

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot ResetThemeRange(string themeId, string optionId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);
            var manifest = FindManifest(themeId);
            var option = FindOption(manifest, optionId);
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);

            if (!themeConfiguration.Installed)
            {
                throw new InvalidOperationException("Install the theme before changing its options.");
            }

            if (!string.Equals(option.Type, "range", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("This theme option is not a range.");
            }

            themeConfiguration.Values[option.Id] = option.DefaultNumber.ToString(CultureInfo.InvariantCulture);
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot CreateProfile(string title)
    {
        lock (_gate)
        {
            var trimmedTitle = (title ?? string.Empty).Trim();
            if (trimmedTitle.Length < 3)
            {
                throw new InvalidOperationException("Enter a profile name with at least 3 characters.");
            }

            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);

            var profileId = CreateUniqueProfileId(trimmedTitle, configuration);
            configuration.Profiles[profileId] = BuildStoredProfile(trimmedTitle, configuration);
            configuration.SelectedProfileId = profileId;

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot InstallProfile(string profileId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);

            if (configuration.Profiles.ContainsKey(profileId))
            {
                return BuildSnapshot(configuration);
            }

            var catalogEntry = FindProfileCatalogEntry(profileId);
            configuration.Profiles[profileId] = CloneStoredProfile(catalogEntry.StoredProfile);
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot ApplyProfile(string profileId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);

            if (!configuration.Profiles.TryGetValue(profileId, out var profile) || profile is null)
            {
                throw new InvalidOperationException("Install the profile before applying it.");
            }

            ApplyProfileConfiguration(configuration, profile);
            configuration.SelectedProfileId = profileId;

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot UpdateProfile(string profileId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);

            if (!configuration.Profiles.TryGetValue(profileId, out var profile) || profile is null)
            {
                throw new InvalidOperationException("Install the profile before updating it.");
            }

            var replacement = BuildStoredProfile(profile.Title, configuration);
            replacement.Author = profile.Author;
            replacement.Description = $"Updated from the current Tools for Steam theme stack on {DateTime.Now:yyyy-MM-dd}.";
            replacement.SourceLabel = profile.SourceLabel;
            replacement.DownloadCount = profile.DownloadCount;
            replacement.Version = profile.Version;

            configuration.Profiles[profileId] = replacement;
            configuration.SelectedProfileId = profileId;

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public ThemesSnapshot RemoveProfile(string profileId)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            EnsureCatalogDefaults(configuration);

            if (!configuration.Profiles.Remove(profileId))
            {
                throw new InvalidOperationException("The requested profile could not be found.");
            }

            if (string.Equals(configuration.SelectedProfileId, profileId, StringComparison.OrdinalIgnoreCase))
            {
                configuration.SelectedProfileId = null;
            }

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    private ThemesSnapshot BuildSnapshot(ThemesConfiguration configuration)
    {
        EnsureCatalogDefaults(configuration);

        var installedThemes = _catalog
            .Where(manifest => GetThemeConfiguration(configuration, manifest.Id).Installed)
            .OrderBy(manifest => manifest.Title, StringComparer.OrdinalIgnoreCase)
            .Select(manifest => BuildThemeState(manifest, configuration))
            .ToList();

        var browseThemes = _catalog
            .Where(manifest => configuration.Settings.ShowCommunityThemes || !manifest.IsCommunity)
            .OrderByDescending(manifest => manifest.DownloadCount)
            .ThenBy(manifest => manifest.Title, StringComparer.OrdinalIgnoreCase)
            .Select(manifest => BuildThemeState(manifest, configuration))
            .ToList();

        var profiles = BuildProfilesState(configuration);

        return new ThemesSnapshot(
            new ThemesSettingsState(
                configuration.Settings.ThemeEngineEnabled,
                configuration.Settings.ShowCommunityThemes,
                configuration.Settings.SingleThemeMode,
                configuration.Settings.AutoEnableOnInstall),
            installedThemes,
            browseThemes,
            profiles,
            BuildActiveCss(configuration, BuildQuickAccessRenderTarget()),
            BuildStatusText(configuration, installedThemes, profiles),
            _localThemesFolder);
    }

    private ThemeState BuildThemeState(ThemeManifest manifest, ThemesConfiguration configuration)
    {
        var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);
        var optionStates = manifest.Options
            .Select(option => BuildOptionState(option, themeConfiguration))
            .ToList();

        var statusText = !themeConfiguration.Installed
            ? manifest.SourceLabel == "Local"
                ? "Local theme available"
                : "Available to install"
            : themeConfiguration.Enabled
                ? "Installed and active"
                : "Installed";

        return new ThemeState(
            manifest.Id,
            manifest.Title,
            manifest.Author,
            manifest.Version,
            manifest.Description,
            manifest.StoreDescription,
            themeConfiguration.Installed,
            themeConfiguration.Enabled,
            statusText,
            manifest.SourceLabel,
            manifest.DownloadCount,
            manifest.Targets,
            optionStates);
    }

    private ThemeOptionState BuildOptionState(ThemeOptionManifest option, ThemeInstallationConfiguration configuration)
    {
        var storedValue = GetStoredOptionValue(option, configuration);
        var choices = option.Choices
            .Select(choice => new ThemeChoiceState(choice.Id, choice.Title))
            .ToList();

        return option.Type.ToLowerInvariant() switch
        {
            "toggle" => new ThemeOptionState(
                option.Id,
                option.Title,
                option.Description,
                option.Type,
                ParseBoolValue(storedValue),
                null,
                null,
                null,
                null,
                null,
                null,
                choices),
            "choice" => new ThemeOptionState(
                option.Id,
                option.Title,
                option.Description,
                option.Type,
                null,
                null,
                null,
                null,
                null,
                null,
                storedValue,
                choices),
            "range" => new ThemeOptionState(
                option.Id,
                option.Title,
                option.Description,
                option.Type,
                null,
                ParseRangeValue(option, storedValue),
                option.Min,
                option.Max,
                option.Step,
                option.Unit,
                null,
                choices),
            _ => new ThemeOptionState(
                option.Id,
                option.Title,
                option.Description,
                option.Type,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                choices),
        };
    }

    private ThemesProfilesState BuildProfilesState(ThemesConfiguration configuration)
    {
        var installedProfiles = configuration.Profiles
            .OrderBy(entry => entry.Value.Title, StringComparer.OrdinalIgnoreCase)
            .Select(entry => BuildInstalledProfileState(entry.Key, entry.Value, configuration))
            .ToList();

        var browseProfiles = _profileCatalog
            .Where(entry => !configuration.Profiles.ContainsKey(entry.Id))
            .OrderByDescending(entry => entry.StoredProfile.DownloadCount)
            .ThenBy(entry => entry.StoredProfile.Title, StringComparer.OrdinalIgnoreCase)
            .Select(entry => BuildBrowseProfileState(entry, configuration))
            .ToList();

        var currentProfileMatches = !string.IsNullOrWhiteSpace(configuration.SelectedProfileId) &&
                                    configuration.Profiles.TryGetValue(configuration.SelectedProfileId, out var selectedProfile) &&
                                    selectedProfile is not null &&
                                    ProfileMatchesCurrentSetup(selectedProfile, configuration);

        return new ThemesProfilesState(
            configuration.SelectedProfileId,
            currentProfileMatches,
            installedProfiles,
            browseProfiles);
    }

    private ThemeProfileState BuildInstalledProfileState(
        string profileId,
        ThemeProfileConfiguration profile,
        ThemesConfiguration configuration)
    {
        var isSelected = string.Equals(configuration.SelectedProfileId, profileId, StringComparison.OrdinalIgnoreCase);
        var matchesCurrentSetup = ProfileMatchesCurrentSetup(profile, configuration);

        var statusText = isSelected
            ? matchesCurrentSetup
                ? "Selected and matches current setup"
                : "Selected but differs from current setup"
            : matchesCurrentSetup
                ? "Matches current setup"
                : "Installed profile";

        return new ThemeProfileState(
            profileId,
            profile.Title,
            profile.Author,
            profile.Description,
            profile.Version,
            statusText,
            profile.SourceLabel,
            profile.DownloadCount,
            true,
            isSelected,
            matchesCurrentSetup,
            BuildProfileThemeStates(profile.Themes));
    }

    private ThemeProfileState BuildBrowseProfileState(
        ThemeProfileCatalogEntry entry,
        ThemesConfiguration configuration)
    {
        var profile = entry.StoredProfile;

        return new ThemeProfileState(
            entry.Id,
            profile.Title,
            profile.Author,
            profile.Description,
            profile.Version,
            "Available to install",
            profile.SourceLabel,
            profile.DownloadCount,
            false,
            false,
            false,
            BuildProfileThemeStates(profile.Themes));
    }

    private IReadOnlyList<ThemeProfileThemeState> BuildProfileThemeStates(
        IReadOnlyDictionary<string, ThemeProfileThemeConfiguration> themes)
    {
        return themes
            .Where(entry => entry.Value.Installed)
            .OrderBy(entry => ResolveThemeTitle(entry.Key), StringComparer.OrdinalIgnoreCase)
            .Select(entry => new ThemeProfileThemeState(
                entry.Key,
                ResolveThemeTitle(entry.Key),
                entry.Value.Installed,
                entry.Value.Enabled,
                entry.Value.Values.Count))
            .ToList();
    }

    private string BuildActiveCss(ThemesConfiguration configuration, ThemeRenderTarget renderTarget)
    {
        if (!configuration.Settings.ThemeEngineEnabled)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var manifest in _catalog)
        {
            var themeConfiguration = GetThemeConfiguration(configuration, manifest.Id);
            if (!themeConfiguration.Installed || !themeConfiguration.Enabled)
            {
                continue;
            }

            foreach (var injectRule in GetActiveInjectRules(manifest, themeConfiguration))
            {
                if (!injectRule.Targets.Any(targetPattern => MatchesTarget(renderTarget, targetPattern)))
                {
                    continue;
                }

                var css = string.Join(Environment.NewLine, injectRule.CssLines);
                foreach (var option in manifest.Options)
                {
                    var tokenValue = ResolveCssTokenValue(option, GetStoredOptionValue(option, themeConfiguration));
                    css = css.Replace($"{{{{{option.Id}}}}}", tokenValue, StringComparison.Ordinal);
                }

                if (string.IsNullOrWhiteSpace(css))
                {
                    continue;
                }

                if (string.Equals(manifest.SourceLabel, "CSS Loader", StringComparison.OrdinalIgnoreCase))
                {
                    css = RewriteCssLoaderSelectors(css);
                }

                builder.AppendLine($"/* Theme: {manifest.Title} */");
                builder.AppendLine(css);
                builder.AppendLine();
            }
        }

        return builder.ToString().Trim();
    }

    private string BuildStatusText(
        ThemesConfiguration configuration,
        IReadOnlyList<ThemeState> installedThemes,
        ThemesProfilesState profiles)
    {
        if (!configuration.Settings.ThemeEngineEnabled)
        {
            return "Theme engine is currently disabled.";
        }

        var activeCount = installedThemes.Count(theme => theme.Enabled);
        var selectedProfileLabel = string.IsNullOrWhiteSpace(profiles.SelectedProfileId)
            ? "No profile selected."
            : profiles.CurrentSetupMatchesSelectedProfile
                ? "Selected profile is in sync."
                : "Selected profile needs an update.";

        return activeCount > 0
            ? $"{installedThemes.Count} installed - {activeCount} active. {selectedProfileLabel}"
            : $"{installedThemes.Count} installed - no active themes. {selectedProfileLabel}";
    }

    private List<ThemeManifest> LoadCatalog()
    {
        var themesById = new Dictionary<string, ThemeManifest>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in LoadThemeCatalogDocument(EmbeddedAssetReader.ReadText(_catalogAssetPath), null, false))
        {
            themesById[manifest.Id] = manifest;
        }

        foreach (var manifest in LoadLocalThemeCatalog())
        {
            themesById[manifest.Id] = manifest;
        }

        return themesById.Values
            .OrderBy(theme => theme.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IEnumerable<ThemeManifest> LoadThemeCatalogDocument(
        string json,
        string? baseDirectory,
        bool isLocal)
    {
        ThemeCatalogDocument? document = null;
        ThemeManifest? singleTheme = null;
        CssLoaderThemeDocument? cssLoaderTheme = null;

        try
        {
            document = JsonSerializer.Deserialize<ThemeCatalogDocument>(json, JsonOptions);
        }
        catch
        {
        }

        if (document?.Themes?.Count > 0)
        {
            foreach (var theme in document.Themes)
            {
                var normalized = NormalizeThemeManifest(theme, baseDirectory, isLocal);
                if (!string.IsNullOrWhiteSpace(normalized.Id))
                {
                    yield return normalized;
                }
            }

            yield break;
        }

        try
        {
            singleTheme = JsonSerializer.Deserialize<ThemeManifest>(json, JsonOptions);
        }
        catch
        {
        }

        if (singleTheme is not null && !string.IsNullOrWhiteSpace(singleTheme.Id))
        {
            yield return NormalizeThemeManifest(singleTheme, baseDirectory, isLocal);
            yield break;
        }

        try
        {
            cssLoaderTheme = JsonSerializer.Deserialize<CssLoaderThemeDocument>(json, JsonOptions);
        }
        catch
        {
        }

        if (cssLoaderTheme is not null && !string.IsNullOrWhiteSpace(cssLoaderTheme.Name))
        {
            yield return NormalizeThemeManifest(ConvertCssLoaderTheme(cssLoaderTheme), baseDirectory, isLocal);
        }
    }

    private IEnumerable<ThemeManifest> LoadLocalThemeCatalog()
    {
        Directory.CreateDirectory(_localThemesFolder);

        foreach (var themeFile in Directory.EnumerateFiles(_localThemesFolder, "theme.json", SearchOption.AllDirectories))
        {
            var baseDirectory = Path.GetDirectoryName(themeFile);
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                continue;
            }

            string json;
            try
            {
                json = File.ReadAllText(themeFile);
            }
            catch
            {
                continue;
            }

            foreach (var manifest in LoadThemeCatalogDocument(json, baseDirectory, true))
            {
                yield return manifest;
            }
        }
    }

    private ThemeManifest NormalizeThemeManifest(ThemeManifest manifest, string? baseDirectory, bool isLocal)
    {
        manifest.Targets ??= [];
        manifest.CssLines ??= [];
        manifest.CssFiles ??= [];
        manifest.Options ??= [];
        manifest.InjectRules ??= [];

        manifest.SourceLabel = string.IsNullOrWhiteSpace(manifest.SourceLabel)
            ? isLocal
                ? "Local"
                : manifest.IsCommunity ? "Community" : "Built-in"
            : manifest.SourceLabel;

        if (isLocal)
        {
            manifest.DefaultInstalled = true;
            manifest.DefaultEnabled = false;
            manifest.DownloadCount = Math.Max(0, manifest.DownloadCount);
        }

        ResolveCssFiles(manifest.CssLines, manifest.CssFiles, baseDirectory);

        foreach (var injectRule in manifest.InjectRules)
        {
            NormalizeInjectRule(injectRule, baseDirectory);
        }

        foreach (var option in manifest.Options)
        {
            option.Choices ??= [];
            option.OnInjectRules ??= [];
            option.OffInjectRules ??= [];

            foreach (var injectRule in option.OnInjectRules)
            {
                NormalizeInjectRule(injectRule, baseDirectory);
            }

            foreach (var injectRule in option.OffInjectRules)
            {
                NormalizeInjectRule(injectRule, baseDirectory);
            }

            foreach (var choice in option.Choices)
            {
                choice.InjectRules ??= [];
                foreach (var injectRule in choice.InjectRules)
                {
                    NormalizeInjectRule(injectRule, baseDirectory);
                }
            }
        }

        return manifest;
    }

    private static void NormalizeInjectRule(ThemeInjectManifest injectRule, string? baseDirectory)
    {
        injectRule.Targets ??= [];
        injectRule.CssLines ??= [];
        injectRule.CssFiles ??= [];
        ResolveCssFiles(injectRule.CssLines, injectRule.CssFiles, baseDirectory);
    }

    private static void ResolveCssFiles(List<string> cssLines, IReadOnlyList<string> cssFiles, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || cssFiles.Count <= 0)
        {
            return;
        }

        foreach (var cssFile in cssFiles)
        {
            if (string.IsNullOrWhiteSpace(cssFile))
            {
                continue;
            }

            var filePath = Path.Combine(baseDirectory, cssFile);
            if (!File.Exists(filePath))
            {
                continue;
            }

            cssLines.Add(File.ReadAllText(filePath));
        }
    }

    private List<ThemeProfileCatalogEntry> LoadProfileCatalog()
    {
        var json = EmbeddedAssetReader.ReadText(_profilesCatalogAssetPath);
        var document = JsonSerializer.Deserialize<ThemeProfileCatalogDocument>(json, JsonOptions);

        return (document?.Profiles ?? [])
            .Where(profile => !string.IsNullOrWhiteSpace(profile.Id))
            .Select(profile => new ThemeProfileCatalogEntry(
                profile.Id,
                new ThemeProfileConfiguration
                {
                    Title = profile.Title,
                    Author = profile.Author,
                    Description = profile.Description,
                    Version = profile.Version,
                    SourceLabel = profile.SourceLabel,
                    DownloadCount = profile.DownloadCount,
                    Themes = profile.Themes.ToDictionary(
                        entry => entry.Key,
                        entry => CloneProfileTheme(entry.Value),
                        StringComparer.OrdinalIgnoreCase),
                }))
            .OrderBy(profile => profile.StoredProfile.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ThemeRenderTarget BuildQuickAccessRenderTarget()
    {
        return new ThemeRenderTarget(
            "QuickAccess_uid2",
            "about:blank?browserviewpopup=1&requestid=2&parentpopup=2");
    }

    private bool EnsureCatalogDefaults(ThemesConfiguration configuration)
    {
        var changed = false;
        var validThemeIds = new HashSet<string>(_catalog.Select(manifest => manifest.Id), StringComparer.OrdinalIgnoreCase);

        foreach (var themeKey in configuration.Themes.Keys.ToArray())
        {
            if (!validThemeIds.Contains(themeKey))
            {
                configuration.Themes.Remove(themeKey);
                changed = true;
            }
        }

        foreach (var manifest in _catalog)
        {
            if (!configuration.Themes.TryGetValue(manifest.Id, out var themeConfiguration) || themeConfiguration is null)
            {
                themeConfiguration = new ThemeInstallationConfiguration
                {
                    Installed = manifest.DefaultInstalled,
                    Enabled = manifest.DefaultInstalled && manifest.DefaultEnabled
                };
                configuration.Themes[manifest.Id] = themeConfiguration;
                changed = true;
            }

            if (themeConfiguration.Values is null)
            {
                themeConfiguration.Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                changed = true;
            }

            foreach (var option in manifest.Options)
            {
                if (!themeConfiguration.Values.ContainsKey(option.Id))
                {
                    themeConfiguration.Values[option.Id] = GetDefaultStoredValue(option);
                    changed = true;
                }
            }

            if (!themeConfiguration.Installed)
            {
                themeConfiguration.Enabled = false;
            }
        }

        foreach (var profileKey in configuration.Profiles.Keys.ToArray())
        {
            configuration.Profiles[profileKey] ??= new ThemeProfileConfiguration();
            configuration.Profiles[profileKey].Themes ??=
                new Dictionary<string, ThemeProfileThemeConfiguration>(StringComparer.OrdinalIgnoreCase);

            foreach (var themeKey in configuration.Profiles[profileKey].Themes.Keys.ToArray())
            {
                if (!validThemeIds.Contains(themeKey))
                {
                    configuration.Profiles[profileKey].Themes.Remove(themeKey);
                    changed = true;
                    continue;
                }

                configuration.Profiles[profileKey].Themes[themeKey] ??= new ThemeProfileThemeConfiguration();
                configuration.Profiles[profileKey].Themes[themeKey].Values ??=
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (configuration.Profiles[profileKey].Themes.Count == 0)
            {
                configuration.Profiles.Remove(profileKey);
                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(configuration.SelectedProfileId)
            && !configuration.Profiles.ContainsKey(configuration.SelectedProfileId))
        {
            configuration.SelectedProfileId = null;
            changed = true;
        }

        return changed;
    }

    private ThemeManifest FindManifest(string themeId)
    {
        return _catalog.FirstOrDefault(manifest =>
            string.Equals(manifest.Id, themeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested theme could not be found.");
    }

    private ThemeProfileCatalogEntry FindProfileCatalogEntry(string profileId)
    {
        return _profileCatalog.FirstOrDefault(entry =>
            string.Equals(entry.Id, profileId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested profile could not be found.");
    }

    private static ThemeOptionManifest FindOption(ThemeManifest manifest, string optionId)
    {
        return manifest.Options.FirstOrDefault(option =>
            string.Equals(option.Id, optionId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested theme option could not be found.");
    }

    private static ThemeInstallationConfiguration GetThemeConfiguration(ThemesConfiguration configuration, string themeId)
    {
        if (!configuration.Themes.TryGetValue(themeId, out var themeConfiguration) || themeConfiguration is null)
        {
            themeConfiguration = new ThemeInstallationConfiguration();
            configuration.Themes[themeId] = themeConfiguration;
        }

        themeConfiguration.Values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return themeConfiguration;
    }

    private static string GetStoredOptionValue(ThemeOptionManifest option, ThemeInstallationConfiguration configuration)
    {
        if (configuration.Values.TryGetValue(option.Id, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return GetDefaultStoredValue(option);
    }

    private static string GetDefaultStoredValue(ThemeOptionManifest option)
    {
        return option.Type.ToLowerInvariant() switch
        {
            "toggle" => option.DefaultBool.ToString().ToLowerInvariant(),
            "choice" => option.DefaultChoiceId
                ?? option.Choices.FirstOrDefault()?.Id
                ?? string.Empty,
            "range" => option.DefaultNumber.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty,
        };
    }

    private static bool ParseBoolValue(string value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseRangeValue(ThemeOptionManifest option, string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            parsedValue = option.DefaultNumber;
        }

        return ClampRangeValue(option, parsedValue);
    }

    private static int ClampRangeValue(ThemeOptionManifest option, int value)
    {
        return Math.Max(option.Min, Math.Min(option.Max, value));
    }

    private static string ResolveCssTokenValue(ThemeOptionManifest option, string storedValue)
    {
        return option.Type.ToLowerInvariant() switch
        {
            "toggle" => ParseBoolValue(storedValue) ? option.OnValue : option.OffValue,
            "choice" => option.Choices.FirstOrDefault(choice =>
                    string.Equals(choice.Id, storedValue, StringComparison.OrdinalIgnoreCase))?.Value
                ?? option.Choices.FirstOrDefault()?.Value
                ?? string.Empty,
            "range" => ParseRangeValue(option, storedValue).ToString(CultureInfo.InvariantCulture),
            _ => storedValue,
        };
    }

    private static IReadOnlyList<ThemeInjectManifest> GetActiveInjectRules(
        ThemeManifest manifest,
        ThemeInstallationConfiguration configuration)
    {
        var injectRules = new List<ThemeInjectManifest>();
        injectRules.AddRange(GetInjectRules(manifest));

        foreach (var option in manifest.Options)
        {
            var storedValue = GetStoredOptionValue(option, configuration);

            switch (option.Type.ToLowerInvariant())
            {
                case "toggle":
                    injectRules.AddRange(ParseBoolValue(storedValue) ? option.OnInjectRules : option.OffInjectRules);
                    break;
                case "choice":
                    var selectedChoice = option.Choices.FirstOrDefault(choice =>
                        string.Equals(choice.Id, storedValue, StringComparison.OrdinalIgnoreCase));

                    if (selectedChoice is not null)
                    {
                        injectRules.AddRange(selectedChoice.InjectRules);
                    }

                    break;
            }
        }

        return injectRules;
    }

    private static IReadOnlyList<ThemeInjectManifest> GetInjectRules(ThemeManifest manifest)
    {
        if (manifest.InjectRules.Count > 0)
        {
            return manifest.InjectRules;
        }

        if (manifest.CssLines.Count == 0)
        {
            return [];
        }

        return
        [
            new ThemeInjectManifest
            {
                Targets = ["QuickAccess.*"],
                CssLines = manifest.CssLines,
            }
        ];
    }

    private static string RewriteCssLoaderSelectors(string css)
    {
        css = CssLoaderModuleClassRegex.Replace(
            css,
            match =>
            {
                var className = match.Groups[1].Value;
                var prefix = TryGetCssLoaderClassPrefix(className);
                return string.IsNullOrWhiteSpace(prefix)
                    ? match.Value
                    : $"[class*=\"{prefix}\"]";
            });

        foreach (var (prefix, replacement) in CssLoaderSemanticClassMappings)
        {
            css = css.Replace($"[class*=\"{prefix}\"]", replacement, StringComparison.Ordinal);
            css = css.Replace($".{prefix}", replacement, StringComparison.Ordinal);
        }

        return css;
    }

    private static string? TryGetCssLoaderClassPrefix(string className)
    {
        if (string.IsNullOrWhiteSpace(className) ||
            className.StartsWith("steamloader-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = className.Split('_');
        if (parts.Length < 3)
        {
            return null;
        }

        var moduleName = parts[0];
        var localName = parts[1];
        if (string.IsNullOrWhiteSpace(moduleName) ||
            string.IsNullOrWhiteSpace(localName))
        {
            return null;
        }

        var prefix = $"{moduleName}_{localName}_";
        return prefix.Any(char.IsLetter) ? prefix : null;
    }

    private void EnsureBundledThemeSeeds()
    {
        WriteBundledThemeSeed(
            "Clean Gameview",
            CleanGameviewSeedFiles);
    }

    private void WriteBundledThemeSeed(string themeFolderName, IReadOnlyList<string> relativeFilePaths)
    {
        var targetRoot = Path.Combine(_localThemesFolder, "_steamloader", themeFolderName);
        var resourceThemeFolderName = themeFolderName.Replace('-', '_').Replace(' ', '_');

        foreach (var relativeFilePath in relativeFilePaths)
        {
            var assetPath = $"Assets/theme_seeds/{resourceThemeFolderName}/{relativeFilePath.Replace('\\', '/')}";
            var targetPath = Path.Combine(targetRoot, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(targetPath, EmbeddedAssetReader.ReadText(assetPath));
        }
    }

    private static ThemeManifest ConvertCssLoaderTheme(CssLoaderThemeDocument cssLoaderTheme)
    {
        var manifest = new ThemeManifest
        {
            Id = Slugify(cssLoaderTheme.Name),
            Title = cssLoaderTheme.Name,
            Author = string.IsNullOrWhiteSpace(cssLoaderTheme.Author) ? "Unknown" : cssLoaderTheme.Author,
            Version = string.IsNullOrWhiteSpace(cssLoaderTheme.Version) ? "1.0" : cssLoaderTheme.Version,
            Description = cssLoaderTheme.Description ?? string.Empty,
            StoreDescription = cssLoaderTheme.Description ?? string.Empty,
            IsCommunity = true,
            SourceLabel = "CSS Loader",
            Targets = BuildCssLoaderTargets(cssLoaderTheme).ToList(),
            InjectRules = cssLoaderTheme.Inject
                .SelectMany(entry => BuildInjectRulesFromCssLoaderFile(entry.Key, entry.Value))
                .ToList(),
            Options = cssLoaderTheme.Patches
                .Select(entry => ConvertCssLoaderPatch(entry.Key, entry.Value))
                .Where(option => option is not null)
                .Cast<ThemeOptionManifest>()
                .ToList(),
        };

        return manifest;
    }

    private static IReadOnlyList<string> BuildCssLoaderTargets(CssLoaderThemeDocument cssLoaderTheme)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(cssLoaderTheme.Target))
        {
            targets.Add(cssLoaderTheme.Target);
        }

        foreach (var injectTargets in cssLoaderTheme.Inject.Values)
        {
            foreach (var target in ParseTargetArray(injectTargets))
            {
                targets.Add(target);
            }
        }

        return targets.Count > 0 ? targets.ToList() : ["SP"];
    }

    private static ThemeOptionManifest? ConvertCssLoaderPatch(string patchName, CssLoaderPatchManifest patch)
    {
        var patchType = (patch.Type ?? string.Empty).Trim().ToLowerInvariant();
        return patchType switch
        {
            "dropdown" => BuildCssLoaderDropdownOption(patchName, patch),
            "checkbox" => BuildCssLoaderCheckboxOption(patchName, patch),
            _ => null,
        };
    }

    private static ThemeOptionManifest BuildCssLoaderDropdownOption(string patchName, CssLoaderPatchManifest patch)
    {
        var choices = patch.Values
            .Select(entry => new ThemeChoiceManifest
            {
                Id = Slugify(entry.Key),
                Title = entry.Key,
                InjectRules = BuildInjectRulesFromCssLoaderPayload(entry.Value),
            })
            .ToList();

        var defaultChoiceId = choices.FirstOrDefault(choice =>
                string.Equals(choice.Title, patch.Default, StringComparison.OrdinalIgnoreCase))?.Id
            ?? choices.FirstOrDefault()?.Id
            ?? string.Empty;

        return new ThemeOptionManifest
        {
            Id = Slugify(patchName),
            Title = patchName,
            Description = $"Choose the {patchName.ToLowerInvariant()} behavior used by this CSS Loader theme.",
            Type = "choice",
            DefaultChoiceId = defaultChoiceId,
            Choices = choices,
        };
    }

    private static ThemeOptionManifest BuildCssLoaderCheckboxOption(string patchName, CssLoaderPatchManifest patch)
    {
        patch.Values.TryGetValue("Yes", out var yesPayload);
        patch.Values.TryGetValue("No", out var noPayload);

        return new ThemeOptionManifest
        {
            Id = Slugify(patchName),
            Title = patchName,
            Description = $"Turn {patchName.ToLowerInvariant()} on or off for this CSS Loader theme.",
            Type = "toggle",
            DefaultBool = string.Equals(patch.Default, "Yes", StringComparison.OrdinalIgnoreCase),
            OnInjectRules = BuildInjectRulesFromCssLoaderPayload(yesPayload),
            OffInjectRules = BuildInjectRulesFromCssLoaderPayload(noPayload),
        };
    }

    private static List<ThemeInjectManifest> BuildInjectRulesFromCssLoaderFile(string cssFilePath, JsonElement payload)
    {
        var targets = ParseTargetArray(payload);
        return targets.Count > 0
            ? [new ThemeInjectManifest { Targets = targets, CssFiles = [cssFilePath] }]
            : [];
    }

    private static List<ThemeInjectManifest> BuildInjectRulesFromCssLoaderPayload(JsonElement payload)
    {
        var injectRules = new List<ThemeInjectManifest>();
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return injectRules;
        }

        foreach (var property in payload.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var arrayItems = property.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();

            if (arrayItems.Count == 0)
            {
                continue;
            }

            if (property.Name.StartsWith("--", StringComparison.Ordinal))
            {
                if (arrayItems.Count < 2)
                {
                    continue;
                }

                injectRules.Add(new ThemeInjectManifest
                {
                    Targets = arrayItems.Skip(1).ToList(),
                    CssLines = [$":root {{ {property.Name}: {arrayItems[0]}; }}"],
                });
                continue;
            }

            injectRules.Add(new ThemeInjectManifest
            {
                Targets = arrayItems,
                CssFiles = [property.Name],
            });
        }

        return injectRules;
    }

    private static List<string> ParseTargetArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();
    }

    private static bool MatchesTarget(ThemeRenderTarget renderTarget, string targetPattern)
    {
        if (string.IsNullOrWhiteSpace(targetPattern))
        {
            return false;
        }

        foreach (var expandedPattern in ExpandTargetPattern(targetPattern))
        {
            if (MatchesTargetPattern(renderTarget, expandedPattern))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> ExpandTargetPattern(string targetPattern)
    {
        return targetPattern switch
        {
            "SP" =>
            [
                "SharedJSContext",
                "Steam Big Picture Mode",
                "~https://steamloopback.host/routes/~",
                "~https://steamloopback.host/index.html~"
            ],
            "QuickAccess" => ["QuickAccess.*"],
            "MainMenu" => ["MainMenu.*"],
            "Steam Big Picture Mode" =>
            [
                "Big-Picture.*",
                "~https://steamloopback.host/index.html~"
            ],
            _ => [targetPattern]
        };
    }

    private static bool MatchesTargetPattern(ThemeRenderTarget renderTarget, string targetPattern)
    {
        if (targetPattern.Length > 2 &&
            targetPattern.StartsWith('~') &&
            targetPattern.EndsWith('~'))
        {
            var urlPart = targetPattern[1..^1];
            return !string.IsNullOrWhiteSpace(renderTarget.Url) &&
                   renderTarget.Url.Contains(urlPart, StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrWhiteSpace(renderTarget.Title))
        {
            return false;
        }

        if (LooksLikeRegex(targetPattern))
        {
            try
            {
                return Regex.IsMatch(renderTarget.Title, targetPattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return string.Equals(renderTarget.Title, targetPattern, StringComparison.OrdinalIgnoreCase);
            }
        }

        return string.Equals(renderTarget.Title, targetPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRegex(string value)
    {
        return value.IndexOfAny(['*', '.', '+', '?', '[', ']', '(', ')', '|', '^', '$']) >= 0;
    }

    private static void DisableAllThemes(ThemesConfiguration configuration)
    {
        foreach (var themeConfiguration in configuration.Themes.Values)
        {
            themeConfiguration.Enabled = false;
        }
    }

    private static void KeepOnlyFirstEnabledTheme(ThemesConfiguration configuration)
    {
        ThemeInstallationConfiguration? firstEnabledTheme = null;
        foreach (var themeConfiguration in configuration.Themes.Values.Where(theme => theme.Installed && theme.Enabled))
        {
            if (firstEnabledTheme is null)
            {
                firstEnabledTheme = themeConfiguration;
                continue;
            }

            themeConfiguration.Enabled = false;
        }
    }

    private ThemeProfileConfiguration BuildStoredProfile(string title, ThemesConfiguration configuration)
    {
        return new ThemeProfileConfiguration
        {
            Title = title,
            Author = "Tools for Steam",
            Description = $"Created from the current Tools for Steam theme stack on {DateTime.Now:yyyy-MM-dd}.",
            Version = "1.0",
            SourceLabel = "Local",
            DownloadCount = 0,
            Themes = CaptureProfileThemes(configuration),
        };
    }

    private Dictionary<string, ThemeProfileThemeConfiguration> CaptureProfileThemes(ThemesConfiguration configuration)
    {
        return configuration.Themes
            .Where(entry => entry.Value is not null && entry.Value.Installed)
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                entry => entry.Key,
                entry => new ThemeProfileThemeConfiguration
                {
                    Installed = entry.Value.Installed,
                    Enabled = entry.Value.Enabled,
                    Values = new Dictionary<string, string>(entry.Value.Values, StringComparer.OrdinalIgnoreCase),
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private void ApplyProfileConfiguration(ThemesConfiguration configuration, ThemeProfileConfiguration profile)
    {
        foreach (var themeKey in configuration.Themes.Keys.ToArray())
        {
            var themeConfiguration = GetThemeConfiguration(configuration, themeKey);

            if (!profile.Themes.TryGetValue(themeKey, out var profileTheme) || profileTheme is null)
            {
                themeConfiguration.Enabled = false;
                continue;
            }

            if (profileTheme.Installed)
            {
                themeConfiguration.Installed = true;
            }

            themeConfiguration.Enabled = profileTheme.Enabled && profileTheme.Installed;

            foreach (var pair in profileTheme.Values)
            {
                themeConfiguration.Values[pair.Key] = pair.Value;
            }
        }

        if (configuration.Settings.SingleThemeMode)
        {
            KeepOnlyFirstEnabledTheme(configuration);
        }
    }

    private bool ProfileMatchesCurrentSetup(ThemeProfileConfiguration profile, ThemesConfiguration configuration)
    {
        var current = BuildComparableProfileSignature(CaptureProfileThemes(configuration));
        var candidate = BuildComparableProfileSignature(profile.Themes);
        return string.Equals(current, candidate, StringComparison.Ordinal);
    }

    private string BuildComparableProfileSignature(IReadOnlyDictionary<string, ThemeProfileThemeConfiguration> themes)
    {
        var comparable = themes
            .Where(entry => entry.Value.Installed)
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new
            {
                ThemeId = entry.Key,
                Installed = entry.Value.Installed,
                Enabled = entry.Value.Enabled,
                Values = entry.Value.Values
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value))
                    .ToArray()
            })
            .ToArray();

        return JsonSerializer.Serialize(comparable, JsonOptions);
    }

    private string CreateUniqueProfileId(string title, ThemesConfiguration configuration)
    {
        var baseId = Slugify(title);
        if (string.IsNullOrWhiteSpace(baseId))
        {
            baseId = "profile";
        }

        var knownIds = new HashSet<string>(
            configuration.Profiles.Keys.Concat(_profileCatalog.Select(profile => profile.Id)),
            StringComparer.OrdinalIgnoreCase);

        var candidate = baseId;
        var suffix = 2;
        while (knownIds.Contains(candidate))
        {
            candidate = $"{baseId}-{suffix}";
            suffix += 1;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        var previousDash = false;

        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousDash = false;
                continue;
            }

            if (previousDash)
            {
                continue;
            }

            builder.Append('-');
            previousDash = true;
        }

        return builder.ToString().Trim('-');
    }

    private string ResolveThemeTitle(string themeId)
    {
        return _catalog.FirstOrDefault(theme => string.Equals(theme.Id, themeId, StringComparison.OrdinalIgnoreCase))?.Title
               ?? themeId;
    }

    private static ThemeProfileConfiguration CloneStoredProfile(ThemeProfileConfiguration profile)
    {
        return new ThemeProfileConfiguration
        {
            Title = profile.Title,
            Author = profile.Author,
            Description = profile.Description,
            Version = profile.Version,
            SourceLabel = profile.SourceLabel,
            DownloadCount = profile.DownloadCount,
            Themes = profile.Themes.ToDictionary(
                entry => entry.Key,
                entry => CloneProfileTheme(entry.Value),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    private static ThemeProfileThemeConfiguration CloneProfileTheme(ThemeProfileThemeConfiguration theme)
    {
        return new ThemeProfileThemeConfiguration
        {
            Installed = theme.Installed,
            Enabled = theme.Enabled,
            Values = new Dictionary<string, string>(theme.Values, StringComparer.OrdinalIgnoreCase),
        };
    }

    private sealed class ThemeCatalogDocument
    {
        public List<ThemeManifest> Themes { get; set; } = [];
    }

    private sealed class ThemeProfileCatalogDocument
    {
        public List<ThemeProfileManifest> Profiles { get; set; } = [];
    }

    private sealed class CssLoaderThemeDocument
    {
        public string Name { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public Dictionary<string, JsonElement> Inject { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, CssLoaderPatchManifest> Patches { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CssLoaderPatchManifest
    {
        public string Type { get; set; } = string.Empty;

        public string Default { get; set; } = string.Empty;

        public Dictionary<string, JsonElement> Values { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ThemeManifest
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = string.Empty;

        public string Version { get; set; } = "1.0";

        public string Description { get; set; } = string.Empty;

        public string StoreDescription { get; set; } = string.Empty;

        public bool IsCommunity { get; set; }

        public bool DefaultInstalled { get; set; }

        public bool DefaultEnabled { get; set; }

        public int DownloadCount { get; set; }

        public string SourceLabel { get; set; } = string.Empty;

        public List<string> Targets { get; set; } = [];

        public List<string> CssLines { get; set; } = [];

        public List<string> CssFiles { get; set; } = [];

        public List<ThemeInjectManifest> InjectRules { get; set; } = [];

        public List<ThemeOptionManifest> Options { get; set; } = [];
    }

    private sealed class ThemeInjectManifest
    {
        public List<string> Targets { get; set; } = [];

        public List<string> CssLines { get; set; } = [];

        public List<string> CssFiles { get; set; } = [];
    }

    private sealed class ThemeOptionManifest
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public int Min { get; set; }

        public int Max { get; set; }

        public int Step { get; set; } = 1;

        public int DefaultNumber { get; set; }

        public bool DefaultBool { get; set; }

        public string? DefaultChoiceId { get; set; }

        public string Unit { get; set; } = string.Empty;

        public string OnValue { get; set; } = string.Empty;

        public string OffValue { get; set; } = string.Empty;

        public List<ThemeInjectManifest> OnInjectRules { get; set; } = [];

        public List<ThemeInjectManifest> OffInjectRules { get; set; } = [];

        public List<ThemeChoiceManifest> Choices { get; set; } = [];
    }

    private sealed class ThemeChoiceManifest
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public List<ThemeInjectManifest> InjectRules { get; set; } = [];
    }

    private sealed class ThemeProfileManifest
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Author { get; set; } = "Tools for Steam";

        public string Description { get; set; } = string.Empty;

        public string Version { get; set; } = "1.0";

        public string SourceLabel { get; set; } = "Profile";

        public int DownloadCount { get; set; }

        public Dictionary<string, ThemeProfileThemeConfiguration> Themes { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ThemeProfileCatalogEntry(
        string Id,
        ThemeProfileConfiguration StoredProfile);

    private sealed record ThemeRenderTarget(
        string Title,
        string Url);
}
