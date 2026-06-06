using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.CoreAudioApi;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;
using Windows.UI;
using Button = Microsoft.UI.Xaml.Controls.Button;

namespace gcmloader
{
    public sealed partial class MainWindow : Window
    {
        private enum SettingsPane
        {
            Categories,
            Rows
        }

        private sealed class SettingsCategoryDefinition
        {
            public required string Id { get; init; }
            public required string Title { get; init; }
            public required string Subtitle { get; init; }
            public required string Glyph { get; init; }
            public required Button Button { get; init; }
            public required TextBlock SummaryText { get; init; }
            public List<SettingsRowDefinition> Rows { get; } = new();
        }

        private sealed class SettingsRowDefinition
        {
            public required string Id { get; init; }
            public required string CategoryId { get; init; }
            public required string Title { get; init; }
            public required string Description { get; init; }
            public required Button Button { get; init; }
            public required TextBlock ValueText { get; init; }
            public required StackPanel ValueHost { get; init; }
            public string? SectionTitle { get; init; }
            public Action? OnActivate { get; init; }
            public Action<int>? OnAdjust { get; init; }
        }

        private sealed class ManagedPreloadApp
        {
            public string Name { get; set; } = "New App";
            public string Path { get; set; } = string.Empty;
            public string Arguments { get; set; } = string.Empty;
            public bool StartHidden { get; set; }
        }

        private sealed class ManagedShortcut
        {
            public required string Function { get; init; }
            public string Key1 { get; set; } = "None";
            public string Key2 { get; set; } = "None";
            public double HoldDuration { get; set; }
            public bool Enabled { get; set; }
        }

        private static readonly string[] SettingsShortcutFunctions =
        {
            "taskmanager",
            "shortcut overlay",
            "kill process",
            "switch tab",
            "audio switch",
            "volume up",
            "volume down",
            "performance overlay",
            "xbox bar",
            "xbox keyboard"
        };

        private static readonly string[] SettingsShortcutButtons =
        {
            "None",
            "Guide",
            "A",
            "B",
            "X",
            "Y",
            "DPadUp",
            "DPadDown",
            "DPadLeft",
            "DPadRight",
            "Start",
            "Back",
            "LeftThumb",
            "RightThumb",
            "LeftShoulder",
            "RightShoulder"
        };

        private static readonly double[] SettingsShortcutHoldDurations =
        {
            0.0,
            0.25,
            0.5,
            0.75,
            1.0,
            1.5,
            2.0,
            3.0,
            5.0
        };

        private static readonly HashSet<string> SteamControllerShortcutRowIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "shortcut-enabled",
            "shortcut-key1",
            "shortcut-key2",
            "shortcut-hold",
            "shortcut-reset"
        };

        private const string DefaultThemeAccent = "Graphite";
        private const string DefaultThemeCardTint = "Graphite";
        private const string DefaultThemeGlassStrength = "Clear";
        private const string DefaultThemeCardSize = "Default";
        private const string DefaultThemeDockSize = "Default";
        private const string DefaultThemeDockPosition = "Bottom";
        private const string DefaultThemeTopDockSize = "Default";
        private const string DefaultThemeTopDockPosition = "Center";
        private const string ThemeDefaultsVersion = "graphite-v1";

        private static readonly string[] ThemeAccentPresets =
        {
            "Graphite",
            "Ice Blue",
            "Aurora Green",
            "Warm Amber",
            "Crimson",
            "Violet"
        };

        private static readonly string[] ThemeCardTintPresets =
        {
            "Frost Glass",
            "Deep Navy",
            "Graphite",
            "Forest",
            "Amber Smoke"
        };

        private static readonly string[] ThemeGlassStrengthPresets =
        {
            "Clear",
            "Balanced",
            "Solid"
        };

        private static readonly string[] ThemeSizePresets =
        {
            "Compact",
            "Default",
            "Large"
        };

        private static readonly string[] ThemeDockPositionPresets =
        {
            "Bottom",
            "Top"
        };

        private static readonly string[] ThemeTopDockPositionPresets =
        {
            "Left",
            "Center",
            "Right"
        };

        private readonly List<SettingsCategoryDefinition> _settingsCategories = new();
        private readonly Dictionary<string, SettingsCategoryDefinition> _settingsCategoriesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SettingsRowDefinition> _settingsRows = new();
        private readonly Dictionary<string, SettingsRowDefinition> _settingsRowsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ManagedPreloadApp> _managedPreloadApps = new();
        private readonly List<ManagedShortcut> _managedShortcuts = new();
        private readonly List<string> _settingsAudioDevices = new();

        private SettingsPane _settingsPane = SettingsPane.Categories;
        private int _selectedSettingsCategoryIndex;
        private int _selectedSettingsRowIndex;
        private int _selectedManagedPreloadIndex;
        private int _selectedManagedShortcutIndex;
        private bool _isCapturingShortcutButton;
        private bool _capturePrimaryShortcutButton;
        private bool _isSettingsOverlayInitialized;

