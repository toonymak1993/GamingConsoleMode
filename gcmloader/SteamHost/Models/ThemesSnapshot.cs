namespace SteamLoader.App.Models;

public sealed record ThemeChoiceState(
    string Id,
    string Title);

public sealed record ThemeOptionState(
    string Id,
    string Title,
    string Description,
    string Type,
    bool? BoolValue,
    int? NumberValue,
    int? Min,
    int? Max,
    int? Step,
    string? Unit,
    string? SelectedChoiceId,
    IReadOnlyList<ThemeChoiceState> Choices);

public sealed record ThemeState(
    string Id,
    string Title,
    string Author,
    string Version,
    string Description,
    string StoreDescription,
    bool Installed,
    bool Enabled,
    string StatusText,
    string SourceLabel,
    int DownloadCount,
    IReadOnlyList<string> Targets,
    IReadOnlyList<ThemeOptionState> Options);

public sealed record ThemeProfileThemeState(
    string ThemeId,
    string ThemeTitle,
    bool Installed,
    bool Enabled,
    int OptionCount);

public sealed record ThemeProfileState(
    string Id,
    string Title,
    string Author,
    string Description,
    string Version,
    string StatusText,
    string SourceLabel,
    int DownloadCount,
    bool Installed,
    bool Selected,
    bool MatchesCurrentSetup,
    IReadOnlyList<ThemeProfileThemeState> Themes);

public sealed record ThemesProfilesState(
    string? SelectedProfileId,
    bool CurrentSetupMatchesSelectedProfile,
    IReadOnlyList<ThemeProfileState> InstalledProfiles,
    IReadOnlyList<ThemeProfileState> BrowseProfiles);

public sealed record ThemesSettingsState(
    bool ThemeEngineEnabled,
    bool ShowCommunityThemes,
    bool SingleThemeMode,
    bool AutoEnableOnInstall);

public sealed record ThemesSnapshot(
    ThemesSettingsState Settings,
    IReadOnlyList<ThemeState> InstalledThemes,
    IReadOnlyList<ThemeState> BrowseThemes,
    ThemesProfilesState Profiles,
    string ActiveCss,
    string StatusText,
    string LocalThemesFolder);