        private string PreloadAppsJsonPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "preloadapps.json");

        private void ApplySteamOnlyMode()
        {
            AppSettings.Save("launcher", "steam");
        }

        private void InitializeSettingsOverlay()
        {
            if (_isSettingsOverlayInitialized)
            {
                return;
            }

            ApplySteamOnlyMode();
            EnsureSettingsDefaults();
            BuildSettingsRows();
            _isSettingsOverlayInitialized = true;
        }

        private void EnsureSettingsOverlayInitialized()
        {
            if (!_isSettingsOverlayInitialized)
            {
                InitializeSettingsOverlay();
            }

            ReloadSettingsOverlayState();
            _settingsPane = SettingsPane.Categories;
            _selectedSettingsCategoryIndex = Math.Clamp(_selectedSettingsCategoryIndex, 0, Math.Max(0, _settingsCategories.Count - 1));
            RebuildVisibleSettingsRows();
            RefreshSettingsOverlayValues();
        }

        private void BuildSettingsRows()
        {
            if (_settingsCategories.Count > 0)
            {
                return;
            }

            SettingsCategoryHost.Children.Clear();
            SettingsRowsHost.Children.Clear();
            _settingsCategories.Clear();
            _settingsCategoriesById.Clear();
            _settingsRows.Clear();
            _settingsRowsById.Clear();

            AddSettingsCategory("steam", "Steam", "Steam-only launcher path and core launch behavior.", "\uE7BF");
            AddSettingsRow(
                "steam",
                "steam-path",
                "Steam executable",
                "Pick a manual Steam path when auto-detection is not enough.",
                async () => await SelectSteamPathAsync(),
                sectionTitle: "Launcher");
            AddSettingsRow(
                "steam",
                "steam-path-reset",
                "Reset Steam path",
                "Return to automatic Steam detection.",
                ResetSteamPath,
                _ => ResetSteamPath(),
                "Launcher");
            AddSettingsRow(
                "steam",
                "steam-store-sync-enabled",
                "Sync third-party libraries",
                "Sync supported Epic, GOG and Xbox libraries into Steam before Steam launches.",
                ToggleSteamStoreSyncEnabledSetting,
                _ => ToggleSteamStoreSyncEnabledSetting(),
                "Store Sync");
            AddSettingsRow(
                "steam",
                "steam-store-sync-artwork",
                "Sync artwork",
                "Download Steam artwork for imported non-Steam games when Store Sync updates shortcuts.",
                ToggleSteamStoreSyncArtworkSetting,
                _ => ToggleSteamStoreSyncArtworkSetting(),
                "Store Sync");
            AddSettingsRow(
                "steam",
                "steam-store-sync-epic",
                "Epic Games",
                "Import launchable Epic Games titles into Steam automatically.",
                () => ToggleSteamStoreEnabledSetting("epic-games", SteamStoreSyncEpicKey),
                _ => ToggleSteamStoreEnabledSetting("epic-games", SteamStoreSyncEpicKey),
                "Stores");
            AddSettingsRow(
                "steam",
                "steam-store-sync-gog",
                "GOG Galaxy",
                "Import detected GOG installs into Steam automatically.",
                () => ToggleSteamStoreEnabledSetting("gog-galaxy", SteamStoreSyncGogKey),
                _ => ToggleSteamStoreEnabledSetting("gog-galaxy", SteamStoreSyncGogKey),
                "Stores");
            AddSettingsRow(
                "steam",
                "steam-store-sync-xbox",
                "Xbox / Game Pass",
                "Scan Xbox library folders and import launchable titles into Steam.",
                () => ToggleSteamStoreEnabledSetting("xbox-game-pass", SteamStoreSyncXboxKey),
                _ => ToggleSteamStoreEnabledSetting("xbox-game-pass", SteamStoreSyncXboxKey),
                "Stores");
            AddSettingsRow(
                "steam",
                "steam-store-sync-run",
                "Run Store Sync now",
                "Manually refresh Steam shortcuts and artwork right now.",
                async () => await RunSteamStoreSyncNowAsync(),
                sectionTitle: "Store Sync");
            AddSettingsRow(
                "steam",
                "steam-plugin-host-enabled",
                "Steam plugin host",
                "Run GCM's built-in Steam Quick Access tools host in the background.",
                async () => await ToggleSteamPluginHostEnabledSettingAsync(),
                sectionTitle: "Quick Access Host");
            AddSettingsRow(
                "steam",
                "steam-plugin-host-devmode",
                "Launch Steam in developer mode",
                "Start Steam with developer-mode support so the Quick Access host can reach Steam's DevTools port.",
                ToggleSteamPluginDeveloperModeSetting,
                _ => ToggleSteamPluginDeveloperModeSetting(),
                "Quick Access Host");
            AddSettingsRow(
                "steam",
                "steam-plugin-host-status",
                "Plugin host status",
                "Refresh the current status of the Steam Quick Access host and its Steam attachment state.",
                async () => await RefreshSteamPluginHostStatusAsync(),
                sectionTitle: "Quick Access Host");
            AddSettingsRow(
                "steam",
                "steam-plugin-devtools-status",
                "Steam DevTools port",
                "Check whether Steam exposes SharedJSContext and Quick Access over 127.0.0.1:8080.",
                async () => await RefreshSteamPluginHostStatusAsync(),
                sectionTitle: "Quick Access Host");
            AddSettingsRow(
                "steam",
                "steam-plugin-log",
                "Latest service log",
                "Shows the latest integrated Steam host or Store Sync message from GCM.",
                async () => await RefreshSteamPluginHostStatusAsync(),
                sectionTitle: "Quick Access Host");

            AddSettingsCategory("controller", "Controller", "Live controller mode, Steam Controller restrictions and shortcut compatibility.", "\uE7FC");
            AddSettingsRow(
                "controller",
                "controller-dualsense",
                "DualSense",
                "Shows whether DualSense-style navigation is the active controller input mode.",
                RefreshControllerSettingsStatus,
                _ => RefreshControllerSettingsStatus(),
                "Active Controller");
            AddSettingsRow(
                "controller",
                "controller-steam",
                "Steam Controller",
                "Shows whether Steam Controller mode is currently active in GCM.",
                RefreshControllerSettingsStatus,
                _ => RefreshControllerSettingsStatus(),
                "Active Controller");
            AddSettingsRow(
                "controller",
                "controller-xbox",
                "Xbox Controller",
                "Shows whether Xbox-style navigation is the active controller input mode.",
                RefreshControllerSettingsStatus,
                _ => RefreshControllerSettingsStatus(),
                "Active Controller");
            AddSettingsRow(
                "controller",
                "controller-shortcut-compat",
                "Controller Shortcuts",
                "Full shortcut compatibility is only available on Xbox and DualSense controllers.",
                RefreshControllerSettingsStatus,
                _ => RefreshControllerSettingsStatus(),
                "Steam Controller Mode");
            AddSettingsRow(
                "controller",
                "controller-steam-tools",
                "Tools for Steam",
                "Steam Controller mode keeps Tools for Steam features like the GCM task manager handoff available.",
                RefreshControllerSettingsStatus,
                _ => RefreshControllerSettingsStatus(),
                "Steam Controller Mode");
            AddSettingsRow(
                "controller",
                "controller-gcm-navigation",
                "GCM Navigation",
                "Basic Game Console Mode navigation remains available while Steam Controller mode is active.",
                RefreshControllerSettingsStatus,
                _ => RefreshControllerSettingsStatus(),
                "Steam Controller Mode");

            AddSettingsCategory("startup", "Startup", "Boot video, startup apps and Steam intro behavior.", "\uE7AD");
            AddSettingsRow(
                "startup",
                "startup-apps",
                "Control startup apps",
                "Disable noisy Windows startup apps before Steam opens.",
                () => ToggleBooleanSetting("usewinpartstartapps"),
                _ => ToggleBooleanSetting("usewinpartstartapps"),
                "Boot Flow");
            AddSettingsRow(
                "startup",
                "startup-video-enabled",
                "Use custom boot video",
                "Play your own boot video inside GCM before the UI appears.",
                () => ToggleBooleanSetting("usestartupvideo"),
                _ => ToggleBooleanSetting("usestartupvideo"),
                "Boot Video");
            AddSettingsRow(
                "startup",
                "steam-startup-video-enabled",
                "Replace Steam startup video",
                "Inject the chosen boot video into Steam Big Picture startup.",
                ToggleSteamStartupInjection,
                _ => ToggleSteamStartupInjection(),
                "Boot Video");
            AddSettingsRow(
                "startup",
                "startup-video-path",
                "Select boot video",
                "Choose the video file used for the custom GCM boot animation.",
                async () => await SelectStartupVideoAsync(),
                sectionTitle: "Boot Video");
            AddSettingsRow(
                "startup",
                "startup-video-reset",
                "Reset boot video",
                "Clear the custom boot video file path.",
                ResetStartupVideoPath,
                _ => ResetStartupVideoPath(),
                "Boot Video");

            AddSettingsCategory("preload", "Preload Apps", "Warm up helper tools before Steam takes over.", "\uE8B7");
            AddSettingsRow(
                "preload",
                "preload-enabled",
                "Use preload app list",
                "Launch your curated helper apps before Steam opens.",
                () => ToggleBooleanSetting("usepreloadlist"),
                _ => ToggleBooleanSetting("usepreloadlist"),
                "Preload List");
            AddSettingsRow(
                "preload",
                "preload-selected",
                "Selected preload app",
                "Cycle through configured preload apps.",
                () => CycleSelectedPreloadApp(1),
                CycleSelectedPreloadApp,
                "Selected App");
            AddSettingsRow(
                "preload",
                "preload-add",
                "Add preload app",
                "Choose an executable, shortcut, or script to preload.",
                async () => await AddPreloadAppAsync(),
                sectionTitle: "Preload List");
            AddSettingsRow(
                "preload",
                "preload-hidden",
                "Toggle selected app hidden",
                "Start the selected preload app minimized or hidden.",
                ToggleSelectedPreloadHidden,
                _ => ToggleSelectedPreloadHidden(),
                "Selected App");
            AddSettingsRow(
                "preload",
                "preload-arguments",
                "Selected app launch arguments",
                "Edit the command line parameters for the selected preload app.",
                async () => await EditSelectedPreloadArgumentsAsync(),
                sectionTitle: "Selected App");
            AddSettingsRow(
                "preload",
                "preload-repath",
                "Change selected app path",
                "Replace the executable or shortcut used by the selected preload app.",
                async () => await RepathSelectedPreloadAppAsync(),
                sectionTitle: "Selected App");
            AddSettingsRow(
                "preload",
                "preload-remove",
                "Remove selected preload app",
                "Delete the selected preload app entry from the list.",
                async () => await RemoveSelectedPreloadAppAsync(),
                sectionTitle: "Selected App");

            AddSettingsCategory("shortcuts", "Gamepad Shortcuts", "Configure every shortcut directly with your controller.", "\uE7FC");
            AddSettingsRow(
                "shortcuts",
                "shortcut-popup",
                "Shortcut popup",
                "Show a confirmation popup when a controller shortcut triggers.",
                () => ToggleBooleanSetting("shortcutpopup"),
                _ => ToggleBooleanSetting("shortcutpopup"),
                "Shortcut Behavior");
            AddSettingsRow(
                "shortcuts",
                "shortcut-selected",
                "Selected shortcut function",
                "Cycle through the controller actions that can be configured.",
                () => CycleSelectedShortcut(1),
                CycleSelectedShortcut,
                "Shortcut Behavior");
            AddSettingsRow(
                "shortcuts",
                "shortcut-enabled",
                "Enable selected shortcut",
                "Turn the currently selected controller shortcut on or off.",
                ToggleSelectedShortcutEnabled,
                _ => ToggleSelectedShortcutEnabled(),
                "Shortcut Behavior");
            AddSettingsRow(
                "shortcuts",
                "shortcut-key1",
                "Primary shortcut button",
                "Press A to listen for the next button, or use Right and X to cycle.",
                () => BeginShortcutButtonCapture(true),
                dir => CycleShortcutButton(true, dir),
                "Button Binding");
            AddSettingsRow(
                "shortcuts",
                "shortcut-key2",
                "Secondary shortcut button",
                "Press A to listen for the next button, or use Right and X to cycle.",
                () => BeginShortcutButtonCapture(false),
                dir => CycleShortcutButton(false, dir),
                "Button Binding");
            AddSettingsRow(
                "shortcuts",
                "shortcut-hold",
                "Shortcut hold time",
                "Adjust how long the combo must be held before it fires.",
                () => CycleShortcutHoldDuration(1),
                CycleShortcutHoldDuration,
                "Button Binding");
            AddSettingsRow(
                "shortcuts",
                "shortcut-reset",
                "Reset selected shortcut",
                "Restore the current shortcut to its default combo and hold time.",
                ResetSelectedShortcut,
                _ => ResetSelectedShortcut(),
                "Button Binding");

            AddSettingsCategory("theme", "Theme", "Tune Graphite glass, accent colors, card scale and both docks.", "\uE771");
            AddSettingsRow(
                "theme",
                "theme-accent",
                "Accent color",
                "Changes the subtle highlight color used by cards and active UI elements.",
                () => CycleThemePreset("theme_accent", ThemeAccentPresets, 1, true),
                dir => CycleThemePreset("theme_accent", ThemeAccentPresets, dir, true),
                "Colors");
            AddSettingsRow(
                "theme",
                "theme-card-tint",
                "Card glass tint",
                "Controls the transparent background color behind launcher and process cards.",
                () => CycleThemePreset("theme_card_tint", ThemeCardTintPresets, 1, true),
                dir => CycleThemePreset("theme_card_tint", ThemeCardTintPresets, dir, true),
                "Colors");
            AddSettingsRow(
                "theme",
                "theme-glass-strength",
                "Glass strength",
                "Adjusts how transparent or solid the shell glass should feel.",
                () => CycleThemePreset("theme_glass_strength", ThemeGlassStrengthPresets, 1, true),
                dir => CycleThemePreset("theme_glass_strength", ThemeGlassStrengthPresets, dir, true),
                "Glass");
            AddSettingsRow(
                "theme",
                "theme-card-size",
                "Card size",
                "Scales the launcher and live process cards.",
                () => CycleThemePreset("theme_card_size", ThemeSizePresets, 1, true),
                dir => CycleThemePreset("theme_card_size", ThemeSizePresets, dir, true),
                "Layout");
            AddSettingsRow(
                "theme",
                "theme-dock-size",
                "Bottom dock size",
                "Scales the status and controller hint dock.",
                () => CycleThemePreset("theme_dock_size", ThemeSizePresets, 1, false),
                dir => CycleThemePreset("theme_dock_size", ThemeSizePresets, dir, false),
                "Layout");
            AddSettingsRow(
                "theme",
                "theme-dock-position",
                "Dock position",
                "Choose whether the controller/status dock sits at the bottom or top.",
                () => CycleThemePreset("theme_dock_position", ThemeDockPositionPresets, 1, false),
                dir => CycleThemePreset("theme_dock_position", ThemeDockPositionPresets, dir, false),
                "Layout");
            AddSettingsRow(
                "theme",
                "theme-top-dock-size",
                "Top dock size",
                "Scales the top status dock with clock, audio, settings and power actions.",
                () => CycleThemePreset("theme_top_dock_size", ThemeSizePresets, 1, false),
                dir => CycleThemePreset("theme_top_dock_size", ThemeSizePresets, dir, false),
                "Top Dock");
            AddSettingsRow(
                "theme",
                "theme-top-dock-position",
                "Top dock position",
                "Pins the top status dock left, center or right.",
                () => CycleThemePreset("theme_top_dock_position", ThemeTopDockPositionPresets, 1, false),
                dir => CycleThemePreset("theme_top_dock_position", ThemeTopDockPositionPresets, dir, false),
                "Top Dock");
            AddSettingsRow(
                "theme",
                "theme-reset",
                "Reset theme",
                "Restore the default Graphite glass theme, dock positions and card scale.",
                ResetThemeSettings,
                _ => ResetThemeSettings(),
                "Defaults");

            AddSettingsCategory("audio-visuals", "Audio & Visuals", "Audio handoff, wallpaper and display helpers.", "\uE8D6");
            AddSettingsRow(
                "audio-visuals",
                "preaudio-enabled",
                "Pre-audio device switch",
                "Switch to a start device before Steam and restore the original device when leaving.",
                () => ToggleBooleanSetting("usepreaudio"),
                _ => ToggleBooleanSetting("usepreaudio"),
                "Audio Handoff");
            AddSettingsRow(
                "audio-visuals",
                "preaudio-start",
                "Pre-audio start device",
                "Pick which output device becomes active before Steam launches.",
                () => CycleAudioDeviceSetting("preaudiostart", 1),
                dir => CycleAudioDeviceSetting("preaudiostart", dir),
                "Audio Handoff");
            AddSettingsRow(
                "audio-visuals",
                "preaudio-end",
                "Pre-audio restore device",
                "Pick which output device is restored when GCM exits.",
                () => CycleAudioDeviceSetting("preaudioend", 1),
                dir => CycleAudioDeviceSetting("preaudioend", dir),
                "Audio Handoff");
            AddSettingsRow(
                "audio-visuals",
                "wallpaper-enabled",
                "Use GCM wallpaper",
                "Swap the desktop wallpaper while GCM is active.",
                ToggleWallpaperSetting,
                _ => ToggleWallpaperSetting(),
                "Wallpaper");
            AddSettingsRow(
                "audio-visuals",
                "wallpaper-path",
                "Choose wallpaper file",
                "Pick the wallpaper image to use while GCM is running.",
                async () => await SelectWallpaperAsync(),
                sectionTitle: "Wallpaper");

            AddSettingsCategory("loader-ui", "Loader UI", "Remaining loader-side behavior and local system settings.", "\uE790");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-refresh",
                "Refresh local runtime status",
                "Re-check the local-only runtime path that GCM uses instead of a privileged helper service.",
                async () => await RefreshPrivilegedServiceStatusAsync(),
                sectionTitle: "GCM Runtime");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-status",
                "Runtime mode",
                "Shows whether GCM is running in the streamlined local mode without a background service.",
                async () => await RefreshPrivilegedServiceStatusAsync(notifyUser: false),
                sectionTitle: "GCM Runtime");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-shell",
                "Shell handoff mode",
                "Shows whether Windows currently points the Winlogon shell at GCM. Runtime handoffs no longer rewrite that boot entry.",
                async () => await RefreshPrivilegedServiceStatusAsync(notifyUser: false),
                sectionTitle: "GCM Runtime");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-uac",
                "UAC handoff mode",
                "Shows how UAC-related cleanup behaves when GCM runs without a privileged helper.",
                async () => await RefreshPrivilegedServiceStatusAsync(notifyUser: false),
                sectionTitle: "GCM Runtime");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-keyboard",
                "Keyboard redirect bridge",
                "Shows the local keyboard redirect fallback used for the Windows touch keyboard bridge.",
                async () => await RefreshPrivilegedServiceStatusAsync(notifyUser: false),
                sectionTitle: "GCM Runtime");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-touchkeyboard",
                "Touch keyboard bridge",
                "Shows how GCM reaches the Windows touch keyboard subsystem directly in local mode.",
                async () => await RefreshPrivilegedServiceStatusAsync(notifyUser: false),
                sectionTitle: "GCM Runtime");
            AddSettingsRow(
                "loader-ui",
                "gcm-service-log",
                "Latest runtime note",
                "Displays the newest local runtime note so startup and subsystem issues stay visible in one place.",
                async () => await RefreshPrivilegedServiceStatusAsync(notifyUser: false),
                sectionTitle: "Diagnostics");
            AddSettingsRow(
                "loader-ui",
                "discord-card",
                "Show Discord launcher card",
                "Keep the Discord card visible in the launcher column.",
                ToggleDiscordCardSetting,
                _ => ToggleDiscordCardSetting(),
                "Shell UI");
            AddSettingsRow(
                "loader-ui",
                "taskbar-enabled",
                "Keep Windows taskbar visible",
                "Let the standard Windows taskbar stay visible while GCM runs.",
                ToggleTaskbarSetting,
                _ => ToggleTaskbarSetting(),
                "Shell UI");
            AddSettingsRow(
                "loader-ui",
                "steamgriddb-key",
                "SteamGridDB API key",
                "Set the key used for in-loader artwork searches.",
                async () => await EditSteamGridApiKeyAsync(),
                sectionTitle: "Artwork");
            AddSettingsRow(
                "loader-ui",
                "uac-enabled",
                "Re-enable UAC after exit",
                "Restore Windows UAC when leaving GCM.",
                () => ToggleBooleanSetting("uac"),
                _ => ToggleBooleanSetting("uac"),
                "System");
            AddSettingsRow(
                "loader-ui",
                "windows-settings",
                "Open Windows Settings",
                "Jump to the native Windows settings app without leaving the loader flow.",
                OpenWindowsSettings,
                sectionTitle: "System");

            _selectedSettingsCategoryIndex = 0;
            _selectedSettingsRowIndex = 0;
        }

        private void AddSettingsCategory(string id, string title, string subtitle, string glyph)
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(18, 16, 18, 16),
                Tag = id
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconHolder = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Child = new FontIcon
                {
                    Glyph = glyph,
                    FontSize = 20,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var textPanel = new StackPanel
            {
                Spacing = 3,
                Margin = new Thickness(14, 0, 0, 0)
            };
            textPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White)
            });

            var summaryText = new TextBlock
            {
                Text = "Ready",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxWidth = 120,
                Opacity = 0.9,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };

            grid.Children.Add(iconHolder);
            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);
            Grid.SetColumn(summaryText, 2);
            grid.Children.Add(summaryText);

            button.Content = grid;
            button.Click += SettingsCategory_Click;
            SettingsCategoryHost.Children.Add(button);

            var category = new SettingsCategoryDefinition
            {
                Id = id,
                Title = title,
                Subtitle = subtitle,
                Glyph = glyph,
                Button = button,
                SummaryText = summaryText
            };

            _settingsCategories.Add(category);
            _settingsCategoriesById[id] = category;
        }

        private void AddSettingsRow(
            string categoryId,
            string id,
            string title,
            string description,
            Action onActivate,
            Action<int>? onAdjust = null,
            string? sectionTitle = null)
        {
            if (!_settingsCategoriesById.TryGetValue(categoryId, out SettingsCategoryDefinition? category))
            {
                throw new InvalidOperationException($"Unknown settings category '{categoryId}'.");
            }

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(22, 20, 22, 20),
                Margin = new Thickness(0),
                Tag = id
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rowTextPanel = new StackPanel { Spacing = 6 };
            rowTextPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White)
            });
            rowTextPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.74,
                Foreground = new SolidColorBrush(Colors.White)
            });

            var valueText = new TextBlock
            {
                Text = "...",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                Opacity = 0.96,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 340,
                VerticalAlignment = VerticalAlignment.Center
            };

            var valueHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            valueHost.Children.Add(valueText);

            rowGrid.Children.Add(rowTextPanel);
            Grid.SetColumn(valueHost, 1);
            rowGrid.Children.Add(valueHost);

            button.Content = rowGrid;
            button.Click += SettingsRow_Click;

            var row = new SettingsRowDefinition
            {
                Id = id,
                CategoryId = categoryId,
                Title = title,
                Description = description,
                Button = button,
                ValueText = valueText,
                ValueHost = valueHost,
                SectionTitle = sectionTitle,
                OnActivate = onActivate,
                OnAdjust = onAdjust
            };

            category.Rows.Add(row);
            _settingsRows.Add(row);
            _settingsRowsById[id] = row;
        }

        private void SettingsCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string id)
            {
                return;
            }

            int index = _settingsCategories.FindIndex(category => string.Equals(category.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            SelectSettingsCategory(index);
            _settingsPane = SettingsPane.Categories;
            UpdateVisualFocus();
        }

        private void SettingsRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string id || !_settingsRowsById.TryGetValue(id, out SettingsRowDefinition? row))
            {
                return;
            }

            int categoryIndex = _settingsCategories.FindIndex(category => string.Equals(category.Id, row.CategoryId, StringComparison.OrdinalIgnoreCase));
            if (categoryIndex >= 0 && categoryIndex != _selectedSettingsCategoryIndex)
            {
                SelectSettingsCategory(categoryIndex);
            }

            List<SettingsRowDefinition> activeRows = GetActiveSettingsRows();
            int index = activeRows.FindIndex(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            _selectedSettingsRowIndex = index;
            _settingsPane = SettingsPane.Rows;
            UpdateVisualFocus();
            row.OnActivate?.Invoke();
        }

        private void SelectSettingsCategory(int index)
        {
            if (_settingsCategories.Count == 0)
            {
                _selectedSettingsCategoryIndex = 0;
                _selectedSettingsRowIndex = 0;
                SettingsRowsHost.Children.Clear();
                return;
            }

            index = Math.Clamp(index, 0, _settingsCategories.Count - 1);
            if (_selectedSettingsCategoryIndex == index && SettingsRowsHost.Children.Count > 0)
            {
                _selectedSettingsRowIndex = Math.Clamp(_selectedSettingsRowIndex, 0, Math.Max(0, GetActiveSettingsRows().Count - 1));
                RefreshSettingsOverlayValues();
                return;
            }

            CancelShortcutButtonCapture();
            _selectedSettingsCategoryIndex = index;
            _selectedSettingsRowIndex = 0;
            RebuildVisibleSettingsRows();
            RefreshSettingsOverlayValues();
        }

        private void RebuildVisibleSettingsRows()
        {
            SettingsRowsHost.Children.Clear();

            List<SettingsRowDefinition> activeRows = GetActiveSettingsRows();
            string? lastSectionTitle = null;
            foreach (SettingsRowDefinition row in activeRows)
            {
                if (!string.IsNullOrWhiteSpace(row.SectionTitle) &&
                    !string.Equals(lastSectionTitle, row.SectionTitle, StringComparison.OrdinalIgnoreCase))
                {
                    SettingsRowsHost.Children.Add(CreateSettingsSectionHeader(row.SectionTitle));
                    lastSectionTitle = row.SectionTitle;
                }

                SettingsRowsHost.Children.Add(row.Button);
            }

            if (activeRows.Count == 0)
            {
                _selectedSettingsRowIndex = 0;
                return;
            }

            _selectedSettingsRowIndex = Math.Clamp(_selectedSettingsRowIndex, 0, activeRows.Count - 1);
        }

        private TextBlock CreateSettingsSectionHeader(string title)
        {
            return new TextBlock
            {
                Text = title.ToUpperInvariant(),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 232, 242, 255)),
                CharacterSpacing = 90,
                Margin = new Thickness(4, 10, 0, 0)
            };
        }

        private List<SettingsRowDefinition> GetActiveSettingsRows()
        {
            if (_settingsCategories.Count == 0 || _selectedSettingsCategoryIndex < 0 || _selectedSettingsCategoryIndex >= _settingsCategories.Count)
            {
                return new List<SettingsRowDefinition>();
            }

            SettingsCategoryDefinition category = _settingsCategories[_selectedSettingsCategoryIndex];
            if (string.Equals(category.Id, "shortcuts", StringComparison.OrdinalIgnoreCase) &&
                IsSteamControllerInputModeActive())
            {
                return category.Rows
                    .Where(row => SteamControllerShortcutRowIds.Contains(row.Id))
                    .ToList();
            }

            return category.Rows;
        }

        private SettingsCategoryDefinition? SelectedSettingsCategoryOrNull()
        {
            if (_settingsCategories.Count == 0 || _selectedSettingsCategoryIndex < 0 || _selectedSettingsCategoryIndex >= _settingsCategories.Count)
            {
                return null;
            }

            return _settingsCategories[_selectedSettingsCategoryIndex];
        }

        private SettingsRowDefinition? SelectedSettingsRowOrNull()
        {
            List<SettingsRowDefinition> rows = GetActiveSettingsRows();
            if (rows.Count == 0 || _selectedSettingsRowIndex < 0 || _selectedSettingsRowIndex >= rows.Count)
            {
                return null;
            }

            return rows[_selectedSettingsRowIndex];
        }

        private void RefreshControllerSettingsStatus()
        {
            RefreshSettingsOverlayValues();
        }

        private bool IsControllerTypeActive(ControllerType controllerType)
        {
            return _hasObservedControllerInput && _lastActiveControllerType == controllerType;
        }

        private string FormatControllerModeSummary()
        {
            if (!_hasObservedControllerInput)
            {
                return "Waiting for input";
            }

            return _lastActiveControllerType switch
            {
                ControllerType.PlayStation => "DualSense active",
                ControllerType.SteamController => "Steam Controller mode",
                _ => "Xbox active"
            };
        }

        private void ReloadSettingsOverlayState()
        {
            LoadManagedPreloadApps();
            LoadManagedShortcuts();
            LoadAudioDeviceChoices();
        }

        private void EnsureSettingsDefaults()
        {
            EnsureBooleanSetting("usewinpartstartapps", true);
            EnsureBooleanSetting("shortcutpopup", true);
            EnsureBooleanSetting("enable_taskbar", false);
            EnsureBooleanSetting("usestartupvideo", false);
            EnsureBooleanSetting("usesteamstartupvideo", false);
            EnsureBooleanSetting("usepreloadlist", false);
            EnsureBooleanSetting("usepreaudio", false);
            EnsureBooleanSetting("gcmwallpaper", false);
            EnsureBooleanSetting("show_discord", true);
            EnsureBooleanSetting("uac", true);

            EnsureStringSetting("steamlauncherpath", string.Empty);
            EnsureStringSetting("startupvideo_path", string.Empty);
            EnsureStringSetting("preaudiostart", GetDefaultAudioDeviceName());
            EnsureStringSetting("preaudioend", GetDefaultAudioDeviceName());
            EnsureStringSetting("gcmwallpaperpath", string.Empty);
            EnsureStringSetting("steamgriddb_api_key", string.Empty);
            EnsureStringSetting("theme_accent", DefaultThemeAccent);
            EnsureStringSetting("theme_card_tint", DefaultThemeCardTint);
            EnsureStringSetting("theme_glass_strength", DefaultThemeGlassStrength);
            EnsureStringSetting("theme_card_size", DefaultThemeCardSize);
            EnsureStringSetting("theme_dock_size", DefaultThemeDockSize);
            EnsureStringSetting("theme_dock_position", DefaultThemeDockPosition);
            EnsureStringSetting("theme_top_dock_size", DefaultThemeTopDockSize);
            EnsureStringSetting("theme_top_dock_position", DefaultThemeTopDockPosition);
            MigrateThemeDefaultsIfNeeded();
            EnsureSteamIntegrationDefaults();

            EnsureDefaultShortcuts();
        }

        private void MigrateThemeDefaultsIfNeeded()
        {
            string currentVersion = GetSetting("theme_defaults_version", string.Empty, false);
            if (string.Equals(currentVersion, ThemeDefaultsVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool stillUsingPreviousDefaults =
                string.Equals(GetSetting("theme_accent", string.Empty, false), "Ice Blue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetSetting("theme_card_tint", string.Empty, false), "Frost Glass", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetSetting("theme_glass_strength", string.Empty, false), "Balanced", StringComparison.OrdinalIgnoreCase);

            if (stillUsingPreviousDefaults)
            {
                AppSettings.Save("theme_accent", DefaultThemeAccent);
                AppSettings.Save("theme_card_tint", DefaultThemeCardTint);
                AppSettings.Save("theme_glass_strength", DefaultThemeGlassStrength);
            }

            AppSettings.Save("theme_defaults_version", ThemeDefaultsVersion);
        }

        private void EnsureBooleanSetting(string key, bool value)
        {
            try
            {
                AppSettings.Load<bool>(key);
            }
            catch
            {
                AppSettings.Save(key, value);
            }
        }

        private void EnsureStringSetting(string key, string value)
        {
            try
            {
                AppSettings.Load<string>(key);
            }
            catch
            {
                AppSettings.Save(key, value);
            }
        }

        private static ManagedShortcut CreateDefaultManagedShortcut(string function)
        {
            return new ManagedShortcut
            {
                Function = function,
                Key1 = string.Equals(function, "taskmanager", StringComparison.OrdinalIgnoreCase) ? "Back" : "None",
                Key2 = string.Equals(function, "taskmanager", StringComparison.OrdinalIgnoreCase) ? "X" : "None",
                HoldDuration = 0.0,
                Enabled = string.Equals(function, "taskmanager", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static bool TryMigrateTaskManagerShortcutToInstant(TomlTableArray shortcutsArray)
        {
            foreach (TomlTable entry in shortcutsArray)
            {
                string function = entry["function"]?.ToString() ?? string.Empty;
                if (!string.Equals(function, "taskmanager", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string key1 = entry["key1"]?.ToString() ?? "None";
                string key2 = entry["key2"]?.ToString() ?? "None";
                bool enabled = !entry.ContainsKey("enabled") || Convert.ToBoolean(entry["enabled"]);
                double holdDuration = entry.ContainsKey("hold_duration")
                    ? Convert.ToDouble(entry["hold_duration"])
                    : 0.0;

                bool isPreviousDefault =
                    enabled &&
                    string.Equals(key1, "Back", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(key2, "X", StringComparison.OrdinalIgnoreCase) &&
                    Math.Abs(holdDuration - 1.0) < 0.001;

                if (!isPreviousDefault)
                {
                    return false;
                }

                entry["hold_duration"] = 0.0;
                return true;
            }

            return false;
        }

        private void EnsureDefaultShortcuts()
        {
            string settingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");

            TomlTable rootTable;
            try
            {
                rootTable = File.Exists(settingsFilePath)
                    ? Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel()
                    : new TomlTable();
            }
            catch
            {
                rootTable = new TomlTable();
            }

            if (rootTable.TryGetValue("shortcuts", out var shortcutsObj) &&
                shortcutsObj is TomlTableArray shortcutsArray &&
                shortcutsArray.Count > 0)
            {
                if (TryMigrateTaskManagerShortcutToInstant(shortcutsArray))
                {
                    Directory.CreateDirectory(settingsFolder);
                    File.WriteAllText(settingsFilePath, Toml.FromModel(rootTable));
                }

                return;
            }

            var defaultShortcuts = new TomlTableArray();
            foreach (string function in SettingsShortcutFunctions)
            {
                ManagedShortcut shortcut = CreateDefaultManagedShortcut(function);
                var entry = new TomlTable
                {
                    ["function"] = shortcut.Function,
                    ["key1"] = shortcut.Key1,
                    ["key2"] = shortcut.Key2,
                    ["hold_duration"] = shortcut.HoldDuration,
                    ["enabled"] = shortcut.Enabled
                };

                defaultShortcuts.Add(entry);
            }

            rootTable["shortcuts"] = defaultShortcuts;
            Directory.CreateDirectory(settingsFolder);
            File.WriteAllText(settingsFilePath, Toml.FromModel(rootTable));
        }

        private void LoadManagedPreloadApps()
        {
            _managedPreloadApps.Clear();

            try
            {
                if (File.Exists(PreloadAppsJsonPath))
                {
                    string json = File.ReadAllText(PreloadAppsJsonPath);
                    var loaded = JsonSerializer.Deserialize<List<ManagedPreloadApp>>(json);
                    if (loaded != null)
                    {
                        _managedPreloadApps.AddRange(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to load preload apps: {ex.Message}");
            }

            if (_managedPreloadApps.Count == 0)
            {
                _selectedManagedPreloadIndex = 0;
            }
            else
            {
                _selectedManagedPreloadIndex = Math.Clamp(_selectedManagedPreloadIndex, 0, _managedPreloadApps.Count - 1);
            }
        }

        private void SaveManagedPreloadApps()
        {
            try
            {
                string? folder = Path.GetDirectoryName(PreloadAppsJsonPath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string json = JsonSerializer.Serialize(_managedPreloadApps, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PreloadAppsJsonPath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to save preload apps: {ex.Message}");
            }
        }

        private void LoadManagedShortcuts()
        {
            _managedShortcuts.Clear();

            string settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "gcmsettings",
                "settings.toml");

            var existing = new Dictionary<string, ManagedShortcut>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (File.Exists(settingsFilePath))
                {
                    var rootTable = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();
                    if (rootTable.TryGetValue("shortcuts", out var shortcutsObj) &&
                        shortcutsObj is TomlTableArray shortcutsArray)
                    {
                        foreach (TomlTable entry in shortcutsArray)
                        {
                            string function = entry["function"]?.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(function))
                            {
                                continue;
                            }

                            existing[function] = new ManagedShortcut
                            {
                                Function = function,
                                Key1 = entry["key1"]?.ToString() ?? "None",
                                Key2 = entry["key2"]?.ToString() ?? "None",
                                HoldDuration = entry.ContainsKey("hold_duration")
                                    ? Convert.ToDouble(entry["hold_duration"])
                                    : 0.0,
                                Enabled = entry.ContainsKey("enabled") && Convert.ToBoolean(entry["enabled"])
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to load shortcuts: {ex.Message}");
            }

            foreach (string function in SettingsShortcutFunctions)
            {
                if (existing.TryGetValue(function, out ManagedShortcut? entry))
                {
                    _managedShortcuts.Add(entry);
                }
                else
                {
                    _managedShortcuts.Add(CreateDefaultManagedShortcut(function));
                }
            }

            if (_managedShortcuts.Count == 0)
            {
                _selectedManagedShortcutIndex = 0;
            }
            else
            {
                _selectedManagedShortcutIndex = Math.Clamp(_selectedManagedShortcutIndex, 0, _managedShortcuts.Count - 1);
            }
        }

        private void SaveManagedShortcuts()
        {
            string settingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");

            TomlTable rootTable;
            try
            {
                rootTable = File.Exists(settingsFilePath)
                    ? Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel()
                    : new TomlTable();
            }
            catch
            {
                rootTable = new TomlTable();
            }

            var shortcutsArray = new TomlTableArray();
            foreach (ManagedShortcut shortcut in _managedShortcuts)
            {
                shortcutsArray.Add(new TomlTable
                {
                    ["function"] = shortcut.Function,
                    ["key1"] = shortcut.Key1,
                    ["key2"] = shortcut.Key2,
                    ["hold_duration"] = shortcut.HoldDuration,
                    ["enabled"] = shortcut.Enabled
                });
            }

            rootTable["shortcuts"] = shortcutsArray;
            Directory.CreateDirectory(settingsFolder);
            File.WriteAllText(settingsFilePath, Toml.FromModel(rootTable));
            LoadShortcutsFromSettings();
        }

        private void LoadAudioDeviceChoices()
        {
            _settingsAudioDevices.Clear();

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = enumerator
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .Where(device => !device.FriendlyName.Contains("Steam Streaming", StringComparison.OrdinalIgnoreCase))
                    .Select(device => device.FriendlyName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();

                _settingsAudioDevices.AddRange(devices);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to enumerate audio devices: {ex.Message}");
            }
        }

        private string GetDefaultAudioDeviceName()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).FriendlyName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void RefreshSettingsOverlayValues()
        {
            if (_settingsCategories.Count == 0 || _settingsRows.Count == 0)
            {
                return;
            }

            SetSettingsRowValue("steam-path", FormatSteamPathSummary());
            SetSettingsRowValue("steam-path-reset", "Auto-detect");
            RefreshSteamIntegrationSettingsValues();
            SetSettingsRowStatusValue("controller-dualsense", IsControllerTypeActive(ControllerType.PlayStation) ? "Active" : "Inactive", IsControllerTypeActive(ControllerType.PlayStation));
            SetSettingsRowStatusValue("controller-steam", IsControllerTypeActive(ControllerType.SteamController) ? "Active" : "Inactive", IsControllerTypeActive(ControllerType.SteamController));
            SetSettingsRowStatusValue("controller-xbox", IsControllerTypeActive(ControllerType.Xbox) ? "Active" : "Inactive", IsControllerTypeActive(ControllerType.Xbox));
            SetSettingsRowStatusValue("controller-shortcut-compat", IsSteamControllerInputModeActive() ? "Restricted" : "Full", !IsSteamControllerInputModeActive());
            SetSettingsRowStatusValue("controller-steam-tools", IsSteamControllerInputModeActive() ? "Ready" : "Standby", IsSteamControllerInputModeActive());
            SetSettingsRowStatusValue("controller-gcm-navigation", _hasObservedControllerInput ? "Ready" : "Waiting", _hasObservedControllerInput);
            SetSettingsRowValue("startup-apps", FormatEnabled(GetSetting("usewinpartstartapps", true)));
            SetSettingsRowValue("startup-video-enabled", FormatEnabled(GetSetting("usestartupvideo", false)));
            SetSettingsRowValue("steam-startup-video-enabled", FormatEnabled(GetSetting("usesteamstartupvideo", false)));
            SetSettingsRowValue("startup-video-path", ShortenForUi(GetSetting("startupvideo_path", string.Empty), "Not selected"));
            SetSettingsRowValue("startup-video-reset", "Clear file");

            SetSettingsRowValue("preload-enabled", $"{FormatEnabled(GetSetting("usepreloadlist", false))} | {_managedPreloadApps.Count} app(s)");
            SetSettingsRowValue("preload-selected", FormatSelectedPreloadSummary());
            SetSettingsRowValue("preload-add", "Choose file");
            SetSettingsRowValue("preload-hidden", FormatSelectedPreloadHiddenSummary());
            SetSettingsRowValue("preload-arguments", FormatSelectedPreloadArgumentsSummary());
            SetSettingsRowValue("preload-repath", "Change file");
            SetSettingsRowValue("preload-remove", _managedPreloadApps.Count == 0 ? "Nothing to remove" : "Remove app");

            SetSettingsRowValue("shortcut-popup", FormatEnabled(GetSetting("shortcutpopup", true)));
            SetSettingsRowValue("shortcut-selected", FormatSelectedShortcutSummary());
            SetSettingsRowValue("shortcut-enabled", FormatSelectedShortcutEnabledSummary());
            SetSettingsRowShortcutButtonValue("shortcut-key1", SelectedShortcutOrNull()?.Key1 ?? "None");
            SetSettingsRowShortcutButtonValue("shortcut-key2", SelectedShortcutOrNull()?.Key2 ?? "None");
            SetSettingsRowValue("shortcut-hold", FormatHoldDuration(SelectedShortcutOrNull()?.HoldDuration ?? 0.0));
            SetSettingsRowValue("shortcut-reset", "Reset binding");

            SetSettingsRowValue("theme-accent", GetSetting("theme_accent", DefaultThemeAccent));
            SetSettingsRowValue("theme-card-tint", GetSetting("theme_card_tint", DefaultThemeCardTint));
            SetSettingsRowValue("theme-glass-strength", GetSetting("theme_glass_strength", DefaultThemeGlassStrength));
            SetSettingsRowValue("theme-card-size", GetSetting("theme_card_size", DefaultThemeCardSize));
            SetSettingsRowValue("theme-dock-size", GetSetting("theme_dock_size", DefaultThemeDockSize));
            SetSettingsRowValue("theme-dock-position", GetSetting("theme_dock_position", DefaultThemeDockPosition));
            SetSettingsRowValue("theme-top-dock-size", GetSetting("theme_top_dock_size", DefaultThemeTopDockSize));
            SetSettingsRowValue("theme-top-dock-position", GetSetting("theme_top_dock_position", DefaultThemeTopDockPosition));
            SetSettingsRowValue("theme-reset", "Restore defaults");

            SetSettingsRowValue("preaudio-enabled", FormatEnabled(GetSetting("usepreaudio", false)));
            SetSettingsRowValue("preaudio-start", GetSetting("preaudiostart", string.Empty, false).NullIfEmpty("Not set"));
            SetSettingsRowValue("preaudio-end", GetSetting("preaudioend", string.Empty, false).NullIfEmpty("Not set"));
            SetSettingsRowValue("wallpaper-enabled", FormatEnabled(GetSetting("gcmwallpaper", false)));
            SetSettingsRowValue("wallpaper-path", ShortenForUi(GetSetting("gcmwallpaperpath", string.Empty), "Not selected"));

            SetSettingsRowValue("discord-card", FormatEnabled(GetSetting("show_discord", true)));
            SetSettingsRowValue("taskbar-enabled", FormatEnabled(GetSetting("enable_taskbar", false)));
            SetSettingsRowValue("steamgriddb-key", FormatApiKeySummary(GetSetting("steamgriddb_api_key", string.Empty, false)));
            SetSettingsRowValue("uac-enabled", FormatEnabled(GetSetting("uac", true)));
            SetSettingsRowValue("windows-settings", "Open");
            SetSettingsRowValue("gcm-service-refresh", "Run check");
            SetSettingsRowValue("gcm-service-log", BuildPrivilegedServiceLogSummary());
            SetSettingsRowStatusValue("gcm-service-status", BuildPrivilegedServiceStatusSummary(), _isPrivilegedServiceReady);

            string pendingServiceCheckText = _isPrivilegedServiceReady ? "Local mode active" : "Local mode unavailable";
            bool pendingServiceCheckPositive = _isPrivilegedServiceReady;

            GcmSubsystemHealth? shellSubsystem = FindPrivilegedSubsystem("Winlogon shell access");
            SetSettingsRowStatusValue(
                "gcm-service-shell",
                shellSubsystem == null ? pendingServiceCheckText : $"{shellSubsystem.Status} | {ShortenForUi(shellSubsystem.Details, shellSubsystem.Status)}",
                shellSubsystem?.IsReady ?? pendingServiceCheckPositive);

            GcmSubsystemHealth? uacSubsystem = FindPrivilegedSubsystem("UAC policy access");
            SetSettingsRowStatusValue(
                "gcm-service-uac",
                uacSubsystem == null ? pendingServiceCheckText : $"{uacSubsystem.Status} | {ShortenForUi(uacSubsystem.Details, uacSubsystem.Status)}",
                uacSubsystem?.IsReady ?? pendingServiceCheckPositive);

            GcmSubsystemHealth? keyboardSubsystem = FindPrivilegedSubsystem("Keyboard redirect access");
            SetSettingsRowStatusValue(
                "gcm-service-keyboard",
                keyboardSubsystem == null ? pendingServiceCheckText : $"{keyboardSubsystem.Status} | {ShortenForUi(keyboardSubsystem.Details, keyboardSubsystem.Status)}",
                keyboardSubsystem?.IsReady ?? pendingServiceCheckPositive);

            GcmSubsystemHealth? touchKeyboardSubsystem = FindPrivilegedSubsystem("Touch keyboard control");
            SetSettingsRowStatusValue(
                "gcm-service-touchkeyboard",
                touchKeyboardSubsystem == null ? pendingServiceCheckText : $"{touchKeyboardSubsystem.Status} | {ShortenForUi(touchKeyboardSubsystem.Details, touchKeyboardSubsystem.Status)}",
                touchKeyboardSubsystem?.IsReady ?? pendingServiceCheckPositive);

            SetSettingsCategorySummary("controller", FormatControllerModeSummary());
            SetSettingsCategorySummary("startup", GetSetting("usestartupvideo", false) ? "Video ready" : "Steam boot");
            SetSettingsCategorySummary("preload", _managedPreloadApps.Count == 0 ? "No apps" : $"{_managedPreloadApps.Count} app(s)");
            SetSettingsCategorySummary("shortcuts", IsSteamControllerInputModeActive() ? "Task Manager only" : $"{_managedShortcuts.Count(shortcut => shortcut.Enabled)} active");
            SetSettingsCategorySummary("theme", $"{GetSetting("theme_accent", DefaultThemeAccent)} | Cards {GetSetting("theme_card_size", DefaultThemeCardSize)} | Top {GetSetting("theme_top_dock_position", DefaultThemeTopDockPosition)}");
            SetSettingsCategorySummary("audio-visuals", $"{CountVisualSettingsEnabled()} active");
            SetSettingsCategorySummary("loader-ui", $"{BuildPrivilegedServiceStatusSummary()} | {CountLoaderUiSettingsEnabled()} active");

            UpdateSettingsContextPanel();
            UpdateSettingsFooter();
        }

        private void SetSettingsRowValue(string id, string text)
        {
            if (_settingsRowsById.TryGetValue(id, out SettingsRowDefinition? row))
            {
                row.ValueHost.Children.Clear();
                row.ValueHost.Children.Add(row.ValueText);
                row.ValueText.Text = text;
            }
        }

        private void SetSettingsRowStatusValue(string id, string text, bool isPositive)
        {
            if (!_settingsRowsById.TryGetValue(id, out SettingsRowDefinition? row))
            {
                return;
            }

            Color dotColor = isPositive
                ? Color.FromArgb(255, 95, 220, 122)
                : Color.FromArgb(255, 226, 81, 81);

            row.ValueHost.Children.Clear();
            row.ValueHost.Children.Add(new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(dotColor),
                BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            row.ValueHost.Children.Add(row.ValueText);
            row.ValueText.Text = text;
        }

        private void SetSettingsRowShortcutButtonValue(string id, string buttonName)
        {
            if (!_settingsRowsById.TryGetValue(id, out SettingsRowDefinition? row))
            {
                return;
            }

            row.ValueHost.Children.Clear();

            if (string.IsNullOrWhiteSpace(buttonName) || string.Equals(buttonName, "None", StringComparison.OrdinalIgnoreCase))
            {
                row.ValueText.Text = "None";
                row.ValueHost.Children.Add(row.ValueText);
                return;
            }

            row.ValueHost.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri($"ms-appx:///Assets/{GetControllerIconAssetPath(buttonName)}")),
                Width = 34,
                Height = 34,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        private void SetSettingsCategorySummary(string id, string text)
        {
            if (_settingsCategoriesById.TryGetValue(id, out SettingsCategoryDefinition? category))
            {
                category.SummaryText.Text = text;
            }
        }

        private void UpdateSettingsVisualFocus()
        {
            if (_settingsCategories.Count == 0)
            {
                return;
            }

            UpdateSettingsContextPanel();
            UpdateSettingsFooter();

            Brush accentBrush = (Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
            SettingsCategoryDefinition? selectedCategory = SelectedSettingsCategoryOrNull();
            List<SettingsRowDefinition> activeRows = GetActiveSettingsRows();

            for (int i = 0; i < _settingsCategories.Count; i++)
            {
                SettingsCategoryDefinition category = _settingsCategories[i];
                bool isSelectedCategory = i == _selectedSettingsCategoryIndex;
                bool isFocusedCategory = isSelectedCategory && _settingsPane == SettingsPane.Categories;

                category.Button.BorderBrush = isFocusedCategory
                    ? accentBrush
                    : isSelectedCategory
                        ? new SolidColorBrush(Color.FromArgb(112, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(38, 255, 255, 255));
                category.Button.BorderThickness = isFocusedCategory ? new Thickness(2) : new Thickness(1);
                category.Button.Background = isFocusedCategory
                    ? new SolidColorBrush(Color.FromArgb(62, 255, 255, 255))
                    : isSelectedCategory
                        ? new SolidColorBrush(Color.FromArgb(34, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));

                if (isFocusedCategory)
                {
                    category.Button.Focus(FocusState.Programmatic);
                    ScrollSettingsElementIntoView(category.Button, SettingsCategoryHost, SettingsCategoryScrollViewer, 60);
                }
            }

            foreach (SettingsRowDefinition row in _settingsRows)
            {
                bool isVisibleRow = selectedCategory != null && string.Equals(row.CategoryId, selectedCategory.Id, StringComparison.OrdinalIgnoreCase);
                int rowIndex = isVisibleRow ? activeRows.FindIndex(candidate => string.Equals(candidate.Id, row.Id, StringComparison.OrdinalIgnoreCase)) : -1;
                bool isSelectedRow = isVisibleRow && rowIndex == _selectedSettingsRowIndex;
                bool isFocusedRow = isSelectedRow && _settingsPane == SettingsPane.Rows;

                row.Button.BorderBrush = isFocusedRow
                    ? accentBrush
                    : isSelectedRow
                        ? new SolidColorBrush(Color.FromArgb(112, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
                row.Button.BorderThickness = isFocusedRow ? new Thickness(2) : new Thickness(1);
                row.Button.Background = isFocusedRow
                    ? new SolidColorBrush(Color.FromArgb(62, 255, 255, 255))
                    : isSelectedRow
                        ? new SolidColorBrush(Color.FromArgb(34, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));

                if (isFocusedRow)
                {
                    row.Button.Focus(FocusState.Programmatic);
                    ScrollSettingsElementIntoView(row.Button, SettingsRowsHost, SettingsScrollViewer, 90);
                }
            }
        }

        private void ScrollSettingsElementIntoView(FrameworkElement element, FrameworkElement host, ScrollViewer viewer, double padding)
        {
            try
            {
                var transform = element.TransformToVisual(host);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                double targetOffset = position.Y - padding;
                viewer.ChangeView(null, Math.Max(0, targetOffset), null, false);
            }
            catch
            {
                // Ignore layout timing issues.
            }
        }

        private void OpenSettingsOverlay()
        {
            EnsureSettingsOverlayInitialized();

            SettingsOverlay.Visibility = Visibility.Visible;
            _currentFocusArea = FocusArea.SettingsMenu;
            UpdateVisualFocus();
        }

        private void ToggleSettingsOverlay()
        {
            if (SettingsOverlay.Visibility == Visibility.Visible)
            {
                CloseSettingsOverlay();
            }
            else
            {
                OpenSettingsOverlay();
            }
        }

        private void CloseSettingsOverlay()
        {
            CancelShortcutButtonCapture();
            SettingsOverlay.Visibility = Visibility.Collapsed;
            _currentFocusArea = FocusArea.TopButtons;
            _selectedTopButtonIndex = _topButtons.IndexOf(SettingsButton);
            UpdateVisualFocus();
        }

        private void SettingsOverlay_BackdropTapped(object sender, TappedRoutedEventArgs e)
        {
            CloseSettingsOverlay();
        }

        private void SettingsOverlayContent_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void MoveSettingsSelection(int direction)
        {
            if (_settingsPane == SettingsPane.Categories)
            {
                if (_settingsCategories.Count == 0)
                {
                    return;
                }

                _selectedSettingsCategoryIndex = (_selectedSettingsCategoryIndex + direction + _settingsCategories.Count) % _settingsCategories.Count;
                RebuildVisibleSettingsRows();
                RefreshSettingsOverlayValues();
                UpdateVisualFocus();
                return;
            }

            List<SettingsRowDefinition> activeRows = GetActiveSettingsRows();
            if (activeRows.Count == 0)
            {
                return;
            }

            _selectedSettingsRowIndex = (_selectedSettingsRowIndex + direction + activeRows.Count) % activeRows.Count;
            UpdateVisualFocus();
        }

        private void ActivateSelectedSettingsRow()
        {
            if (_settingsPane == SettingsPane.Categories)
            {
                FocusSettingsRows();
                return;
            }

            SelectedSettingsRowOrNull()?.OnActivate?.Invoke();
        }

        private bool AdjustSelectedSettingsRow(int direction)
        {
            if (_settingsPane != SettingsPane.Rows)
            {
                return false;
            }

            SettingsRowDefinition? row = SelectedSettingsRowOrNull();
            if (row == null || row.OnAdjust == null)
            {
                return false;
            }

            row.OnAdjust(direction);
            return true;
        }

        private void FocusSettingsRows()
        {
            if (GetActiveSettingsRows().Count == 0)
            {
                return;
            }

            _settingsPane = SettingsPane.Rows;
            _selectedSettingsRowIndex = Math.Clamp(_selectedSettingsRowIndex, 0, GetActiveSettingsRows().Count - 1);
            UpdateVisualFocus();
        }

        private bool FocusSettingsCategories()
        {
            if (_settingsPane == SettingsPane.Categories)
            {
                return false;
            }

            _settingsPane = SettingsPane.Categories;
            UpdateVisualFocus();
            return true;
        }

        private bool AdvanceSettingsSelection()
        {
            if (_settingsPane == SettingsPane.Categories)
            {
                FocusSettingsRows();
                return true;
            }

            return AdjustSelectedSettingsRow(1);
        }

        private bool RewindSettingsSelection()
        {
            if (_settingsPane != SettingsPane.Rows)
            {
                return false;
            }

            return AdjustSelectedSettingsRow(-1);
        }

        private void BeginShortcutButtonCapture(bool primary)
        {
            if (SelectedShortcutOrNull() == null)
            {
                return;
            }

            _isCapturingShortcutButton = true;
            _capturePrimaryShortcutButton = primary;
            UpdateSettingsContextPanel();
            UpdateSettingsFooter();
            UpdateVisualFocus();
        }

        private void CancelShortcutButtonCapture()
        {
            if (!_isCapturingShortcutButton)
            {
                return;
            }

            _isCapturingShortcutButton = false;
            UpdateSettingsContextPanel();
            UpdateSettingsFooter();
        }

        private bool TryHandleSettingsCaptureInput(
            GamepadButtonFlags newPresses,
            bool stickMovedLeft,
            bool stickMovedRight,
            bool stickMovedUp,
            bool stickMovedDown)
        {
            if (!_isCapturingShortcutButton)
            {
                return false;
            }

            if (stickMovedLeft || stickMovedRight || stickMovedUp || stickMovedDown)
            {
                CancelShortcutButtonCapture();
                return true;
            }

            string? mappedButton = TryMapGamepadPressToShortcutButton(newPresses);
            if (mappedButton == null)
            {
                return false;
            }

            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            if (shortcut == null)
            {
                CancelShortcutButtonCapture();
                return true;
            }

            if (_capturePrimaryShortcutButton)
            {
                shortcut.Key1 = mappedButton;
            }
            else
            {
                shortcut.Key2 = mappedButton;
            }

            SaveManagedShortcuts();
            _isCapturingShortcutButton = false;
            RefreshSettingsOverlayValues();
            UpdateVisualFocus();
            return true;
        }

        private async Task SelectSteamPathAsync()
        {
            string startDirectory = Path.GetDirectoryName(ResolveSteamExecutablePath()) ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string selectedFile = NativeOpenFileDialog.Show(
                "Select steam.exe",
                startDirectory,
                "Steam executable (*.exe)|*.exe|All files (*.*)|*.*");

            if (string.IsNullOrWhiteSpace(selectedFile))
            {
                return;
            }

            if (!File.Exists(selectedFile) ||
                !string.Equals(Path.GetFileName(selectedFile), "steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                await messagebox("Please choose the real steam.exe file.");
                return;
            }

            AppSettings.Save("steamlauncherpath", selectedFile);
            RefreshSettingsOverlayValues();
        }

        private void ResetSteamPath()
        {
            AppSettings.Save("steamlauncherpath", string.Empty);
            RefreshSettingsOverlayValues();
        }

        private void ToggleSteamStartupInjection()
        {
            bool enabled = !GetSetting("usesteamstartupvideo", false);
            AppSettings.Save("usesteamstartupvideo", enabled);

            if (!enabled)
            {
                RenameSteamStartupVideo_End();
            }

            RefreshSettingsOverlayValues();
        }

        private async Task SelectStartupVideoAsync()
        {
            string selectedFile = NativeOpenFileDialog.Show(
                "Select startup video",
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "Video files (*.webm;*.mp4;*.mkv;*.mov)|*.webm;*.mp4;*.mkv;*.mov|All files (*.*)|*.*");

            if (string.IsNullOrWhiteSpace(selectedFile) || !File.Exists(selectedFile))
            {
                return;
            }

            AppSettings.Save("startupvideo_path", selectedFile);
            AppSettings.Save("usestartupvideo", true);
            RefreshSettingsOverlayValues();
            await Task.CompletedTask;
        }

        private void ResetStartupVideoPath()
        {
            AppSettings.Save("startupvideo_path", string.Empty);
            AppSettings.Save("usestartupvideo", false);
            RenameSteamStartupVideo_End();
            RefreshSettingsOverlayValues();
        }

        private void CycleSelectedPreloadApp(int direction)
        {
            if (_managedPreloadApps.Count == 0)
            {
                RefreshSettingsOverlayValues();
                return;
            }

            _selectedManagedPreloadIndex = (_selectedManagedPreloadIndex + direction + _managedPreloadApps.Count) % _managedPreloadApps.Count;
            RefreshSettingsOverlayValues();
        }

        private async Task AddPreloadAppAsync()
        {
            string selectedFile = NativeOpenFileDialog.Show(
                "Select preload app",
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Apps and shortcuts (*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url)|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|All files (*.*)|*.*");

            if (string.IsNullOrWhiteSpace(selectedFile) || !File.Exists(selectedFile))
            {
                return;
            }

            _managedPreloadApps.Add(new ManagedPreloadApp
            {
                Name = Path.GetFileNameWithoutExtension(selectedFile),
                Path = selectedFile,
                Arguments = string.Empty,
                StartHidden = false
            });

            _selectedManagedPreloadIndex = _managedPreloadApps.Count - 1;
            SaveManagedPreloadApps();
            RefreshSettingsOverlayValues();
            await Task.CompletedTask;
        }

        private void ToggleSelectedPreloadHidden()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                return;
            }

            app.StartHidden = !app.StartHidden;
            SaveManagedPreloadApps();
            RefreshSettingsOverlayValues();
        }

        private async Task EditSelectedPreloadArgumentsAsync()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                await messagebox("Add a preload app first.");
                return;
            }

            string? value = await ShowTextInputDialogAsync(
                "Launch Arguments",
                "Edit the command line arguments for the selected preload app.",
                app.Arguments,
                "-silent -minimized");

            if (value == null)
            {
                return;
            }

            app.Arguments = value.Trim();
            SaveManagedPreloadApps();
            RefreshSettingsOverlayValues();
        }

        private async Task RepathSelectedPreloadAppAsync()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                await messagebox("Add a preload app first.");
                return;
            }

            string startDirectory = Path.GetDirectoryName(app.Path) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string selectedFile = NativeOpenFileDialog.Show(
                "Replace preload app path",
                startDirectory,
                "Apps and shortcuts (*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url)|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|All files (*.*)|*.*");

            if (string.IsNullOrWhiteSpace(selectedFile) || !File.Exists(selectedFile))
            {
                return;
            }

            app.Path = selectedFile;
            app.Name = Path.GetFileNameWithoutExtension(selectedFile);
            SaveManagedPreloadApps();
            RefreshSettingsOverlayValues();
        }

        private async Task RemoveSelectedPreloadAppAsync()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                await messagebox("There is no preload app to remove.");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Remove preload app",
                Content = $"Remove '{app.Name}' from the preload list?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            _managedPreloadApps.RemoveAt(_selectedManagedPreloadIndex);
            if (_managedPreloadApps.Count == 0)
            {
                _selectedManagedPreloadIndex = 0;
            }
            else
            {
                _selectedManagedPreloadIndex = Math.Clamp(_selectedManagedPreloadIndex, 0, _managedPreloadApps.Count - 1);
            }

            SaveManagedPreloadApps();
            RefreshSettingsOverlayValues();
        }

        private void CycleSelectedShortcut(int direction)
        {
            if (IsSteamControllerInputModeActive())
            {
                RefreshSettingsOverlayValues();
                return;
            }

            if (_managedShortcuts.Count == 0)
            {
                return;
            }

            _selectedManagedShortcutIndex = (_selectedManagedShortcutIndex + direction + _managedShortcuts.Count) % _managedShortcuts.Count;
            RefreshSettingsOverlayValues();
        }

        private void ToggleSelectedShortcutEnabled()
        {
            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            if (shortcut == null)
            {
                return;
            }

            shortcut.Enabled = !shortcut.Enabled;
            SaveManagedShortcuts();
            RefreshSettingsOverlayValues();
        }

        private void CycleShortcutButton(bool primary, int direction)
        {
            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            if (shortcut == null)
            {
                return;
            }

            string currentValue = primary ? shortcut.Key1 : shortcut.Key2;
            int currentIndex = Array.FindIndex(SettingsShortcutButtons, button => string.Equals(button, currentValue, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int newIndex = (currentIndex + direction + SettingsShortcutButtons.Length) % SettingsShortcutButtons.Length;
            if (primary)
            {
                shortcut.Key1 = SettingsShortcutButtons[newIndex];
            }
            else
            {
                shortcut.Key2 = SettingsShortcutButtons[newIndex];
            }

            SaveManagedShortcuts();
            RefreshSettingsOverlayValues();
        }

        private void CycleShortcutHoldDuration(int direction)
        {
            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            if (shortcut == null)
            {
                return;
            }

            int currentIndex = Array.FindIndex(
                SettingsShortcutHoldDurations,
                value => Math.Abs(value - shortcut.HoldDuration) < 0.001);

            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int newIndex = (currentIndex + direction + SettingsShortcutHoldDurations.Length) % SettingsShortcutHoldDurations.Length;
            shortcut.HoldDuration = SettingsShortcutHoldDurations[newIndex];
            SaveManagedShortcuts();
            RefreshSettingsOverlayValues();
        }

        private void ResetSelectedShortcut()
        {
            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            if (shortcut == null)
            {
                return;
            }

            ManagedShortcut defaults = CreateDefaultManagedShortcut(shortcut.Function);
            shortcut.Key1 = defaults.Key1;
            shortcut.Key2 = defaults.Key2;
            shortcut.HoldDuration = defaults.HoldDuration;
            shortcut.Enabled = defaults.Enabled;
            SaveManagedShortcuts();
            RefreshSettingsOverlayValues();
        }

        private void CycleThemePreset(string key, IReadOnlyList<string> presets, int direction, bool rebuildCards)
        {
            if (presets == null || presets.Count == 0)
            {
                return;
            }

            string current = GetSetting(key, presets[0], false);
            int currentIndex = presets
                .Select((value, index) => new { value, index })
                .FirstOrDefault(item => string.Equals(item.value, current, StringComparison.OrdinalIgnoreCase))
                ?.index ?? 0;

            int newIndex = (currentIndex + direction + presets.Count) % presets.Count;
            AppSettings.Save(key, presets[newIndex]);
            ApplyResponsiveShellSizing(rebuildCards);
            RefreshSettingsOverlayValues();
            UpdateVisualFocus();
        }

        private void ResetThemeSettings()
        {
            AppSettings.Save("theme_accent", DefaultThemeAccent);
            AppSettings.Save("theme_card_tint", DefaultThemeCardTint);
            AppSettings.Save("theme_glass_strength", DefaultThemeGlassStrength);
            AppSettings.Save("theme_card_size", DefaultThemeCardSize);
            AppSettings.Save("theme_dock_size", DefaultThemeDockSize);
            AppSettings.Save("theme_dock_position", DefaultThemeDockPosition);
            AppSettings.Save("theme_top_dock_size", DefaultThemeTopDockSize);
            AppSettings.Save("theme_top_dock_position", DefaultThemeTopDockPosition);
            AppSettings.Save("theme_defaults_version", ThemeDefaultsVersion);
            ApplyResponsiveShellSizing(rebuildCards: true);
            RefreshSettingsOverlayValues();
            UpdateVisualFocus();
        }

        private void CycleAudioDeviceSetting(string key, int direction)
        {
            if (_settingsAudioDevices.Count == 0)
            {
                LoadAudioDeviceChoices();
                if (_settingsAudioDevices.Count == 0)
                {
                    RefreshSettingsOverlayValues();
                    return;
                }
            }

            string current = GetSetting(key, string.Empty, false);
            int currentIndex = _settingsAudioDevices.FindIndex(name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int newIndex = (currentIndex + direction + _settingsAudioDevices.Count) % _settingsAudioDevices.Count;
            AppSettings.Save(key, _settingsAudioDevices[newIndex]);
            RefreshSettingsOverlayValues();
        }

        private void ToggleWallpaperSetting()
        {
            bool enabled = !GetSetting("gcmwallpaper", false);
            AppSettings.Save("gcmwallpaper", enabled);

            if (!enabled)
            {
                AppSettings.Save("gcmwallpaperpath", string.Empty);
            }

            RefreshSettingsOverlayValues();
        }

        private async Task SelectWallpaperAsync()
        {
            string selectedFile = NativeOpenFileDialog.Show(
                "Select wallpaper",
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Images (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All files (*.*)|*.*");

            if (string.IsNullOrWhiteSpace(selectedFile) || !File.Exists(selectedFile))
            {
                return;
            }

            AppSettings.Save("gcmwallpaperpath", selectedFile);
            AppSettings.Save("gcmwallpaper", true);
            RefreshSettingsOverlayValues();
            await Task.CompletedTask;
        }

        private void ToggleDiscordCardSetting()
        {
            bool enabled = !GetSetting("show_discord", true);
            AppSettings.Save("show_discord", enabled);
            LoadDynamicLauncherCards();
            _selectedLauncherAreaIndex = Math.Clamp(_selectedLauncherAreaIndex, 0, Math.Max(0, _launcherAreaButtons.Count - 1));
            RefreshSettingsOverlayValues();
        }

        private void ToggleTaskbarSetting()
        {
            bool enabled = !GetSetting("enable_taskbar", false);
            AppSettings.Save("enable_taskbar", enabled);

            if (enabled)
            {
                TaskbarVisibility.ShowTaskbar();
            }
            else
            {
                TaskbarVisibility.HideTaskbar();
            }

            RefreshSettingsOverlayValues();
        }

        private async Task EditSteamGridApiKeyAsync()
        {
            string? value = await ShowTextInputDialogAsync(
                "SteamGridDB API Key",
                "Paste the API key used for artwork lookups in the loader.",
                GetSetting("steamgriddb_api_key", string.Empty, false),
                "Paste API key");

            if (value == null)
            {
                return;
            }

            string trimmed = value.Trim();
            AppSettings.Save("steamgriddb_api_key", trimmed);

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _steamGridHelper = new SteamGridDBHelper(null);
            }
            else
            {
                _steamGridHelper = new SteamGridDBHelper(trimmed);
                Directory.CreateDirectory(_imageCachePath);
            }

            RefreshSettingsOverlayValues();
        }

        private void OpenWindowsSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings] Failed to open Windows Settings: {ex.Message}");
            }
        }

        private async Task<string?> ShowTextInputDialogAsync(
            string title,
            string description,
            string currentValue,
            string placeholder)
        {
            var textBox = new TextBox
            {
                Text = currentValue ?? string.Empty,
                PlaceholderText = placeholder,
                AcceptsReturn = false
            };

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(textBox);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? textBox.Text : null;
        }

        private void ToggleBooleanSetting(string key)
        {
            bool currentValue = GetSetting(key, false);
            AppSettings.Save(key, !currentValue);
            RefreshSettingsOverlayValues();
        }

        private ManagedPreloadApp? SelectedPreloadAppOrNull()
        {
            if (_managedPreloadApps.Count == 0 || _selectedManagedPreloadIndex < 0 || _selectedManagedPreloadIndex >= _managedPreloadApps.Count)
            {
                return null;
            }

            return _managedPreloadApps[_selectedManagedPreloadIndex];
        }

        private ManagedShortcut? SelectedShortcutOrNull()
        {
            if (IsSteamControllerInputModeActive())
            {
                return _managedShortcuts.FirstOrDefault(shortcut =>
                    string.Equals(shortcut.Function, "taskmanager", StringComparison.OrdinalIgnoreCase));
            }

            if (_managedShortcuts.Count == 0 || _selectedManagedShortcutIndex < 0 || _selectedManagedShortcutIndex >= _managedShortcuts.Count)
            {
                return null;
            }

            return _managedShortcuts[_selectedManagedShortcutIndex];
        }

        private int CountVisualSettingsEnabled()
        {
            int count = 0;
            if (GetSetting("usepreaudio", false)) count++;
            if (GetSetting("gcmwallpaper", false)) count++;
            return count;
        }

        private int CountLoaderUiSettingsEnabled()
        {
            int count = 0;
            if (GetSetting("show_discord", true)) count++;
            if (GetSetting("enable_taskbar", false)) count++;
            if (GetSetting("uac", true)) count++;
            if (!string.IsNullOrWhiteSpace(GetSetting("steamgriddb_api_key", string.Empty, false))) count++;
            return count;
        }

        private void UpdateSettingsContextPanel()
        {
            SettingsCategoryDefinition? category = SelectedSettingsCategoryOrNull();
            if (category == null)
            {
                return;
            }

            SettingsSectionEyebrow.Text = _isCapturingShortcutButton
                ? "Listening"
                : _settingsPane == SettingsPane.Categories ? "Category" : "Setting";
            SettingsSectionTitle.Text = category.Title;
            SettingsSectionSubtitle.Text = category.Subtitle;
            SettingsContextGlyph.Glyph = category.Glyph;

            if (_isCapturingShortcutButton)
            {
                SettingsContextTitle.Text = _capturePrimaryShortcutButton
                    ? "Press the primary button now"
                    : "Press the secondary button now";
                SettingsContextText.Text = "The next gamepad button press will be saved. Move the stick to cancel listening.";
                return;
            }

            if (string.Equals(category.Id, "shortcuts", StringComparison.OrdinalIgnoreCase))
            {
                ManagedShortcut? shortcut = SelectedShortcutOrNull();
                if (shortcut == null)
                {
                    SettingsContextTitle.Text = "No shortcut selected";
                    SettingsContextText.Text = "Choose a shortcut function first, then configure its buttons and hold time.";
                    return;
                }

                if (IsSteamControllerInputModeActive())
                {
                    SettingsContextTitle.Text = "Steam Controller Mode";
                    SettingsContextText.Text = "Only the Task Manager shortcut is configurable while the Steam Controller is the active navigation device. Full shortcut support returns as soon as you navigate with Xbox or DualSense again.";
                    return;
                }

                SettingsContextTitle.Text = FormatShortcutFunctionDisplayName(shortcut.Function);
                SettingsContextText.Text = $"Current combo: {FormatShortcutCombo(shortcut)} | Hold: {FormatHoldDuration(shortcut.HoldDuration)} | {FormatEnabled(shortcut.Enabled)}. Press A on primary or secondary to capture a real controller button.";
                return;
            }

            if (string.Equals(category.Id, "preload", StringComparison.OrdinalIgnoreCase))
            {
                ManagedPreloadApp? app = SelectedPreloadAppOrNull();
                if (app == null)
                {
                    SettingsContextTitle.Text = "No preload app selected";
                    SettingsContextText.Text = "Add helper apps that should start before Steam. You can change their path, arguments and hidden state here.";
                    return;
                }

                SettingsContextTitle.Text = app.Name;
                SettingsContextText.Text = $"{ShortenForUi(app.Path, "No file selected")} | {FormatSelectedPreloadHiddenSummary()} | {FormatSelectedPreloadArgumentsSummary()}";
                return;
            }

            SettingsRowDefinition? row = SelectedSettingsRowOrNull();
            if (row != null && _settingsPane == SettingsPane.Rows)
            {
                SettingsContextTitle.Text = row.Title;
                SettingsContextText.Text = $"{row.Description} Current: {row.ValueText.Text}";
                return;
            }

            SettingsContextTitle.Text = category.Title;
            SettingsContextText.Text = category.Subtitle;
        }

        private void UpdateSettingsFooter()
        {
            if (_isCapturingShortcutButton)
            {
                SettingsFooterText.Text = "Listening for a gamepad button. Press any button to save, or move the stick to cancel.";
                return;
            }

            SettingsFooterText.Text = _settingsPane == SettingsPane.Categories
                ? "Up/Down: browse categories. Right or A: open category. B: close."
                : IsSteamControllerInputModeActive() && string.Equals(SelectedSettingsCategoryOrNull()?.Id, "shortcuts", StringComparison.OrdinalIgnoreCase)
                    ? "Steam Controller mode: only the Task Manager shortcut is available here. Left: categories."
                    : "Up/Down: move. Right: next value. X: previous value. A: apply. Left: categories.";
        }

        private string? TryMapGamepadPressToShortcutButton(GamepadButtonFlags newPresses)
        {
            if (newPresses == GamepadButtonFlags.None)
            {
                return null;
            }

            foreach (string buttonName in SettingsShortcutButtons)
            {
                if (string.Equals(buttonName, "None", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_buttonMap.TryGetValue(buttonName, out GamepadButtonFlags flag) && (newPresses & flag) != 0)
                {
                    return buttonName;
                }
            }

            return null;
        }

        private string FormatSelectedPreloadSummary()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                return "No app selected";
            }

            return $"{_selectedManagedPreloadIndex + 1}/{_managedPreloadApps.Count} | {app.Name}";
        }

        private string FormatSelectedPreloadHiddenSummary()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                return "No app selected";
            }

            return app.StartHidden ? "Starts hidden" : "Starts visible";
        }

        private string FormatSelectedPreloadArgumentsSummary()
        {
            ManagedPreloadApp? app = SelectedPreloadAppOrNull();
            if (app == null)
            {
                return "No app selected";
            }

            return string.IsNullOrWhiteSpace(app.Arguments)
                ? "No launch args"
                : ShortenForUi(app.Arguments, "No launch args");
        }

        private string FormatSelectedShortcutSummary()
        {
            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            if (shortcut == null)
            {
                return "No shortcut selected";
            }

            if (IsSteamControllerInputModeActive())
            {
                return "Task Manager only";
            }

            return $"{_selectedManagedShortcutIndex + 1}/{_managedShortcuts.Count} | {FormatShortcutFunctionDisplayName(shortcut.Function)}";
        }

        private string FormatSelectedShortcutEnabledSummary()
        {
            ManagedShortcut? shortcut = SelectedShortcutOrNull();
            return shortcut == null ? "Not available" : FormatEnabled(shortcut.Enabled);
        }

        private string FormatSteamPathSummary()
        {
            string savedPath = GetSetting("steamlauncherpath", string.Empty, false);
            if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
            {
                return $"Manual | {ShortenForUi(savedPath, "Not found")}";
            }

            string detectedPath = AutoDetectLauncherPath("steam");
            return string.IsNullOrWhiteSpace(detectedPath)
                ? "Auto | Not found"
                : $"Auto | {ShortenForUi(detectedPath, "Not found")}";
        }

        private string ResolveSteamExecutablePath()
        {
            string savedPath = GetSetting("steamlauncherpath", string.Empty, false);
            if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
            {
                return savedPath;
            }

            string detectedPath = AutoDetectLauncherPath("steam");
            if (!string.IsNullOrWhiteSpace(detectedPath) && File.Exists(detectedPath))
            {
                return detectedPath;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe");
        }

        private static string FormatShortcutFunctionDisplayName(string function)
        {
            return function.Trim().ToLowerInvariant() switch
            {
                "taskmanager" => "Task Manager",
                "shortcut overlay" => "Shortcut Overlay",
                "kill process" => "Kill Process",
                "switch tab" => "Switch Tab",
                "audio switch" => "Audio Switch",
                "volume up" => "Volume Up",
                "volume down" => "Volume Down",
                "performance overlay" => "Performance Overlay",
                "xbox bar" => "Xbox Bar",
                "xbox keyboard" => "Xbox Keyboard",
                _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(function)
            };
        }

        private static string FormatShortcutCombo(ManagedShortcut shortcut)
        {
            bool hasPrimary = !string.Equals(shortcut.Key1, "None", StringComparison.OrdinalIgnoreCase);
            bool hasSecondary = !string.Equals(shortcut.Key2, "None", StringComparison.OrdinalIgnoreCase);

            if (!hasPrimary && !hasSecondary)
            {
                return "Not assigned";
            }

            if (!hasSecondary)
            {
                return shortcut.Key1;
            }

            if (!hasPrimary)
            {
                return shortcut.Key2;
            }

            return $"{shortcut.Key1} + {shortcut.Key2}";
        }

        private static string FormatEnabled(bool enabled) => enabled ? "Enabled" : "Disabled";

        private static string ShortenForUi(string? text, string fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            return text.Length <= 56
                ? text
                : text[..24] + " ... " + text[^24..];
        }

        private static string FormatApiKeySummary(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "Missing";
            }

            if (key.Length <= 8)
            {
                return "Configured";
            }

            return $"Configured | {key[..4]}...{key[^4..]}";
        }

        private static string FormatHoldDuration(double holdDuration)
        {
            return holdDuration <= 0.0
                ? "Instant"
                : $"{holdDuration:0.##}s";
        }

        private string GetThemeAccentName()
        {
            return GetSetting("theme_accent", DefaultThemeAccent, false);
        }

        private string GetThemeCardTintName()
        {
            return GetSetting("theme_card_tint", DefaultThemeCardTint, false);
        }

        private string GetThemeGlassStrengthName()
        {
            return GetSetting("theme_glass_strength", DefaultThemeGlassStrength, false);
        }

        private Color GetThemeAccentColor(byte alpha)
        {
            return GetThemeAccentName() switch
            {
                "Graphite" => Color.FromArgb(alpha, 176, 184, 196),
                "Aurora Green" => Color.FromArgb(alpha, 116, 230, 152),
                "Warm Amber" => Color.FromArgb(alpha, 255, 189, 94),
                "Crimson" => Color.FromArgb(alpha, 255, 94, 112),
                "Violet" => Color.FromArgb(alpha, 175, 133, 255),
                _ => Color.FromArgb(alpha, 108, 188, 255)
            };
        }

        private Color GetThemeCardTintColor(byte alpha)
        {
            return GetThemeCardTintName() switch
            {
                "Deep Navy" => Color.FromArgb(alpha, 20, 50, 96),
                "Graphite" => Color.FromArgb(alpha, 72, 76, 84),
                "Forest" => Color.FromArgb(alpha, 32, 76, 52),
                "Amber Smoke" => Color.FromArgb(alpha, 102, 72, 35),
                _ => Color.FromArgb(alpha, 243, 248, 255)
            };
        }

        private byte GetThemeGlassAlpha(byte clear, byte balanced, byte solid)
        {
            return GetThemeGlassStrengthName() switch
            {
                "Clear" => clear,
                "Solid" => solid,
                _ => balanced
            };
        }

        private double GetThemeCardScaleMultiplier()
        {
            return GetSetting("theme_card_size", DefaultThemeCardSize, false) switch
            {
                "Compact" => 0.9,
                "Large" => 1.12,
                _ => 1.0
            };
        }

        private double GetThemeDockScaleMultiplier()
        {
            return GetSetting("theme_dock_size", DefaultThemeDockSize, false) switch
            {
                "Compact" => 0.9,
                "Large" => 1.14,
                _ => 1.0
            };
        }

        private double GetThemeTopDockScaleMultiplier()
        {
            return GetSetting("theme_top_dock_size", DefaultThemeTopDockSize, false) switch
            {
                "Compact" => 0.9,
                "Large" => 1.18,
                _ => 1.0
            };
        }

        private bool IsThemeDockTop()
        {
            return string.Equals(GetSetting("theme_dock_position", DefaultThemeDockPosition, false), "Top", StringComparison.OrdinalIgnoreCase);
        }

        private HorizontalAlignment GetThemeTopDockHorizontalAlignment()
        {
            return GetSetting("theme_top_dock_position", DefaultThemeTopDockPosition, false) switch
            {
                "Left" => HorizontalAlignment.Left,
                "Right" => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center
            };
        }

        private Thickness GetThemeTopDockMargin(double layoutScale)
        {
            return GetThemeTopDockHorizontalAlignment() switch
            {
                HorizontalAlignment.Left => new Thickness(24 * layoutScale, 18 * layoutScale, 0, 0),
                HorizontalAlignment.Right => new Thickness(0, 18 * layoutScale, 24 * layoutScale, 0),
                _ => new Thickness(20 * layoutScale, 18 * layoutScale, 20 * layoutScale, 0)
            };
        }

        private void ApplyThemeToTopDock(double layoutScale, double topDockScale)
        {
            if (infopanelright == null)
            {
                return;
            }

            infopanelright.HorizontalAlignment = GetThemeTopDockHorizontalAlignment();
            infopanelright.VerticalAlignment = VerticalAlignment.Top;
            infopanelright.Margin = GetThemeTopDockMargin(layoutScale);
            infopanelright.Padding = new Thickness(13 * topDockScale, 8 * topDockScale, 13 * topDockScale, 8 * topDockScale);
            infopanelright.CornerRadius = new CornerRadius(18 * topDockScale);
            infopanelright.Background = new SolidColorBrush(GetThemeAccentColor(GetThemeGlassAlpha(74, 106, 138)));
            infopanelright.BorderBrush = new SolidColorBrush(GetThemeAccentColor(122));
            infopanelright.BorderThickness = new Thickness(1.0 * topDockScale, 0, 1.0 * topDockScale, 1.15 * topDockScale);
            EnsureGlassRailReflection(infopanelright, topDockScale, true);

            StackPanel host = infopanelright.Child as StackPanel;
            if (host == null && infopanelright.Child is Grid railHost)
            {
                host = railHost.Children.OfType<StackPanel>().FirstOrDefault();
            }

            if (host != null)
            {
                host.Spacing = 18 * topDockScale;
            }
        }

        private void ApplyThemeToBottomLegend(double layoutScale, double dockScale)
        {
            if (BottomLegendBar == null)
            {
                return;
            }

            Grid.SetRow(BottomLegendBar, IsThemeDockTop() ? 0 : 2);
            BottomLegendBar.VerticalAlignment = IsThemeDockTop() ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            BottomLegendBar.Margin = IsThemeDockTop()
                ? new Thickness(0)
                : new Thickness(0);
            BottomLegendBar.Padding = new Thickness(28 * dockScale, 10 * dockScale, 28 * dockScale, 10 * dockScale);
            BottomLegendBar.CornerRadius = new CornerRadius(0);
            BottomLegendBar.Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(178, 206, 232)));
            BottomLegendBar.BorderBrush = new SolidColorBrush(GetThemeAccentColor(92));
            BottomLegendBar.BorderThickness = IsThemeDockTop()
                ? new Thickness(0, 0, 0, 1 * dockScale)
                : new Thickness(0, 1 * dockScale, 0, 0);
            EnsureGlassRailReflection(BottomLegendBar, dockScale, false);

            if (BottomStatusText != null)
            {
                BottomStatusText.FontSize = 14 * dockScale;
                BottomStatusText.Margin = new Thickness(0);
            }

            if (BottomStatusPopup != null)
            {
                BottomStatusPopup.MaxWidth = 760 * dockScale;
                BottomStatusPopup.Padding = new Thickness(18 * dockScale, 10 * dockScale, 18 * dockScale, 10 * dockScale);
                BottomStatusPopup.CornerRadius = new CornerRadius(16 * dockScale);
                BottomStatusPopup.Background = new SolidColorBrush(GetThemeAccentColor(GetThemeGlassAlpha(92, 126, 156)));
                BottomStatusPopup.BorderBrush = new SolidColorBrush(GetThemeAccentColor(118));
                BottomStatusPopup.BorderThickness = new Thickness(1 * dockScale);
            }

            if (BottomLegendItemsHost != null)
            {
                BottomLegendItemsHost.Spacing = 12 * dockScale;
            }
        }

        private T GetSetting<T>(string key, T fallback, bool createIfMissing = true)
        {
            try
            {
                return AppSettings.Load<T>(key);
            }
            catch
            {
                if (createIfMissing)
                {
                    AppSettings.Save(key, fallback!);
                }

                return fallback;
            }
        }
    }

    internal static class SettingsStringExtensions
    {
        public static string NullIfEmpty(this string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
