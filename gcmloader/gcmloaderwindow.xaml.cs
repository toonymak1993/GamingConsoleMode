using Discord;
using Discord.WebSocket;
using DualSenseAPI;
using HidSharp;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting; 
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Media;
using System.Management;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using Tomlyn.Model;
using Vanara.PInvoke;
using Windows.Devices.Power;
using Windows.Devices.Enumeration;
using Windows.Gaming.Input;
using Windows.Management.Deployment;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.Playback;
using Windows.Networking.Connectivity;
using Windows.System;
using Windows.UI;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;
using Application = Microsoft.UI.Xaml.Application;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Color = Windows.UI.Color;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;

namespace gcmloader
{

    public sealed partial class MainWindow : Window
    {
        #region needed
        private void BoostProcessPriority()
        {
            try
            {
                using (Process p = Process.GetCurrentProcess())
                {
                    // "High" ist sicher und reicht meistens aus. 
                    // "RealTime" wäre gefährlich (kann Maus/Tastatur blockieren).
                    p.PriorityClass = ProcessPriorityClass.High;
                }
                Debug.WriteLine("[Performance] Process priority set to HIGH.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Performance] Failed to boost priority: {ex.Message}");
            }
        }

        #region cardimagecontrol




        private ProgramCardEntry _currentEditingCardEntry = null; // Stores the card we are currently editing
        private List<string> _currentImageSearchResults = new List<string>();
        private int _selectedImageGridIndex = 0;
        private bool _hasCheckedForGithubReleaseUpdate;
        private FocusArea _githubReleaseReturnFocusArea = FocusArea.Cards;
        private GithubReleaseInfo _availableGithubReleaseUpdate;
        private const string GithubLatestReleaseApiUrl = "https://api.github.com/repos/toonymak1993/GameConsoleMode/releases/latest";
        private FocusArea _gameOptionsReturnFocusArea = FocusArea.Cards;
        private Border _bottomLegendBar;
        private StackPanel _bottomLegendItemsHost;
        private Border _bottomStatusPopup;
        private CompositeTransform _bottomStatusPopupTransform;
        private TextBlock _bottomStatusText;
        private DispatcherTimer _bottomStatusHideTimer;
        private Storyboard _bottomStatusShowStoryboard;
        private Storyboard _bottomStatusHideStoryboard;
        private string _pendingStatusMessage = string.Empty;
        private ControllerType _lastActiveControllerType = ControllerType.Xbox;
        private bool _hasObservedControllerInput;
        private double _currentShellLayoutScale = 1.0;
        private double _currentTopPanelScale = 1.0;
        private bool _isRebuildingResponsiveShell;
        private string _lastLegendSignature = string.Empty;
        private DispatcherTimer _audioOverlayRefreshTimer;
        private bool _allowMasterVolumeSliderWrite;
        private bool _suppressMasterVolumeWrite;
        private readonly Dictionary<string, MediaPlayer> _uiSoundPlayers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _uiSoundLastPlayedUtc = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _uiActionGateSync = new();
        private readonly Dictionary<string, DateTime> _uiActionLastTriggeredUtc = new(StringComparer.OrdinalIgnoreCase);
        private bool _isShellUiReady;
        private bool _isTransitioningToMainUi;
        private bool _taskManagerStartupPending;
        private bool _hasTaskManagerShellInitialized;
        private bool _hasClockStarted;
        private DispatcherTimer _controllerBatteryTimer;
        private readonly Dictionary<string, ResolvedGameInfo> _resolvedGameInfoCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SteamArtworkPaths> _steamArtworkPathCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _gameBackdropCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SteamStoreBackdropInfo> _steamStoreBackdropCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _steamStoreSearchAppIdCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IReadOnlyList<string>> _processLineageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _gameArtworkRequestsInFlight = new(StringComparer.OrdinalIgnoreCase);
        private string _featuredGameIdentity = string.Empty;
        private string _activeGameBackdropIdentity = string.Empty;
        private int _activeGameBackdropLoadVersion;
        private int _backgroundImageLoadVersion;
        private MediaPlayer _backgroundWallpaperPlayer;
        private ProcessData _featuredGameProcessData;
        private List<ProcessData> _deferredProcessData = new();

        private sealed class ResolvedGameInfo
        {
            public bool IsGame { get; set; }
            public int Score { get; set; }
            public string CacheKey { get; set; }
            public string DisplayName { get; set; }
            public string SteamAppId { get; set; }
            public string PosterImagePath { get; set; }
            public string HeroImagePath { get; set; }
            public string DetectionSummary { get; set; }
        }

        private sealed class SteamArtworkPaths
        {
            public string PosterPath { get; set; }
            public string HeroPath { get; set; }
        }

        private sealed class SteamStoreBackdropInfo
        {
            public string BackgroundRawUrl { get; set; }
            public string BackgroundUrl { get; set; }
            public string ScreenshotUrl { get; set; }
        }

        private sealed class GithubReleaseInfo
        {
            public Version ReleaseVersion { get; set; }
            public string VersionText { get; set; }
            public string DisplayTitle { get; set; }
            public string HtmlUrl { get; set; }
        }

        private sealed class AudioRenderDeviceInfo
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public bool IsDefault { get; set; }
        }
        #endregion

        #region soundcontrol
        // Liest Systemlautstärke und setzt den Slider (ohne Loop)

        private void ScrollToAudioItemAnimated(FrameworkElement item, ScrollViewer viewer, StackPanel panel)
        {
            if (item == null || viewer == null || panel == null) return;
            try
            {
                var transform = item.TransformToVisual(panel);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                double itemY = position.Y;
                double itemHeight = item.ActualHeight;
                double viewportHeight = viewer.ActualHeight;

                double targetOffset = itemY - (viewportHeight / 2) + (itemHeight / 2);
                viewer.ChangeView(null, Math.Max(0, Math.Min(targetOffset, viewer.ScrollableHeight)), null, false);
            }
            catch { /* Layout not ready */ }
        }
        private void UpdateMasterVolumeUI()
        {
            if (MasterVolumeSlider == null || MasterVolumeText == null || MasterVolumeIcon == null)
            {
                return;
            }

            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                int volume = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);

                _suppressMasterVolumeWrite = true;
                MasterVolumeSlider.Value = volume;
                _suppressMasterVolumeWrite = false;

                MasterVolumeText.Text = $"{volume}%";
                UpdateVolumeIcon(volume);
            }
            catch
            {
                _suppressMasterVolumeWrite = false;
            }
        }

        // Wird aufgerufen, wenn der Slider bewegt wird
        private void MasterVolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (MasterVolumeText == null || MasterVolumeIcon == null)
            {
                return;
            }

            try
            {
                int newVolume = (int)e.NewValue;
                MasterVolumeText.Text = $"{newVolume}%";
                UpdateVolumeIcon(newVolume);

                if (_suppressMasterVolumeWrite || !_allowMasterVolumeSliderWrite)
                {
                    return;
                }

                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                device.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume / 100.0f;
            }
            catch { }
        }

        private void UpdateVolumeIcon(int volume)
        {
            if (MasterVolumeIcon == null)
            {
                return;
            }

            if (volume == 0) MasterVolumeIcon.Glyph = "\uE74F"; // Mute
            else if (volume < 33) MasterVolumeIcon.Glyph = "\uE993"; // Low
            else if (volume < 66) MasterVolumeIcon.Glyph = "\uE994"; // Mid
            else MasterVolumeIcon.Glyph = "\uE995"; // High
        }

        private void EnsureAudioOverlayRefreshTimer()
        {
            if (_audioOverlayRefreshTimer != null)
            {
                return;
            }

            _audioOverlayRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };

            _audioOverlayRefreshTimer.Tick += (s, e) => RefreshAudioOverlayLiveState();
        }

        private void StartAudioOverlayRefreshLoop()
        {
            EnsureAudioOverlayRefreshTimer();
            _audioOverlayRefreshTimer?.Start();
        }

        private void StopAudioOverlayRefreshLoop()
        {
            _audioOverlayRefreshTimer?.Stop();
        }

        private void RefreshAudioOverlayLiveState()
        {
            if (AudioOverlay == null || AudioOverlay.Visibility != Visibility.Visible)
            {
                return;
            }

            UpdateMasterVolumeUI();

            if (!_isAudioMixerMode)
            {
                return;
            }

            foreach (var row in _audioMixerRows)
            {
                if (row?.Tag is not AudioSessionControl session)
                {
                    continue;
                }

                try
                {
                    if (session.State != AudioSessionState.AudioSessionStateExpired)
                    {
                        UpdateMixerRowVisuals(row, session.SimpleAudioVolume.Volume);
                    }
                }
                catch
                {
                }
            }
        }
        #region AudioMixerLogic

        // --- Audio Mixer Variables ---
        private bool _isAudioMixerMode = false; // False = Devices, True = Mixer
        private int _selectedMixerIndex = 0;
        private List<Border> _audioMixerRows = new List<Border>();

        // Toggles between Output Devices and App Mixer
        private void ToggleAudioTab(bool toMixer)
        {
            _isAudioMixerMode = toMixer;

            if (_isAudioMixerMode)
            {
                // UI updates for mixer mode
                TabHeaderDevices.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                TabHeaderDevices.Opacity = 0.5;
                TabHeaderMixer.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(34, 255, 255, 255));
                TabHeaderMixer.Opacity = 1.0;

                AudioDevicesScrollViewer.Visibility = Visibility.Collapsed;
                AudioMixerScrollViewer.Visibility = Visibility.Visible;

                LegendDevices.Visibility = Visibility.Collapsed;
                LegendMixer.Visibility = Visibility.Visible;

                if (_audioMixerRows.Count == 0 || (DateTime.UtcNow - _lastAudioMixerRefreshUtc).TotalMilliseconds >= 350)
                {
                    RefreshMixerList();
                }
            }
            else
            {
                // UI updates for devices mode
                TabHeaderDevices.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(34, 255, 255, 255));
                TabHeaderDevices.Opacity = 1.0;
                TabHeaderMixer.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                TabHeaderMixer.Opacity = 0.5;

                AudioDevicesScrollViewer.Visibility = Visibility.Visible;
                AudioMixerScrollViewer.Visibility = Visibility.Collapsed;

                LegendDevices.Visibility = Visibility.Visible;
                LegendMixer.Visibility = Visibility.Collapsed;
            }

            // IMPORTANT: Slightly delay the visual update so the layout has time to settle
            this.DispatcherQueue.TryEnqueue(() => UpdateAudioVisualFocus());
        }

        // More robust implementation of RefreshMixerList
        // --- Optimized implementation to prevent duplicate apps in Audio Mixer ---
        // --- Updated RefreshMixerList to filter out "System / Unbekannt" ---
        private void RefreshMixerList()
        {
            if (MixerListStackPanel == null) return;

            MixerListStackPanel.Children.Clear();
            _audioMixerRows.Clear();
            _selectedMixerIndex = 0;
            _lastAudioMixerRefreshUtc = DateTime.UtcNow;

            HashSet<uint> processedPids = new HashSet<uint>();

            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = TryGetDefaultRenderDevice(enumerator);
                if (device == null)
                {
                    ShowMixerFallbackMessage("No default audio output device is available.");
                    return;
                }

                var sessionManager = device.AudioSessionManager;
                try
                {
                    sessionManager.RefreshSessions();
                }
                catch (Exception refreshEx)
                {
                    Debug.WriteLine($"[AudioMixer] RefreshSessions failed: {refreshEx}");
                }

                for (int i = 0; i < sessionManager.Sessions.Count; i++)
                {
                    var session = sessionManager.Sessions[i];

                    // 1. Grundfilter: Abgelaufene Sessions ignorieren
                    if (session.State == AudioSessionState.AudioSessionStateExpired) continue;

                    uint pid = session.GetProcessID;

                    // 2. Doppelte PIDs ignorieren
                    if (pid > 0 && processedPids.Contains(pid)) continue;

                    string displayName = "System / Unbekannt";
                    BitmapImage iconImage = null;

                    if (pid > 0)
                    {
                        try
                        {
                            var proc = Process.GetProcessById((int)pid);
                            if (!string.IsNullOrEmpty(proc.ProcessName))
                            {
                                displayName = proc.ProcessName;
                            }

                            try
                            {
                                if (proc.MainModule != null && !string.IsNullOrEmpty(proc.MainModule.FileName))
                                {
                                    iconImage = GetAppIconAsBitmapImage(proc.MainModule.FileName);
                                }
                            }
                            catch { /* Icon-Zugriff verweigert */ }
                        }
                        catch
                        {
                            // Prozess existiert nicht mehr oder Zugriff verweigert
                            continue;
                        }
                    }
                    else
                    {
                        // PID 0 (System) wird hier ignoriert, um "System / Unbekannt" zu vermeiden
                        continue;
                    }

                    // 3. EXPLIZITER FILTER: Wenn kein Name gefunden wurde, Eintrag nicht anzeigen
                    if (displayName == "System / Unbekannt")
                    {
                        continue;
                    }

                    // Markieren als verarbeitet
                    processedPids.Add(pid);

                    // Zeile erstellen und zur UI hinzufügen
                    var row = CreateMixerRow(displayName, iconImage, session);
                    MixerListStackPanel.Children.Add(row);
                    _audioMixerRows.Add(row);
                }

                if (_audioMixerRows.Count == 0)
                {
                    ShowMixerFallbackMessage("No active app audio sessions are available right now.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioMixer CRITICAL ERROR]: {ex}");
                ShowMixerFallbackMessage("The app mixer could not be loaded right now.");
            }
        }

        // Creates a single row for the mixer
        private Border CreateMixerRow(string name, BitmapImage icon, AudioSessionControl session)
        {
            var border = new Border
            {
                Height = 76,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(18, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(38, 255, 255, 255)),
                Padding = new Thickness(18, 0, 18, 0),
                Margin = new Thickness(8, 6, 8, 6),
                Tag = session,
                // Transform direkt vorbereiten für flüssigere Animation
                RenderTransform = new CompositeTransform { CenterX = 0.5, CenterY = 0.5 },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
            };

            // Responsive grid layout
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); // 1: Name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }); // 2: Slider
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 3: Text

            // 1. Icon
            var img = new Image
            {
                Source = icon ?? new BitmapImage(new Uri("ms-appx:///Assets/game.png")),
                Width = 36,
                Height = 36,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(img, 0);

            // 2. Name
            var txtName = new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(txtName, 1);

            // 3. Slider container
            var sliderContainer = new Grid
            {
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var bgRect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                RadiusX = 4,
                RadiusY = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var fillRect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Fill = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"],
                HorizontalAlignment = HorizontalAlignment.Left,
                RadiusX = 4,
                RadiusY = 4,
                Name = "FillRect",
                Width = 0
            };

            sliderContainer.Children.Add(bgRect);
            sliderContainer.Children.Add(fillRect);
            Grid.SetColumn(sliderContainer, 2);

            // Event for responsive slider
            sliderContainer.SizeChanged += (s, e) =>
            {
                try
                {
                    if (session.State != AudioSessionState.AudioSessionStateExpired)
                        UpdateMixerRowVisuals(border, session.SimpleAudioVolume.Volume);
                }
                catch { }
            };

            // 4. Percent text
            var txtPercent = new TextBlock
            {
                Name = "VolText",
                Text = "0%",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontSize = 14,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 255, 255, 255)),
                Width = 40,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(txtPercent, 3);

            grid.Children.Add(img);
            grid.Children.Add(txtName);
            grid.Children.Add(sliderContainer);
            grid.Children.Add(txtPercent);

            border.Child = grid;

            // Initial update when loaded (safe)
            border.Loaded += (s, e) =>
            {
                try
                {
                    if (session.State != AudioSessionState.AudioSessionStateExpired)
                        UpdateMixerRowVisuals(border, session.SimpleAudioVolume.Volume);
                }
                catch { }
            };

            return border;
        }

        // Updates the bar and the text
        private void UpdateMixerRowVisuals(Border row, float volume)
        {
            if (row.Child is Grid grid)
            {
                // 1. Update text (column 3)
                // Find the element in column 3 to be sure
                var txtBlock = grid.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetColumn(t) == 3);
                if (txtBlock != null)
                {
                    txtBlock.Text = $"{(int)(volume * 100)}%";
                }

                // 2. Update slider width (column 2)
                var sliderGrid = grid.Children.OfType<Grid>().FirstOrDefault(g => Grid.GetColumn(g) == 2);
                if (sliderGrid != null && sliderGrid.Children.Count > 1)
                {
                    if (sliderGrid.Children[1] is Microsoft.UI.Xaml.Shapes.Rectangle fillRect)
                    {
                        // The trick: use the actual rendered width of the container on screen
                        double totalWidth = sliderGrid.ActualWidth;

                        // If the UI hasn't rendered yet, bail out
                        if (totalWidth <= 0) return;

                        // Neue Breite berechnen
                        fillRect.Width = totalWidth * volume;
                    }
                }
            }
        }

        // Adjusts volume and updates the UI bar
        private void AdjustSessionVolume(int rowIndex, float change)
        {
            if (rowIndex < 0 || rowIndex >= _audioMixerRows.Count) return;

            var row = _audioMixerRows[rowIndex];
            if (row.Tag is AudioSessionControl session)
            {
                try
                {
                    float current = session.SimpleAudioVolume.Volume;
                    float newVol = Math.Clamp(current + change, 0.0f, 1.0f);

                    session.SimpleAudioVolume.Volume = newVol;
                    UpdateMixerRowVisuals(row, newVol);
                }
                catch { }
            }
        }

        private void ApplyAudioMixerRowVisual(Border row, bool isSelected)
        {
            if (row == null)
            {
                return;
            }

            row.Background = new SolidColorBrush(isSelected
                ? GetThemeAccentColor(34)
                : Windows.UI.Color.FromArgb(18, 255, 255, 255));
            row.BorderBrush = new SolidColorBrush(isSelected
                ? GetThemeAccentColor(174)
                : Windows.UI.Color.FromArgb(38, 255, 255, 255));
            row.BorderThickness = new Thickness(isSelected ? 1.4 : 1);
        }

        private void ApplyAudioDeviceButtonVisual(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            bool isDefaultDevice = _audioDeviceButtonLookup.TryGetValue(button, out var info) && info.IsDefault;
            byte baseAlpha = isDefaultDevice ? (byte)26 : (byte)16;
            byte borderAlpha = isDefaultDevice ? (byte)78 : (byte)38;

            button.Background = new SolidColorBrush(isSelected
                ? GetThemeAccentColor(34)
                : Windows.UI.Color.FromArgb(baseAlpha, 255, 255, 255));
            button.BorderBrush = new SolidColorBrush(isSelected
                ? GetThemeAccentColor(174)
                : Windows.UI.Color.FromArgb(borderAlpha, 255, 255, 255));
            button.BorderThickness = new Thickness(isSelected ? 1.4 : 1);
        }

        // Fokus Visualisierung für Audio Menü (MIT AUTO-SCROLLING)
        private void UpdateAudioVisualFocus()
        {
            // 1. Reset aller Listen-Elemente
            foreach (var btn in _audioDeviceButtons)
            {
                AnimateScale(btn, false);
                ApplyAudioDeviceButtonVisual(btn, false);
            }
            foreach (var row in _audioMixerRows)
            {
                AnimateScale(row, false);
                ApplyAudioMixerRowVisual(row, false);
            }

            // 2. Reset Master Slider
            MasterVolumeContainer.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            MasterVolumeContainer.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(12, 255, 255, 255));

            // 3. Highlight Logik
            if (_isMasterVolumeFocused)
            {
                // --- MASTER SLIDER FOKUS ---
                // Hellerer Hintergrund + Akzent-Rahmen
                MasterVolumeContainer.Background = new SolidColorBrush(GetThemeAccentColor(28));
                MasterVolumeContainer.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
            }
            else
            {
                // --- LISTEN FOKUS (Wie bisher) ---
                if (_isAudioMixerMode)
                {
                    if (_audioMixerRows.Count > _selectedMixerIndex && _selectedMixerIndex >= 0)
                    {
                        var active = _audioMixerRows[_selectedMixerIndex];
                        ApplyAudioMixerRowVisual(active, true);
                        AnimateScale(active, true);
                        // Auto-Scroll Logic hier einfügen wenn nötig...
                    }
                }
                else
                {
                    if (_audioDeviceButtons.Count > _selectedAudioDeviceIndex && _selectedAudioDeviceIndex >= 0)
                    {
                        var active = _audioDeviceButtons[_selectedAudioDeviceIndex];
                        ApplyAudioDeviceButtonVisual(active, true);
                        AnimateScale(active, true);
                        // Auto-Scroll Logic hier einfügen wenn nötig...
                    }
                }
            }
        }
        #endregion


        // Cache for sounds to avoid loading from disk every time
        private readonly Dictionary<string, Uri> _soundCache = new();
        private List<Button> _audioDeviceButtons = new List<Button>();
        private readonly Dictionary<Button, AudioRenderDeviceInfo> _audioDeviceButtonLookup = new();
        private int _selectedAudioDeviceIndex = 0;
        private bool _isAudioFlyoutAnimating;
        private int _audioFlyoutRequestVersion;
        private DateTime _lastAudioMixerRefreshUtc = DateTime.MinValue;

        private void ResetAudioFlyoutState(bool collapseOverlay)
        {
            _allowMasterVolumeSliderWrite = false;
            StopAudioOverlayRefreshLoop();

            if (AudioPanelTransform != null)
            {
                AudioPanelTransform.TranslateY = 100;
            }

            if (AudioOverlay != null)
            {
                AudioOverlay.IsHitTestVisible = !collapseOverlay;
                AudioOverlay.Opacity = 0;
                AudioOverlay.Visibility = collapseOverlay ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private MMDevice TryGetDefaultRenderDevice(MMDeviceEnumerator enumerator)
        {
            if (enumerator == null)
            {
                return null;
            }

            foreach (var role in new[] { Role.Multimedia, Role.Console, Role.Communications })
            {
                try
                {
                    return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
                }
                catch
                {
                }
            }

            try
            {
                return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .Cast<MMDevice>()
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private void ShowAudioFlyoutFallbackMessage(string message)
        {
            if (SimpleAudioList == null)
            {
                return;
            }

            SimpleAudioList.Children.Clear();
            _audioDeviceButtons.Clear();
            _audioDeviceButtonLookup.Clear();
            _selectedAudioDeviceIndex = 0;

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                Opacity = 0.88,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12)
            };

            var host = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 18, 16, 18),
                Child = messageBlock
            };

            SimpleAudioList.Children.Add(host);
        }

        private void ShowMixerFallbackMessage(string message)
        {
            if (MixerListStackPanel == null)
            {
                return;
            }

            MixerListStackPanel.Children.Clear();
            _audioMixerRows.Clear();
            _selectedMixerIndex = 0;

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                Opacity = 0.88,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(12)
            };

            var host = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 18, 16, 18),
                Child = messageBlock
            };

            MixerListStackPanel.Children.Add(host);
        }

        private void ShowAudioFlyoutLoadingMessage(string message)
        {
            if (SimpleAudioList == null)
            {
                return;
            }

            SimpleAudioList.Children.Clear();
            _audioDeviceButtons.Clear();
            _audioDeviceButtonLookup.Clear();
            _selectedAudioDeviceIndex = 0;

            var progressRing = new ProgressRing
            {
                IsActive = true,
                Width = 28,
                Height = 28,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var messageBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                Opacity = 0.88,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(progressRing);
            stack.Children.Add(messageBlock);

            var host = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 18, 16, 18),
                Child = stack
            };

            SimpleAudioList.Children.Add(host);
        }

        private async Task<List<AudioRenderDeviceInfo>> GetAudioRenderDevicesAsync()
        {
            var devices = new List<AudioRenderDeviceInfo>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = TryGetDefaultRenderDevice(enumerator);

                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Cast<MMDevice>())
                {
                    string deviceId = device.ID ?? string.Empty;
                    string displayName = device.FriendlyName?.Trim();
                    if (string.IsNullOrWhiteSpace(displayName) || !seenIds.Add(deviceId))
                    {
                        continue;
                    }

                    devices.Add(new AudioRenderDeviceInfo
                    {
                        Id = deviceId,
                        DisplayName = displayName,
                        IsDefault = defaultDevice != null &&
                                    string.Equals(defaultDevice.ID, deviceId, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] NAudio render device enumeration failed: {ex}");
            }

            if (devices.Count > 0)
            {
                return devices;
            }

            try
            {
                string defaultDeviceId = string.Empty;
                try
                {
                    defaultDeviceId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default) ?? string.Empty;
                }
                catch (Exception defaultIdEx)
                {
                    Debug.WriteLine($"[Audio] WinRT default render device lookup failed: {defaultIdEx}");
                }

                var infos = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector());
                foreach (var info in infos)
                {
                    if (info == null || string.IsNullOrWhiteSpace(info.Name))
                    {
                        continue;
                    }

                    string deviceId = info.Id ?? string.Empty;
                    if (!seenIds.Add(deviceId))
                    {
                        continue;
                    }

                    devices.Add(new AudioRenderDeviceInfo
                    {
                        Id = deviceId,
                        DisplayName = info.Name.Trim(),
                        IsDefault = !string.IsNullOrWhiteSpace(defaultDeviceId) &&
                                    string.Equals(defaultDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] WinRT render device enumeration failed: {ex}");
            }

            return devices;
        }

        // --- High-End Animation for the entire Sound Panel ---
        private async void OpenAudioFlyout()
        {
            try
            {
                if (AudioOverlay == null || AudioPanelTransform == null || SimpleAudioList == null)
                {
                    Debug.WriteLine("[Audio] Overlay controls are not ready.");
                    return;
                }

                if (_isAudioFlyoutAnimating)
                {
                    return;
                }

                if (AudioOverlay.Visibility == Visibility.Visible && AudioOverlay.Opacity > 0.98)
                {
                    return;
                }

                int requestVersion = ++_audioFlyoutRequestVersion;
                _isAudioFlyoutAnimating = true;

                ToggleAudioTab(false);
                _isMasterVolumeFocused = false;
                _allowMasterVolumeSliderWrite = false;

                SimpleAudioList.Children.Clear();
                _audioDeviceButtons.Clear();
                _audioDeviceButtonLookup.Clear();
                _selectedAudioDeviceIndex = 0;

                AudioOverlay.Visibility = Visibility.Visible;
                AudioOverlay.IsHitTestVisible = true;
                AudioOverlay.Opacity = 0;
                AudioPanelTransform.TranslateY = 100;
                _currentFocusArea = FocusArea.AudioMenu;

                ShowAudioFlyoutLoadingMessage("Loading audio devices...");
                UpdateVisualFocus();

                var sb = new Storyboard();
                var duration = TimeSpan.FromMilliseconds(450);
                var easing = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut };

                var fadeIn = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(250) };
                Storyboard.SetTarget(fadeIn, AudioOverlay);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                var slideUp = new DoubleAnimation
                {
                    From = 100,
                    To = 0,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(slideUp, AudioPanelTransform);
                Storyboard.SetTargetProperty(slideUp, "TranslateY");

                sb.Children.Add(fadeIn);
                sb.Children.Add(slideUp);
                sb.Completed += (_, _) => _isAudioFlyoutAnimating = false;
                sb.Begin();

                // Give WinUI one frame so the panel becomes visible before we touch audio APIs.
                await Task.Yield();

                if (requestVersion != _audioFlyoutRequestVersion || AudioOverlay.Visibility != Visibility.Visible)
                {
                    return;
                }

                UpdateMasterVolumeUI();

                try
                {
                    SimpleAudioList.Children.Clear();

                    var devices = await GetAudioRenderDevicesAsync();

                    foreach (var device in devices)
                    {
                        var btn = new Button
                        {
                            Tag = device.DisplayName,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Height = 74,
                            CornerRadius = new CornerRadius(18),
                            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(device.IsDefault ? (byte)26 : (byte)16, 255, 255, 255)),
                            BorderThickness = new Thickness(1),
                            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(device.IsDefault ? (byte)78 : (byte)38, 255, 255, 255)),
                            Padding = new Thickness(18, 0, 18, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Stretch
                        };

                        var contentGrid = new Grid();
                        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        string friendlyNameLower = device.DisplayName.ToLowerInvariant();
                        string glyph = friendlyNameLower.Contains("headset") || friendlyNameLower.Contains("kopfhörer") || friendlyNameLower.Contains("headphones")
                            ? "\uE76B"
                            : "\uE7F5";

                        var iconHost = new Border
                        {
                            Width = 44,
                            Height = 44,
                            CornerRadius = new CornerRadius(14),
                            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(26, 255, 255, 255)),
                            Child = new FontIcon
                            {
                                Glyph = glyph,
                                FontSize = 18,
                                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                        Grid.SetColumn(iconHost, 0);

                        var textStack = new StackPanel
                        {
                            Spacing = 3,
                            Margin = new Thickness(14, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        textStack.Children.Add(new TextBlock
                        {
                            Text = device.DisplayName,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 15,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        textStack.Children.Add(new TextBlock
                        {
                            Text = device.IsDefault ? "Current default output" : "Available output",
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 12,
                            Opacity = 0.68,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        Grid.SetColumn(textStack, 1);

                        if (device.IsDefault)
                        {
                            var badge = new Border
                            {
                                Background = new SolidColorBrush(GetThemeAccentColor(42)),
                                BorderBrush = new SolidColorBrush(GetThemeAccentColor(112)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(12),
                                Padding = new Thickness(10, 6, 10, 6),
                                VerticalAlignment = VerticalAlignment.Center,
                                Child = new TextBlock
                                {
                                    Text = "DEFAULT",
                                    FontSize = 11,
                                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                    CharacterSpacing = 80
                                }
                            };
                            Grid.SetColumn(badge, 2);
                            contentGrid.Children.Add(badge);
                        }

                        contentGrid.Children.Add(iconHost);
                        contentGrid.Children.Add(textStack);

                        btn.Content = contentGrid;
                        _audioDeviceButtonLookup[btn] = device;
                        btn.Click += (s, e) =>
                        {
                            if (TrySelectAudioDevice(device.DisplayName))
                            {
                                PlayActivationSound();
                            }
                        };

                        _audioDeviceButtons.Add(btn);
                        SimpleAudioList.Children.Add(btn);
                    }

                    if (_audioDeviceButtons.Count == 0)
                    {
                        ShowAudioFlyoutFallbackMessage("No active audio output devices were found.");
                    }
                }
                catch (Exception deviceEx)
                {
                    Debug.WriteLine("[Audio] Device enumeration failed: " + deviceEx);
                    ShowAudioFlyoutFallbackMessage("Audio devices could not be loaded right now.");
                }

                if (requestVersion != _audioFlyoutRequestVersion || AudioOverlay.Visibility != Visibility.Visible)
                {
                    return;
                }

                await Task.Delay(10);
                SimpleAudioList.UpdateLayout();

                _allowMasterVolumeSliderWrite = true;
                StartAudioOverlayRefreshLoop();
                UpdateVisualFocus();
            }
            catch (Exception ex)
            {
                _isAudioFlyoutAnimating = false;
                ResetAudioFlyoutState(true);
                _currentFocusArea = FocusArea.TopButtons;
                Debug.WriteLine("[Audio] Flyout failed: " + ex.Message);
                SendOverlayNotification("Audio panel failed to open");
                UpdateVisualFocus();
            }
        }

        private void CloseAudioFlyout()
        {
            _audioFlyoutRequestVersion++;
            _allowMasterVolumeSliderWrite = false;
            StopAudioOverlayRefreshLoop();

            if (AudioOverlay == null || AudioPanelTransform == null)
            {
                _isAudioFlyoutAnimating = false;
                _currentFocusArea = FocusArea.TopButtons;
                UpdateVisualFocus();
                return;
            }

            if (AudioOverlay.Visibility != Visibility.Visible)
            {
                _isAudioFlyoutAnimating = false;
                _currentFocusArea = FocusArea.TopButtons;
                UpdateVisualFocus();
                return;
            }

            _isAudioFlyoutAnimating = true;

            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new ExponentialEase { Exponent = 5, EasingMode = EasingMode.EaseIn };
            var sb = new Storyboard();

            // Fade Out der gesamten Ebene
            var fadeOut = new DoubleAnimation { To = 0.0, Duration = duration };
            Storyboard.SetTarget(fadeOut, AudioOverlay);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            // Inhalt nach unten gleiten lassen
            var slideDown = new DoubleAnimation { To = 100, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(slideDown, AudioPanelTransform);
            Storyboard.SetTargetProperty(slideDown, "TranslateY");

            sb.Children.Add(fadeOut);
            sb.Children.Add(slideDown);

            sb.Completed += (s, e) => {
                _isAudioFlyoutAnimating = false;
                AudioOverlay.IsHitTestVisible = false;
                AudioOverlay.Visibility = Visibility.Collapsed;
                _currentFocusArea = FocusArea.TopButtons;
                UpdateVisualFocus();
            };

            sb.Begin();
        }

        private void ToggleAudioFlyout()
        {
            if (AudioOverlay == null)
            {
                return;
            }

            if (_isAudioFlyoutAnimating)
            {
                return;
            }

            if (AudioOverlay.Visibility == Visibility.Visible && AudioOverlay.Opacity < 0.01)
            {
                ResetAudioFlyoutState(true);
            }

            if (AudioOverlay.Visibility == Visibility.Visible) CloseAudioFlyout();
            else OpenAudioFlyout();
        }

        private bool TryToggleAudioFlyout(string gateKey)
        {
            if (!TryAcquireUiActionGate(gateKey, 280))
            {
                return false;
            }

            if (AudioOverlay == null || _isAudioFlyoutAnimating)
            {
                return false;
            }

            ToggleAudioFlyout();
            return true;
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() => TryToggleAudioFlyout("audio-volume-button"));
        }

        private void AudioOverlay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CloseAudioFlyout();
        }

        private void SetAudioDevice(string deviceName)
        {
            // Bereinigung für NirCmd
            string cleanedName = deviceName.Split('(')[0].Trim();
            NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{cleanedName}\"");
            SendOverlayNotification("Audio: " + cleanedName);
            CloseAudioFlyout();
        }

        private bool TrySelectAudioDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName) || !TryAcquireUiActionGate("audio-set-device", 320))
            {
                return false;
            }

            SetAudioDevice(deviceName);
            return true;
        }

        #endregion soundcontrol
        #region psdualsense
        // PS5 HID Hardware
        private HidDevice _ps5Device;
        private HidStream _ps5Stream;
        private byte[] _hidInputBuffer = new byte[64];
        private HidDevice _steamDeckPuckDevice;
        private HidStream _steamDeckPuckStream;
        private readonly byte[] _steamDeckPuckInputBuffer = new byte[64];

        // Navigation State für PlayStation (Index 4 reserviert)
        private GamepadButtonFlags _lastPs5ButtonState = GamepadButtonFlags.None;
        private DateTime _ps5NextAllowedInputTime = DateTime.MinValue;
        private bool _ps5StickCentered = true;
        private GamepadButtonFlags _lastSteamDeckPuckButtonState = GamepadButtonFlags.None;
        private DateTime _steamDeckPuckNextAllowedInputTime = DateTime.MinValue;
        private bool _steamDeckPuckStickCentered = true;
        private GamepadButtonFlags[] _lastShortcutButtons = new GamepadButtonFlags[5];

        private const int SteamDeckPuckVendorId = 0x28DE;
        private const int SteamDeckPuckProductId = 0x1304;
        private const int SteamDeckPuckControllerIndex = 7;

        private const uint SteamDeckButtonA = 0x00000001;
        private const uint SteamDeckButtonB = 0x00000002;
        private const uint SteamDeckButtonX = 0x00000004;
        private const uint SteamDeckButtonY = 0x00000008;
        private const uint SteamDeckButtonView = 0x00000040;
        private const uint SteamDeckButtonRightStick = 0x00000020;
        private const uint SteamDeckButtonRightShoulder = 0x00000200;
        private const uint SteamDeckButtonDPadDown = 0x00000400;
        private const uint SteamDeckButtonDPadRight = 0x00000800;
        private const uint SteamDeckButtonDPadLeft = 0x00001000;
        private const uint SteamDeckButtonDPadUp = 0x00002000;
        private const uint SteamDeckButtonMenu = 0x00004000;
        private const uint SteamDeckButtonLeftStick = 0x00008000;
        private const uint SteamDeckButtonSteam = 0x00010000;
        private const uint SteamDeckButtonLeftShoulder = 0x00080000;

        #endregion psdualsense
        #region controllerbattery icon
        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_BROWSER_HOME = 7;
        // Updates the controller battery status and UI icon
        #region controllerbattery icon

        // Die verbesserte Hybrid-Methode: Zeigt immer den echten Wert an, auch wenn es 10% sind.
        private void UpdateControllerBatteryStatus()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    bool isAnyControllerConnected = false;

                    // =================================================================
                    // METHOD 1: SharpDX.XInput (Natives Xbox-Protokoll)
                    // Funktioniert am besten mit Dongle / Kabel
                    // =================================================================
                    Controller activeController = null;
                    for (int i = 0; i < 4; i++)
                    {
                        var temp = new Controller((UserIndex)i);
                        if (temp.IsConnected) { activeController = temp; break; }
                    }

                    if (activeController != null)
                    {
                        isAnyControllerConnected = true;
                        var batteryInfo = activeController.GetBatteryInformation(BatteryDeviceType.Gamepad);

                        if (batteryInfo.BatteryType == BatteryType.Wired)
                        {
                            UpdateControllerUI_Text("USB", true);
                            return;
                        }

                        // Über XInput haben wir klare Hardware-Stufen (High, Med, Low)
                        if (batteryInfo.BatteryType != BatteryType.Disconnected && batteryInfo.BatteryType != BatteryType.Unknown)
                        {
                            UpdateControllerUI_State(batteryInfo.BatteryLevel);
                            return;
                        }
                    }

                    // =================================================================
                    // METHOD 2: Windows.Gaming.Input (Modern API)
                    // Für Bluetooth (gibt Prozentwerte aus)
                    // =================================================================
                    if (Windows.Gaming.Input.Gamepad.Gamepads.Count > 0)
                    {
                        isAnyControllerConnected = true;
                        var modernGamepad = Windows.Gaming.Input.Gamepad.Gamepads[0];
                        var report = modernGamepad.TryGetBatteryReport();

                        if (report != null && report.Status != Windows.System.Power.BatteryStatus.NotPresent)
                        {
                            bool isCharging = report.Status == Windows.System.Power.BatteryStatus.Charging;

                            if (report.FullChargeCapacityInMilliwattHours.HasValue &&
                                report.RemainingCapacityInMilliwattHours.HasValue &&
                                report.FullChargeCapacityInMilliwattHours.Value > 0)
                            {
                                int exactPercentage = (int)(((double)report.RemainingCapacityInMilliwattHours.Value / report.FullChargeCapacityInMilliwattHours.Value) * 100);

                                // HIER IST DER FIX: Wir filtern nichts mehr weg! 
                                // Wir zeigen exakt das an, was der Controller an Windows funkt.
                                UpdateControllerUI_Exact(exactPercentage, isCharging);
                                return;
                            }
                        }
                    }

                    // =================================================================
                    // METHOD 3: DER FALLBACK
                    // Wenn XInput "Unknown" liefert UND WGI keine Prozente hat.
                    // =================================================================
                    if (isAnyControllerConnected)
                    {
                        UpdateControllerUI_Text("Connected", false);
                    }
                    else
                    {
                        ControllerStatusGroup.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BatteryError] {ex.Message}");
                    ControllerStatusGroup.Visibility = Visibility.Collapsed;
                }
            });
        }

        // Helper 1: Zeigt exakte Prozente an (WGI)
        private void UpdateControllerUI_Exact(int percentage, bool isCharging)
        {
            ControllerStatusGroup.Visibility = Visibility.Visible;

            if (isCharging)
            {
                ControllerBatteryText.Text = "Charging...";
                ControllerBatteryText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            }
            else
            {
                ControllerBatteryText.Text = $"{percentage}%";
                // Farbwechsel auf Rot erst bei unter 10%, da wiederaufladbare Batterien (1.2V) oft dauerhaft 10% melden
                ControllerBatteryText.Foreground = percentage < 10
                    ? new SolidColorBrush(Microsoft.UI.Colors.Red)
                    : new SolidColorBrush(Microsoft.UI.Colors.White);
            }
        }

        // Helper 2: Hardware-Status (XInput)
        private void UpdateControllerUI_State(BatteryLevel level)
        {
            ControllerStatusGroup.Visibility = Visibility.Visible;

            switch (level)
            {
                case BatteryLevel.Empty:
                    ControllerBatteryText.Text = "Critical";
                    ControllerBatteryText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    break;
                case BatteryLevel.Low:
                    ControllerBatteryText.Text = "Low";
                    ControllerBatteryText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 165, 0));
                    break;
                case BatteryLevel.Medium:
                    ControllerBatteryText.Text = "Medium";
                    ControllerBatteryText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    break;
                case BatteryLevel.Full:
                    ControllerBatteryText.Text = "Full";
                    ControllerBatteryText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    break;
                default:
                    ControllerBatteryText.Text = "Connected";
                    ControllerBatteryText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                    break;
            }
        }

        // Helper 3: Generischer Text
        private void UpdateControllerUI_Text(string text, bool isCharging)
        {
            ControllerStatusGroup.Visibility = Visibility.Visible;
            ControllerBatteryText.Text = text;
            ControllerBatteryText.Foreground = isCharging
                ? new SolidColorBrush(Microsoft.UI.Colors.LightGreen)
                : new SolidColorBrush(Microsoft.UI.Colors.White);
        }

        #endregion controllerbattery icon

        #endregion controllerbattery icon
        #region mousecontrol 

        private void ParkMouseCursor()
        {
            // 1. Fokus-Check: Nur parken, wenn GCM das aktive Fenster ist
            IntPtr foregroundHwnd = GetForegroundWindow();
            IntPtr selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            if (foregroundHwnd != selfHwnd)
            {
                // GCM ist im Hintergrund (z.B. ein Spiel läuft) -> Maus absolut nicht anrühren!
                return;
            }

            // 2. Eigentliche Park-Logik (nur wenn Fokus vorhanden)
            SetCursorPos(9999, 9999);

            if (_isCursorVisible)
            {
                while (ShowCursor(false) >= 0) ;
                _isCursorVisible = false;
            }
        }



        // NEW: COM Interface definitions to control the touch keyboard directly.
        [ComImport, Guid("4CE576FA-83DC-4F88-951C-9D0782B4E376")]
        class UIHostNoLaunch
        {
        }

        [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITipInvocation
        {
            void Toggle(IntPtr desktopWindow);
        }

        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private DateTime _lastScrollTime = DateTime.MinValue;

        private float _cursorXRemainder = 0f; 
        private float _cursorYRemainder = 0f; 




        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        private bool _isMouseModeActive = false;
        private DateTime? _comboPressTime = null;
        private bool _mouseModeToggledThisPress = false;
        private bool _isKeyboardVisible = false;
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // Note: You might already have a POINT struct, if not, add this one.
        // If you do, ensure it's public or accessible.
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        // Add these mouse event constants if they are missing
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        #endregion mousecontrol
        #region App Launcher

        bool isAppListLoaded = false;

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxName);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink { }
        private List<Button> _launcherResultButtons = new List<Button>();
        private int _selectedLauncherResultIndex = 0;
        private ObservableCollection<AppInfo> AllInstalledApps { get; } = new ObservableCollection<AppInfo>();
        private ObservableCollection<AppInfo> VisibleAppLauncherApps { get; } = new ObservableCollection<AppInfo>();
        private readonly HashSet<string> _favoriteAppIds = new(StringComparer.OrdinalIgnoreCase);
        private AppLauncherFilter _appLauncherFilter = AppLauncherFilter.All;
        private string _appLauncherSearchText = string.Empty;

        private enum AppLauncherFilter
        {
            All,
            Favorites,
            Desktop,
            Microsoft
        }

        private static readonly AppLauncherFilter[] AppLauncherFilterOrder =
        {
            AppLauncherFilter.All,
            AppLauncherFilter.Favorites,
            AppLauncherFilter.Desktop,
            AppLauncherFilter.Microsoft
        };

        private sealed class AppDiscoveryInfo
        {
            public string Name { get; init; } = string.Empty;
            public string LaunchTarget { get; init; } = string.Empty;
            public string FilePath { get; init; } = string.Empty;
            public string LaunchKind { get; init; } = "Executable";
            public string SourceLabel { get; init; } = "DESKTOP";
            public string IconPath { get; init; } = string.Empty;
            public string StableId { get; init; } = string.Empty;
        }

        private string AppLauncherFavoritesPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "appfavorites.json");
        #endregion
        #region playnite launcher window
        // Add these with your other window style constants
        private bool _isSendingKeys = false;
        private const long WS_BORDER = 0x00800000L;
        private const long WS_SYSMENU = 0x00080000L;
        private const long WS_MINIMIZEBOX = 0x00020000L;

        #endregion playnite launcher window
        #region ui status
        private DispatcherTimer _statusUpdateTimer;
        #endregion ui status
        #region keyboardatstart
        private void SetupKeyboardAutoStartTask()
        {
            const string taskName = "GCM_KeyboardStart";

            try
            {
                using (TaskService ts = new TaskService())
                {
                    // Alte Aufgabe entfernen, um Konfiguration sauber zu überschreiben
                    if (ts.FindTask(taskName) != null)
                    {
                        ts.RootFolder.DeleteTask(taskName);
                    }

                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "Startet die Eingabe-UI beim Systemstart unabhängig von der Anmeldung.";
                    td.RegistrationInfo.Author = "gcm_keyboardstart";

                    // --- TRIGGER: NUR BEIM SYSTEMSTART (BOOT) ---
                    td.Triggers.Add(new BootTrigger { Enabled = true });

                    // --- SETTINGS (Wie in deinem XML) ---
                    td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.AllowHardTerminate = true;
                    td.Settings.StartWhenAvailable = true; // Wichtig: Starten, sobald der Dienst bereit ist
                    td.Settings.RunOnlyIfNetworkAvailable = false;
                    td.Settings.AllowDemandStart = true;
                    td.Settings.Enabled = true;
                    td.Settings.Hidden = false;
                    td.Settings.Priority = ProcessPriorityClass.Normal;

                    // --- AKTION & PFAD ---
                    string tabTipPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                        @"microsoft shared\ink\TabTip.exe");

                    if (File.Exists(tabTipPath))
                    {
                        // WICHTIG: Den Pfad in Anführungszeichen setzen, falls Leerzeichen enthalten sind
                        td.Actions.Add(new ExecAction($"\"{tabTipPath}\"", "startkeyboard"));

                        // --- PRINCIPAL: UNABHÄNGIG VOM NUTZER ---
                        // "S-1-5-18" ist die SID für das SYSTEM-Konto. 
                        // Das ist nötig, damit die Aufgabe beim Booten ohne Login läuft.
                        td.Principal.UserId = "SYSTEM";
                        td.Principal.LogonType = TaskLogonType.ServiceAccount;
                        td.Principal.RunLevel = TaskRunLevel.Highest;

                        ts.RootFolder.RegisterTaskDefinition(taskName, td);
                        Debug.WriteLine($"[Task] {taskName} erfolgreich für System-Boot erstellt.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Task] Fehler: {ex.Message}");
            }
        }
        #endregion keyboardatstart
        #region autoscaling

        private uint _lastCheckedDpi = 0;
        private int _lastCheckedWidth = 0;
        private int _lastCheckedHeight = 0;
        private WindowProcDelegate _nativeScalingWindowProc;
        private IntPtr _originalWindowProc = IntPtr.Zero;
        private bool _nativeScalingHookInstalled;
        private int _pendingScaleRefreshRequestId;
        private readonly bool _freezeRuntimeScaling = true;
        private bool _hasFrozenScalingSnapshot;
        private double _frozenLogicalWidth;
        private double _frozenLogicalHeight;
        private int _frozenPhysicalWidth;
        private int _frozenPhysicalHeight;
        private double _frozenDpiScale = 1.0;
        private DispatcherTimer _runtimeScaleMonitorTimer;
        private bool _isRuntimeScaleReloading;
        private double _lastObservedLiveDpiScale = 1.0;
        private int _lastObservedLiveMonitorWidth;
        private int _lastObservedLiveMonitorHeight;

        private void HideWindowFromAltTab(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            // Konstanten für den erweiterten Fensterstil
            const long WS_EX_APPWINDOW = 0x00040000L;
            const long WS_EX_TOOLWINDOW = 0x00000080L;

            // Aktuellen Stil abrufen
            long exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);

            // ToolWindow hinzufügen (versteckt es) und AppWindow entfernen
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;

            // Neuen Stil anwenden
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)exStyle);

            Debug.WriteLine($"[Alt+Tab Fix] Fenster {hwnd} erfolgreich als ToolWindow markiert.");
        }


        private DispatcherTimer _displayWatchdogTimer;

        private void TriggerDisplayUpdate()
        {
            // Verhindert, dass die Funktion 100x abgefeuert wird, während man den Slider zieht
            if (_displayWatchdogTimer == null)
            {
                _displayWatchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _displayWatchdogTimer.Tick += (s, e) =>
                {
                    _displayWatchdogTimer.Stop();
                    ForceDpiRedraw(); // HIER triggern wir dein bewährtes Redraw!
                };
            }

            _displayWatchdogTimer.Stop();
            _displayWatchdogTimer.Start();
        }

        private void StartRuntimeScaleMonitor()
        {
            if (_runtimeScaleMonitorTimer != null)
            {
                return;
            }

            CaptureCurrentLiveScaleState();

            _runtimeScaleMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(900)
            };

            _runtimeScaleMonitorTimer.Tick += (s, e) =>
            {
                if (_isRuntimeScaleReloading)
                {
                    return;
                }

                if (!HasLiveScaleStateChanged())
                {
                    return;
                }

                Debug.WriteLine("[GCM] Runtime scaling watchdog detected a real scale/monitor change.");
                _ = SoftReloadUiForScaleChangeAsync("Watchdog");
            };

            _runtimeScaleMonitorTimer.Start();
        }

        private void CaptureCurrentLiveScaleState()
        {
            _lastObservedLiveDpiScale = GetLiveDpiScaleFactor();

            IntPtr hwnd = IntPtr.Zero;
            try
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            }
            catch
            {
            }

            if (TryGetCurrentMonitorBounds(hwnd, out RECT bounds))
            {
                _lastObservedLiveMonitorWidth = Math.Max(1, bounds.Right - bounds.Left);
                _lastObservedLiveMonitorHeight = Math.Max(1, bounds.Bottom - bounds.Top);
            }
            else
            {
                GetLivePhysicalResolution(out _lastObservedLiveMonitorWidth, out _lastObservedLiveMonitorHeight);
            }
        }

        private bool HasLiveScaleStateChanged()
        {
            double liveDpiScale = GetLiveDpiScaleFactor();
            bool dpiChanged = Math.Abs(liveDpiScale - _lastObservedLiveDpiScale) > 0.01;

            IntPtr hwnd = IntPtr.Zero;
            try
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            }
            catch
            {
            }

            int liveWidth;
            int liveHeight;
            if (TryGetCurrentMonitorBounds(hwnd, out RECT bounds))
            {
                liveWidth = Math.Max(1, bounds.Right - bounds.Left);
                liveHeight = Math.Max(1, bounds.Bottom - bounds.Top);
            }
            else
            {
                GetLivePhysicalResolution(out liveWidth, out liveHeight);
            }

            bool monitorChanged = liveWidth != _lastObservedLiveMonitorWidth || liveHeight != _lastObservedLiveMonitorHeight;
            return dpiChanged || monitorChanged;
        }

        private double _currentRatio = 1.0;
        private const int DWMWA_EXCLUDED_FROM_PEEK = 12;
        private const int DWMWA_FLIP3D_POLICY = 9;
        private const int DWMFLIP_NONE = 1;
        //vrr control
        public void EnsureVrrDisabledViaRegistry()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string registryPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

                // DISABLEDXMAXIMIZEDWINDOWEDMODE = Deaktiviert VRR im Fenstermodus
                // DISABLEFullScreenOptimizations = Verhindert den "Game-Mode" Wechsel
                string flags = "~ DISABLEDXMAXIMIZEDWINDOWEDMODE DISABLEFullScreenOptimizations";

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath, true))
                {
                    if (key != null)
                    {
                        var existingValue = key.GetValue(exePath);
                        if (existingValue == null || !existingValue.ToString().Contains("DISABLEFullScreenOptimizations"))
                        {
                            key.SetValue(exePath, flags);
                            Debug.WriteLine("[GCM] VRR & Fullscreen-Optimierung via Registry deaktiviert.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler beim Setzen der Registry-Flags: {ex.Message}");
            }
        }


        // This triggers whenever Windows changes the resolution/scaling
        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("[GCM] Display settings changed detected. Re-scaling UI...");
            _ = SoftReloadUiForScaleChangeAsync("SystemEvents.DisplaySettingsChanged");
        }

        #region Advanced Scaling & Redraw Logic

        /* * Documentation:
         * In WinUI 3, XamlRoot.Changed is the ultimate listener for DPI and resolution shifts.
         * We use it to trigger a full physical and logical recalculation of the window size.
         */
        private void SetupScalingEvents(FrameworkElement root)
        {
            if (root.XamlRoot != null)
            {
                root.XamlRoot.Changed += (sender, args) =>
                {
                    _ = SoftReloadUiForScaleChangeAsync("XamlRoot.Changed");
                };
            }
        }

        private void InstallNativeScalingHook()
        {
            if (_nativeScalingHookInstalled)
            {
                return;
            }

            try
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                _nativeScalingWindowProc = NativeScalingWindowProc;
                IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_nativeScalingWindowProc);
                _originalWindowProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, hookPtr);
                _nativeScalingHookInstalled = _originalWindowProc != IntPtr.Zero;

                Debug.WriteLine(_nativeScalingHookInstalled
                    ? "[GCM] Native scaling hook installed."
                    : "[GCM] Native scaling hook installation returned a null original proc.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Failed to install native scaling hook: {ex}");
            }
        }

        private IntPtr NativeScalingWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            IntPtr result = _originalWindowProc != IntPtr.Zero
                ? CallWindowProc(_originalWindowProc, hWnd, msg, wParam, lParam)
                : IntPtr.Zero;

            switch (msg)
            {
                case WM_DPICHANGED:
                    Debug.WriteLine("[GCM] WM_DPICHANGED received.");
                    TryApplySuggestedDpiRect(hWnd, lParam);
                    _ = SoftReloadUiForScaleChangeAsync("WM_DPICHANGED");
                    break;

                case WM_DISPLAYCHANGE:
                    Debug.WriteLine("[GCM] WM_DISPLAYCHANGE received.");
                    _ = SoftReloadUiForScaleChangeAsync("WM_DISPLAYCHANGE");
                    break;

                case WM_SETTINGCHANGE:
                    _ = SoftReloadUiForScaleChangeAsync("WM_SETTINGCHANGE");
                    break;
            }

            return result;
        }

        private void RequestScaleRefresh(string reason, int delayMs = 120, bool forceMonitorResize = false)
        {
            int requestId = ++_pendingScaleRefreshRequestId;

            this.DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(delayMs);
                if (requestId != _pendingScaleRefreshRequestId)
                {
                    return;
                }

                RefreshScalingLayout(reason, forceMonitorResize);
            });
        }

        private async Task SoftReloadUiForScaleChangeAsync(string reason)
        {
            int requestId = ++_pendingScaleRefreshRequestId;
            await Task.Delay(260);

            if (requestId != _pendingScaleRefreshRequestId || _isRuntimeScaleReloading)
            {
                return;
            }

            var completionSource = new TaskCompletionSource<bool>();

            if (!DispatcherQueue.TryEnqueue(async () =>
            {
                if (_isRuntimeScaleReloading)
                {
                    completionSource.TrySetResult(false);
                    return;
                }

                _isRuntimeScaleReloading = true;

                try
                {
                    Debug.WriteLine($"[GCM] Starting soft UI reload for scale change via {reason}.");

                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    if (MainContent != null)
                    {
                        MainContent.Visibility = Visibility.Collapsed;
                    }

                    ResetScalingSnapshot();
                    ForceMonitorResizePulse(hwnd);
                    await Task.Delay(90);
                    RefreshScalingLayout($"RuntimeScaleReload:{reason}", forceMonitorResize: true);
                    CaptureCurrentLiveScaleState();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GCM] Soft UI reload for scale change failed: {ex}");
                }
                finally
                {
                    if (MainContent != null)
                    {
                        MainContent.Visibility = Visibility.Visible;
                    }

                    _isRuntimeScaleReloading = false;
                    completionSource.TrySetResult(true);
                }
            }))
            {
                return;
            }

            await completionSource.Task;
        }

        private void ResetScalingSnapshot()
        {
            _hasFrozenScalingSnapshot = false;
            _frozenLogicalWidth = 0;
            _frozenLogicalHeight = 0;
            _frozenPhysicalWidth = 0;
            _frozenPhysicalHeight = 0;
            _frozenDpiScale = 1.0;
        }

        private void ForceMonitorResizePulse(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !TryGetCurrentMonitorBounds(hwnd, out RECT bounds))
            {
                return;
            }

            int width = Math.Max(1, bounds.Right - bounds.Left);
            int height = Math.Max(1, bounds.Bottom - bounds.Top);
            int pulseWidth = Math.Max(1, width - 2);
            int pulseHeight = Math.Max(1, height - 2);
            uint flags = SWP_NOZORDER | 0x0010 | SWP_SHOWWINDOW | 0x0020;

            SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, pulseWidth, pulseHeight, flags);
            SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, width, height, flags);
        }

        private void CaptureScalingSnapshotIfNeeded()
        {
            if (_hasFrozenScalingSnapshot)
            {
                return;
            }

            if (!TryReadLiveLogicalViewport(out double logicalWidth, out double logicalHeight))
            {
                return;
            }

            GetLivePhysicalResolution(out int physicalWidth, out int physicalHeight);

            if (logicalWidth <= 0 || logicalHeight <= 0 || physicalWidth <= 0 || physicalHeight <= 0)
            {
                return;
            }

            _frozenLogicalWidth = logicalWidth;
            _frozenLogicalHeight = logicalHeight;
            _frozenPhysicalWidth = physicalWidth;
            _frozenPhysicalHeight = physicalHeight;
            _frozenDpiScale = GetLiveDpiScaleFactor();
            _hasFrozenScalingSnapshot = _frozenDpiScale > 0;

            Debug.WriteLine($"[GCM] Scaling snapshot captured: {_frozenPhysicalWidth}x{_frozenPhysicalHeight} @ {_frozenDpiScale:F2} -> logical {_frozenLogicalWidth:F0}x{_frozenLogicalHeight:F0}");
        }

        private bool ShouldFreezeRuntimeScalingFor(string reason)
        {
            return _freezeRuntimeScaling &&
                   _hasFrozenScalingSnapshot &&
                   !reason.StartsWith("RuntimeScaleReload:", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(reason, "InitialLoad", StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshScalingLayout(string reason, bool forceMonitorResize = true)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (hwnd != IntPtr.Zero && forceMonitorResize)
                {
                    TryResizeWindowToCurrentMonitor(hwnd);
                }

                if (ShouldFreezeRuntimeScalingFor(reason))
                {
                    if (_isShellUiReady && MainContent != null)
                    {
                        MainContent.Visibility = Visibility.Visible;
                    }

                    if (this.Content is FrameworkElement frozenRoot)
                    {
                        frozenRoot.InvalidateMeasure();
                        frozenRoot.UpdateLayout();
                    }

                    if (hwnd != IntPtr.Zero)
                    {
                        _lastCheckedDpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);

                        if (TryGetCurrentMonitorBounds(hwnd, out RECT frozenMonitorBounds))
                        {
                            _lastCheckedWidth = Math.Max(0, frozenMonitorBounds.Right - frozenMonitorBounds.Left);
                            _lastCheckedHeight = Math.Max(0, frozenMonitorBounds.Bottom - frozenMonitorBounds.Top);
                        }
                    }

                    Debug.WriteLine($"[GCM] Ignoring runtime scaling refresh via {reason}; keeping startup scale snapshot.");
                    return;
                }

                UpdateScale();
                ApplyResponsiveShellSizing(rebuildCards: true);
                CaptureScalingSnapshotIfNeeded();

                if (_isShellUiReady && MainContent != null)
                {
                    MainContent.Visibility = Visibility.Visible;
                }

                if (this.Content is FrameworkElement root)
                {
                    root.InvalidateMeasure();
                    root.UpdateLayout();
                }

                if (hwnd != IntPtr.Zero)
                {
                    _lastCheckedDpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);

                    if (TryGetCurrentMonitorBounds(hwnd, out RECT monitorBounds))
                    {
                        _lastCheckedWidth = Math.Max(0, monitorBounds.Right - monitorBounds.Left);
                        _lastCheckedHeight = Math.Max(0, monitorBounds.Bottom - monitorBounds.Top);
                    }
                }

                Debug.WriteLine($"[GCM] Scaling layout refreshed via {reason}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Scaling layout refresh failed via {reason}: {ex}");
            }
        }

        private bool TryGetCurrentMonitorBounds(IntPtr hwnd, out RECT bounds)
        {
            bounds = default;

            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            MONITORINFO monitorInfo = new MONITORINFO();
            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            bounds = monitorInfo.rcMonitor;
            return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
        }

        private void TryResizeWindowToCurrentMonitor(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (TryGetCurrentMonitorBounds(hwnd, out RECT bounds))
            {
                SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    bounds.Left,
                    bounds.Top,
                    Math.Max(1, bounds.Right - bounds.Left),
                    Math.Max(1, bounds.Bottom - bounds.Top),
                    SWP_NOZORDER | 0x0010 | SWP_SHOWWINDOW | 0x0020);
                return;
            }

            int pWidth = Vanara.PInvoke.User32.GetSystemMetrics(
                Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
            int pHeight = Vanara.PInvoke.User32.GetSystemMetrics(
                Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);

            if (pWidth > 0 && pHeight > 0)
            {
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, pWidth, pHeight, SWP_NOZORDER | 0x0010 | SWP_SHOWWINDOW | 0x0020);
            }
        }

        private void TryApplySuggestedDpiRect(IntPtr hwnd, IntPtr lParam)
        {
            if (hwnd == IntPtr.Zero || lParam == IntPtr.Zero)
            {
                return;
            }

            try
            {
                RECT suggestedRect = Marshal.PtrToStructure<RECT>(lParam);
                int width = Math.Max(1, suggestedRect.Right - suggestedRect.Left);
                int height = Math.Max(1, suggestedRect.Bottom - suggestedRect.Top);

                SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    suggestedRect.Left,
                    suggestedRect.Top,
                    width,
                    height,
                    SWP_NOZORDER | 0x0010 | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Failed to apply suggested DPI rect: {ex}");
            }
        }

        /* * Documentation:
  * ForceDpiRedraw uses native Win32 flags to force a complete window recalculation.
  * SWP_FRAMECHANGED (0x0020) tells Windows that the window's scaling context has changed
  * and it needs to discard its current DPI cache for this HWND.
  */


        private void ForceDpiRedraw()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                TryResizeWindowToCurrentMonitor(hwnd);
                RefreshScalingLayout("ForceDpiRedraw", forceMonitorResize: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ForceDpiRedraw Error] {ex.Message}");
            }
        }



        /* * Documentation:
         * UpdateScale calculates the uniform ratio based on the XamlRoot's effective size.
         * We use Math.Min to ensure the UI fits perfectly on any screen (e.g. 16:10 or 21:9).
         */
        /* * Documentation:
  * This helper retrieves the actual DPI scaling factor from the window handle.
  * For example, if Windows is set to 150%, this returns 1.5.
  */
        private double GetDpiScaleFactor()
        {
            if (_freezeRuntimeScaling && _hasFrozenScalingSnapshot)
            {
                return _frozenDpiScale;
            }

            return GetLiveDpiScaleFactor();
        }

        private double GetLiveDpiScaleFactor()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
            return dpi / 96.0;
        }

        private bool TryGetLogicalViewport(out double logicalWidth, out double logicalHeight)
        {
            logicalWidth = 0;
            logicalHeight = 0;

            if (_freezeRuntimeScaling && _hasFrozenScalingSnapshot)
            {
                logicalWidth = _frozenLogicalWidth;
                logicalHeight = _frozenLogicalHeight;
                return logicalWidth > 0 && logicalHeight > 0;
            }

            return TryReadLiveLogicalViewport(out logicalWidth, out logicalHeight);
        }

        private bool TryReadLiveLogicalViewport(out double logicalWidth, out double logicalHeight)
        {
            logicalWidth = 0;
            logicalHeight = 0;

            try
            {
                if (RootGrid?.XamlRoot != null)
                {
                    logicalWidth = RootGrid.XamlRoot.Size.Width;
                    logicalHeight = RootGrid.XamlRoot.Size.Height;
                }

                if (logicalWidth <= 0 || logicalHeight <= 0)
                {
                    logicalWidth = RootGrid?.ActualWidth ?? 0;
                    logicalHeight = RootGrid?.ActualHeight ?? 0;
                }

                if (logicalWidth > 0 && logicalHeight > 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scale] Failed to read logical viewport directly: {ex.Message}");
            }

            try
            {
                double dpiScale = GetDpiScaleFactor();
                if (dpiScale <= 0)
                {
                    return false;
                }

                int pWidth = Vanara.PInvoke.User32.GetSystemMetrics(
                    Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
                int pHeight = Vanara.PInvoke.User32.GetSystemMetrics(
                    Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);

                if (pWidth <= 0 || pHeight <= 0)
                {
                    return false;
                }

                logicalWidth = pWidth / dpiScale;
                logicalHeight = pHeight / dpiScale;
                return logicalWidth > 0 && logicalHeight > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scale] Failed to derive logical viewport from DPI: {ex.Message}");
                return false;
            }
        }

        /* * Documentation:
  * This method calculates the scaling ratio by comparing the logical viewport 
  * provided by WinUI (Effective Pixels) with our 1080p target design.
  * It uses Math.Min to ensure the UI fits on any screen aspect ratio (Uniform scaling).
  */
        private void RootGrid_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
        {
            if (MainContent == null) return;
            try
            {
                if (!TryGetLogicalViewport(out double logicalW, out double logicalH))
                {
                    return;
                }

                const double baseW = 1920.0;
                const double baseH = 1080.0;
                double ratio = Math.Min(logicalW / baseW, logicalH / baseH);
                if (ratio <= 0) return;

                MainContent.Width = baseW;
                MainContent.Height = baseH;
                MainContent.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center;
                MainContent.VerticalAlignment = VerticalAlignment.Center;
                MainContent.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

                if (MainContent.RenderTransform is not ScaleTransform scaleTransform)
                {
                    scaleTransform = new ScaleTransform();
                    MainContent.RenderTransform = scaleTransform;
                }

                scaleTransform.ScaleX = ratio;
                scaleTransform.ScaleY = ratio;

                GetPhysicalResolution(out int physicalWidth, out int physicalHeight);
                double dpiScale = GetDpiScaleFactor();
                Debug.WriteLine($"[Scale] {physicalWidth}x{physicalHeight} @ {dpiScale:F2} -> logical {logicalW:F0}x{logicalH:F0} -> ratio {ratio:F4}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Scale Error] {ex.Message}");
            }
        }



        private void UpdateScale()
        {
            RootGrid_SizeChanged(null, null);
        }

        #endregion

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        const int SM_CXSCREEN = 0; // Physische Breite
        const int SM_CYSCREEN = 1; // Physische Höhe


        private void GetPhysicalResolution(out int width, out int height)
        {
            if (_freezeRuntimeScaling && _hasFrozenScalingSnapshot)
            {
                width = _frozenPhysicalWidth;
                height = _frozenPhysicalHeight;
                return;
            }

            GetLivePhysicalResolution(out width, out height);
        }

        private void GetLivePhysicalResolution(out int width, out int height)
        {
            IntPtr hwnd = IntPtr.Zero;

            try
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            }
            catch
            {
            }

            if (TryGetCurrentMonitorBounds(hwnd, out RECT bounds))
            {
                width = Math.Max(1, bounds.Right - bounds.Left);
                height = Math.Max(1, bounds.Bottom - bounds.Top);
                return;
            }

            // Diese Methode liest die harten Pixel aus, keine skalierten Werte
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN);
        }
        private int _originalScreenWidth;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        // DPI Context Konstanten
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        private void TriggerAutomaticResync()
        {
            Debug.WriteLine("[GCM] Automatic scaling change detected. Queueing hard resync...");
            _lastKnownDpi = GetLiveDpiScaleFactor() * 96.0;

            _ = SoftReloadUiForScaleChangeAsync("TriggerAutomaticResync");
        }

        #endregion autoscaling
        #region window engine

        #region mouse engine

        private readonly HashSet<string> _autoMouseApps = new(StringComparer.OrdinalIgnoreCase)
{
    "Discord",
    "Spotify",
    "chrome",
    "opera gx",
    "opera",
    "Microsoft Edge",
    "moonlight"
};

        private DispatcherTimer _autoMouseTimer;
        private bool _wasAutoMouseActivated = false;

        // --- Optimierte AutoMouse Engine Logik ---
        private void AutoMouseEngine_Tick(object sender, object e)
        {
            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero) return;

            GetWindowThreadProcessId(fgHwnd, out uint pid);
            if (pid == 0) return;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string procName = proc.ProcessName;

                if (IsSteamControllerInputModeActive())
                {
                    if (_isMouseModeActive && _wasAutoMouseActivated)
                    {
                        _isMouseModeActive = false;
                        _wasAutoMouseActivated = false;

                        this.DispatcherQueue.TryEnqueue(() => ParkMouseCursor());
                    }

                    return;
                }

                bool isTargetApp = _autoMouseApps.Contains(procName);

                if (isTargetApp && !_isMouseModeActive)
                {
                    // App im Fokus (z.B. Discord) & Maus aus -> Aktivieren
                    _isMouseModeActive = true;
                    _wasAutoMouseActivated = true;

                    // FIX: Cursor explizit für Discord-Fenster sichtbar machen
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        // Cursor-Zähler von Windows erhöhen, bis er sichtbar ist (>= 0)
                        while (ShowCursor(true) < 0) ;
                        _isCursorVisible = true;

                        // Cursor aus der Park-Position holen und in die Mitte des Bildschirms setzen
                        // Dies triggert ein UI-Update im Ziel-Fenster (Discord)
                        SetCursorPos(GetScreenWidth() / 2, GetScreenHeight() / 2);

                        Debug.WriteLine($"[AutoMouse] Cursor forced visible for {procName}");
                        SendOverlayNotification($"Mouse Mode: Auto ({procName})");
                    });
                }
                else if (!isTargetApp && _isMouseModeActive && _wasAutoMouseActivated)
                {
                    // Ziel-App verlassen -> Deaktivieren
                    _isMouseModeActive = false;
                    _wasAutoMouseActivated = false;

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        ParkMouseCursor();
                        Debug.WriteLine("[AutoMouse] Cursor parked after leaving target app.");
                    });
                }
            }
            catch (Exception)
            {
                // Falls Prozess beendet wurde
            }
        }
        #endregion mouse engine

        // Enum for internal switching
        public enum ControllerType { Xbox, PlayStation, SteamController }

        private readonly object _steamLaunchGateSync = new();
        private bool _steamLaunchInProgress;
        private DateTime _lastSteamLaunchRequestUtc = DateTime.MinValue;

        private ControllerType ResolveControllerTypeForIndex(int controllerIndex)
        {
            if (controllerIndex == SteamDeckPuckControllerIndex)
            {
                return ControllerType.SteamController;
            }

            return IsPlayStationControllerIndex(controllerIndex) ? ControllerType.PlayStation : ControllerType.Xbox;
        }

        private bool IsSteamControllerInputModeActive()
        {
            return _hasObservedControllerInput && _lastActiveControllerType == ControllerType.SteamController;
        }

        private void DisableMouseModeForSteamController()
        {
            if (!_isMouseModeActive)
            {
                return;
            }

            _isMouseModeActive = false;
            _wasAutoMouseActivated = false;
            _mouseToggleLocked = false;
            ParkMouseCursor();
        }

        private void ApplyActiveControllerType(ControllerType controllerType)
        {
            bool typeChanged = !_hasObservedControllerInput || _lastActiveControllerType != controllerType;
            ControllerType previousType = _lastActiveControllerType;

            _hasObservedControllerInput = true;
            _lastActiveControllerType = controllerType;

            if (controllerType == ControllerType.SteamController)
            {
                DisableMouseModeForSteamController();
            }

            if (!typeChanged)
            {
                return;
            }

            if (controllerType == ControllerType.SteamController && previousType != ControllerType.SteamController)
            {
                SendOverlayNotification("Switching to Steam Controller mode...");
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isSettingsOverlayInitialized)
                {
                    RebuildVisibleSettingsRows();
                    RefreshSettingsOverlayValues();
                }

                UpdateVisualFocus();
            });
        }

        private bool TryAcquireUiActionGate(string gateKey, int cooldownMilliseconds)
        {
            lock (_uiActionGateSync)
            {
                DateTime now = DateTime.UtcNow;
                if (_uiActionLastTriggeredUtc.TryGetValue(gateKey, out var lastTriggeredUtc) &&
                    (now - lastTriggeredUtc).TotalMilliseconds < cooldownMilliseconds)
                {
                    return false;
                }

                _uiActionLastTriggeredUtc[gateKey] = now;
                return true;
            }
        }

        private bool TryBeginSteamLaunch()
        {
            lock (_steamLaunchGateSync)
            {
                DateTime now = DateTime.UtcNow;
                if (_steamLaunchInProgress)
                {
                    return false;
                }

                if ((now - _lastSteamLaunchRequestUtc).TotalMilliseconds < 1500)
                {
                    return false;
                }

                _steamLaunchInProgress = true;
                _lastSteamLaunchRequestUtc = now;
                return true;
            }
        }

        private void EndSteamLaunch()
        {
            lock (_steamLaunchGateSync)
            {
                _steamLaunchInProgress = false;
                _lastSteamLaunchRequestUtc = DateTime.UtcNow;
            }
        }
        // In MainWindow.cs, bei den anderen Klassenvariablen
        private DispatcherTimer _windowEngineTimer;
        private HashSet<IntPtr> _knownWindowHandles = new HashSet<IntPtr>();
        private bool _isEngineInitialized = false;
        private DateTime _appStartTime;
        // FÜGE DIESE KONSTANTEN WIEDER OBEN HINZU
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const long WS_THICKFRAME = 0x00040000L;
        #endregion window engine
        #region wingamepad
        private bool _isExiting = false; 
        #endregion wingamepad
        #region Startup Video
        private MediaPlayer _startupMediaPlayer;
        private bool startupVideoFinished = false;
        private bool _isVideoPlaybackInitiated = false;
        #endregion
        #region steamgriddb - picture for taskmanager

        private static void LogImageMapping(string message)
        {
            try
            {
                // Pfad exakt wie von dir gewünscht
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                              "gcmsettings", "image_cache", "image_mapping_log.txt");

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, logEntry);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Mapping Log Error: " + ex.Message);
            }
        }

        // Speichert welche Bilder wir bereits auf der Platte gefunden haben (Key -> Pfad)
        private Dictionary<string, string> _verifiedImageCache = new Dictionary<string, string>();
        // Zeitstempel, um den Cache alle 30 Sekunden mal zu aktualisieren
        private DateTime _lastCacheRefresh = DateTime.MinValue;

        

        private string FindCachedImageFile(string cleanName)
        {
            if (string.IsNullOrWhiteSpace(cleanName)) return null;

            // Nur alle 5 Sekunden wirklich auf die Festplatte schauen
            // Das verhindert das Ruckeln komplett, erkennt neue Bilder aber schnell genug.
            if ((DateTime.Now - _lastCacheRefresh).TotalSeconds > 5)
            {
                _verifiedImageCache.Clear();
                if (Directory.Exists(_imageCachePath))
                {
                    try
                    {
                        var files = Directory.GetFiles(_imageCachePath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(s => s.EndsWith(".jpg") || s.EndsWith(".png") || s.EndsWith(".webp") || s.EndsWith(".jpeg"));

                        foreach (var file in files)
                        {
                            string nameOnly = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                            if (!_verifiedImageCache.ContainsKey(nameOnly))
                                _verifiedImageCache.Add(nameOnly, file);
                        }
                    }
                    catch { }
                }
                _lastCacheRefresh = DateTime.Now;
            }

            // Blitzschneller RAM-Zugriff
            if (_verifiedImageCache.TryGetValue(cleanName.ToLowerInvariant(), out string cachedPath))
            {
                return cachedPath;
            }

            return null;
        }

        // Cache for the local name search
        private List<string> _steamLibraryPathsCache = null;
        private Dictionary<string, string> _localGameNameCache = new Dictionary<string, string>();

        public record SearchResult(int id, string name);
        public record SearchResponse(bool success, SearchResult[] data);
        public record ImageResult(string url);
        public record ImageResponse(bool success, ImageResult[] data);

        private readonly Dictionary<string, SearchResult> _gameIdCache = new(); // Geändert von int? zu SearchResult
        public SteamGridDBHelper _steamGridHelper;
        private readonly string _imageCachePath = Path.Combine(SettingsFolder, "image_cache");
        private readonly string _backdropCachePath = Path.Combine(SettingsFolder, "backdrop_cache");

        public class SteamGridDBHelper
        {
            private readonly HttpClient _httpClient = new();
            private readonly string _apiKey;

            private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // ÄNDERUNG: Dynamische Prüfung statt "true"
            public bool IsApiKeySet => !string.IsNullOrWhiteSpace(_apiKey);

            public SteamGridDBHelper(string apiKeyFromSettings)
            {
                // ÄNDERUNG: Wir nehmen den Key aus den Settings (oder null)
                _apiKey = apiKeyFromSettings?.Trim();

                _httpClient.DefaultRequestHeaders.Clear();

                // Nur Authorization-Header setzen, wenn ein Key da ist
                if (IsApiKeySet)
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }

                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GCM/1.0");
            }

            public async Task<List<string>> GetVerticalImagesForGameAsync(int gameId)
            {
                var urls = new List<string>();

                // ÄNDERUNG: Sofort abbrechen, wenn kein Key gesetzt ist
                if (!IsApiKeySet) return urls;

                try
                {
                    // "?dimensions=600x900" filtert für korrekte Cover-Größe
                    var response = await _httpClient.GetAsync($"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[SteamGridDB Images] Error {response.StatusCode}");
                        return urls;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var imageData = JsonSerializer.Deserialize<ImageResponse>(json, _jsonOptions);

                    if (imageData != null && imageData.success && imageData.data != null)
                    {
                        // Nimm die ersten 20 Ergebnisse
                        foreach (var img in imageData.data.Take(20))
                        {
                            urls.Add(img.url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SteamGridDB List Error] {ex.Message}");
                }
                return urls;
            }

            public async Task<List<string>> GetHeroImagesForGameAsync(int gameId)
            {
                var urls = new List<string>();

                if (!IsApiKeySet) return urls;

                try
                {
                    var response = await _httpClient.GetAsync($"https://www.steamgriddb.com/api/v2/heroes/game/{gameId}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[SteamGridDB Heroes] Error {response.StatusCode}");
                        return urls;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var imageData = JsonSerializer.Deserialize<ImageResponse>(json, _jsonOptions);

                    if (imageData != null && imageData.success && imageData.data != null)
                    {
                        foreach (var img in imageData.data.Take(20))
                        {
                            urls.Add(img.url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SteamGridDB Hero Error] {ex.Message}");
                }

                return urls;
            }

            public async Task<SearchResult> SearchForGameIdAsync(string gameName)
            {
                // ÄNDERUNG: Prüfung auf API Key
                if (!IsApiKeySet || string.IsNullOrWhiteSpace(gameName)) return null;

                try
                {
                    string encodedName = Uri.EscapeDataString(gameName);
                    var url = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{encodedName}";

                    Debug.WriteLine($"[SteamGridDB] Searching: {url}");

                    var response = await _httpClient.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        // Wir werfen hier keinen harten Fehler mehr, sondern loggen nur, um Abstürze zu vermeiden
                        Debug.WriteLine($"API Error: {response.StatusCode} - {errorContent}");
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var searchData = JsonSerializer.Deserialize<SearchResponse>(json, _jsonOptions);

                    if (searchData != null && searchData.success && searchData.data.Length > 0)
                    {
                        return searchData.data[0];
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SteamGridDB Search Error] {ex.Message}");
                    // throw; // Nicht werfen, damit die UI nicht crasht
                }
                return null;
            }

            // Alte Methode (leitet an die neue weiter)
            public async Task<string> GetGridImageUrlAsync(int gameId)
            {
                if (!IsApiKeySet) return null;
                try
                {
                    var list = await GetVerticalImagesForGameAsync(gameId);
                    return list.FirstOrDefault();
                }
                catch { return null; }
            }

            public async Task<string> GetHeroImageUrlAsync(int gameId)
            {
                if (!IsApiKeySet) return null;
                try
                {
                    var list = await GetHeroImagesForGameAsync(gameId);
                    return list.FirstOrDefault();
                }
                catch { return null; }
            }
        }

        // --- Helper to verify or create the cache directory ---
        private void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(_imageCachePath)) Directory.CreateDirectory(_imageCachePath);
        }

        // --- Helper to generate a clean filename for the cache ---
        private string GetCacheFileName(string gameName)
        {
            // Use your existing cleaner method
            string cleaned = CleanGameNameForSearch(gameName);

            // Replace characters that are invalid in file names
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                cleaned = cleaned.Replace(c, '_');
            }

            // Ensure the filename is not too long
            if (cleaned.Length > 50) cleaned = cleaned.Substring(0, 50);

            return Path.Combine(_imageCachePath, $"{cleaned}.jpg");
        }

        // --- Helper to load an image from disk into the UI thread safely ---
        // --- PERFORMANCE FIX: Bild beim Laden skalieren ---
        // --- PERFORMANCE FIX: Bild im Hintergrund laden & skalieren ---
        // --- FIX: Robustes Laden über MemoryStream ---
        private async Task LoadImageToUiAsync(
      Border card,
      Image imgControl,
      Image iconControl,
      TextBlock txtControl,
      string filePath,
      string titleOverride)
        {
            // Datei lesen (im Hintergrund)
            byte[] imageBytes;
            try
            {
                var info = new FileInfo(filePath);
                if (info.Length == 0) return; // Leere Dateien ignorieren

                imageBytes = await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageLoad] Fehler beim Lesen von {filePath}: {ex.Message}");
                return;
            }

            // UI Update (im Vordergrund)
            card.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.DecodePixelWidth = 512; // sharper cards without decoding full-size artwork

                        // WICHTIG: SetSourceAsync liest den Stream. Da ms im using ist, muss das await hier stehen.
                        await bitmap.SetSourceAsync(ms.AsRandomAccessStream());

                        imgControl.Source = bitmap;
                        imgControl.Stretch = Stretch.UniformToFill;

                        // Wenn Bild da ist -> Icon wegblenden
                        AnimateCrossFade(imgControl, iconControl);
                    }

                    if (txtControl != null && !string.IsNullOrEmpty(titleOverride))
                    {
                        txtControl.Text = titleOverride.ToUpper();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageLoad] Bild defekt oder Format falsch: {filePath} ({ex.Message})");
                    // Optional: Defekte Datei löschen, damit sie neu geladen wird
                    // try { File.Delete(filePath); } catch { }
                }
            });
        }
        private string GetSteamInstallPath()
        {
            try
            {
                // Try reading the 64-bit path first
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("InstallPath")?.ToString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                }

                // Fallback to the 32-bit path
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("InstallPath")?.ToString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            return path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetSteamInstallPath] Error reading registry: {ex.Message}");
            }
            return null;
        }

        // DEBUG-LOGGING-VERSION (ENGLISH) - V3 (Correct Manifest Path)
        private string GetSteamAppIdFromExePath(string exePath) // steamInstallPath parameter removed
        {
            Logger.Log($"[DEBUG] GetSteamAppIdFromExePath started.\n    EXE-Path: {exePath}");

            try
            {
                // 1. Get the game's directory from the exe path
                var gameDirectoryInfo = new DirectoryInfo(Path.GetDirectoryName(exePath));

                // 2. Check if it's actually in a "steamapps\common" folder.
                if (!gameDirectoryInfo.FullName.Contains(@"steamapps\common", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log("[DEBUG] GetSteamAppIdFromExePath: Path is not in 'steamapps\\common'. Aborting.");
                    return null; // Not a Steam game library
                }

                // ==================================================================
                // ### START OF FIX V3 ###
                // Find both the game install name AND the correct manifest folder

                string gameInstallName = null;
                DirectoryInfo manifestFolderDir = null;
                DirectoryInfo currentDir = gameDirectoryInfo; // e.g., .../Win64

                // Loop safeguard: max 10 levels up, and stop if we hit the drive root
                for (int i = 0; i < 10 && currentDir != null && currentDir.Parent != null; i++)
                {
                    // Is the PARENT "common"?
                    if (currentDir.Parent.Name.Equals("common", StringComparison.OrdinalIgnoreCase))
                    {
                        // Yes. So CURRENT is the game folder
                        gameInstallName = currentDir.Name; // "Days Gone"

                        // And the PARENT of "common" must be "steamapps"
                        if (currentDir.Parent.Parent != null && currentDir.Parent.Parent.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
                        {
                            manifestFolderDir = currentDir.Parent.Parent; // "D:\SteamLibrary\steamapps"
                            break; // Found everything!
                        }
                    }
                    currentDir = currentDir.Parent; // Move up
                }

                // 4. Check if we found both
                if (string.IsNullOrEmpty(gameInstallName) || manifestFolderDir == null)
                {
                    Logger.Log($"[DEBUG] GetSteamAppIdFromExePath: Could not find 'steamapps/common/GameName' structure. Aborting.");
                    return null;
                }

                string manifestFolder = manifestFolderDir.FullName;
                Logger.Log($"[DEBUG] GetSteamAppIdFromExePath: Found game folder name: '{gameInstallName}'");
                Logger.Log($"[DEBUG] GetSteamAppIdFromExePath: Scanning *correct* manifest folder: {manifestFolder}");

                // ### END OF FIX V3 ###
                // ==================================================================

                // 6. Search all appmanifest_*.acf files in that *correct* folder
                foreach (var file in Directory.EnumerateFiles(manifestFolder, "appmanifest_*.acf"))
                {
                    string content = File.ReadAllText(file);

                    // 7. Check if the manifest file points to our game's "installdir"
                    // Regex checks if the value *starts with* our game name
                    string pattern = $"\"installdir\"\\s+\"{Regex.Escape(gameInstallName)}[^\"]*\"";

                    if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                    {
                        // 8. If yes, extract the AppID from the filename
                        var match = Regex.Match(Path.GetFileName(file), @"appmanifest_(\d+)\.acf");
                        if (match.Success)
                        {
                            string foundAppId = match.Groups[1].Value;
                            Logger.Log($"[DEBUG] GetSteamAppIdFromExePath: SUCCESS!\n    File: {file}\n    AppID found: {foundAppId}");
                            return foundAppId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[DEBUG] GetSteamAppIdFromExePath: ERROR searching for AppID for {exePath}: {ex.Message}");
            }

            Logger.Log($"[DEBUG] GetSteamAppIdFromExePath: No AppID found for '{exePath}'.");
            return null; // Nothing found
        }

        private async Task<string> SearchCacheFolderForImageAsync(string appCacheDirectory)
        {
            if (!Directory.Exists(appCacheDirectory))
            {
                Logger.Log($"[DEBUG] SearchCache: Cache folder does not exist: {appCacheDirectory}");
                return null;
            }

            Logger.Log($"[DEBUG] SearchCache: Scanning for images in {appCacheDirectory}...");

            // ==================================================================
            // ### START OF FIX V5 (Prioritized Search) ###
            // ==================================================================

            string preferredImage = null;
            string fallbackImage = null;
            string librarySearchPattern = "library_*.jpg";
            string headerSearchPattern = "library_header.jpg";

            // Get all subdirectories (e.g., "0c47d1...")
            string[] subDirectories = await Task.Run(() => Directory.GetDirectories(appCacheDirectory));

            // --- PASS 1: Search ALL subfolders for the PREFERRED image (poster) ---
            foreach (string subDir in subDirectories)
            {
                string[] libraryFiles = await Task.Run(() => Directory.GetFiles(subDir, librarySearchPattern));
                if (libraryFiles.Length > 0)
                {
                    preferredImage = libraryFiles[0]; // Found the best type
                    Logger.Log($"[DEBUG] SearchCache: SUCCESS! (Pass 1) Found preferred poster in subfolder: {preferredImage}");
                    return preferredImage;
                }
            }

            // --- PASS 2: Search the ROOT folder for the PREFERRED image ---
            string[] rootLibraryFiles = await Task.Run(() => Directory.GetFiles(appCacheDirectory, librarySearchPattern));
            if (rootLibraryFiles.Length > 0)
            {
                preferredImage = rootLibraryFiles[0];
                Logger.Log($"[DEBUG] SearchCache: SUCCESS! (Pass 2) Found preferred poster in root: {preferredImage}");
                return preferredImage;
            }

            Logger.Log($"[DEBUG] SearchCache: (Pass 1 & 2) No preferred poster (library_*.jpg) found. Starting search for fallback (library_header.jpg)...");

            // --- PASS 3: Search ALL subfolders for the FALLBACK image (header) ---
            foreach (string subDir in subDirectories)
            {
                string localHeaderImage = Path.Combine(subDir, headerSearchPattern);
                if (await Task.Run(() => File.Exists(localHeaderImage)))
                {
                    fallbackImage = localHeaderImage; // Found a fallback
                    Logger.Log($"[DEBUG] SearchCache: WARNING! (Pass 3) No poster found. Returning fallback header from subfolder: {fallbackImage}");
                    return fallbackImage;
                }
            }

            // --- PASS 4: Search the ROOT folder for the FALLBACK image ---
            string rootHeaderImage = Path.Combine(appCacheDirectory, headerSearchPattern);
            if (await Task.Run(() => File.Exists(rootHeaderImage)))
            {
                fallbackImage = rootHeaderImage;
                Logger.Log($"[DEBUG] SearchCache: WARNING! (Pass 4) No poster found. Returning fallback header from root: {fallbackImage}");
                return fallbackImage;
            }

            // ### END OF FIX V5 ###
            // ==================================================================

            Logger.Log($"[DEBUG] SearchCache: No image found in root or subfolders.");
            return null;
        }

        private SteamArtworkPaths GetLocalSteamArtworkPaths(string steamAppId)
        {
            if (string.IsNullOrWhiteSpace(steamAppId))
            {
                return null;
            }

            if (_steamArtworkPathCache.TryGetValue(steamAppId, out SteamArtworkPaths cachedPaths))
            {
                return cachedPaths;
            }

            var artworkPaths = new SteamArtworkPaths();

            try
            {
                string steamInstallPath = GetSteamInstallPath();
                if (string.IsNullOrWhiteSpace(steamInstallPath))
                {
                    _steamArtworkPathCache[steamAppId] = artworkPaths;
                    return artworkPaths;
                }

                string libraryCacheRoot = Path.Combine(steamInstallPath, "appcache", "librarycache");
                if (!Directory.Exists(libraryCacheRoot))
                {
                    _steamArtworkPathCache[steamAppId] = artworkPaths;
                    return artworkPaths;
                }

                string appFolder = Path.Combine(libraryCacheRoot, steamAppId);
                string[] posterCandidates =
                {
                    Path.Combine(libraryCacheRoot, $"{steamAppId}_library_600x900.jpg"),
                    Path.Combine(appFolder, "library_600x900.jpg")
                };

                artworkPaths.PosterPath = posterCandidates.FirstOrDefault(File.Exists);
                if (string.IsNullOrWhiteSpace(artworkPaths.PosterPath) && Directory.Exists(appFolder))
                {
                    artworkPaths.PosterPath = Directory.EnumerateFiles(appFolder, "*600x900.jpg", SearchOption.AllDirectories).FirstOrDefault();
                }

                string[] heroCandidates =
                {
                    Path.Combine(appFolder, "library_hero.jpg"),
                    Path.Combine(appFolder, "library_hero_blur.jpg"),
                    Path.Combine(appFolder, "header.jpg"),
                    Path.Combine(libraryCacheRoot, $"{steamAppId}_library_hero.jpg"),
                    Path.Combine(libraryCacheRoot, $"{steamAppId}_hero.jpg"),
                    Path.Combine(libraryCacheRoot, $"{steamAppId}_library_header.jpg"),
                    Path.Combine(libraryCacheRoot, $"{steamAppId}_header.jpg")
                };

                artworkPaths.HeroPath = heroCandidates.FirstOrDefault(File.Exists);
                if (string.IsNullOrWhiteSpace(artworkPaths.HeroPath) && Directory.Exists(appFolder))
                {
                    artworkPaths.HeroPath =
                        Directory.EnumerateFiles(appFolder, "library_hero*.jpg", SearchOption.AllDirectories).FirstOrDefault() ??
                        Directory.EnumerateFiles(appFolder, "header.jpg", SearchOption.AllDirectories).FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(artworkPaths.HeroPath))
                {
                    artworkPaths.HeroPath = artworkPaths.PosterPath;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[DEBUG] GetLocalSteamArtworkPaths Error: {ex.Message}");
            }

            _steamArtworkPathCache[steamAppId] = artworkPaths;
            return artworkPaths;
        }

        private void PopulateLocalSteamArtworkPaths(ResolvedGameInfo gameInfo)
        {
            if (gameInfo == null || string.IsNullOrWhiteSpace(gameInfo.SteamAppId))
            {
                return;
            }

            SteamArtworkPaths artworkPaths = GetLocalSteamArtworkPaths(gameInfo.SteamAppId);
            if (artworkPaths == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(gameInfo.PosterImagePath))
            {
                gameInfo.PosterImagePath = artworkPaths.PosterPath;
            }

            if (string.IsNullOrWhiteSpace(gameInfo.HeroImagePath))
            {
                gameInfo.HeroImagePath = artworkPaths.HeroPath;
            }
        }


        private async Task<string> FindLocalSteamImageAsync(string exePath)
        {
            Logger.Log($"[DEBUG] FindLocalSteamImageAsync: Searching strict vertical cover for: {exePath}");
            try
            {
                string steamAppId = await Task.Run(() => GetSteamAppIdFromExePath(exePath));
                if (string.IsNullOrEmpty(steamAppId))
                {
                    return null;
                }

                SteamArtworkPaths artworkPaths = GetLocalSteamArtworkPaths(steamAppId);
                if (!string.IsNullOrWhiteSpace(artworkPaths?.PosterPath))
                {
                    Logger.Log($"[DEBUG] Success! Found official Steam vertical cover: {artworkPaths.PosterPath}");
                    return artworkPaths.PosterPath;
                }

                Logger.Log("[DEBUG] No local VERTICAL image found. (Ignoring banners to prevent distortion)");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"[DEBUG] FindLocalSteamImageAsync Error: {ex.Message}");
                return null;
            }
        }


        #endregion dsteamgriddb - picture for taskmanager
        #region trigger vibration
        // Triggers a short vibration on the gamepad for haptic feedback
        private async Task TriggerVibration(int controllerIndex, float intensity, int durationMs)
        {
            try
            {
                var controller = new Controller((UserIndex)controllerIndex);
                if (!controller.IsConnected) return;

                // Create vibration state (converting 0.0-1.0 to ushort 0-65535)
                var vibration = new Vibration
                {
                    LeftMotorSpeed = (ushort)(65535 * intensity),
                    RightMotorSpeed = (ushort)(65535 * intensity)
                };

                // Start vibration
                controller.SetVibration(vibration);

                // Wait for the specified duration without blocking the UI
                await Task.Delay(durationMs);

                // Stop vibration by sending zero intensity
                controller.SetVibration(new Vibration());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VibrationError] Could not trigger vibration: {ex.Message}");
            }
        }

        #endregion trigger vibration

        private List<Border> _launcherAreaButtons = new List<Border>();
        private List<ProcessData> _latestProcessData = new();
        private int _selectedLauncherAreaIndex = 0;

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);
        private CancellationTokenSource _taskbarHiderCts;
        #region focus
        private bool _isStartupGracePeriod = true;
        /// <summary>
        /// Richtet einen Timer ein, der regelmäßig prüft, ob das Fenster noch den Fokus hat.
        /// </summary>
        private void SetupFocusWatcher()
        {
            // Dieser Timer prüft regelmäßig (jede Sekunde), ob das Fenster den Fokus verloren hat.
            _focusCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _focusCheckTimer.Tick += _focusCheckTimer_Tick;
            _focusCheckTimer.Start();

            // Dieser Timer wird nur bei Bedarf gestartet und läuft nur einmal.
            _minimizeGracePeriodTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3) // 3 Sekunden Gnadenfrist
            };
            _minimizeGracePeriodTimer.Tick += MinimizeWindowAfterGracePeriod;

            _focusReturnWatchdogTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _focusReturnWatchdogTimer.Tick += async (s, e) => await FocusReturnWatchdog_TickAsync();
            _focusReturnWatchdogTimer.Start();
        }

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// This method is called after the 3-second grace period timer has elapsed.
        /// </summary>
        private void MinimizeWindowAfterGracePeriod(object sender, object e)
        {
            // Stoppe den Timer, da er nur einmal laufen soll.
            _minimizeGracePeriodTimer.Stop();

            // Letzte Sicherheitsprüfung: Ist das Fenster WIRKLICH immer noch im Hintergrund?
            if (!IsWindowInForeground())
            {
                Debug.WriteLine("Grace period ended and window is still not in focus. Minimizing now.");
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
                {
                    //overlappedPresenter.Minimize();
                }
            }
            else
            {
                Debug.WriteLine("Grace period ended, but window regained focus. No action taken.");
            }
        }

        private void AnimateOverlayOpacity(UIElement element, double toOpacity, bool hideWhenDone = false)
        {
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                To = toOpacity,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)), // 0.3 Sekunden für einen sanften Übergang
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);

            if (hideWhenDone)
            {
                storyboard.Completed += (s, e) => {
                    element.Visibility = Visibility.Collapsed;
                };
            }

            storyboard.Begin();
        }




        private void _focusCheckTimer_Tick(object sender, object e)
        {
            if (_isStartupGracePeriod) return;

            IntPtr foregroundHwnd = GetForegroundWindow();
            IntPtr selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            bool hasFocus = (foregroundHwnd == selfHwnd);

            // Nur umschalten, wenn sich der Status geändert hat! 
            // Dauerndes Setzen von Visibility/Play/Pause verursacht Ruckler.
            if (hasFocus != _lastFocusState)
            {
                _lastFocusState = hasFocus;
                var player = BackgroundVideoPlayer.MediaPlayer;

                if (hasFocus)
                {
                    player?.Play();
                    BackgroundVideoPlayer.Visibility = Visibility.Visible;
                    AnimateOverlayOpacity(FocusLossOverlay, 0.0, true);
                }
                else
                {
                    player?.Pause();
                    BackgroundVideoPlayer.Visibility = Visibility.Collapsed; // Sofort weg für GPU-Freigabe
                    FocusLossOverlay.Visibility = Visibility.Visible;
                    AnimateOverlayOpacity(FocusLossOverlay, 1.0);
                }
            }
        }

        private void ArmFocusReturnWatchdog(FocusReturnTarget target, TimeSpan holdDuration)
        {
            _focusReturnTarget = target;
            _focusReturnHoldUntilUtc = DateTime.UtcNow.Add(holdDuration);
            _shellFocusInterruptionArmed = false;
        }

        private void ClearFocusReturnWatchdog()
        {
            _focusReturnTarget = FocusReturnTarget.None;
            _focusReturnHoldUntilUtc = DateTime.MinValue;
            _shellFocusInterruptionArmed = false;
            _lastShellInterruptionSeenUtc = DateTime.MinValue;
        }

        private string TryGetForegroundProcessName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0)
                {
                    return string.Empty;
                }

                return Process.GetProcessById((int)pid).ProcessName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private IntPtr ResolveExpectedFocusHwnd(FocusReturnTarget target)
        {
            return target switch
            {
                FocusReturnTarget.GcmTaskManager => WinRT.Interop.WindowNative.GetWindowHandle(this),
                FocusReturnTarget.SteamBigPicture => _lastKnownSteamBigPictureHwnd != IntPtr.Zero
                    ? _lastKnownSteamBigPictureHwnd
                    : FindSteamBigPictureWindow(),
                FocusReturnTarget.GameWindow => _lastKnownGameHwnd,
                _ => IntPtr.Zero
            };
        }

        private bool HasExpectedForeground(FocusReturnTarget target, IntPtr expectedHwnd, IntPtr foregroundHwnd)
        {
            if (target == FocusReturnTarget.GcmTaskManager)
            {
                return IsWindowInForeground();
            }

            return expectedHwnd != IntPtr.Zero && foregroundHwnd == expectedHwnd;
        }

        private async Task<bool> RestoreExpectedFocusAsync()
        {
            if (_focusReturnTarget == FocusReturnTarget.GcmTaskManager)
            {
                await ForceGcmToFront();
                return IsWindowInForeground();
            }

            if (_focusReturnTarget == FocusReturnTarget.SteamBigPicture)
            {
                if (_steamBigPictureWasParkedByTaskManager)
                {
                    return await RestoreParkedSteamBigPictureAsync();
                }

                IntPtr steamHwnd = ResolveExpectedFocusHwnd(FocusReturnTarget.SteamBigPicture);
                if (steamHwnd == IntPtr.Zero)
                {
                    return false;
                }

                MakeSelfNonTopmost();
                ShowWindow(steamHwnd, SW_SHOW);
                ShowWindow(steamHwnd, SW_RESTORE);
                ShowWindow(steamHwnd, SW_SHOWMAXIMIZED);
                await Task.Delay(140);
                await ForcefullyBringToForeground(steamHwnd);
                return true;
            }

            if (_focusReturnTarget == FocusReturnTarget.GameWindow)
            {
                IntPtr gameHwnd = ResolveExpectedFocusHwnd(FocusReturnTarget.GameWindow);
                if (gameHwnd == IntPtr.Zero)
                {
                    return false;
                }

                MakeSelfNonTopmost();
                ShowWindow(gameHwnd, SW_SHOW);
                if (IsIconic(gameHwnd))
                {
                    ShowWindow(gameHwnd, SW_RESTORE);
                }
                await Task.Delay(140);
                await ForcefullyBringToForeground(gameHwnd);
                return GetForegroundWindow() == gameHwnd;
            }

            return false;
        }

        private async Task FocusReturnWatchdog_TickAsync()
        {
            if (_isStartupGracePeriod || _isRecoveringExpectedFocus || _focusReturnTarget == FocusReturnTarget.None)
            {
                return;
            }

            bool keepGcmTargetAlive = _focusReturnTarget == FocusReturnTarget.GcmTaskManager && _steamBigPictureWasParkedByTaskManager;
            if (!keepGcmTargetAlive && DateTime.UtcNow > _focusReturnHoldUntilUtc)
            {
                ClearFocusReturnWatchdog();
                return;
            }

            IntPtr foregroundHwnd = GetForegroundWindow();
            IntPtr expectedHwnd = ResolveExpectedFocusHwnd(_focusReturnTarget);

            if (HasExpectedForeground(_focusReturnTarget, expectedHwnd, foregroundHwnd))
            {
                _shellFocusInterruptionArmed = false;
                return;
            }

            string foregroundProcessName = TryGetForegroundProcessName(foregroundHwnd);
            bool isShellInterruption = _shellInterruptionProcessNames.Contains(foregroundProcessName);
            bool isNeutralReturnHost = string.IsNullOrWhiteSpace(foregroundProcessName) || _shellNeutralReturnHosts.Contains(foregroundProcessName);

            if (isShellInterruption)
            {
                _shellFocusInterruptionArmed = true;
                _lastShellInterruptionSeenUtc = DateTime.UtcNow;
                return;
            }

            if (!_shellFocusInterruptionArmed)
            {
                return;
            }

            if (!isNeutralReturnHost)
            {
                _shellFocusInterruptionArmed = false;
                return;
            }

            if (DateTime.UtcNow - _lastShellInterruptionSeenUtc < TimeSpan.FromMilliseconds(350))
            {
                return;
            }

            _shellFocusInterruptionArmed = false;
            _isRecoveringExpectedFocus = true;

            try
            {
                bool restored = await RestoreExpectedFocusAsync();
                if (restored && _focusReturnTarget == FocusReturnTarget.SteamBigPicture)
                {
                    _focusReturnHoldUntilUtc = DateTime.UtcNow.AddSeconds(15);
                }
            }
            finally
            {
                _isRecoveringExpectedFocus = false;
            }
        }

        private bool _lastFocusState = true;

        private enum FocusReturnTarget
        {
            None,
            GcmTaskManager,
            SteamBigPicture,
            GameWindow
        }

        private DispatcherTimer _focusCheckTimer;
        private DispatcherTimer _focusReturnWatchdogTimer;
        private DispatcherTimer _minimizeGracePeriodTimer;
        private bool _isOverlayActive = false;
        private IntPtr _parkedSteamBigPictureHwnd = IntPtr.Zero;
        private IntPtr _lastKnownSteamBigPictureHwnd = IntPtr.Zero;
        private IntPtr _lastKnownGameHwnd = IntPtr.Zero;
        private bool _steamBigPictureWasParkedByTaskManager = false;
        private FocusReturnTarget _focusReturnTarget = FocusReturnTarget.None;
        private DateTime _focusReturnHoldUntilUtc = DateTime.MinValue;
        private bool _shellFocusInterruptionArmed = false;
        private DateTime _lastShellInterruptionSeenUtc = DateTime.MinValue;
        private bool _isRecoveringExpectedFocus = false;
        private readonly HashSet<string> _shellInterruptionProcessNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "StartMenuExperienceHost",
            "SearchHost",
            "SearchApp",
            "ShellExperienceHost",
            "TextInputHost",
            "SystemSettings",
            "GameBar",
            "GameBarFTServer",
            "Widgets",
            "DisplaySwitch"
        };

        private readonly HashSet<string> _shellNeutralReturnHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "explorer",
            "sihost"
        };

        [DllImport("user32.dll")]
        private static extern bool LockSetForegroundWindow(uint uLockCode);
        /// <summary>
        /// Setzt das Hauptfenster in einen normalen Zustand (nicht "Immer im Vordergrund").
        /// </summary>
        private void MakeSelfNonTopmost()
        {
            IntPtr selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetWindowPos(selfHwnd, new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE); // HWND_NOTOPMOST
        }
        #endregion focus

        #region mouse

        private static bool _isCursorVisible = true;

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);
        private DispatcherTimer _mouseIdleTimer;
        private static bool _isProgrammaticMouseMovement = false;
        private const long WS_CAPTION = 0x00C00000L;


        // For checking the real maximized state by comparing window size to monitor size
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            public MONITORINFO()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
                rcMonitor = new RECT();
                rcWork = new RECT();
                dwFlags = 0;
            }
        }

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        /// <summary>
        /// Richtet den Timer und die Events ein, um den Cursor bei Inaktivität auszublenden.
        /// </summary>
        private void SetupMouseIdleBehavior()
        {
            _mouseIdleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _mouseIdleTimer.Tick += _mouseIdleTimer_Tick;

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.PointerMoved += RootElement_PointerMoved;
            }

            // Starte den Timer initial, um den Cursor nach 2 Sekunden auszublenden, falls keine Bewegung stattfindet.
            _mouseIdleTimer.Start();
        }

        /// <summary>
        /// Wird aufgerufen, wenn der Timer abläuft (2 Sekunden keine Bewegung).
        /// </summary>
        private void _mouseIdleTimer_Tick(object sender, object e)
        {
            // Verstecke den Cursor, wenn er sichtbar ist, und merke dir den Zustand.
            if (_isCursorVisible)
            {
                while (ShowCursor(false) >= 0) ; // Rufe so oft auf, bis der Zähler negativ ist
                _isCursorVisible = false;
            }
            _mouseIdleTimer.Stop();
        }

        /// <summary>
        /// Wird aufgerufen, sobald die Maus über dem Fenster bewegt wird.
        /// </summary>
        private void RootElement_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Ignoriere programmgesteuerte Mausbewegungen.
            if (_isProgrammaticMouseMovement) return;

            // Zeige den Cursor, wenn er versteckt ist, und merke dir den Zustand.
            if (!_isCursorVisible)
            {
                while (ShowCursor(true) < 0) ; // Rufe so oft auf, bis der Zähler >= 0 ist
                _isCursorVisible = true;
            }

            // Setze den Timer zurück.
            _mouseIdleTimer.Stop();
            _mouseIdleTimer.Start();
        }

        #endregion mouse

        private class ProcessData
        {
            public string ProductName { get; set; }
            public string ExePath { get; set; }
            public IntPtr Hwnd { get; set; }
            public Process Proc { get; set; }
            public ResolvedGameInfo GameInfo { get; set; }
        }
        // NEU: Eine kleine Klasse, um die Shortcut-Daten zu halten
        private class ShortcutData
        {
            public string Key1 { get; set; }
            public string Key2 { get; set; }
            public string Function { get; set; }
            public double HoldDuration { get; set; } // New: Seconds
            public bool Enabled { get; set; }
        }

        // Optimized runtime object to handle input logic without string parsing
        private class RuntimeShortcut
        {
            public GamepadButtonFlags RequiredButtons; // Bitmask of buttons that must be pressed
            public string FunctionName;
            public double HoldDurationSeconds;
            public string DisplayText;


            // State tracking per controller index (0-3 Xbox, 4+ PS)
            // We use array size 10 to be safe
            public DateTime[] HoldStartTimes = new DateTime[10];
            public bool[] HasTriggered = new bool[10];

            public RuntimeShortcut()
            {
                // Initialize arrays with default values
                for (int i = 0; i < 10; i++) HoldStartTimes[i] = DateTime.MaxValue;
            }
        }
        // The new main list for active shortcuts
        private List<RuntimeShortcut> _runtimeShortcuts = new();

        // Action mapping remains the same
        private Dictionary<string, System.Action> _shortcutActions = new();


        // Diese Variable speichert den Xbox-Prozess, damit wir ihn später überwachen können.
        private static Process monitoredXboxProcess = null;
        private int _selectedCardIndex = 0;
        private int _selectedGameOptionIndex = 0;
        private int _selectedButtonIndex = 0;
        private DispatcherTimer _taskRefreshTimer;
        private HashSet<GamepadButtonFlags> _pressedButtons = new();
        private static string startart = null;
        private const byte VK_F11 = 0x7A;
        //gamepad
        // Füge diese Deklarationen für die Gamepad-Steuerung hinzu, falls sie fehlen:

        // Die drei Fokus-Bereiche unserer App
        private enum FocusArea { Launcher, QuickLaunchers, Cards, TopButtons, PowerMenu, AppLauncher, AudioMenu, ImageSelection, GameOptions, StartupVideo, SettingsMenu, WindowsReturnConfirm, GithubReleasePrompt }
        private List<Border> _quickLauncherButtons = new List<Border>();
        private int _selectedQuickLauncherIndex = 0;
        private FocusArea _currentFocusArea = FocusArea.Cards;

        // Index und Liste für die oberen Buttons
        private int _selectedTopButtonIndex = 0;
        private List<Button> _topButtons = new List<Button>();
        private List<Button> _powerMenuItems = new List<Button>(); 
        private int _selectedPowerMenuItemIndex = 0; 



        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int GWLP_WNDPROC = -4;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_OVERLAPPEDWINDOW = 0x00CF0000L;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint WM_DISPLAYCHANGE = 0x007E;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint WM_DPICHANGED = 0x02E0;

        private delegate IntPtr WindowProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        #region TaskManager

        #region Discord
       

        private void StartDiscord()
        {
            // Versuche zuerst, ein laufendes Discord-Fenster in den Vordergrund zu bringen
            Process discordProcess = Process.GetProcessesByName("Discord").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (discordProcess != null)
            {
                TaskManagerBringWindowToForeground(discordProcess.MainWindowHandle);
                return;
            }

            // Wenn Discord nicht läuft, versuche es zu starten
            try
            {
                string discordPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord", "Update.exe");
                if (File.Exists(discordPath))
                {
                    Process.Start(new ProcessStartInfo(discordPath) { Arguments = "--processStart Discord.exe", UseShellExecute = true });
                }
                else
                {
                    Debug.WriteLine("Discord not found at the default location.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start Discord: {ex.Message}");
            }
        }
        #endregion discord
    
        private const int ASFW_ANY = -1; // Code für "Jeder darf nach vorne"

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
      
        private void TaskManagerBringWindowToForeground(IntPtr hWnd)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindow, out uint foregroundThread);
            GetWindowThreadProcessId(hWnd, out uint targetThread);

            if (foregroundThread != targetThread)
            {
                AttachThreadInput(targetThread, foregroundThread, true);
                SetForegroundWindow(hWnd);
                AttachThreadInput(targetThread, foregroundThread, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
            }

            BringWindowToTop(hWnd);
            ShowWindow(hWnd, SW_RESTORE);
        }


        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        private const uint GA_ROOTOWNER = 3;
        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;

        private static bool IsAltTabWindow(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd)) return false;

            IntPtr hwndTry = GetAncestor(hWnd, GA_ROOTOWNER);
            IntPtr hwndWalk = IntPtr.Zero;

            while ((hwndWalk = GetLastActivePopup(hwndTry)) != hwndWalk)
            {
                if (IsWindowVisible(hwndWalk)) break;
            }

            return hwndWalk == hWnd;
        }


        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        //alt Tab
        private bool _altTabCycleActive = false;
        private CancellationTokenSource _altTabTokenSource;

        #endregion TaskManager


        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        private const int HORZRES = 8; // Horizontal resolution
        private const int VERTRES = 10; // Vertical resolution
        private static StreamWriter logWriter;
        private static readonly string SettingsFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "gcmsettings"
);
private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.toml");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public MainWindow()
        {
           // Logger.Initialize();
            App.StartupTrace("MainWindow ctor begin");
            this.InitializeComponent();
            RootGrid.SizeChanged += RootGrid_SizeChanged;
            ApplySteamOnlyMode();
            MigrateThemeDefaultsIfNeeded();
            EnsureDefaultShortcuts();

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) =>
            {
                TriggerAutomaticResync();
            };

            // 2. Reagiert auf Änderungen des Skalierungs-Sliders (DPI)
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.Window ||
                    e.Category == Microsoft.Win32.UserPreferenceCategory.Desktop)
                {
                    TriggerAutomaticResync();
                }
            };

            // [BOOT LOGIK] START: Overlay auf "Boot-Modus" zwingen
            FocusLossOverlay.Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
            FocusLossOverlay.Opacity = 1.0;
            FocusLossOverlay.Visibility = Visibility.Visible;
            _isOverlayActive = true;

            string bootLogoPath = "ms-appx:///Assets/steam_logo.png";

            if (FocusLossOverlay.Child is Image logoImage)
            {
                logoImage.Source = new BitmapImage(new Uri(bootLogoPath));
                logoImage.Width = 150;
                logoImage.Height = 150;
            }

            EnsureVrrDisabledViaRegistry();
            SetupKeyboardAutoStartTask();

            ControllerLogger.InitializeLogs();
            perfectsettings();
            StartTaskbarHidingLoop();

            // Nativer WinUI 3 Event-Handler für den Start
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += (s, e) =>
                {
                    App.StartupTrace("Root element loaded.");
                    InstallNativeScalingHook();
                    SetupScalingEvents(rootElement);
                    RefreshScalingLayout("InitialLoad", forceMonitorResize: true);
                    StartRuntimeScaleMonitor();
                    FocusSink.Focus(FocusState.Programmatic);

                    if (!_isVideoPlaybackInitiated)
                    {
                        _isVideoPlaybackInitiated = true;
                        App.StartupTrace("Starting startup video flow.");
                        PlayStartupVideo();
                    }
                };
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _soundCache["nav"] = new Uri(Path.Combine(baseDir, "Assets\\nav.wav"));
            _soundCache["play"] = new Uri(Path.Combine(baseDir, "Assets\\play.wav"));
            _soundCache["pause"] = new Uri(Path.Combine(baseDir, "Assets\\pause.wav"));
            EnsureUiSoundPlayers();

            _autoMouseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _autoMouseTimer.Tick += AutoMouseEngine_Tick;
            _autoMouseTimer.Start();

            try
            {
                string apiKey = AppSettings.Load<string>("steamgriddb_api_key");
                Directory.CreateDirectory(_imageCachePath);
                Directory.CreateDirectory(_backdropCachePath);
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _steamGridHelper = new SteamGridDBHelper(null);
                }
                else
                {
                    _steamGridHelper = new SteamGridDBHelper(apiKey);
                }
            }
            catch { _steamGridHelper = new SteamGridDBHelper(null); }

            StartControllerBatteryMonitoring();
            MinimizeAllToDesktop();
            LoadDynamicLauncherCards();
            ApplyResponsiveShellSizing();
            _quickLauncherButtons = new List<Border>();
            QuickLauncherPanel.Visibility = Visibility.Collapsed;

            _topButtons = new List<Button> { ExitGcmButton, VolumeButton, SettingsButton, AppLauncherButton, ShutdownButton };
            _powerMenuItems = new List<Button> { SleepMenuItem, RestartMenuItem, ShutdownMenuItem, LogOffMenuItem };

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.UnhandledException += CurrentApp_UnhandledException;

            this.Activated += MainWindow_Activated;
            this.Activated += (s, e) => this.Content.Focus(FocusState.Programmatic);

            LoadShortcutsFromSettings();
            SetupGamepad();
            SetupStatusTimer();
            Start();

            ShowTaskManager();
            SetupFocusWatcher();
            SetupMouseIdleBehavior();
            StartStartupGracePeriodTimer();

            StartAsynctasks();
            _appStartTime = DateTime.UtcNow;
            SetupWindowEngine();

            ForceStartupVideoFullscreen();
            this.Activate();
            InstallNativeScalingHook();
            App.StartupTrace("MainWindow ctor completed.");
        }

        private void StartControllerBatteryMonitoring()
        {
            if (_controllerBatteryTimer != null)
            {
                return;
            }

            _controllerBatteryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _controllerBatteryTimer.Tick += (s, e) => UpdateControllerBatteryStatus();
            _controllerBatteryTimer.Start();
            UpdateControllerBatteryStatus();
        }

        private void StartStartupGracePeriodTimer()
        {
            var gracePeriodTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            gracePeriodTimer.Tick += (s, e) =>
            {
                gracePeriodTimer.Stop();
                _isStartupGracePeriod = false;

                AnimateOverlayOpacity(FocusLossOverlay, 0.0, true);
                _isOverlayActive = false;

                FocusLossOverlay.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(216, 0, 0, 0));
                if (FocusLossOverlay.Child is Image logoImage2)
                {
                    logoImage2.Source = new BitmapImage(new Uri("ms-appx:///Assets/gcm_ui_logo.png"));
                    logoImage2.Width = 200;
                    logoImage2.Height = 200;
                }
            };
            gracePeriodTimer.Start();
        }

        #region App Launcher Logic
        private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _appLauncherSearchText = AppSearchBox?.Text ?? string.Empty;
            RefreshAppLauncherView(keepSelection: true);
        }


        /// <summary>
        /// This event is triggered when the semi-transparent background of the App Launcher is clicked.
        /// </summary>
        private void AppLauncher_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // A click on the background should close the launcher.
            // We can simply call the same method that opens and closes it.
            ToggleAppLauncher_Click(sender, null); // Using null for RoutedEventArgs is fine here
        }

        /// <summary>
        /// This event is triggered when anything INSIDE the main content border is clicked.
        /// </summary>
        private void AppLauncherContent_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // By marking the event as "handled," we stop it from "bubbling up" to the parent Grid.
            // This prevents a click on the GridView from also being treated as a background click.
            e.Handled = true;
        }

        private async Task LoadInstalledAppsAsync()
        {
            Debug.WriteLine("[AppLauncher] Starting controller-first hybrid app scan...");
            isAppListLoaded = false;
            AllInstalledApps.Clear();
            VisibleAppLauncherApps.Clear();
            LoadAppLauncherFavorites();

            DispatcherQueue.TryEnqueue(() =>
            {
                AppLoadingRing.IsActive = true;
                AppLoadingRing.Visibility = Visibility.Visible;
                NoAppsFoundText.Visibility = Visibility.Collapsed;
                NoSearchResultsText.Visibility = Visibility.Collapsed;
                AppLauncherCountText.Text = "Scanning...";
            });

            var appData = await Task.Run(() =>
            {
                var discoveredApps = new List<AppDiscoveryInfo>();
                var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                ScanPackagedApps(discoveredApps, seenIds, seenNames);

                string[] startMenuPaths = {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
                };

                foreach (var path in startMenuPaths)
                {
                    ScanFolderForLnks(path, discoveredApps, seenIds, seenNames);
                }

                ScanRegistryApps(discoveredApps, seenIds, seenNames);

                return discoveredApps
                    .Where(app => !string.IsNullOrWhiteSpace(app.Name) && !string.IsNullOrWhiteSpace(app.LaunchTarget))
                    .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

            foreach (var app in appData)
            {
                string stableId = string.IsNullOrWhiteSpace(app.StableId)
                    ? BuildAppStableId(app.LaunchKind, app.LaunchTarget, app.Name)
                    : app.StableId;

                AllInstalledApps.Add(new AppInfo
                {
                    Name = app.Name,
                    FilePath = app.FilePath,
                    LaunchTarget = app.LaunchTarget,
                    LaunchKind = app.LaunchKind,
                    SourceLabel = app.SourceLabel,
                    StableId = stableId,
                    IsFavorite = _favoriteAppIds.Contains(stableId),
                    Icon = ResolveAppLauncherIcon(app)
                });
            }

            Debug.WriteLine($"[AppLauncher] Hybrid Scan finished. Total found: {AllInstalledApps.Count} applications.");
            isAppListLoaded = true;

            DispatcherQueue.TryEnqueue(() =>
            {
                AppLoadingRing.IsActive = false;
                AppLoadingRing.Visibility = Visibility.Collapsed;
                RefreshAppLauncherView();
            });
        }

        private void ScanPackagedApps(List<AppDiscoveryInfo> discoveredApps, HashSet<string> seenIds, HashSet<string> seenNames)
        {
            try
            {
                var packageManager = new PackageManager();
                foreach (var package in packageManager.FindPackagesForUser(string.Empty))
                {
                    IReadOnlyList<Windows.ApplicationModel.Core.AppListEntry> entries;
                    try
                    {
                        entries = package.GetAppListEntries();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        string aumid = entry.AppUserModelId;
                        string name = entry.DisplayInfo?.DisplayName;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = package.DisplayName;
                        }
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(aumid))
                        {
                            continue;
                        }

                        string stableId = BuildAppStableId("Packaged", aumid, name);
                        if (!seenIds.Add(stableId))
                        {
                            continue;
                        }

                        seenNames.Add(name);
                        discoveredApps.Add(new AppDiscoveryInfo
                        {
                            Name = name,
                            LaunchTarget = aumid,
                            FilePath = package.InstalledLocation?.Path ?? string.Empty,
                            LaunchKind = "Packaged",
                            SourceLabel = "MICROSOFT",
                            StableId = stableId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLauncher] Packaged app scan failed: {ex.Message}");
            }
        }

        private void ScanRegistryApps(List<AppDiscoveryInfo> discoveredApps, HashSet<string> seenIds, HashSet<string> seenNames)
        {
            string[] registryPaths =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            RegistryKey[] rootKeys = { Registry.CurrentUser, Registry.LocalMachine };
            foreach (RegistryKey rootKey in rootKeys)
            {
                foreach (string registryPath in registryPaths)
                {
                    try
                    {
                        using RegistryKey key = rootKey.OpenSubKey(registryPath);
                        if (key == null)
                        {
                            continue;
                        }

                        foreach (string subKeyName in key.GetSubKeyNames())
                        {
                            using RegistryKey appKey = key.OpenSubKey(subKeyName);
                            if (appKey == null)
                            {
                                continue;
                            }

                            string name = appKey.GetValue("DisplayName")?.ToString();
                            if (string.IsNullOrWhiteSpace(name) || ShouldSkipRegistryApp(name))
                            {
                                continue;
                            }

                            string target = ResolveRegistryLaunchTarget(appKey);
                            if (string.IsNullOrWhiteSpace(target))
                            {
                                continue;
                            }

                            string stableId = BuildAppStableId("Executable", target, name);
                            if (!seenIds.Add(stableId))
                            {
                                continue;
                            }

                            discoveredApps.Add(new AppDiscoveryInfo
                            {
                                Name = name,
                                LaunchTarget = target,
                                FilePath = target,
                                IconPath = target,
                                LaunchKind = "Executable",
                                SourceLabel = "DESKTOP",
                                StableId = stableId
                            });
                            seenNames.Add(name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AppLauncher] Registry scan failed at {rootKey.Name}\\{registryPath}: {ex.Message}");
                    }
                }
            }
        }

        private static bool ShouldSkipRegistryApp(string name)
        {
            string lowered = name.ToLowerInvariant();
            return lowered.Contains("update") ||
                   lowered.Contains("runtime") ||
                   lowered.Contains("redistributable") ||
                   lowered.Contains("driver") ||
                   lowered.Contains("sdk") ||
                   lowered.Contains("uninstall");
        }

        private static string ResolveRegistryLaunchTarget(RegistryKey appKey)
        {
            string displayIcon = appKey.GetValue("DisplayIcon")?.ToString();
            string fromIcon = NormalizeExecutablePath(displayIcon);
            if (!string.IsNullOrWhiteSpace(fromIcon) && File.Exists(fromIcon))
            {
                return fromIcon;
            }

            string installLocation = appKey.GetValue("InstallLocation")?.ToString();
            if (!string.IsNullOrWhiteSpace(installLocation))
            {
                string normalizedLocation = Environment.ExpandEnvironmentVariables(installLocation.Trim('"'));
                if (Directory.Exists(normalizedLocation))
                {
                    try
                    {
                        return Directory
                            .EnumerateFiles(normalizedLocation, "*.exe", SearchOption.TopDirectoryOnly)
                            .FirstOrDefault(file => !Path.GetFileName(file).Contains("unins", StringComparison.OrdinalIgnoreCase));
                    }
                    catch
                    {
                    }
                }
            }

            return string.Empty;
        }

        private static string NormalizeExecutablePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            string path = Environment.ExpandEnvironmentVariables(rawPath.Trim());
            if (path.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = path.IndexOf('"', 1);
                if (closingQuote > 1)
                {
                    path = path.Substring(1, closingQuote - 1);
                }
            }
            else
            {
                int commaIndex = path.LastIndexOf(',');
                if (commaIndex > 0)
                {
                    path = path[..commaIndex];
                }
            }

            return path.Trim('"', ' ');
        }

        private void ScanFolderForLnks(string folderPath, List<AppDiscoveryInfo> discoveredApps, HashSet<string> seenIds, HashSet<string> seenNames)
        {
            try
            {
                foreach (var lnkFile in Directory.GetFiles(folderPath, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(lnkFile);
                    if (string.IsNullOrWhiteSpace(name) || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string resolvedTarget = ResolveLnkShortcut(lnkFile);
                    string stableId = BuildAppStableId("Shortcut", lnkFile, name);
                    if (!seenIds.Add(stableId))
                    {
                        continue;
                    }

                    discoveredApps.Add(new AppDiscoveryInfo
                    {
                        Name = name,
                        LaunchTarget = lnkFile,
                        FilePath = lnkFile,
                        IconPath = !string.IsNullOrWhiteSpace(resolvedTarget) ? resolvedTarget : lnkFile,
                        LaunchKind = "Shortcut",
                        SourceLabel = "DESKTOP",
                        StableId = stableId
                    });
                    seenNames.Add(name);
                }

                foreach (var subFolderPath in Directory.GetDirectories(folderPath))
                {
                    ScanFolderForLnks(subFolderPath, discoveredApps, seenIds, seenNames);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine($"[AppLauncher] SKIPPED protected folder: {folderPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLauncher] Error scanning folder '{folderPath}': {ex.Message}");
            }
        }

        private string ResolveLnkShortcut(string lnkPath)
        {
            try
            {
                var shellLink = (IShellLinkW)new ShellLink();
                var persistFile = (IPersistFile)shellLink;
                persistFile.Load(lnkPath, 0);

                var sb = new StringBuilder(1024);
                shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private BitmapImage ResolveAppLauncherIcon(AppDiscoveryInfo app)
        {
            if (!string.IsNullOrWhiteSpace(app.IconPath))
            {
                BitmapImage icon = GetAppIconAsBitmapImage(app.IconPath);
                if (icon != null)
                {
                    return icon;
                }
            }

            return app.LaunchKind.Equals("Packaged", StringComparison.OrdinalIgnoreCase)
                ? new BitmapImage(new Uri("ms-appx:///Assets/windowsicon.png"))
                : new BitmapImage(new Uri("ms-appx:///Assets/game.png"));
        }

        private static string BuildAppStableId(string kind, string target, string name)
        {
            string identity = !string.IsNullOrWhiteSpace(target) ? target : name;
            return $"{kind}:{identity}".ToLowerInvariant();
        }

        private void LoadAppLauncherFavorites()
        {
            _favoriteAppIds.Clear();

            try
            {
                if (!File.Exists(AppLauncherFavoritesPath))
                {
                    return;
                }

                string json = File.ReadAllText(AppLauncherFavoritesPath);
                string[] ids = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
                foreach (string id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _favoriteAppIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLauncher] Favorite load failed: {ex.Message}");
            }
        }

        private void SaveAppLauncherFavorites()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppLauncherFavoritesPath));
                File.WriteAllText(
                    AppLauncherFavoritesPath,
                    JsonSerializer.Serialize(_favoriteAppIds.OrderBy(id => id), new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLauncher] Favorite save failed: {ex.Message}");
            }
        }

        private bool MatchesAppLauncherFilter(AppInfo app)
        {
            return _appLauncherFilter switch
            {
                AppLauncherFilter.Favorites => app.IsFavorite,
                AppLauncherFilter.Desktop => !app.LaunchKind.Equals("Packaged", StringComparison.OrdinalIgnoreCase),
                AppLauncherFilter.Microsoft => app.LaunchKind.Equals("Packaged", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        private void AppLauncherTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.Tag is string tag &&
                Enum.TryParse(tag, true, out AppLauncherFilter filter))
            {
                SetAppLauncherFilter(filter);
                PlayNavigationSound();
            }
        }

        private void SetAppLauncherFilter(AppLauncherFilter filter, bool keepSelection = false)
        {
            if (_appLauncherFilter == filter)
            {
                UpdateAppLauncherStatus();
                return;
            }

            _appLauncherFilter = filter;
            RefreshAppLauncherView(keepSelection);
        }

        private void RefreshAppLauncherView(bool keepSelection = false)
        {
            if (VisibleAppLauncherApps == null)
            {
                return;
            }

            string selectedId = keepSelection && AppGridView?.SelectedItem is AppInfo selectedApp
                ? selectedApp.StableId
                : string.Empty;

            string search = _appLauncherSearchText?.Trim() ?? string.Empty;
            var filtered = AllInstalledApps
                .Where(MatchesAppLauncherFilter)
                .Where(app => string.IsNullOrWhiteSpace(search) || app.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(app => app.IsFavorite)
                .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            VisibleAppLauncherApps.Clear();
            foreach (AppInfo app in filtered)
            {
                VisibleAppLauncherApps.Add(app);
            }

            if (AppGridView != null)
            {
                if (VisibleAppLauncherApps.Count == 0)
                {
                    AppGridView.SelectedIndex = -1;
                }
                else
                {
                    int selectedIndex = !string.IsNullOrWhiteSpace(selectedId)
                        ? VisibleAppLauncherApps.ToList().FindIndex(app => string.Equals(app.StableId, selectedId, StringComparison.OrdinalIgnoreCase))
                        : -1;
                    AppGridView.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
                    AppGridView.ScrollIntoView(AppGridView.SelectedItem);
                }
            }

            UpdateAppLauncherStatus();
        }

        private void UpdateAppLauncherStatus()
        {
            StyleAppLauncherTab(AppLauncherTabAllButton, _appLauncherFilter == AppLauncherFilter.All);
            StyleAppLauncherTab(AppLauncherTabFavoritesButton, _appLauncherFilter == AppLauncherFilter.Favorites);
            StyleAppLauncherTab(AppLauncherTabDesktopButton, _appLauncherFilter == AppLauncherFilter.Desktop);
            StyleAppLauncherTab(AppLauncherTabMicrosoftButton, _appLauncherFilter == AppLauncherFilter.Microsoft);

            if (AppLauncherCountText != null)
            {
                AppLauncherCountText.Text = $"{VisibleAppLauncherApps.Count}/{AllInstalledApps.Count} apps";
            }

            if (NoAppsFoundText != null)
            {
                NoAppsFoundText.Visibility = AllInstalledApps.Count == 0 && AppLoadingRing.Visibility != Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (NoSearchResultsText != null)
            {
                NoSearchResultsText.Visibility = AllInstalledApps.Count > 0 && VisibleAppLauncherApps.Count == 0 && AppLoadingRing.Visibility != Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void StyleAppLauncherTab(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.Background = new SolidColorBrush(isSelected
                ? Color.FromArgb(86, 255, 255, 255)
                : Color.FromArgb(28, 255, 255, 255));
            button.BorderBrush = new SolidColorBrush(isSelected
                ? Color.FromArgb(210, 255, 255, 255)
                : Color.FromArgb(42, 255, 255, 255));
            button.BorderThickness = new Thickness(isSelected ? 2 : 1);
            button.Foreground = new SolidColorBrush(isSelected
                ? Microsoft.UI.Colors.White
                : Color.FromArgb(210, 255, 255, 255));
        }

        private void CycleAppLauncherFilter(int direction = 1)
        {
            int currentIndex = Array.IndexOf(AppLauncherFilterOrder, _appLauncherFilter);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + direction + AppLauncherFilterOrder.Length) % AppLauncherFilterOrder.Length;
            SetAppLauncherFilter(AppLauncherFilterOrder[nextIndex]);
        }

        private void ToggleSelectedAppFavorite()
        {
            if (AppGridView?.SelectedItem is not AppInfo app || string.IsNullOrWhiteSpace(app.StableId))
            {
                return;
            }

            app.IsFavorite = !app.IsFavorite;
            if (app.IsFavorite)
            {
                _favoriteAppIds.Add(app.StableId);
            }
            else
            {
                _favoriteAppIds.Remove(app.StableId);
            }

            SaveAppLauncherFavorites();
            RefreshAppLauncherView(keepSelection: true);
        }

        #endregion


        #region window engine

        /// <summary>
        /// Prüft, ob ein Fenster maximiert werden kann (indem es nach dem Maximize-Button im Stil sucht).
        /// </summary>
        private bool CanWindowBeMaximized(IntPtr hWnd)
        {
            long style = (long)GetWindowLongPtr(hWnd, GWL_STYLE);
            return (style & WS_MAXIMIZEBOX) != 0;
        }

        /// <summary>
        /// Prüft anhand des Dateipfads, ob es sich wahrscheinlich um ein Spiel handelt.
        /// </summary>
        private bool IsLikelyGame(Process proc)
        {
            try
            {
                string exePath = proc.MainModule?.FileName;
                string windowTitle = proc.MainWindowTitle;
                return ResolveGameInfo(proc, windowTitle, exePath, proc.MainWindowHandle).IsGame;
            }
            catch { /* Zugriff verweigert, ignorieren */ }
            return false;
        }

        /// <summary>
        /// Erzwingt den randlosen Vollbildmodus (nur für Spiele verwenden).
        /// </summary>
        private void ForceWindowFullscreen(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            int screenWidth = GetScreenWidth();
            int screenHeight = GetScreenHeight();
            IntPtr style = GetWindowLongPtr(hwnd, GWL_STYLE);
            style = (IntPtr)((long)style & ~WS_CAPTION & ~WS_THICKFRAME);
            SetWindowLongPtr(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, HWND_TOP, 0, 0, screenWidth, screenHeight, 0x0020);
        }
        private void SetupWindowEngine()
        {
            _windowEngineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _windowEngineTimer.Tick += WindowEngine_Tick;
            _windowEngineTimer.Start();
        }

        /// <summary>
        /// Die Kernlogik der Window Engine, wird jede Sekunde ausgeführt.
        /// </summary>
        /// <summary>
        /// Die Kernlogik der Window Engine, wird jede Sekunde ausgeführt.
        /// </summary>
        private void WindowEngine_Tick(object sender, object e)
        {
            if (!_isShellUiReady)
            {
                return;
            }

            // Initialisierungs-Logik bleibt unverändert
            if (!_isEngineInitialized)
            {
                if ((DateTime.UtcNow - _appStartTime).TotalSeconds > 10)
                {
                    _knownWindowHandles = GetCurrentWindows();
                    _isEngineInitialized = true;
                    Debug.WriteLine($"[WindowEngine] Initialized with {_knownWindowHandles.Count} windows.");
                }
                return;
            }

            var currentWindows = GetCurrentWindows();
            var newWindows = currentWindows.Except(_knownWindowHandles).ToList();

            if (newWindows.Any())
            {
                IntPtr newWindowHwnd = newWindows.Last();

                try
                {
                    GetWindowThreadProcessId(newWindowHwnd, out uint pid);
                    if (pid == 0) return;

                    Process newProcess = Process.GetProcessById((int)pid);

                    // --- NEUE, INTELLIGENTE ENTSCHEIDUNGSLOGIK ---

                    // Fall 1: Ist es ein Spiel?
                    if (IsLikelyGame(newProcess))
                    {
                        Debug.WriteLine($"[WindowEngine] Game detected: {newProcess.ProcessName}. Forcing fullscreen.");
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            BringToFrontAndFocus(newWindowHwnd);
                            ForceWindowFullscreen(newWindowHwnd);
                            // Optional: GCM ausblenden, um maximale Performance zu geben
                            // ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(this), 0); 
                        });
                    }
                    // Fall 2: Ist es eine normale App, die Maximieren unterstützt?
                    else if (CanWindowBeMaximized(newWindowHwnd))
                    {
                        Debug.WriteLine($"[WindowEngine] App detected: {newProcess.ProcessName}. Maximizing.");
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            BringToFrontAndFocus(newWindowHwnd);
                            ShowWindow(newWindowHwnd, SW_SHOWMAXIMIZED);
                        });
                    }
                    // Fall 3: Es ist ein Dialog oder ein Fenster mit fester Größe.
                    else
                    {
                        Debug.WriteLine($"[WindowEngine] Dialog/Fixed window detected: {newProcess.ProcessName}. Bringing to front only.");
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            BringToFrontAndFocus(newWindowHwnd);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowEngine] Error processing new window, ignoring: {ex.Message}");
                }
            }
            _knownWindowHandles = currentWindows;
        }




        /// <summary>
        /// Sammelt alle relevanten, sichtbaren Hauptfenster.
        /// </summary>
        private HashSet<IntPtr> GetCurrentWindows()
        {
            var windows = new HashSet<IntPtr>();
            var selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            EnumWindows((hWnd, lParam) => {
                // Filtere unser eigenes Fenster, unsichtbare Fenster und "Kind"-Fenster heraus
                if (hWnd == selfHwnd || !IsWindowVisible(hWnd) || GetWindow(hWnd, (uint)GetWindowCmd.GW_OWNER) != IntPtr.Zero)
                    return true;

                // Filtere Fenster ohne Titel und spezielle "Tool"-Fenster heraus
                var style = (WindowStylesEx)GetWindowLong(hWnd, WindowLongFlags.GWL_EXSTYLE);
                if (GetWindowTextLength(hWnd) == 0 || style.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW) || IsCloaked(hWnd))
                    return true;

                windows.Add(hWnd);
                return true;
            }, IntPtr.Zero);

            return windows;
        }
        #endregion window engine

        #region uistatustimer
        private void SetupStatusTimer()
        {
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Aktualisiert alle 5 Sekunden
            };
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
            _statusUpdateTimer.Start();

            // Rufe die Aktualisierung einmal direkt beim Start auf
            StatusUpdateTimer_Tick(null, null);
        }

        /// <summary>
        /// Wird vom Timer alle 5 Sekunden aufgerufen, um die Status-Icons zu aktualisieren.
        /// </summary>
        private void StatusUpdateTimer_Tick(object sender, object e)
        {
            UpdateBatteryStatus();
            UpdateNetworkStatus();
        }

        /// <summary>
        /// Liest den Akku-Status aus und aktualisiert das Icon und die Prozentanzeige.
        /// Versteckt die Anzeige automatisch auf Desktop-PCs ohne Akku.
        /// </summary>
        private void UpdateBatteryStatus()
        {
            try
            {
                var batteryReport = Battery.AggregateBattery.GetReport();

                // Prüfen, ob überhaupt ein Akku vorhanden ist
                if (batteryReport.FullChargeCapacityInMilliwattHours == null ||
                    batteryReport.RemainingCapacityInMilliwattHours == null ||
                    batteryReport.Status == Windows.System.Power.BatteryStatus.NotPresent)
                {
                    BatteryIcon.Visibility = Visibility.Collapsed;
                    BatteryPercentageText.Visibility = Visibility.Collapsed;
                    return;
                }

                // Wenn ein Akku da ist, sorge dafür, dass die Elemente sichtbar sind
                BatteryIcon.Visibility = Visibility.Visible;
                BatteryPercentageText.Visibility = Visibility.Visible;

                double percentage = (double)batteryReport.RemainingCapacityInMilliwattHours.Value / batteryReport.FullChargeCapacityInMilliwattHours.Value * 100;
                BatteryPercentageText.Text = $"{Math.Round(percentage)}%";

                var isCharging = batteryReport.Status == Windows.System.Power.BatteryStatus.Charging;
                int percentageStep = (int)(Math.Round(percentage / 10.0)); // In 10er-Schritte für die Icons umrechnen

                // Wähle das passende Glyph aus der Segoe Fluent Icons Schriftart
                BatteryIcon.Glyph = (isCharging, percentageStep) switch
                {
                    (true, _) => "\uE83E",   // Lade-Symbol
                    (false, 10) => "\uE83F", // 100%
                    (false, 9) => "\uE83E",  // 90%
                    (false, 8) => "\uE83D",  // 80%
                    (false, 7) => "\uE83C",  // 70%
                    (false, 6) => "\uE83B",  // 60%
                    (false, 5) => "\uE83A",  // 50%
                    (false, 4) => "\uE839",  // 40%
                    (false, 3) => "\uE838",  // 30%
                    (false, 2) => "\uE837",  // 20%
                    (false, 1) => "\uE836",  // 10%
                    _ => "\uE835"           // 0% oder weniger
                };
            }
            catch (Exception ex)
            {
                // Fehler abfangen, falls die API aus irgendeinem Grund nicht verfügbar ist
                Debug.WriteLine($"[UpdateBatteryStatus] Error: {ex.Message}");
                BatteryIcon.Visibility = Visibility.Collapsed;
                BatteryPercentageText.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Prüft den Netzwerkstatus und wählt das passende Icon für WLAN, LAN oder keine Verbindung.
        /// </summary>
        private void UpdateNetworkStatus()
        {
            var connectionProfile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();

            if (connectionProfile == null || connectionProfile.GetNetworkConnectivityLevel() < NetworkConnectivityLevel.InternetAccess)
            {
                NetworkStatusIcon.Glyph = "\uF140"; // "Keine Verbindung"-Icon
                return;
            }

            if (connectionProfile.IsWlanConnectionProfile)
            {
                // Signalstärke für WLAN
                byte? signalBars = connectionProfile.GetSignalBars();
                NetworkStatusIcon.Glyph = signalBars switch
                {
                    4 or 5 => "\uE704", // 4 Balken (max)
                    3 => "\uE703",      // 3 Balken
                    2 => "\uE702",      // 2 Balken
                    1 => "\uE701",      // 1 Balken
                    _ => "\uE700"       // Kein Signal
                };
            }
            else if (connectionProfile.NetworkAdapter.IanaInterfaceType == 6) // IANA-Typ 6 ist "ethernetCsmacd"
            {
                NetworkStatusIcon.Glyph = "\uE839"; // Ethernet-Icon
            }
            else
            {
                NetworkStatusIcon.Glyph = "\uE774"; // Generisches "Netzwerk"-Icon für andere Verbindungen
            }
        }

        #endregion ui statustimer

        private void perfectsettings()
        {
            // Usewinpartstartapps
            try
            {
                AppSettings.Load<bool>("usewinpartstartapps");
            }
            catch
            {
                // if not set, set it to false
                AppSettings.Save("usewinpartstartapps", true);

            }
            //Shortcutpopups
            try
            {
                AppSettings.Load<bool>("shortcutpopup");
            }
            catch
            {
                // if not set, set it to false
                AppSettings.Save("shortcutpopup", true);

            }

            // Add setting for the taskbar toggle
            try
            {
                AppSettings.Load<bool>("enable_taskbar");
            }
            catch
            {
                // if not set, set it to false (Disabled)
                AppSettings.Save("enable_taskbar", false);
            }

            // Add setting for the start menu toggle
            try
            {
                AppSettings.Load<bool>("enable_startmenu");
            }
            catch
            {
                // if not set, set it to false (Disabled)
                AppSettings.Save("enable_startmenu", false);
            }

            DisableLoginOnWakeup();
        }

        #region mainwindow design
        #region mini launcher


        private void LoadAllLauncherSettings()
        {
            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    string exe = AppSettings.Load<string>($"button{i}link");
                    string icon = AppSettings.Load<string>($"button{i}image");

                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe) &&
                        !string.IsNullOrEmpty(icon) && File.Exists(icon))
                    {
                        AssignLauncherApp(i - 1, exe, icon); // Index 0–4
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Launcher {i}] skipped: {ex.Message}");
                    // Optional: you can log or ignore silently
                }
            }
        }



        private void AssignLauncherApp(int index, string exePath, string optionalIconPath = null)
        {
            // Ziel-Element finden
            Border tile = index switch
            {
                0 => LauncherTile0,
                1 => LauncherTile1,
                2 => LauncherTile2,
                3 => LauncherTile3,
                4 => LauncherTile4,
                _ => null
            };

            if (tile == null || !File.Exists(exePath))
                return;

            try
            {
                BitmapImage bitmap = null;

                // 1. Versuche benutzerdefiniertes Icon zu laden
                if (!string.IsNullOrEmpty(optionalIconPath) && File.Exists(optionalIconPath))
                {
                    bitmap = new BitmapImage(new Uri(optionalIconPath, UriKind.Absolute));
                }
                else
                {
                    // 2. Fallback: Icon aus .exe extrahieren
                    var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        using (var ms = new MemoryStream())
                        {
                            icon.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            ms.Position = 0;

                            bitmap = new BitmapImage();
                            bitmap.SetSource(ms.AsRandomAccessStream());
                        }
                    }
                }

                if (bitmap != null)
                {
                    // Image erzeugen
                    var image = new Image
                    {
                        Source = bitmap,
                        Width = 64,
                        Height = 64,
                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Kachelinhalt setzen
                    tile.Child = image;

                    // Klick zum Starten (inkl. Fokus-Korrektur)
                    tile.PointerPressed += (s, e) =>
                    {
                        MakeSelfNonTopmost(); // Stellt sicher, dass das Fenster den Fokus abgibt
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ Failed to launch {exePath}: {ex.Message}");
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Icon handling failed: {ex.Message}");
            }
        }
        #endregion mini launcher

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            TaskbarManager.RestoreOriginalState();
            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[DOMAIN EXCEPTION] {DateTime.Now}: {e.ExceptionObject}\n");

            // Removed BackToWindows() to prevent forceful app termination on background errors.
            // This allows us to read the log without the app instantly closing.
        }

        private void CurrentApp_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            TaskbarManager.RestoreOriginalState();

            // Mark the exception as handled so Windows doesn't kill the app.
            e.Handled = true;

            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[FATAL UI EXCEPTION] {DateTime.Now}: {e.Message}\n");

            // REMOVED BackToWindows()! 
            // If a small XAML binding fails, the app will now SURVIVE and build the UI anyway.
            Debug.WriteLine($"[UI Exception Handled to prevent crash] {e.Message}");
        }

        private void WifiButton_Click(object sender, RoutedEventArgs e)
        {
          
            // Öffnet die WLAN-Einstellungen
            Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true });
        }

        private void BluetoothButton_Click(object sender, RoutedEventArgs e)
        {
            // Öffnet die Bluetooth-Einstellungen
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }

        private DispatcherTimer _timer;
        private TimeSpan _elapsedTime;
        private void StartClock()
        {
            if (_hasClockStarted)
            {
                return;
            }

            _hasClockStarted = true;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timer.Tick += (sender, e) =>
            {
                // Zeigt die aktuelle Uhrzeit im Format HH:mm:ss
                ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            };

            _timer.Start();
        }
        #endregion mainwindow design

        private void StartTaskbarHidingLoop()
        {
            try
            {
                // Stop any previous loop if it exists
                _taskbarHiderCts?.Cancel();
                _taskbarHiderCts = new CancellationTokenSource();

                Task.Run(async () =>
                {
                    // *** ADD THIS CHECK ***
                    bool enableTaskbar = false;
                    try { enableTaskbar = AppSettings.Load<bool>("enable_taskbar"); } catch { }

                    if (enableTaskbar)
                    {
                        Debug.WriteLine("Taskbar is set to ENABLED. Skipping persistent hiding loop.");
                        // If taskbar is enabled, we must ensure it's visible.
                        TaskbarVisibility.ShowTaskbar();
                        return; // Exit the task
                    }
                    // *** END OF ADDITION ***

                    Debug.WriteLine("Starting persistent taskbar hiding loop...");
                    while (!_taskbarHiderCts.Token.IsCancellationRequested)
                    {
                        // Use your robust hide method
                        TaskbarVisibility.HideTaskbar();
                        try
                        {
                            // Check and hide 4 times per second.x
                            await Task.Delay(50, _taskbarHiderCts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            // This is expected when we stop the loop.
                            Debug.WriteLine("Taskbar hiding loop stopped.");
                            break; // Exit the while loop
                        }
                    }
                    Debug.WriteLine("Taskbar hiding loop task finished.");
                }, _taskbarHiderCts.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting taskbar hider: {ex.Message}");
            }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                // VRR / Schwarzbild Fix: Erzwinge Standard-Desktop-Rendering.
                int disableFullscreenTransform = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref disableFullscreenTransform, sizeof(int));
                int policy = DWMFLIP_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_FLIP3D_POLICY, ref policy, sizeof(int));

                uint currentDpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
                int currentWidth = Vanara.PInvoke.User32.GetSystemMetrics(Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
                int currentHeight = Vanara.PInvoke.User32.GetSystemMetrics(Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);

                if (!_isShellUiReady || MainContent == null || MainContent.Visibility != Visibility.Visible)
                {
                    _lastCheckedDpi = currentDpi;
                    _lastCheckedWidth = currentWidth;
                    _lastCheckedHeight = currentHeight;
                    App.StartupTrace("Fullscreen activation deferred until shell is ready.");
                    return;
                }

                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                bool scaleChanged = _lastCheckedDpi == 0 ||
                                    _lastCheckedDpi != currentDpi ||
                                    _lastCheckedWidth != currentWidth ||
                                    _lastCheckedHeight != currentHeight;

                if (scaleChanged)
                {
                    Debug.WriteLine("[GCM] Skalierungsänderung erkannt. Aktualisiere Layout ohne Presenter-Reset...");
                    _ = SoftReloadUiForScaleChangeAsync("MainWindow_Activated");
                }

                if (appWindow.Presenter.Kind != Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                {
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Activation/fullscreen handling failed: {ex.Message}");
                App.StartupTrace($"Activation/fullscreen handling failed: {ex}");
            }
        }

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private bool IsWindowInForeground()
        {
            IntPtr fgHwnd = GetForegroundWindow();
            IntPtr mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 1. Hat das Hauptfenster den Fokus?
            if (fgHwnd == mainHwnd) return true;

            // 2. NEU: Hat unser Shortcut-Overlay den Fokus?
            if (_globalShortcutOverlay != null)
            {
                IntPtr overlayHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_globalShortcutOverlay);
                if (fgHwnd == overlayHwnd) return true;
            }

            return false;
        }

        private int GetScreenWidth()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int width = GetDeviceCaps(hdc, HORZRES);
            ReleaseDC(IntPtr.Zero, hdc);
            return width;
        }
        private int GetScreenHeight()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int height = GetDeviceCaps(hdc, VERTRES);
            ReleaseDC(IntPtr.Zero, hdc);
            return height;
        }

        private DiscordSocketClient _client;

        #region Handhelds
        public static bool IsHandheld()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
                if (key != null)
                {
                    // Read known identifiers from BIOS
                    string family = key.GetValue("SystemFamily")?.ToString() ?? string.Empty;
                    string product = key.GetValue("SystemProductName")?.ToString() ?? string.Empty;
                    string sku = key.GetValue("SystemSKU")?.ToString() ?? string.Empty;

                    // Check original known handhelds
                    if (!string.IsNullOrEmpty(family))
                    {
                        if (family.Contains("ROG Ally", StringComparison.OrdinalIgnoreCase) ||
                            family.Contains("Claw", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    // Check against list of known handheld identifiers
                    string[] handheldIdentifiers = new[]
                    {
                "rog ally", "claw", "steam deck", "one-netbook", "onexplayer", "ayaneo"
            };

                    // Check all values for known handheld strings
                    foreach (string value in new[] { family, product, sku })
                    {
                        foreach (string id in handheldIdentifiers)
                        {
                            if (!string.IsNullOrEmpty(value) &&
                                value.Contains(id, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // fail silently if registry access fails
            }

            return false;
        }

        #endregion Handhelds
        #endregion needed
        #region methodes
        #region Image Selection & Instant Update Logic

        /// <summary>
        /// Opens the overlay to change the artwork for a specific card.
        /// Triggers an automatic search using the game's name.
        /// </summary>
        private void OpenGameOptions(ProgramCardEntry entry)
        {
            if (entry == null) return;

            _gameOptionsReturnFocusArea = _currentFocusArea == FocusArea.GameOptions
                ? FocusArea.Cards
                : _currentFocusArea;
            _currentEditingCardEntry = entry;
            _currentFocusArea = FocusArea.GameOptions;

            // UI Reset
            GameOptionsOverlay.Visibility = Visibility.Visible;
            GameOptionsMainPanel.Visibility = Visibility.Visible;
            ArtworkSearchPanel.Visibility = Visibility.Collapsed;

            // Fenstergröße anpassen (Hauptmenü ist klein, Suche ist groß)
            GameOptionsMenuBorder.Width = 500;
            GameOptionsMenuBorder.Height = double.NaN; // Auto-Height

            // Titel setzen
            GameOptionsSubtitle.Text = $"Selected: {entry.ProductName}";

            // Suspend-Status prüfen und Button anpassen
            bool canControlProcess = entry.Proc != null && !entry.Proc.HasExited;
            BtnSuspendGame.IsEnabled = canControlProcess;
            BtnSuspendGame.Opacity = canControlProcess ? 1.0 : 0.45;
            bool isSuspended = canControlProcess && ProcessSuspender.IsProcessSuspended(entry.Proc.Id);
            if (isSuspended)
            {
                TxtSuspendTitle.Text = "Resume Game";
                TxtSuspendDesc.Text = "Continue playing where you left off";
                IconSuspend.Glyph = "\uE768"; // Play Icon
            }
            else
            {
                TxtSuspendTitle.Text = "Suspend Game";
                TxtSuspendDesc.Text = canControlProcess ? "Freezes the game to save resources" : "Process is not available anymore";
                IconSuspend.Glyph = "\uE769"; // Pause Icon
            }

            // Fokus auf ersten Button
            BtnSuspendGame.Focus(FocusState.Programmatic);
            UpdateVisualFocus();
        }

        private void BtnSuspendGame_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditingCardEntry?.Proc == null || _currentEditingCardEntry.Proc.HasExited) return;

            // Logik abrufen: Schläft er schon?
            bool isSuspended = ProcessSuspender.IsProcessSuspended(_currentEditingCardEntry.Proc.Id);

            // Aktion ausführen (Gegenteil vom aktuellen Status)
            // true = freeze, false = resume
            ToggleGameSuspend(_currentEditingCardEntry.Proc, !isSuspended);

            // Visuelles Feedback
            if (!isSuspended) SendOverlayNotification("Game Suspended ❄");
            else SendOverlayNotification("Game Resumed ▶");

            // Menü schließen
            CloseGameOptions();
        }

        private void BtnChangeArtwork_Click(object sender, RoutedEventArgs e)
        {
            // Wechsel zur Artwork-Seite
            GameOptionsMainPanel.Visibility = Visibility.Collapsed;
            ArtworkSearchPanel.Visibility = Visibility.Visible;

            // Fenster vergrößern für die Bilder
            GameOptionsMenuBorder.Width = 1000;
            GameOptionsMenuBorder.Height = 700;

            // Suche starten
            ImageSearchBox.Text = _currentEditingCardEntry.GameInfo?.DisplayName ?? _currentEditingCardEntry.ProductName;
            ImageSearchButton_Click(null, null);
        }

        private void BtnBackToOptions_Click(object sender, RoutedEventArgs e)
        {
            // Zurück zum Hauptmenü
            ArtworkSearchPanel.Visibility = Visibility.Collapsed;
            GameOptionsMainPanel.Visibility = Visibility.Visible;

            GameOptionsMenuBorder.Width = 500;
            GameOptionsMenuBorder.Height = double.NaN;

            BtnChangeArtwork.Focus(FocusState.Programmatic);
        }

        private void CloseGameOptions()
        {
            GameOptionsOverlay.Visibility = Visibility.Collapsed;
            _currentFocusArea = _gameOptionsReturnFocusArea;
            _currentEditingCardEntry = null;
            UpdateVisualFocus();
        }

        private void GameOptionsOverlay_BackdropTapped(object sender, TappedRoutedEventArgs e)
        {
            CloseGameOptions();
        }


        /// <summary>
        /// Handles the search button click. Fetches covers from SteamGridDB, Steam, and open fallbacks.
        /// </summary>
        private async void ImageSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Content.XamlRoot == null) return;

            string searchTerm = ImageSearchBox.Text;
            if (string.IsNullOrWhiteSpace(searchTerm)) return;

            ImageSearchProgress.Visibility = Visibility.Visible;
            NoImagesFoundText.Visibility = Visibility.Collapsed;
            ImageResultsGrid.ItemsSource = null;

            try
            {
                string cleanedName = CleanGameNameForSearch(searchTerm);

                // Debug-Text während der Suche
                NoImagesFoundText.Text = $"Searching artwork for '{cleanedName}'...";
                NoImagesFoundText.Visibility = Visibility.Visible;

                var urls = await SearchArtworkUrlsAsync(cleanedName, _currentEditingCardEntry?.GameInfo);
                _currentImageSearchResults = urls;

                if (urls.Count > 0)
                {
                    ImageResultsGrid.ItemsSource = urls;
                    _selectedImageGridIndex = 0;
                    ImageResultsGrid.SelectedIndex = 0;
                    ImageResultsGrid.Focus(FocusState.Programmatic);
                    NoImagesFoundText.Visibility = Visibility.Collapsed;
                    return;
                }

                NoImagesFoundText.Text = $"No artwork found for '{cleanedName}'.";
                NoImagesFoundText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // HIER wird der echte Fehler angezeigt!
                NoImagesFoundText.Text = $"Artwork search error: {ex.Message}";
                NoImagesFoundText.Visibility = Visibility.Visible;
            }
            finally
            {
                ImageSearchProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<List<string>> SearchArtworkUrlsAsync(string searchTerm, ResolvedGameInfo gameInfo)
        {
            var urls = new List<string>();

            void AddUrl(string url)
            {
                if (!string.IsNullOrWhiteSpace(url) &&
                    !urls.Any(existing => existing.Equals(url, StringComparison.OrdinalIgnoreCase)))
                {
                    urls.Add(url);
                }
            }

            string cleanedName = CleanGameNameForSearch(searchTerm);
            if (string.IsNullOrWhiteSpace(cleanedName))
            {
                return urls;
            }

            if (!string.IsNullOrWhiteSpace(gameInfo?.SteamAppId))
            {
                foreach (string url in GetSteamArtworkCandidateUrls(gameInfo.SteamAppId))
                {
                    AddUrl(url);
                }
            }

            if (_steamGridHelper?.IsApiKeySet == true)
            {
                try
                {
                    var searchResult = await _steamGridHelper.SearchForGameIdAsync(cleanedName);
                    if (searchResult != null)
                    {
                        foreach (string url in await _steamGridHelper.GetVerticalImagesForGameAsync(searchResult.id))
                        {
                            AddUrl(url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ArtworkSearch] SteamGridDB failed: {ex.Message}");
                }
            }

            string steamStoreAppId = !string.IsNullOrWhiteSpace(gameInfo?.SteamAppId)
                ? gameInfo.SteamAppId
                : await TryFindSteamStoreAppIdAsync(cleanedName);

            if (!string.IsNullOrWhiteSpace(steamStoreAppId))
            {
                foreach (string url in GetSteamArtworkCandidateUrls(steamStoreAppId))
                {
                    AddUrl(url);
                }
            }

            foreach (string url in await SearchCheapSharkArtworkUrlsAsync(cleanedName))
            {
                AddUrl(url);
            }

            return urls.Take(30).ToList();
        }

        private IEnumerable<string> GetSteamArtworkCandidateUrls(string steamAppId)
        {
            if (string.IsNullOrWhiteSpace(steamAppId))
            {
                yield break;
            }

            string appId = steamAppId.Trim();
            string[] cdnRoots =
            {
                "https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps",
                "https://cdn.cloudflare.steamstatic.com/steam/apps",
                "https://cdn.akamai.steamstatic.com/steam/apps"
            };

            foreach (string root in cdnRoots)
            {
                yield return $"{root}/{appId}/library_600x900.jpg";
                yield return $"{root}/{appId}/library_600x900_2x.jpg";
                yield return $"{root}/{appId}/portrait.png";
                yield return $"{root}/{appId}/capsule_616x353.jpg";
                yield return $"{root}/{appId}/header.jpg";
            }
        }

        private async Task<string> TryFindSteamStoreAppIdAsync(string gameName)
        {
            string cleanedName = CleanGameNameForSearch(gameName);
            if (string.IsNullOrWhiteSpace(cleanedName))
            {
                return null;
            }

            if (_steamStoreSearchAppIdCache.TryGetValue(cleanedName, out string cachedAppId))
            {
                return cachedAppId;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GCM/1.0");

                string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(cleanedName)}&l=en&cc=US";
                string json = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("items", out JsonElement itemsElement) ||
                    itemsElement.ValueKind != JsonValueKind.Array)
                {
                    _steamStoreSearchAppIdCache[cleanedName] = null;
                    return null;
                }

                string bestAppId = null;
                double bestSimilarity = 0.0;

                foreach (JsonElement item in itemsElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("id", out JsonElement idElement) ||
                        !item.TryGetProperty("name", out JsonElement nameElement))
                    {
                        continue;
                    }

                    string itemName = nameElement.GetString();
                    double similarity = CalculateSimilarity(
                        CleanGameNameForSearch(cleanedName),
                        CleanGameNameForSearch(itemName));

                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestAppId = idElement.ToString();
                    }
                }

                if (bestSimilarity < 0.45)
                {
                    bestAppId = null;
                }

                _steamStoreSearchAppIdCache[cleanedName] = bestAppId;
                return bestAppId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ArtworkSearch] Steam store search failed: {ex.Message}");
                _steamStoreSearchAppIdCache[cleanedName] = null;
                return null;
            }
        }

        private async Task<List<string>> SearchCheapSharkArtworkUrlsAsync(string gameName)
        {
            var urls = new List<string>();
            string cleanedName = CleanGameNameForSearch(gameName);
            if (string.IsNullOrWhiteSpace(cleanedName))
            {
                return urls;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GCM/1.0");

                string url = $"https://www.cheapshark.com/api/1.0/games?title={Uri.EscapeDataString(cleanedName)}&limit=10";
                string json = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return urls;
                }

                foreach (JsonElement item in doc.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("external", out JsonElement titleElement) ||
                        !item.TryGetProperty("thumb", out JsonElement thumbElement))
                    {
                        continue;
                    }

                    string title = titleElement.GetString();
                    string thumb = thumbElement.GetString();
                    double similarity = CalculateSimilarity(
                        CleanGameNameForSearch(cleanedName),
                        CleanGameNameForSearch(title));

                    if (similarity >= 0.45 && !string.IsNullOrWhiteSpace(thumb))
                    {
                        urls.Add(thumb);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ArtworkSearch] CheapShark fallback failed: {ex.Message}");
            }

            return urls;
        }

        /// <summary>
        /// Handles clicking on an image in the grid.
        /// </summary>
        private void ImageResultsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string url)
            {
                DownloadAndApplyImage(url);
            }
        }

        /// <summary>
        /// Downloads the selected image, overwrites the cache file, and INSTANTLY refreshes the UI card.
        /// </summary>
        private async void DownloadAndApplyImage(string url)
        {
            if (_currentEditingCardEntry == null) return;

            ImageSearchProgress.Visibility = Visibility.Visible;

            try
            {
                await Task.Run(async () =>
                {
                    // Wir nutzen den Prozess-basierten Key zum Speichern!
                    string stableKey = GetStableCacheKey(
                        _currentEditingCardEntry.ProductName,
                        _currentEditingCardEntry.ExePath,
                        _currentEditingCardEntry.Proc);

                    string cachePath = Path.Combine(_imageCachePath, $"{stableKey}.jpg");

                    using (var client = new HttpClient())
                    {
                        var data = await client.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(cachePath, data);
                    }
                    InvalidateArtworkCacheLookup();

                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        RefreshCardImageVisuals(cachePath);
                        CloseImageSelectorButton_Click(null, null);
                        SendOverlayNotification("Artwork saved as: " + stableKey);
                        ImageSearchProgress.Visibility = Visibility.Collapsed;
                    });
                });
            }
            catch { ImageSearchProgress.Visibility = Visibility.Collapsed; }
        }

        /// <summary>
        /// Handles the "Browse Local" button to pick a file from disk.
        /// </summary>
        private async void BrowseLocalButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditingCardEntry == null) return;

            // 1. Fenster Handle holen
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // 2. Fenster kurz minimieren, damit der Dialog nicht verdeckt wird
            ShowWindow(hwnd, 6); // 6 = SW_MINIMIZE

            // Kurze Pause, damit die Animation durchläuft
            await Task.Delay(200);

            // 3. Den "Old School" Dialog starten (läuft im selben Thread, blockiert aber nicht die Logik)
            var dialog = new Win32OpenFileDialog();
            bool result = dialog.ShowDialog(hwnd);

            // 4. Fenster sofort wiederherstellen
            ShowWindow(hwnd, 9); // 9 = SW_RESTORE
            BringToFrontAndFocus(hwnd);

            if (result)
            {
                string sourcePath = dialog.FileName;

                if (!File.Exists(sourcePath)) return;

                ImageSearchProgress.Visibility = Visibility.Visible;

                // Verarbeitung im Hintergrund, damit die UI nicht einfriert
                await Task.Run(async () =>
                {
                    try
                    {
                        // A. Namen generieren (passend zur Scanner-Logik)
                        string stableKey = GetStableCacheKey(
                            _currentEditingCardEntry.ProductName,
                            _currentEditingCardEntry.ExePath,
                            _currentEditingCardEntry.Proc
                        );

                        string cachePath = Path.Combine(_imageCachePath, $"{stableKey}.jpg");

                        // B. Datei sicher kopieren (Stream verhindert Sperr-Fehler)
                        using (var sourceStream = File.OpenRead(sourcePath))
                        using (var memoryStream = new MemoryStream())
                        {
                            await sourceStream.CopyToAsync(memoryStream);
                            byte[] data = memoryStream.ToArray();
                            await File.WriteAllBytesAsync(cachePath, data);
                        }
                        InvalidateArtworkCacheLookup();

                        // C. UI aktualisieren
                        this.DispatcherQueue.TryEnqueue(() =>
                        {
                            RefreshCardImageVisuals(cachePath);
                            CloseImageSelectorButton_Click(null, null);
                            SendOverlayNotification("Artwork saved as: " + stableKey);
                            ImageSearchProgress.Visibility = Visibility.Collapsed;
                        });
                    }
                    catch (Exception ex)
                    {
                        this.DispatcherQueue.TryEnqueue(async () =>
                        {
                            ImageSearchProgress.Visibility = Visibility.Collapsed;
                            await messagebox($"Fehler beim Speichern: {ex.Message}");
                        });
                    }
                });
            }
        }

        // Füge das ganz am Ende der Datei ein, NACH der letzten geschweiften Klammer } von MainWindow
        public class Win32OpenFileDialog
        {
            [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            private struct OPENFILENAME
            {
                public int lStructSize;
                public IntPtr hwndOwner;
                public IntPtr hInstance;
                public string lpstrFilter;
                public string lpstrCustomFilter;
                public int nMaxCustFilter;
                public int nFilterIndex;
                public string lpstrFile;
                public int nMaxFile;
                public string lpstrFileTitle;
                public int nMaxFileTitle;
                public string lpstrInitialDir;
                public string lpstrTitle;
                public int Flags;
                public short nFileOffset;
                public short nFileExtension;
                public string lpstrDefExt;
                public IntPtr lCustData;
                public IntPtr lpfnHook;
                public string lpTemplateName;
                public IntPtr pvReserved;
                public int dwReserved;
                public int FlagsEx;
            }

            public string FileName { get; private set; }

            public bool ShowDialog(IntPtr owner)
            {
                var ofn = new OPENFILENAME();
                ofn.lStructSize = Marshal.SizeOf(ofn);
                ofn.hwndOwner = owner;
                ofn.lpstrFilter = "Bilder (JPG, PNG, WEBP)\0*.jpg;*.jpeg;*.png;*.webp\0Alle Dateien\0*.*\0";
                ofn.lpstrFile = new string(new char[256]);
                ofn.nMaxFile = ofn.lpstrFile.Length;
                ofn.lpstrFileTitle = new string(new char[64]);
                ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
                ofn.lpstrTitle = "Cover auswählen";
                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008; // Explorer-Style, FileMustExist, PathMustExist

                if (GetOpenFileName(ref ofn))
                {
                    FileName = ofn.lpstrFile;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Helper to forcefully refresh the image on the specific card currently being edited.
        /// This makes the change visible immediately without restart.
        /// </summary>
        private void RefreshCardImageVisuals(string newImagePath)
        {
            if (_currentEditingCardEntry == null || _currentEditingCardEntry.Card == null) return;

            // Find the Image controls inside the specific card's UI tree
            if (_currentEditingCardEntry.Card.Child is Grid grid)
            {
                var loadedImage = grid.Children.OfType<Image>().FirstOrDefault(i => i.Name == "LoadedImage");

                // Find the icon (which is usually the other image in the grid)
                var iconImage = grid.Children.OfType<Image>().FirstOrDefault(i => i.Source != loadedImage?.Source);

                if (loadedImage != null)
                {
                    // We call our robust loading method. 
                    // Since it opens the file as a stream, it reads the NEW content immediately.
                    // We pass 'null' as titleOverride to keep the existing title.
                    _ = LoadImageToUiAsync(_currentEditingCardEntry.Card, loadedImage, iconImage, null, newImagePath, null);
                }
            }
        }

        private void CloseImageSelectorButton_Click(object sender, RoutedEventArgs e)
        {
            GameOptionsOverlay.Visibility = Visibility.Collapsed;
            _currentFocusArea = _gameOptionsReturnFocusArea;
            _currentEditingCardEntry = null;
            UpdateVisualFocus(); // Refresh highlights
        }

        #endregion
        #region methodes for code

        public static void SendOverlayNotification(string message)
        {
            try
            {
                bool shortcutpopup = true;
                try { shortcutpopup = AppSettings.Load<bool>("shortcutpopup"); } catch { }

                if (shortcutpopup)
                {
                    try
                    {
                        string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "shortcut.wav");
                        if (System.IO.File.Exists(soundPath))
                        {
                            new System.Media.SoundPlayer(soundPath).Play();
                        }
                    }
                    catch { }
                }

                if (App.m_window != null)
                {
                    App.m_window.ShowInAppNotification(message);
                }
            }
            catch
            {
                AppSettings.Save("shortcutpopup", true);
            }
        }

        public void ShowInAppNotification(string message)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                string cleanMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
                _pendingStatusMessage = cleanMessage;

                EnsureSelectionSurfaceReferences();
                if (string.IsNullOrWhiteSpace(cleanMessage))
                {
                    HideBottomStatusPopup();
                    return;
                }

                if (_bottomStatusText == null || _bottomStatusPopup == null)
                {
                    return;
                }

                _bottomStatusText.Text = $"[{DateTime.Now:HH:mm:ss}] {cleanMessage}";
                ShowBottomStatusPopup();
            });
        }

        private void EnsureBottomStatusTimer()
        {
            if (_bottomStatusHideTimer != null)
            {
                return;
            }

            _bottomStatusHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4.5)
            };
            _bottomStatusHideTimer.Tick += (_, _) =>
            {
                _bottomStatusHideTimer.Stop();
                HideBottomStatusPopup();
            };
        }

        private void ShowBottomStatusPopup()
        {
            if (_bottomStatusPopup == null)
            {
                return;
            }

            EnsureBottomStatusTimer();
            _bottomStatusHideTimer.Stop();
            StopBottomStatusAnimations();

            _bottomStatusPopup.Visibility = Visibility.Visible;
            _bottomStatusPopup.Opacity = Math.Max(_bottomStatusPopup.Opacity, 0.05);

            if (_bottomStatusPopupTransform != null)
            {
                _bottomStatusPopupTransform.TranslateY = 14;
                _bottomStatusPopupTransform.ScaleX = 0.98;
                _bottomStatusPopupTransform.ScaleY = 0.98;
            }

            _bottomStatusShowStoryboard = CreateBottomStatusStoryboard(1.0, 0.0, 1.0, TimeSpan.FromMilliseconds(260), EasingMode.EaseOut);
            _bottomStatusShowStoryboard.Begin();
            _bottomStatusHideTimer.Start();
        }

        private void HideBottomStatusPopup()
        {
            if (_bottomStatusPopup == null || _bottomStatusPopup.Visibility != Visibility.Visible)
            {
                return;
            }

            _bottomStatusHideTimer?.Stop();
            StopBottomStatusAnimations();

            _bottomStatusHideStoryboard = CreateBottomStatusStoryboard(0.0, 12.0, 0.98, TimeSpan.FromMilliseconds(220), EasingMode.EaseIn);
            _bottomStatusHideStoryboard.Completed += (_, _) =>
            {
                if (_bottomStatusPopup != null && _bottomStatusPopup.Opacity <= 0.02)
                {
                    _bottomStatusPopup.Visibility = Visibility.Collapsed;
                }
            };
            _bottomStatusHideStoryboard.Begin();
        }

        private Storyboard CreateBottomStatusStoryboard(double opacity, double translateY, double scale, TimeSpan duration, EasingMode easingMode)
        {
            var storyboard = new Storyboard();
            var easing = new ExponentialEase
            {
                Exponent = 4,
                EasingMode = easingMode
            };

            AddBottomStatusAnimation(storyboard, _bottomStatusPopup, "Opacity", opacity, duration, easing);

            if (_bottomStatusPopupTransform != null)
            {
                AddBottomStatusAnimation(storyboard, _bottomStatusPopupTransform, "TranslateY", translateY, duration, easing);
                AddBottomStatusAnimation(storyboard, _bottomStatusPopupTransform, "ScaleX", scale, duration, easing);
                AddBottomStatusAnimation(storyboard, _bottomStatusPopupTransform, "ScaleY", scale, duration, easing);
            }

            return storyboard;
        }

        private void AddBottomStatusAnimation(Storyboard storyboard, DependencyObject target, string propertyPath, double to, TimeSpan duration, EasingFunctionBase easing)
        {
            if (storyboard == null || target == null)
            {
                return;
            }

            var animation = new DoubleAnimation
            {
                To = to,
                Duration = new Duration(duration),
                EasingFunction = easing
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyPath);
            storyboard.Children.Add(animation);
        }

        private void StopBottomStatusAnimations()
        {
            try { _bottomStatusShowStoryboard?.Stop(); } catch { }
            try { _bottomStatusHideStoryboard?.Stop(); } catch { }
        }


        private System.Threading.Tasks.Task messagebox(string dialog)
        {
            var messagebox = new ContentDialog
            {
                Title = "Information",
                Content = dialog,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            return messagebox.ShowAsync().AsTask(); // Rückgabe des Tasks
        }
        static string exeFolder()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string folderPath = Path.GetDirectoryName(exePath);
            return folderPath;
        }
        static void SetupLogging()
        {
            try
            {
                string logFilePath = Path.Combine(exeFolder(), "log.txt");
                File.Delete(logFilePath);
                logWriter = new StreamWriter(logFilePath, true) { AutoFlush = true };

                // Redirect standard output and error output
                Console.SetOut(logWriter);
                Console.SetError(logWriter);

                Console.WriteLine("Application started...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error setting up log: " + ex.Message);
            }
        }


        #endregion methodes for code
        #region functions
        #region boilr gamysync

        public string RunBoilrNoUI()
        {
            try
            {
                // Dynamisch AppData Pfad holen
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string boilrExePath = Path.Combine(appDataPath, "gcmsettings", "windows_BoilR.exe");

                Console.WriteLine("Checking for BoilR exe at:\n" + boilrExePath);

                if (!File.Exists(boilrExePath))
                {
                    Console.WriteLine("❌ BoilR.exe not found. Skipping execution.");
                    return "❌ BoilR.exe not found:\n" + boilrExePath;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = boilrExePath,
                    Arguments = "--no-ui",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("❌ BoilR error:\n" + error);
                        return "❌ BoilR error:\n" + error;
                    }

                    Console.WriteLine("✅ BoilR executed successfully with --no-ui.");
                    return "✅ BoilR executed successfully with --no-ui.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Error running BoilR:\n" + ex.Message);
                return "❌ Error running BoilR:\n" + ex.Message;
            }
        }
        #endregion boilr gamesync
        #region rog ally

        // not needed in Hybrid version
        private void allybuttonfix()
        {
            string audioSwitchExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "AudioSwitch", "AudioSwitch.exe");
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configSourcePath = Path.Combine(AppContext.BaseDirectory, "Settings.xml");
            string configTargetDir = Path.Combine(localAppData, "AudioSwitch");
            string configTargetPath = Path.Combine(configTargetDir, "Settings.xml");

            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string startupLink = Path.Combine(startupFolder, "AudioSwitch.lnk");

            try
            {
                bool alreadyInstalled = File.Exists(audioSwitchExe);
                string installerPath = Path.Combine(AppContext.BaseDirectory, "asforally.exe");

                if (!alreadyInstalled)
                {
                    if (!File.Exists(installerPath))
                    {
                       
                        return;
                    }

                    Process installer = new Process();
                    installer.StartInfo.FileName = installerPath;
                    installer.StartInfo.Arguments = "/verySilent";
                    installer.StartInfo.UseShellExecute = false;
                    installer.StartInfo.CreateNoWindow = true;

                    installer.Start();
                    installer.WaitForExit();
                }

                // After install or if already installed:

                // Delete startup shortcut if it exists
                if (File.Exists(startupLink))
                {
                    File.Delete(startupLink);
                }

                // Ensure target config directory exists
                Directory.CreateDirectory(configTargetDir);

                // Replace config (XML file)
                if (File.Exists(configSourcePath))
                {
                    File.Copy(configSourcePath, configTargetPath, true);
                }
                else
                {
                   
                }

                // Show final status
                if (alreadyInstalled)
                {

                }
                else
                {

                    //start
                    //Device is Rog ally
                    // check for Audio Button Software
                    // Define the path to the AudioSwitch executable
                    string exePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                            "AudioSwitch",
                            "AudioSwitch.exe"
                        );

                    // path to audioswitch settings
                    string settingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AudioSwitch",
                        "Settings.xml"
                    );

                    // Check if both the executable and the settings file exist
                    if (File.Exists(exePath) && File.Exists(settingsPath))
                    {
                        try
                        {
                            // Wait for 3 seconds before starting
                            System.Threading.Thread.Sleep(3000);

                            // Start the AudioSwitch executable
                            Process.Start(exePath);
                            Console.WriteLine("start AudioSwitch after newly install");
                        }
                        catch (Exception ex)
                        {
                            // Log any error while trying to start the process
                            Console.WriteLine("Failed to start AudioSwitch after install: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log any error while trying to start the process
                Console.WriteLine("Failed to start AudioSwitch after new install: " + ex.Message);
            }
        }
        #endregion rog ally

  
        public class PreloadAppEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string Arguments { get; set; }
            public bool StartHidden { get; set; }
        }

        // WICHTIG: Die Methode ist jetzt 'async Task', damit wir kurz warten können!
        public async Task prestartlist()
        {
            try
            {
                bool usePreloadList = AppSettings.Load<bool>("usepreloadlist");
                if (!usePreloadList)
                {
                    Debug.WriteLine("[PreloadList] Feature ist deaktiviert.");
                    return;
                }

                string jsonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "preloadapps.json");

                if (!File.Exists(jsonPath)) return;

                string json = File.ReadAllText(jsonPath);
                var appsToStart = JsonSerializer.Deserialize<List<PreloadAppEntry>>(json);

                if (appsToStart == null || appsToStart.Count == 0) return;

                foreach (var app in appsToStart)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(app.Path)) continue;

                        // --- FALL A: Weblinks ---
                        if (app.Path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            app.Path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            Process.Start(new ProcessStartInfo { FileName = app.Path, UseShellExecute = true });
                            continue;
                        }

                        // --- FALL B: Lokale Programme (.exe, .bat, .lnk) ---
                        if (File.Exists(app.Path))
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = app.Path,
                                Arguments = string.IsNullOrWhiteSpace(app.Arguments) ? "" : app.Arguments,
                                UseShellExecute = true
                            };

                            // Versuch der App vorher schon zu sagen, dass sie minimiert starten soll
                            if (app.StartHidden)
                            {
                                psi.WindowStyle = ProcessWindowStyle.Minimized;
                            }

                            Process p = Process.Start(psi);

                            // --- DIE MAGIE: Warten & Hart Minimieren ---
                            if (app.StartHidden && p != null)
                            {
                                // Wir warten bis zu 3 Sekunden, ob die App ein Fenster erstellt
                                int retries = 0;
                                while (p.MainWindowHandle == IntPtr.Zero && retries < 30)
                                {
                                    await Task.Delay(100);
                                    p.Refresh();
                                    retries++;
                                }

                                // Wenn ein Fenster da ist -> Ab in den Hintergrund damit!
                                if (p.MainWindowHandle != IntPtr.Zero)
                                {
                                    // 7 = SW_SHOWMINNOACTIVE (Minimieren OHNE den Fokus zu klauen!)
                                    ShowWindow(p.MainWindowHandle, 7);
                                    Debug.WriteLine($"[PreloadList] {app.Name} wurde in den Hintergrund gezwungen.");
                                }

                                // GCM sofort wieder dominant in den Vordergrund holen!
                                await ForceGcmToFront();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PreloadList] Fehler bei '{app.Name}': {ex.Message}");
                    }
                }

                // Am Ende zur absoluten Sicherheit: GCM nochmal nach ganz vorne holen
                await ForceGcmToFront();
                Debug.WriteLine("[PreloadList] Alle Apps erfolgreich abgearbeitet.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PreloadList] Kritischer Fehler: {ex.Message}");
            }
        }
        public static void preaudio(bool start,bool end)
        {
            try
            {
                bool preaudio = AppSettings.Load<bool>("usepreaudio");
                if (preaudio == true)
                {
                    if (end == true)
                    {
                        string preaudioend = AppSettings.Load<string>("preaudioend");
                        NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{preaudioend}\"");
                    }
                    else if (start == true)
                    {
                        string preaudiostart = AppSettings.Load<string>("preaudiostart");
                        NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{preaudiostart}\"");
                    }
                }
                else
                {
                    Console.WriteLine("no preaudio set");
                }
            }
            catch
            {
                Console.WriteLine("no preaudio set or problem");
            }
        }

        private static bool IsAlreadyRunning()
        {
            string currentProcessName = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(currentProcessName);
            return processes.Length > 1;
        }
        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        #region Hybrid Wallpaper Logic (Live & Static)

        private void SetBackgroundImage(int width, int height)
        {
            try
            {
                string rawPath = "";
                bool useGcmWallpaper = false;

                // 1. Einstellung laden mit Fehlerprüfung
                try
                {
                    useGcmWallpaper = AppSettings.Load<bool>("gcmwallpaper");
                }
                catch { useGcmWallpaper = false; }

                if (useGcmWallpaper)
                {
                    try { rawPath = AppSettings.Load<string>("gcmwallpaperpath"); } catch { rawPath = ""; }
                }

                // 2. FALLBACK auf Windows-Standard, falls GCM-Pfad leer oder Datei nicht existiert
                if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath.Trim('"').Trim()))
                {
                    rawPath = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", "") as string;
                    Debug.WriteLine("[Wallpaper] Nutze Windows Fallback: " + rawPath);
                }

                // 3. Letzte Prüfung: Wenn auch Windows-Pfad leer oder ungültig, Abbruch vor Crash
                if (string.IsNullOrEmpty(rawPath))
                {
                    Debug.WriteLine("[Wallpaper] Kein Wallpaper gefunden. Nutze leeren Hintergrund.");
                    StopLiveWallpaper();
                    if (BackgroundImage != null)
                    {
                        BackgroundImage.Source = null;
                    }
                    return;
                }

                string cleanPath = rawPath.Trim('"').Trim();

                // Falls Datei trotz Registry-Eintrag nicht existiert
                if (!File.Exists(cleanPath))
                {
                    Debug.WriteLine("[Wallpaper] Datei existiert nicht: " + cleanPath);
                    return;
                }

                // 4. WEICHE: Video oder Bild
                string extension = Path.GetExtension(cleanPath).ToLower();
                string[] videoExtensions = { ".mp4", ".webm", ".mkv", ".mov", ".wmv", ".avi" };

                if (videoExtensions.Contains(extension))
                {
                    Debug.WriteLine("[Wallpaper] Starte Live-Video: " + cleanPath);
                    SetupLiveWallpaper(cleanPath);
                }
                else
                {
                    Debug.WriteLine("[Wallpaper] Setze statisches Bild: " + cleanPath);
                    StopLiveWallpaper();
                    _ = ApplyStaticBackgroundImageAsync(cleanPath, Math.Max(width, height));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wallpaper Critical Error] {ex.Message}");
                App.StartupTrace($"Wallpaper critical error: {ex}");
            }
        }

        private async Task ApplyStaticBackgroundImageAsync(string cleanPath, int targetDecodeSize)
        {
            int loadVersion = Interlocked.Increment(ref _backgroundImageLoadVersion);
            byte[] imageBytes;

            try
            {
                var info = new FileInfo(cleanPath);
                if (!info.Exists || info.Length <= 0)
                {
                    App.StartupTrace($"Wallpaper skipped because file is empty or missing: {cleanPath}");
                    return;
                }

                imageBytes = await File.ReadAllBytesAsync(cleanPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Wallpaper] Fehler beim Lesen des Bildes: " + ex.Message);
                App.StartupTrace($"Wallpaper read failed: {ex}");
                return;
            }

            DispatcherQueue.TryEnqueue(async () =>
            {
                if (loadVersion != _backgroundImageLoadVersion || BackgroundImage == null)
                {
                    return;
                }

                try
                {
                    var bitmap = new BitmapImage
                    {
                        DecodePixelWidth = Math.Clamp(targetDecodeSize, 1920, 4096)
                    };

                    using (var ms = new MemoryStream(imageBytes))
                    {
                        await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                    }

                    if (loadVersion != _backgroundImageLoadVersion)
                    {
                        return;
                    }

                    BackgroundImage.Source = bitmap;
                    BackgroundImage.Visibility = Visibility.Visible;

                    if (BackgroundVideoPlayer != null)
                    {
                        BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                    }

                    App.StartupTrace("Wallpaper static image applied.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[Wallpaper] Fehler beim Laden des Bildes: " + ex.Message);
                    App.StartupTrace($"Wallpaper static image failed: {ex}");
                    try { BackgroundImage.Source = null; } catch { }
                }
            });
        }

        private void SetupLiveWallpaper(string videoPath)
        {
            try
            {
                StopLiveWallpaper();

                var player = new Windows.Media.Playback.MediaPlayer();
                _backgroundWallpaperPlayer = player;

                // OPTIMIERUNG 1: Video-Eigenschaften für Performance setzen
                player.IsVideoFrameServerEnabled = false; // Wir brauchen keinen Zugriff auf einzelne Frames
                player.AudioCategory = MediaPlayerAudioCategory.Other;

                // OPTIMIERUNG 2: Hardware-Dekodierung bevorzugen
                // Das entlastet die CPU, auf der auch dein Fenster-Scanner läuft
                player.Source = MediaSource.CreateFromUri(new Uri(videoPath, UriKind.Absolute));

                player.IsLoopingEnabled = true;
                player.IsMuted = true;

                if (BackgroundVideoPlayer != null)
                {
                    BackgroundVideoPlayer.SetMediaPlayer(player);
                    BackgroundVideoPlayer.Visibility = Visibility.Visible;
                }

                // WICHTIG: Den Player-Typ auf 'Hardware' zwingen (über das UI Element)
                if (BackgroundVideoPlayer != null)
                {
                    BackgroundVideoPlayer.Opacity = 1.0;
                }

                player.Play();
            }
            catch (Exception ex) { Debug.WriteLine($"[Wallpaper] Error: {ex.Message}"); }
        }

        private void StopLiveWallpaper()
        {
            try
            {
                var player = _backgroundWallpaperPlayer;
                if (player != null)
                {
                    player.Pause();
                    if (BackgroundVideoPlayer != null)
                    {
                        BackgroundVideoPlayer.SetMediaPlayer(null);
                        BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                    }
                    player.Dispose();
                    _backgroundWallpaperPlayer = null;
                }
            }
            catch { }
        }

      

        #endregion

        private string Settwallpaper()
        {
            try
            {
                bool gcmwallpaper = AppSettings.Load<bool>("gcmwallpaper");
                if (gcmwallpaper == true)
                {

                    string wallpaperpath = AppSettings.Load<string>("gcmwallpaperpath");
                    return wallpaperpath;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                Console.WriteLine("wallpaper gui error");
                return null;
            }
        }
        static void KillTargetProcess(string processName)
        {
            // Get all processes with the specified name
            Process[] processes = Process.GetProcessesByName(processName);

            foreach (Process proc in processes)
            {
                try
                {
                    // Attempt to kill the process
                    proc.Kill();
                    Console.WriteLine($"Terminated process: {proc.ProcessName} (ID: {proc.Id})");
                }
                catch (Exception ex)
                {
                    // Log if something goes wrong
                    Console.WriteLine($"Could not terminate process {proc.ProcessName}: {ex.Message}");
                }
            }
        }
        static void cssloader()
        {
            try
            {
                bool cssloadertogglestatus = AppSettings.Load<bool>("usecssloader");
                if (cssloadertogglestatus == true)
                {
                    // Check if CSSLOADER Desktop is installed
                    string cssloaderExePath = @"C:\Program Files\CSSLoader Desktop\CSSLoader Desktop.exe";

                    if (File.Exists(cssloaderExePath))
                    {
                        try
                        {
                            // Get the dynamic user profile path
                            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            string startupCssLoaderPath = Path.Combine(userProfilePath, @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\CssLoader-Standalone-Headless.exe");

                            // Check if the CSS Loader Standalone Headless file exists
                            if (File.Exists(startupCssLoaderPath))
                            {
                                // Start the process
                                Process.Start(startupCssLoaderPath);
                            }
                            else
                            {
                                Console.WriteLine("CSS Loader Standalone Headless is not installed in the Startup folder.");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Handle any errors that occur during the process
                            Console.WriteLine($"An error occurred while starting the CSS Loader: {ex.Message}");
                        }
                    }
                    else
                    {
                        // CSS Loader Desktop is not installed
                        Console.WriteLine("CSS Loader Desktop is not installed.");
                        return;
                    }
                }
                else
                {

                }
            }
            catch
            {

            }
        }
        static bool VerifySettings()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string settingsFolderPath = Path.Combine(appDataPath, "gcmsettings");
                string settingsFilePath = Path.Combine(settingsFolderPath, "settings.toml");

                // Ensure the directory exists
                if (!Directory.Exists(settingsFolderPath))
                {
                    Directory.CreateDirectory(settingsFolderPath);
                    Console.WriteLine($"[Settings] Created missing settings directory: {settingsFolderPath}");
                }

                // If the file is completely missing, create a basic fallback file instead of exiting the app.
                if (!File.Exists(settingsFilePath))
                {
                    Console.WriteLine($"[Settings] The file 'settings.toml' is missing. Creating a default configuration...");
                    string defaultSettings = "launcher = \"steam\"\n" +
                                             "usewinpartstartapps = true\n" +
                                             "shortcutpopup = true\n" +
                                             "enable_taskbar = false\n" +
                                             "enable_startmenu = false\n";
                    File.WriteAllText(settingsFilePath, defaultSettings);
                }

                // Return true so the app proceeds with startup normally
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while verifying the settings: {ex.Message}");
                // We still return true to try and force the app to survive and rely on try-catches later
                return true;
            }
        }
        private void FirstStart()
        {
            CleanupLogging();
            Environment.Exit(0);
        }
        static void CleanupLogging()
        {
            if (logWriter != null)
            {
                Console.WriteLine("Application stopped...");
                logWriter.Close();
            }

            // Restore standard output and error output
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        static void KillProcess(string ProcessName)
        {
            ProcessName = ProcessName.Substring(0, ProcessName.Length - 4);
            bool explorersStillRunning = true;

            while (explorersStillRunning)
            {
                // Get all processes named "explorer"
                var explorerProcesses = Process.GetProcessesByName(ProcessName);

                // If no "explorer" process found, exit loop
                if (!explorerProcesses.Any())
                {
                    Console.WriteLine(explorerProcesses + " process have been successfully killed.");
                    explorersStillRunning = false;
                }
                else
                {
                    // Kill each "explorer" process
                    foreach (var process in explorerProcesses)
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit(); // Optional, to ensure process is terminated
                            Console.WriteLine(process + "process killed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error attempting to kill : {ex.Message}");
                        }
                    }
                }
            }
        }
        private async void BackToWindows()
        {
            // 1. Notify the user what is happening
            SendOverlayNotification("Restoring desktop...");

            // 2. Visual Delay (1.5 seconds) - Gives the feeling of "booting up"
            await Task.Delay(1500);

            // Restore essential settings before restarting Explorer
            TaskbarManager.RestoreOriginalState();
            await TaskManagerReEnableServicesAsync();
            MakeSelfNonTopmost();
            MinimizeAllToDesktop();
            Console.WriteLine("Exit-Button clicked. Restoring desktop and exiting app...");

            // Unregister to prevent crashes during shutdown
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            try
            {
                // 1. Reset shell to explorer.exe in Registry
                if (!await TrySetWinlogonShellViaServiceAsync("explorer.exe"))
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("Shell", "explorer.exe", RegistryValueKind.String);
                        }
                    }
                }

                // 2. Restore startup apps
                if (AppSettings.Load<bool>("usewinpartstartapps"))
                {
                    StartupControl.RestoreStartupApps();
                }

                // 3. Task Manager Style Restart
                // By killing and restarting explorer.exe, Windows naturally rebuilds the 
                // Taskbar, Desktop (Progman), and Icons without us needing to unhide them manually.
                Console.WriteLine("Restarting explorer.exe (Task Manager style)...");

                // Kill all running instances of explorer
                KillProcess("explorer.exe");

                // Give Windows a brief moment to clear file locks and handles
                await Task.Delay(500);

                // Start a fresh explorer.exe instance
                Process.Start("explorer.exe");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring Windows: {ex.Message}");
            }

            // Clean up Steam video
            try
            {
                RenameSteamStartupVideo_End();
                Debug.WriteLine("[Cleanup] Steam startup video restored.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cleanup] Error restoring Steam startup video: {ex.Message}");
            }

            // Remaining cleanup tasks
            KillProcess("PluginLoader_noconsole.exe");
            Console.WriteLine("PluginLoader_noconsole killed");
            CleanupLogging();
            preaudio(false, true);

            // Restore UAC settings
            try
            {
                if (AppSettings.Load<bool>("uac"))
                {
                    if (!await TrySetUacModeViaServiceAsync(true))
                    {
                        uac("on");
                    }
                }
            }
            catch
            {
                uac("on");
            }

            // Exit safely
            Environment.Exit(0);
        }

        //xbox
        // 1. Benötigte P/Invoke-Deklaration (am besten am Anfang der Klasse platzieren)


        // 2. Benötigte Konstante (auch am Anfang der Klasse platzieren)
        private const int SW_SHOWMAXIMIZED = 3;

        // 3. Die eigentliche Methode
        /// <summary>
        /// Forces a window into true borderless fullscreen by resizing it to the screen dimensions.
        /// </summary>

        private double GetScaleFactor()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
            return dpi / 96.0;
        }

        private void MinimizeSelf()
        {
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
                {
                    overlappedPresenter.Minimize();
                    Debug.WriteLine("Main window minimized.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to minimize main window: {ex.Message}");
            }
        }
        /// <summary>
        /// Switches focus reliably to the specified window handle,
        /// restoring it if it is minimized, but not changing the size otherwise.
        /// </summary>
        private void SwitchToWindowReliably(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            // Step 1: If the window is minimized, we MUST restore it.
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            // Step 2: Use the AttachThreadInput trick to reliably set the foreground window.
            IntPtr foregroundHwnd = GetForegroundWindow();
            uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
            uint ourThreadId = GetCurrentThreadId();

            AttachThreadInput(ourThreadId, foregroundThreadId, true);
            SetForegroundWindow(hwnd);
            AttachThreadInput(ourThreadId, foregroundThreadId, false);
        }
        /// <summary>
        /// Forces a window to the exact dimensions of the screen, creating a borderless fullscreen effect.
        /// Uses the same logic as the main window's startup.
        /// </summary>
        private void ForceFullscreenResize(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            // Get the screen dimensions using your existing helper methods.
            int screenWidth = GetScreenWidth();
            int screenHeight = GetScreenHeight();

            // Use SetWindowPos to resize and position the window to fill the entire screen,
            // making it the top-level window without changing its Z-order insertion.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        private IntPtr FindSteamBigPictureWindow()
        {
            IntPtr steamHwnd = IntPtr.Zero;

            EnumWindows((hWnd, lParam) => {
                // Window must be visible
                if (!IsWindowVisible(hWnd))
                    return true; // Continue searching

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                try
                {
                    Process p = Process.GetProcessById((int)pid);
                    // IMPORTANT: The modern Big Picture UI is often rendered by "steamwebhelper"!
                    if (!p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase) &&
                        !p.ProcessName.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { return true; }

                StringBuilder classNameBuilder = new StringBuilder(256);
                GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                string className = classNameBuilder.ToString();

                // "SDL_app" = New Steam Deck UI / "CUIEngineWin32" = Old Big Picture
                if (className.Equals("CUIEngineWin32", StringComparison.OrdinalIgnoreCase) ||
                    className.Equals("SDL_app", StringComparison.OrdinalIgnoreCase))
                {
                    // --- THE FIX: Ignore 1x1 pixel phantom windows ---
                    GetWindowRect(hWnd, out RECT rect);
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width > 100 && height > 100)
                    {
                        steamHwnd = hWnd;
                        _lastKnownSteamBigPictureHwnd = hWnd;
                        Debug.WriteLine($"[GCM] Reliable Steam BP window found (Handle: {hWnd}, Size: {width}x{height})");
                        return false; // Stop searching, we found it!
                    }
                }

                return true;
            }, IntPtr.Zero);

            return steamHwnd;
        }
        private const byte VK_ESCAPE = 0x1B;
        // Add these with your other GDI32 constants
        private const int LOGPIXELSX = 88; // Horizontal DPI
        private const int LOGPIXELSY = 90; // Vertical DPI
        private int GetDpiX()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
            ReleaseDC(IntPtr.Zero, hdc);
            return dpi;
        }

        private int GetDpiY()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int dpi = GetDeviceCaps(hdc, LOGPIXELSY);
            ReleaseDC(IntPtr.Zero, hdc);
            return dpi;
        }
        /// <summary>
        /// Brings a window robustly to the foreground and performs a focus correction
        /// for Playnite by clicking in the top-right and sending an Escape signal.
        /// </summ ary>
        /// 

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        private const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        private const uint SPIF_SENDWININICHANGE = 0x02;

        private async Task ForcefullyBringToForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            Debug.WriteLine($"[GCM] Wende Nuclear-Focus auf Handle {hWnd} an...");

            // --- SCHRITT 1: Windows Fokus-Sperre auf OS-Ebene deaktivieren ---
            uint currentTimeout = 0;
            uint zeroTimeout = 0;
            // Aktuellen Wert speichern
            SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref currentTimeout, 0);
            // Sperre auf 0 Millisekunden (ausgeschaltet) setzen
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref zeroTimeout, SPIF_SENDWININICHANGE);

            // --- SCHRITT 2: Der "Holzhammer"-Loop ---
            // Wir lassen nicht locker, bis das Fenster WIRKLICH im Vordergrund ist (max. 10 Versuche)
            int attempts = 0;
            while (GetForegroundWindow() != hWnd && attempts < 10)
            {
                // 1. Fenster sichtbar machen
                if (IsIconic(hWnd)) ShowWindow(hWnd, 9); // SW_RESTORE
                else ShowWindow(hWnd, 5); // SW_SHOW

                // 2. Hardware-TopMost zwingen
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

                // 3. Undokumentierte Taskbar-API für Fokus
                SwitchToThisWindow(hWnd, true);

                // 4. Standard Fokus-APIs (mit Thread-Attach zur Sicherheit)
                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != hWnd)
                {
                    uint fgThread = GetWindowThreadProcessId(fgHwnd, out _);
                    uint myThread = GetCurrentThreadId();
                    AttachThreadInput(myThread, fgThread, true);

                    SetForegroundWindow(hWnd);
                    BringWindowToTop(hWnd);

                    AttachThreadInput(myThread, fgThread, false);
                }

                await Task.Delay(100); // 100ms warten, dann prüfen ob es geklappt hat
                attempts++;
            }

            // --- SCHRITT 3: Aufräumen ---
            // TopMost wieder wegnehmen, sonst klebt Steam für immer ganz oben
            SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

            // Windows Fokus-Sperre wieder auf den Ursprungswert zurücksetzen
            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, ref currentTimeout, SPIF_SENDWININICHANGE);

            if (GetForegroundWindow() == hWnd)
                Debug.WriteLine($"[GCM] Nuclear-Focus ERFOLGREICH nach {attempts} Versuchen!");
            else
                Debug.WriteLine($"[GCM] Nuclear-Focus FEHLGESCHLAGEN nach {attempts} Versuchen.");

            // --- SCHRITT 4: Playnite spezifische Logik (wie gehabt) ---
            string launcher = "";
            try { launcher = AppSettings.Load<string>("launcher"); } catch { }

            if (launcher == "playnite")
            {
                await Task.Delay(250);
                try
                {
                    long style = (long)GetWindowLongPtr(hWnd, GWL_STYLE);
                    style &= ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
                    SetWindowLongPtr(hWnd, GWL_STYLE, (IntPtr)style);

                    int screenWidth = GetScreenWidth();
                    int screenHeight = GetScreenHeight();
                    SetWindowPos(hWnd, HWND_TOP, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
                }
                catch { }

                await Task.Delay(300);
                try
                {
                    while (ShowCursor(false) >= 0) ;
                    await Task.Delay(32);

                    int screenWidth = GetScreenWidth();
                    int screenHeight = GetScreenHeight();
                    int clickX = screenWidth / 2;
                    int clickY = (int)((0.5 / 2.54) * GetDpiY());

                    SetCursorPos(clickX, clickY);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(50);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                    await Task.Delay(50);
                    keybd_event(0x1B, 0, 0x0000, UIntPtr.Zero); // ESC Down
                    keybd_event(0x1B, 0, 0x0002, UIntPtr.Zero); // ESC Up

                    SetCursorPos(screenWidth - 1, screenHeight - 1);
                }
                finally
                {
                    while (ShowCursor(true) < 0) ;
                }
            }
        }
        // Sucht vollautomatisch nach den Pfaden, OHNE die settings.toml zu nutzen!
        // --- HELPER: Auto-Detect Launcher Paths ---
        // Automatically finds the executable paths for supported launchers without checking settings
        // --- HELPER: Auto-Detect Launcher Paths ---
        public static string AutoDetectLauncherPath(string launcher)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = null;

            switch (launcher)
            {
                case "steam":
                    try
                    {
                        string configuredSteamPath = AppSettings.Load<string>("steamlauncherpath");
                        if (!string.IsNullOrWhiteSpace(configuredSteamPath) && File.Exists(configuredSteamPath))
                        {
                            return configuredSteamPath;
                        }
                    }
                    catch
                    {
                        // Fall back to registry-based auto-detection below.
                    }

                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ??
                                     Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        if (key != null)
                        {
                            string installPath = key.GetValue("InstallPath")?.ToString();
                            if (!string.IsNullOrEmpty(installPath))
                            {
                                path = Path.Combine(installPath, "steam.exe");
                            }
                        }
                    }
                    break;

                case "playnite":
                    // 1. Zuerst exakt in deinem Standard-Pfad schauen (99% der Fälle)
                    string defaultPath = Path.Combine(localAppData, "Playnite", "Playnite.FullscreenApp.exe");
                    if (File.Exists(defaultPath))
                    {
                        path = defaultPath;
                        break;
                    }

                    // 2. Falls es woanders installiert wurde: Registry checken
                    string[] playniteRegPaths = {
                @"SOFTWARE\Playnite",
                @"SOFTWARE\WOW6432Node\Playnite",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Playnite",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Playnite",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{14AB2B56-32A1-4F29-BEE2-0BA851CC07C7}_is1" // Playnite Installer GUID
            };

                    RegistryKey[] roots = { Registry.CurrentUser, Registry.LocalMachine };
                    foreach (var root in roots)
                    {
                        foreach (var regPath in playniteRegPaths)
                        {
                            using (var key = root.OpenSubKey(regPath))
                            {
                                if (key != null)
                                {
                                    string installDir = (key.GetValue("InstallPath") as string) ?? (key.GetValue("InstallLocation") as string);
                                    if (!string.IsNullOrEmpty(installDir))
                                    {
                                        string fullPath = Path.Combine(installDir, "Playnite.FullscreenApp.exe");
                                        if (File.Exists(fullPath))
                                        {
                                            path = fullPath;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (path != null) break;
                        }
                        if (path != null) break;
                    }
                    break;

                case "gfn":
                    path = Path.Combine(localAppData, "NVIDIA Corporation", "GeForceNOW", "CEF", "GeForceNOW.exe");
                    if (!File.Exists(path))
                    {
                        string lnkPath = Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\NVIDIA GeForce NOW.lnk");
                        if (File.Exists(lnkPath)) path = lnkPath; // Der .lnk Trick für GFN funktioniert, da hier keine Fullscreen/Desktop Unterscheidung existiert
                    }
                    break;
            }

            if (path != null && File.Exists(path)) return path;

            return null;
        }
        private async void SwitchToSpecificLauncher(string launcherId)
        {
            if (!TryBeginSteamLaunch())
            {
                Debug.WriteLine("[GCM] Ignoring duplicate Steam quick-launch request.");
                return;
            }

            MakeSelfNonTopmost();
            launcherId = "steam";
            Debug.WriteLine($"[GCM] Quick-Launch ausgelöst für: '{launcherId}'...");

            try
            {
                if (await TryReturnToSteamViaTaskViewAsync())
                {
                    return;
                }

                await StartSteam(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler beim Quick-Launch: {ex.Message}");
            }
            finally
            {
                EndSteamLaunch();
            }
        }

        private async void SwitchToConfiguredLauncher()
        {
            if (!TryBeginSteamLaunch())
            {
                Debug.WriteLine("[GCM] Ignoring duplicate Steam launcher request.");
                return;
            }

            MakeSelfNonTopmost();
            ApplySteamOnlyMode();
            Debug.WriteLine("[GCM] Wechsle zu konfiguriertem Launcher: 'steam'...");

            try
            {
                if (await TryReturnToSteamViaTaskViewAsync())
                {
                    return;
                }

                await StartSteam(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler während des Launcher-Wechsels: {ex.Message}");
            }
            finally
            {
                EndSteamLaunch();
            }
        }

        private async void SwitchToFeaturedGame()
        {
            var gameData = _featuredGameProcessData;
            if (gameData?.Proc == null || gameData.Proc.HasExited)
            {
                return;
            }

            try
            {
                if (ProcessSuspender.IsProcessSuspended(gameData.Proc.Id))
                {
                    SendOverlayNotification($"Waking up: {gameData.Proc.ProcessName}...");
                    await Task.Delay(1200);
                    ToggleGameSuspend(gameData.Proc, false);
                    _suspendedGamePid = 0;
                    await Task.Delay(400);
                }

                MakeSelfNonTopmost();
                IntPtr targetHwnd = gameData.Hwnd != IntPtr.Zero ? gameData.Hwnd : gameData.Proc.MainWindowHandle;
                if (targetHwnd == IntPtr.Zero)
                {
                    return;
                }

                if (!await TrySwitchToWindowViaTaskViewAsync(
                        targetHwnd,
                        FocusReturnTarget.GameWindow))
                {
                    Debug.WriteLine("[GCM] Game handoff did not report focus; skipping fallback by design.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler beim Wechsel zum Spiel: {ex.Message}");
            }
        }

        private async void CloseFeaturedGame()
        {
            var gameData = _featuredGameProcessData;
            if (gameData == null)
            {
                return;
            }

            await CloseProcessWindowAsync(gameData.Proc, gameData.Hwnd);
        }
        private static void MaximizeXboxWindow(IntPtr hwnd)
        {
            // Prüft, ob ein gültiges Fenster-Handle übergeben wurde
            if (hwnd != IntPtr.Zero)
            {
                // Maximiert das Fenster mit dem Windows-Standardbefehl
                ShowWindow(hwnd, SW_SHOWMAXIMIZED);
            }
        }

        // Stelle sicher, dass diese Hilfsmethode auch in deiner Klasse ist:
        private async Task<IntPtr> FindXboxWindowHandleAsync(int timeoutSeconds = 15)
        {
            IntPtr xboxHwnd = IntPtr.Zero;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0) return true;
                    var titleBuilder = new StringBuilder(256);
                    GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                    if (titleBuilder.ToString().Equals("Xbox", StringComparison.OrdinalIgnoreCase))
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        try
                        {
                            Process p = Process.GetProcessById((int)pid);
                            if (p.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                            {
                                xboxHwnd = hWnd;
                                return false;
                            }
                        }
                        catch { }
                    }
                    return true;
                }, IntPtr.Zero);

                if (xboxHwnd != IntPtr.Zero)
                {
                    stopwatch.Stop();
                    return xboxHwnd;
                }
                if (timeoutSeconds == 0) break;
                await Task.Delay(250);
            }
            return IntPtr.Zero;
        }



        private void LauncherCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Step 1: ALWAYS make our window non-topmost.
            // This allows the new launcher to come to the foreground without a fight.
            MakeSelfNonTopmost();

            // Step 2: Immediately start the switch to the configured launcher.
            SwitchToConfiguredLauncher();
        }


        static void uac(string art)
        {
            if (art == "on")
            {
                try
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 5);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", 1);

                    //  MessageBox.Show("UAC has been successfully enabled.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (UnauthorizedAccessException)
                {
                    throw new Exception("Unauthorized access: you need to run this program as an administrator.");
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable to restore default UAC settings: " + ex.Message);
                }
            }
            else if (art == "off")
            {
                try
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", 0);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", 0);

                    //  MessageBox.Show("UAC has been successfully disabled.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch (Exception ex)
                {
                    //  MessageBox.Show("An error occurred while disabling UAC: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private async Task ConsoleModeToShellAsync()
        {
            // Die Pfade zur Windows Shell Registry
            const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            const string valueName = "Shell";

            try
            {
                // 1. PFAD ERMITTELN: Pfad der aktuellen .exe holen und in Anführungszeichen setzen
                string targetExecutable = Process.GetCurrentProcess().MainModule.FileName;
                if (!targetExecutable.StartsWith("\""))
                {
                    targetExecutable = $"\"{targetExecutable}\"";
                }

                if (await TrySetWinlogonShellViaServiceAsync(targetExecutable))
                {
                    return;
                }

                // 2. FALLBACK: In local mode we only touch HKLM when Windows already grants admin rights.
                if (!IsAdministrator())
                {
                    Debug.WriteLine("[ConsoleMode] Local mode active. Skipping Winlogon shell registration because no administrator token is available.");
                    return;
                }

                // 3. REGISTRY ÖFFNEN: LocalMachine erfordert Admin-Rechte
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, true))
                {
                    if (key == null)
                    {
                        Debug.WriteLine($"[ConsoleMode] ERROR: Registry-Pfad '{keyName}' konnte nicht geöffnet werden.");
                        return;
                    }

                    // 4. RETRY-LOGIK: Wir versuchen es 3x, falls Windows den Zugriff kurzzeitig blockiert
                    const int maxRetries = 3;
                    bool success = false;

                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            // Wert setzen
                            key.SetValue(valueName, targetExecutable, RegistryValueKind.String);

                            // Kurze Pause zur Verarbeitung durch das System
                            await Task.Delay(150);

                            // Überprüfung: Hat es geklappt?
                            string currentValue = key.GetValue(valueName)?.ToString();
                            if (currentValue != null && currentValue.Equals(targetExecutable, StringComparison.OrdinalIgnoreCase))
                            {
                                Debug.WriteLine($"[ConsoleMode] Shell erfolgreich gesetzt im Versuch {i + 1}.");
                                success = true;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ConsoleMode] Versuch {i + 1} fehlgeschlagen: {ex.Message}");
                        }

                        await Task.Delay(500); // Längere Pause vor dem nächsten Versuch
                    }

                    if (!success)
                    {
                        Debug.WriteLine("[ConsoleMode] FATAL: Shell konnte nach mehreren Versuchen nicht geändert werden.");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("[ConsoleMode] Zugriff verweigert. Die App muss als Administrator ausgeführt werden.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConsoleMode] Unbekannter Fehler in ConsoleModeToShell: {ex.Message}");
            }
        }
        private void SettingsVerify()
        {
            try
            {
                if (!VerifySettings())
                {
                    // Instead of killing the application (which looks like a crash to the user),
                    // we simply log the issue and let the application continue using default fallback values.
                    Debug.WriteLine("[Settings] Verification returned false, but proceeding with application startup to prevent immediate exit.");
                }

                ApplySteamOnlyMode();

                string steamPath = "";
                try { steamPath = AppSettings.Load<string>("steamlauncherpath"); } catch { }
                if (!string.IsNullOrEmpty(steamPath) && !File.Exists(steamPath))
                    Debug.WriteLine("[Settings] The Steam path in settings is invalid, but continuing with auto-detect.");

                Console.WriteLine("Settings verified successfully.");
            }
            catch (Exception ex)
            {
                // CRASH REPORT LOGIC - Now much more robust! We don't call BackToWindows() which kills the app.
                string logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");

                string errorMessage =
                    "================================================================\r\n" +
                    "                GCM CRASH REPORT\r\n" +
                    "================================================================\r\n\r\n" +
                    $"TIMESTAMP: {DateTime.Now}\r\n\r\n" +
                    "REASON:\r\n" +
                    "An error occurred during settings verification.\r\n" +
                    "----------------------------------------------------------------\r\n" +
                    "TECHNICAL DETAILS:\r\n" +
                    $"{ex.Message}\r\n" +
                    "----------------------------------------------------------------\r\n";

                File.WriteAllText(logPath, errorMessage);

                try
                {
                    Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
                }
                catch { }

                // We REMOVED the BackToWindows() call here, because BackToWindows() contains Environment.Exit(0)
                // which causes the "app starts and immediately closes" behavior.
            }
        }
        public async System.Threading.Tasks.Task StartAsynctasks()
        {
            try
            {
                await Task.Delay(5000);
                await WaitForShellUiReadyAsync();
                await CheckForGithubReleaseUpdateAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Startup async tasks failed: {ex.Message}");
            }
        }

        private async Task WaitForShellUiReadyAsync(int timeoutMs = 15000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!_isShellUiReady && stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(250);
            }
        }

        private async Task CheckForGithubReleaseUpdateAsync()
        {
            if (_hasCheckedForGithubReleaseUpdate)
            {
                return;
            }

            _hasCheckedForGithubReleaseUpdate = true;

            Version currentVersion = GetCurrentApplicationVersion();

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"GCMLoader/{FormatVersion(currentVersion)}");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                using var response = await client.GetAsync(GithubLatestReleaseApiUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var releaseStream = await response.Content.ReadAsStreamAsync();
                using var releaseDocument = await JsonDocument.ParseAsync(releaseStream);
                JsonElement root = releaseDocument.RootElement;

                string releaseTag = root.TryGetProperty("tag_name", out JsonElement tagElement)
                    ? tagElement.GetString()
                    : string.Empty;

                if (!TryParseReleaseVersion(releaseTag, out Version latestReleaseVersion))
                {
                    return;
                }

                if (latestReleaseVersion <= currentVersion)
                {
                    Debug.WriteLine($"[Update] No newer GitHub release. Current={FormatVersion(currentVersion)} Latest={FormatVersion(latestReleaseVersion)}");
                    return;
                }

                string releaseName = root.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString()
                    : string.Empty;

                string releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement)
                    ? urlElement.GetString()
                    : "https://github.com/toonymak1993/GameConsoleMode/releases/latest";

                _availableGithubReleaseUpdate = new GithubReleaseInfo
                {
                    ReleaseVersion = latestReleaseVersion,
                    VersionText = string.IsNullOrWhiteSpace(releaseTag) ? FormatVersion(latestReleaseVersion) : releaseTag.Trim(),
                    DisplayTitle = string.IsNullOrWhiteSpace(releaseName) ? $"GitHub Release {FormatVersion(latestReleaseVersion)}" : releaseName.Trim(),
                    HtmlUrl = string.IsNullOrWhiteSpace(releaseUrl) ? "https://github.com/toonymak1993/GameConsoleMode/releases/latest" : releaseUrl.Trim()
                };

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_availableGithubReleaseUpdate == null || MainContent == null || MainContent.Visibility != Visibility.Visible)
                    {
                        return;
                    }

                    ShowGithubReleasePrompt(currentVersion);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] GitHub release check failed: {ex.Message}");
            }
        }

        private Version GetCurrentApplicationVersion()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return version;
            }

            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(currentExe))
                {
                    string fileVersion = FileVersionInfo.GetVersionInfo(currentExe).FileVersion;
                    if (TryParseReleaseVersion(fileVersion, out Version parsedFileVersion))
                    {
                        return parsedFileVersion;
                    }
                }
            }
            catch
            {
            }

            return new Version(0, 0, 0, 0);
        }

        private static bool TryParseReleaseVersion(string rawVersion, out Version version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return false;
            }

            Match versionMatch = Regex.Match(rawVersion, @"\d+(?:\.\d+){0,3}");
            if (!versionMatch.Success)
            {
                return false;
            }

            string[] parts = versionMatch.Value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            string normalizedVersion = parts.Length switch
            {
                1 => $"{parts[0]}.0.0",
                2 => $"{parts[0]}.{parts[1]}.0",
                _ => string.Join('.', parts.Take(4))
            };

            return Version.TryParse(normalizedVersion, out version);
        }

        private static string FormatVersion(Version version)
        {
            if (version == null)
            {
                return "0.0.0";
            }

            if (version.Revision > 0)
            {
                return version.ToString(4);
            }

            if (version.Build >= 0)
            {
                return version.ToString(3);
            }

            return version.ToString(2);
        }

        #region winparts

        private const int SW_MINIMIZE = 6;

        // Import Win32 APIs


        // Replace your existing MinimizeAllWindows method with this one
        public static void MinimizeAllWindows()
        {
            EnumWindows((hWnd, lParam) =>
            {
                // Skip invisible windows
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                try
                {
                    var proc = Process.GetProcessById((int)pid);

                    // NEW: Check if the process name contains a GPU vendor name
                    if (proc.ProcessName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                        proc.ProcessName.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                        proc.ProcessName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                        proc.ProcessName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                    {
                        // If so, skip minimizing this window
                        return true;
                    }

                    // Skip UWP shell host to preserve child window
                    if (proc.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    // If it's a normal window, minimize it
                    ShowWindow(hWnd, SW_MINIMIZE);
                }
                catch
                {
                    // Ignore processes we can't access
                }

                return true;
            }, IntPtr.Zero);
        }

        public static void MinimizeAllToDesktop()
        {
            try
            {
                // Das ist der offizielle und sicherste Windows-Weg, um alle Fenster zu minimieren.
                // Entspricht dem "Desktop anzeigen" Befehl.
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shellObject = Activator.CreateInstance(shellType);
                shellType.InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, shellObject, null);

                Debug.WriteLine("[GCM] Alle Fenster (inklusive UWP/Einstellungen) erfolgreich minimiert.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler beim nativen Minimieren: {ex.Message}");
                // Wir nutzen deine perfekte Win+D Shortcut-Simulation als Fallback!
                MinimizeAllViaShortcut();
            }
        }

        //needed
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        private const int SW_HIDE = 0;

        // Method signature changed to async Task to allow non-blocking waits
        public async Task winpart()
        {
            try
            {
                // Only execute if the setting is enabled
                bool usewinpart = true;

                if (usewinpart)
                {
                    try
                    {
                        // Set explorer.exe as the default shell in the registry
                        if (!await TrySetWinlogonShellViaServiceAsync("explorer.exe"))
                        {
                            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", writable: true))
                            {
                                if (key != null)
                                {
                                    key.SetValue("Shell", "explorer.exe", RegistryValueKind.String);
                                    Console.WriteLine("Shell successfully set to explorer.exe.");
                                }
                                else
                                {
                                    Console.WriteLine("Registry key not found.");
                                }
                            }
                        }

                        Console.WriteLine("Starting explorer.exe...");

                        // Check if explorer is already running
                        bool explorerRunning = Process.GetProcessesByName("explorer").Any();
                        await TaskManagerDebloatServicesAsync();

                        if (!explorerRunning)
                        {
                            Process.Start("explorer.exe");
                        }

                        // --- OPTIMIZATION: Smart Wait for Explorer ---
                        // Instead of freezing the app for 5 seconds, we wait asynchronously 
                        // until the Windows Taskbar is actually created by explorer.exe.
                        int timeoutCounter = 0;
                        IntPtr taskbarHandle = IntPtr.Zero;

                        // Poll every 100ms for up to 10 seconds (100 attempts)
                        while (taskbarHandle == IntPtr.Zero && timeoutCounter < 100)
                        {
                            await Task.Delay(100);
                            taskbarHandle = FindWindow("Shell_TrayWnd", null);
                            timeoutCounter++;
                        }

                        // Give Windows a tiny moment to draw the desktop icons properly
                        await Task.Delay(500);
                        // ---------------------------------------------

                        // Now safely hide everything
                        KillProcess("WidgetBoard");
                        KillProcess("WidgetService");
                        DesktopIconController.HideDesktopIcons();

                        // Make taskbar invisible
                        TaskbarVisibility.HideTaskbar();
                        Console.WriteLine("Shell windows successfully hidden.");

                        TaskbarManager.EnableAutoHide();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine("Error: Access Denied. Run the application as an administrator.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error during explorer startup: " + ex.Message);
                    }

                    // Restore gcmloader as the shell for the next boot
                    try
                    {
                        const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                        const string valueName = "Shell";

                        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                        string targetExecutable = Path.Combine(programFilesX86, "GCM", "gcmloader", "gcmloader.exe");

                        if (!File.Exists(targetExecutable))
                        {
                            return;
                        }

                        if (!await TrySetWinlogonShellViaServiceAsync(targetExecutable))
                        {
                            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, writable: true))
                            {
                                if (key != null)
                                {
                                    key.SetValue(valueName, targetExecutable, RegistryValueKind.String);

                                    // Verify the change
                                    string currentValue = key.GetValue(valueName)?.ToString();
                                    if (currentValue == targetExecutable)
                                    {
                                        Console.WriteLine($"Current value: {currentValue} successfully set.");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Failed to set '{valueName}'.");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error restoring shell: {ex.Message}");
                    }
                }
            }
            catch
            {
                AppSettings.Save("usewinpart", false);
                AppSettings.Save("usewinpartstartapps", false);
            }
        }
        #region debloat service

        public async Task TaskManagerDebloatServicesAsync()
        {
            var debloatList = IsHandheld() ? _debloatServicesHandheld : _debloatServicesDesktop;

            foreach (var (serviceName, processName) in debloatList)
            {
                if (!string.IsNullOrWhiteSpace(processName))
                {
                    await DisableServiceAndKillProcessAsync(serviceName, processName);
                }
                else
                {
                    bool stopHandledByService = await TryStopWindowsServiceViaServiceAsync(serviceName);
                    bool startupHandledByService = await TrySetWindowsServiceStartupModeViaServiceAsync(serviceName, "disabled");

                    if (stopHandledByService && startupHandledByService)
                    {
                        Console.WriteLine($"[✓] Stopped and disabled via GCM service: {serviceName}");
                    }
                    else
                    {
                        try
                        {
                            using var service = new ServiceController(serviceName);
                            if (service.Status != ServiceControllerStatus.Stopped &&
                                service.Status != ServiceControllerStatus.StopPending)
                            {
                                service.Stop();
                                service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                                Console.WriteLine($"[✓] Stopped: {serviceName}");
                            }

                            DisableServiceStartup(serviceName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[!] Failed: {serviceName} → {ex.Message}");
                        }
                    }
                }
            }
        }
        public async Task TaskManagerReEnableServicesAsync()
        {
            // Choose correct list based on device mode
            var servicesToEnable = IsHandheld() ? _debloatServicesHandheld : _debloatServicesDesktop;

            // Set each service to start automatically (do not start now)
            foreach (var (serviceName, _) in servicesToEnable)
            {
                if (!await TrySetWindowsServiceStartupModeViaServiceAsync(serviceName, "auto"))
                {
                    SetServiceStartupToAuto(serviceName);
                }
            }
        }
        public static void SetServiceStartupToAuto(string serviceName)
        {
            try
            {
                if (!IsAdministrator())
                {
                    Debug.WriteLine($"[System] Skipping startup-mode restore for {serviceName} because GCM is running without admin rights.");
                    return;
                }

                // Use sc.exe to set service to automatic start
                Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"config \"{serviceName}\" start= auto",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                })?.WaitForExit();

                Debug.WriteLine($"[✓] Service {serviceName} set to automatic startup.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[!] Failed to set {serviceName} to auto: {ex.Message}");
            }
        }
        #region disable
        private static void DisableServiceStartup(string serviceName)
        {
            if (!IsAdministrator())
            {
                Debug.WriteLine($"[System] Skipping startup-mode disable for {serviceName} because GCM is running without admin rights.");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"config \"{serviceName}\" start= disabled",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
        }


        private static readonly List<(string ServiceName, string? ProcessName)> _debloatServicesDesktop = new()
{
    ("SysMain", null),                       // Superfetch – unnötig bei SSD
    ("DiagTrack", null),                     // Telemetrie
    ("MapsBroker", null),                    // Karten-Dienst
    ("RetailDemo", null),                    // Demo-Modus
    ("Fax", null),                           // Fax-Dienst
    ("WSearch", "SearchIndexer"),            // Windows Suche :contentReference[oaicite:1]{index=1}
    ("OneSyncSvc", "OneDrive"),              // OneDrive Sync
    ("PhoneSvc", null),                      // Telefon-Anbindung
    ("WerSvc", null),                        // Fehlerberichte
    ("Spooler", null),                       // Druckerwarteschlange
    ("dmwappushservice", null),              // Push Notifications
    ("ConnectedUserExperiencesAndTelemetry", null), // Feedback & Telemetrie :contentReference[oaicite:2]{index=2}
    ("MessagingService", null),              // App-Nachrichten
    ("ContactDataSvc", null),                // Kontakte Synchronisation
    ("IpOverUsbSvc", null)                   // USB IP-Geräte
};

        private static readonly List<(string ServiceName, string? ProcessName)> _debloatServicesHandheld = new()
{
    ("SysMain", null),
    ("DiagTrack", null),
    ("MapsBroker", null),
    ("RetailDemo", null),
    ("Fax", null),
    ("WSearch", "SearchIndexer"),
    ("OneSyncSvc", "OneDrive"),
    ("WerSvc", null),
    ("dmwappushservice", null),
    ("PhoneSvc", null),
    ("MessagingService", null),
    ("ContactDataSvc", null),
};


        public async Task DisableServiceAndKillProcessAsync(string serviceName, string processName)
        {
            try
            {
                bool stopHandledByService = await TryStopWindowsServiceViaServiceAsync(serviceName);
                bool startupHandledByService = await TrySetWindowsServiceStartupModeViaServiceAsync(serviceName, "disabled");

                if (!stopHandledByService || !startupHandledByService)
                {
                    if (!IsAdministrator())
                    {
                        Debug.WriteLine($"[System] Skipping privileged service shutdown for {serviceName} because GCM is running without admin rights.");
                    }
                    else
                    {
                    using (var service = new ServiceController(serviceName))
                    {
                        if (service.Status != ServiceControllerStatus.Stopped &&
                            service.Status != ServiceControllerStatus.StopPending)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                        }

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = $"config \"{serviceName}\" start= disabled",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        })?.WaitForExit();
                    }
                    }
                }

                // Kill background process (if running)
                foreach (var proc in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        proc.Kill(true); // true = kill child processes too
                        Debug.WriteLine($"Killed process {proc.ProcessName} (PID {proc.Id})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error killing process {proc.ProcessName}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"Service {serviceName} disabled and background process {processName} killed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disabling service or killing process: {ex.Message}");
            }
        }
        #endregion disable

        #endregion debloat service
        public static class DesktopIconController
        {
            private const int SW_HIDE = 0;
            private const int SW_SHOW = 5;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            // Hide both Progman and WorkerW windows that host the desktop icons
            public static void HideDesktopIcons()
            {
                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero)
                {
                    ShowWindow(progman, SW_HIDE);
                }

                IntPtr workerw = FindWindow("WorkerW", null);
                if (workerw != IntPtr.Zero)
                {
                    ShowWindow(workerw, SW_HIDE);
                }

                Console.WriteLine("Desktop icons hidden.");
            }

            // Restore visibility of the desktop host windows
            public static void ShowDesktopIcons()
            {
                // Kill all explorer.exe processes
                foreach (var proc in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        proc.Kill(true); // Kill including child processes
                        Debug.WriteLine("✓ explorer.exe killed.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[!] Error killing explorer.exe: {ex.Message}");
                    }
                }

                // Small delay to ensure processes are terminated
                System.Threading.Thread.Sleep(3000);
            }
        }

        private async Task Showwinpartandlauncher()
        {
            // Autostart-Apps deaktivieren, falls gewünscht
            try
            {
                bool usewinpartstartapps = AppSettings.Load<bool>("usewinpartstartapps");
                if (usewinpartstartapps)
                {
                    StartupControl.DisableAllStartupApps();
                }
            }
            catch { }

            // 1. ZUERST laden wir den Desktop und die Taskleiste im Hintergrund (winpart)
            Debug.WriteLine("Starte WinPart-Modus (Desktop laden)...");
            await winpart();

            // 2. Wir geben dem System kurz Zeit zum Durchatmen (1 Sekunde), 
            // damit keine Popups oder Fokus-Diebe vom Windows-Explorer stören.
            await Task.Delay(1000);

            // 3. ERST JETZT starten wir den eigentlichen Launcher (nach Video und nach Desktop!)
            Debug.WriteLine("WinPart abgeschlossen. Starte nun Launcher: steam");
            ApplySteamOnlyMode();
            await StartSteam();

            // 4. GCM Loader als Shell in die Registry schreiben für den nächsten Start
            await ConsoleModeToShellAsync();
        }
        #endregion winparts

        #endregion functions
        #region launcher

        #region non-admin launch helper
        private const uint TOKEN_DUPLICATE_ACCESS = 0x0002;
        private const uint MAXIMUM_ALLOWED_ACCESS = 0x02000000;
        private const uint CREATE_UNICODE_ENVIRONMENT_FLAG = 0x00000400;

        private enum SECURITY_IMPERSONATION_LEVEL_ENUM { SecurityAnonymous = 0, SecurityIdentification = 1, SecurityImpersonation = 2, SecurityDelegation = 3 }
        private enum TOKEN_TYPE_ENUM { TokenPrimary = 1, TokenImpersonation = 2 }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES_TOKEN
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFOW
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX, dwY, dwXSize, dwYSize;
            public uint dwXCountChars, dwYCountChars;
            public uint dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION_W
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES_TOKEN lpTokenAttributes, SECURITY_IMPERSONATION_LEVEL_ENUM ImpersonationLevel, TOKEN_TYPE_ENUM TokenType, out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(IntPtr hToken, uint dwLogonFlags, string lpApplicationName, string lpCommandLine, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFOW lpStartupInfo, out PROCESS_INFORMATION_W lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        
        private static void StartProcessAsNonAdmin(string filePath, string arguments = null, string workingDirectory = null)
        {
            IntPtr userToken = GetNonElevatedUserToken();

            if (userToken == IntPtr.Zero)
            {
                // Standard launch (will inherit elevated token)
                Debug.WriteLine("[GCM] StartProcessAsNonAdmin: could not obtain non-elevated token, falling back to elevated launch.");
                var psi = new ProcessStartInfo(filePath) { UseShellExecute = true };
                if (!string.IsNullOrEmpty(arguments)) psi.Arguments = arguments;
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                Process.Start(psi);
                return;
            }

            IntPtr duplicateToken = IntPtr.Zero;
            try
            {
                var sa = new SECURITY_ATTRIBUTES_TOKEN { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES_TOKEN>() };

                if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED_ACCESS, ref sa,
                    SECURITY_IMPERSONATION_LEVEL_ENUM.SecurityImpersonation, TOKEN_TYPE_ENUM.TokenPrimary, out duplicateToken))
                    throw new InvalidOperationException($"DuplicateTokenEx failed: {Marshal.GetLastWin32Error()}");

                var si = new STARTUPINFOW
                {
                    cb = Marshal.SizeOf<STARTUPINFOW>(),
                    lpDesktop = "winsta0\\default"
                };

                string cmdLine = string.IsNullOrEmpty(arguments)
                    ? $"\"{filePath}\""
                    : $"\"{filePath}\" {arguments}";

                if (!CreateProcessWithTokenW(duplicateToken, 0, null, cmdLine,
                    CREATE_UNICODE_ENVIRONMENT_FLAG, IntPtr.Zero, workingDirectory, ref si, out var pi))
                    throw new InvalidOperationException($"CreateProcessWithTokenW failed: {Marshal.GetLastWin32Error()}");

                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            }
            finally
            {
                CloseHandle(userToken);
                if (duplicateToken != IntPtr.Zero) CloseHandle(duplicateToken);
            }
        }

        
        private static IntPtr GetNonElevatedUserToken()
        {
            // 1. Try explorer first 
            IntPtr token = TryGetTokenFromProcessName("explorer");
            if (token != IntPtr.Zero) return token;

            // 2. Try other non-elevated user-session processes 
            string[] candidates = { "RuntimeBroker", "sihost", "ctfmon", "taskhostw", "ShellExperienceHost" };
            foreach (string name in candidates)
            {
                token = TryGetTokenFromProcessName(name);
                if (token != IntPtr.Zero)
                {
                    Debug.WriteLine($"[GCM] GetNonElevatedUserToken: obtained token from '{name}'.");
                    return token;
                }
            }

            // 3. WTSQueryUserToken — the last chance :)
            try
            {
                uint sessionId = WTSGetActiveConsoleSessionId();
                if (sessionId != 0xFFFFFFFF && WTSQueryUserToken(sessionId, out token))
                {
                    Debug.WriteLine($"[GCM] GetNonElevatedUserToken: obtained token via WTSQueryUserToken (session {sessionId}).");
                    return token;
                }
                Debug.WriteLine($"[GCM] WTSQueryUserToken failed: {Marshal.GetLastWin32Error()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] WTSQueryUserToken exception: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        private static IntPtr TryGetTokenFromProcessName(string processName)
        {
            try
            {
                Process? proc = Process.GetProcessesByName(processName).FirstOrDefault();
                if (proc == null) return IntPtr.Zero;

                if (OpenProcessToken(proc.Handle, TOKEN_DUPLICATE_ACCESS, out IntPtr token))
                    return token;
            }
            catch { }
            return IntPtr.Zero;
        }
        #endregion non-admin launch helper

        private const uint SWP_NOACTIVATE = 0x0010;
        // Wir fügen den Parameter 'forceRestart' hinzu. Standard ist 'false'.
        private async Task StartSteam(bool forceRestart = false)
        {
            try
            {
                ClearFocusReturnWatchdog();
                ResetParkedSteamState();
                ShowInAppNotification("Preparing Steam...");

                string steamExePath = ResolveSteamExecutablePath();
                if (string.IsNullOrWhiteSpace(steamExePath) || !File.Exists(steamExePath))
                {
                    ShowInAppNotification("Steam executable not found.");
                    throw new FileNotFoundException("Steam could not be found automatically on this system.");
                }

                // Push GCM to the background to make room for Steam
                IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                SetWindowPos(myHwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                // Check if Steam is already running right at the beginning
                var steamProc = Process.GetProcessesByName("steam").FirstOrDefault();
                bool isSteamRunning = steamProc != null;
                bool steamLiefSchon = false;
                bool steamPluginHostEnabled = IsSteamPluginHostEnabled();

                // A "Cold Start" is required if explicitly requested (e.g., on boot) OR if Steam is completely closed
                bool isColdStart = forceRestart || !isSteamRunning;

                if (await ShouldForceSteamRestartForPluginHostAsync(isSteamRunning))
                {
                    Debug.WriteLine("[GCM] Steam plugin host requested a developer-mode restart.");
                    ShowInAppNotification("Restarting Steam for plugin host...");
                    forceRestart = true;
                    isColdStart = true;
                }

                if (steamPluginHostEnabled)
                {
                    await EnsureSteamPluginHostRunningAsync(notifyIfStarting: isColdStart);
                }

                // --- DECKY LOADER INTEGRATION ---
                bool useDeckyLoader = false;
                try
                {
                    useDeckyLoader = AppSettings.Load<bool>("usedeckyloader");
                }
                catch
                {
                    // Fallback in case the setting doesn't exist yet
                    Debug.WriteLine("[GCM] 'usedeckyloader' setting not found, defaulting to false.");
                }

                // We ONLY reset Decky Loader if we are doing a cold start.
                // If we are just switching via the Launcher Card (Warmstart), we skip this heavy process!
                if (useDeckyLoader && isColdStart)
                {
                    Debug.WriteLine("[GCM] Decky Loader enabled & Cold Boot required. Preparing environment...");
                    ShowInAppNotification("Preparing Decky Loader...");

                    // Make sure Steam is completely dead before doing anything with Decky
                    var allSteamProcs = Process.GetProcessesByName("steam")
                                               .Concat(Process.GetProcessesByName("steamwebhelper"))
                                               .ToList();

                    if (allSteamProcs.Any())
                    {
                        foreach (var proc in allSteamProcs)
                        {
                            try { if (!proc.HasExited) proc.Kill(); } catch { }
                        }
                    }

                    // Terminate existing Decky Loader instances (PluginLoader_noconsole)
                    var deckyProcs = Process.GetProcessesByName("PluginLoader_noconsole");
                    if (deckyProcs.Any())
                    {
                        foreach (var proc in deckyProcs)
                        {
                            try { if (!proc.HasExited) proc.Kill(); } catch { }
                        }
                    }

                    // Give the OS a moment to free up file handles and ports
                    await Task.Delay(1500);

                    // Resolve the dynamic path to the user's homebrew folder
                    string userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string deckyPath = Path.Combine(userProfileFolder, "homebrew", "services", "PluginLoader_noconsole.exe");

                    if (File.Exists(deckyPath))
                    {
                        Debug.WriteLine("[GCM] Launching PluginLoader...");
                        ShowInAppNotification("Starting Decky Loader...");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = deckyPath,
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden // Keep the background clean
                        });

                        // Wait for the plugin loader to initialize its hooks before firing up Steam
                        await Task.Delay(2000);
                    }
                    else
                    {
                        Debug.WriteLine($"[GCM] WARNING: Decky Loader executable not found at {deckyPath}");
                    }

                    // Since we forcibly closed Steam above, we MUST guarantee a cold boot for Steam now
                    forceRestart = true;
                    isSteamRunning = false;
                }
                // --- END DECKY LOADER INTEGRATION ---

                // --- CASE A: COLD START (Boot or Steam was closed) ---
                if (forceRestart || !isSteamRunning)
                {
                    Debug.WriteLine("[GCM] Steam Cold Boot into Big Picture Mode...");
                    ShowInAppNotification("Launching Steam Big Picture...");

                    if (IsSteamStoreSyncEnabled())
                    {
                        await RunSteamStoreSyncBeforeLaunchAsync();
                    }

                    // If Decky was OFF, but we still need a force restart, ensure Steam is dead
                    if (!useDeckyLoader && forceRestart)
                    {
                        var steamProcs = Process.GetProcessesByName("steam")
                                                .Concat(Process.GetProcessesByName("steamwebhelper"))
                                                .ToList();
                        if (steamProcs.Any())
                        {
                            foreach (var proc in steamProcs) { try { if (!proc.HasExited) proc.Kill(); } catch { } }
                            await Task.Delay(1500);
                        }
                    }

                    RenameSteamStartupVideo_Start();
                    StartProcessAsNonAdmin(steamExePath, BuildSteamLaunchArguments());
                }
                // --- CASE B: WARM START (Switching via Launcher Card) ---
                else
                {
                    Debug.WriteLine("[GCM] Steam is already running. Triggering Big Picture switch (Warmstart)...");
                    ShowInAppNotification("Switching to Steam Big Picture...");
                    Process.Start(new ProcessStartInfo("steam://open/gamepadui") { UseShellExecute = true });
                    steamLiefSchon = true;
                }

                // --- WAIT FOR THE STEAM WINDOW ---
                IntPtr steamHwnd = IntPtr.Zero;
                int attempts = 0;
                // Faster timeout if it's just a warm switch
                int maxAttempts = steamLiefSchon ? 20 : 60;

                while (attempts < maxAttempts)
                {
                    steamHwnd = FindSteamBigPictureWindow();
                    if (steamHwnd != IntPtr.Zero)
                    {
                        await Task.Delay(800); // Let the UI build up
                        break;
                    }
                    await Task.Delay(250);
                    attempts++;
                }

                // --- MAXIMIZE AND FORCE TO FOREGROUND ---
                if (steamHwnd != IntPtr.Zero)
                {
                    Debug.WriteLine($"[GCM] Steam BP window ready. Applying Nuclear-Focus...");
                    ShowInAppNotification("Steam Big Picture ready.");
                    _lastKnownSteamBigPictureHwnd = steamHwnd;
                    await ForcefullyBringToForeground(steamHwnd);
                    ShowWindow(steamHwnd, 3); // SW_SHOWMAXIMIZED
                    ArmFocusReturnWatchdog(FocusReturnTarget.SteamBigPicture, TimeSpan.FromSeconds(15));
                }
                else
                {
                    Debug.WriteLine("[GCM] Timeout! Steam BP window was not found.");
                    ShowInAppNotification("Steam Big Picture not found.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Error in StartSteam: {ex.Message}");
                ShowInAppNotification("Steam start failed.");
            }
        }

        private async Task StartPlaynite()
        {
            try
            {
                // 1. Läuft die Fullscreen-App vielleicht schon? Dann nur nach vorne holen!
                Process proc = Process.GetProcessesByName("Playnite.FullscreenApp").FirstOrDefault();
                if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
                {
                    Debug.WriteLine("[GCM] Playnite Fullscreen läuft bereits. Bringe in den Vordergrund...");
                    MakeSelfNonTopmost();
                    await ForcefullyBringToForeground(proc.MainWindowHandle);
                    return;
                }

                // 2. WICHTIG: Die Desktop-App zwingend beenden! 
                // Sonst weigert sich Playnite oft, in den Fullscreen-Modus zu wechseln.
                var desktopProcs = Process.GetProcessesByName("Playnite.DesktopApp");
                foreach (var dp in desktopProcs)
                {
                    try
                    {
                        dp.Kill();
                        await Task.Delay(200); // Kurz warten, bis sie wirklich zu ist
                    }
                    catch { }
                }

                // 3. VOLLAUTOMATISCHE ERKENNUNG (Nur FullscreenApp)
                string playnitePath = AutoDetectLauncherPath("playnite");

                if (string.IsNullOrWhiteSpace(playnitePath) || !File.Exists(playnitePath))
                {
                    throw new FileNotFoundException("Playnite Fullscreen.exe konnte nicht gefunden werden.");
                }

                Debug.WriteLine($"[GCM] Starte Playnite Fullscreen von: {playnitePath}");
                MakeSelfNonTopmost();

                // 4. Starten mit WorkingDirectory und strikten Parametern
                StartProcessAsNonAdmin(playnitePath, "--startfullscreen --hidesplashscreen", Path.GetDirectoryName(playnitePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Error in StartPlaynite: {ex.Message}");
                await messagebox("Playnite Fullscreen could not be started. Please check the installation.");
            }
        }

        private async Task StartGfn()
        {
            const int SW_SHOWMAXIMIZED = 3;
            try
            {
                Debug.WriteLine("[GCM] Prüfe GeForce Now...");

                // 1. Wenn GFN schon läuft
                Process[] runningProcs = Process.GetProcessesByName("GeForceNOW");
                foreach (var p in runningProcs)
                {
                    if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                    {
                        MakeSelfNonTopmost();
                        await ForcefullyBringToForeground(p.MainWindowHandle);
                        ShowWindow(p.MainWindowHandle, SW_SHOWMAXIMIZED);
                        return;
                    }
                }

                // 2. VOLLAUTOMATISCHE ERKENNUNG (Keine Settings mehr!)
                string gfnPath = AutoDetectLauncherPath("gfn");

                if (string.IsNullOrWhiteSpace(gfnPath))
                {
                    throw new FileNotFoundException("GeForce Now could not be detected automatically on this system.");
                }

                Process.Start(new ProcessStartInfo(gfnPath) { UseShellExecute = true });

                // 3. Warten, bis das Fenster da ist
                int attempts = 0;
                IntPtr gfnHwnd = IntPtr.Zero;

                while (attempts < 40)
                {
                    await Task.Delay(500);
                    Process[] procs = Process.GetProcessesByName("GeForceNOW");
                    foreach (var p in procs)
                    {
                        if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                        {
                            gfnHwnd = p.MainWindowHandle;
                            break;
                        }
                    }
                    if (gfnHwnd != IntPtr.Zero) break;
                    attempts++;
                }

                if (gfnHwnd != IntPtr.Zero)
                {
                    MakeSelfNonTopmost();
                    await ForcefullyBringToForeground(gfnHwnd);
                    await Task.Delay(100);
                    ShowWindow(gfnHwnd, SW_SHOWMAXIMIZED);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler in StartGfn: {ex.Message}");
                await messagebox("GeForce Now konnte nicht gefunden oder gestartet werden.");
            }
        }


        private async Task StartOtherLauncher()
        {
            try
            {
                string launcherPath = AppSettings.Load<string>("customlauncherpath");

                if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
                {
                    throw new FileNotFoundException("Der Pfad für den Custom Launcher ist ungültig oder wurde nicht gefunden.");
                }

                KillProcess(Path.GetFileName(launcherPath));
                StartProcessAsNonAdmin(launcherPath, null, Path.GetDirectoryName(launcherPath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler in StartOtherLauncher: {ex.Message}");
                await messagebox("Der Custom Launcher konnte nicht gestartet werden. Bitte den Pfad in den Einstellungen prüfen.");
                BackToWindows();
            }
        }




        #endregion launcher
        #region start
        // ERSETZE DEINE KOMPLETTE Start()-METHODE MIT DIESER VERSION

        #region start
        private async Task Start()
        {
            try
            {
                App.StartupTrace("Start() begin.");
                await Task.Yield();
                ShowInAppNotification("GCM starting...");

                // --- 1. INSTANT BLACKOUT & LOCKDOWN ---
                // Versteckt die Taskleiste sofort, noch bevor das Fenster überhaupt gezeichnet ist
                TaskbarManager.EnableAutoHide();
                TaskbarVisibility.HideTaskbar();

                // Mauszeiger sofort in die Ecke sperren und unsichtbar machen
                ParkMouseCursor();

                // Fenster absolut nach vorne zwingen (TopMost), damit NIX das Video überlagert
                IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                SetWindowPos(myHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                // die gesamte Desktop- und Service-Vorbereitung im Hintergrund ab.
                ShowInAppNotification("Preparing desktop shell...");
                Task backgroundSetupTask = SetupSystemAndDesktopAsync();

                // 1. Warten, bis das Video zu 100% fertig ist
                int safetyCounter = 0;
                while (startupVideoFinished == false && safetyCounter < 300) // max 15 Sekunden
                {
                    await Task.Delay(50);
                    safetyCounter++;
                }

                if (!startupVideoFinished)
                {
                    Debug.WriteLine("[Start] Video-Timeout reached! Forcing UI transition.");
                    TransitionToMainUI();
                }

                // 2. Sicherstellen, dass das Setup im Hintergrund WIRKLICH fertig ist.
                // (Meistens ist es das schon längst, da das Video in der Regel länger dauert als der Explorer-Start).
                await backgroundSetupTask;
                App.StartupTrace("Background setup finished.");
                ShowInAppNotification("Desktop preparation complete.");

                // 3. ERST JETZT, wo das Video weg ist und der Desktop 100% bereit ist,
                // starten wir den eigentlichen Launcher. Er poppt jetzt nahezu sofort auf!
                ShowInAppNotification("Preparing Steam Big Picture...");
                await StartConfiguredLauncherAsync();
                App.StartupTrace("Configured launcher start finished.");
                ShowInAppNotification("Steam handoff complete.");

                // 4. GCM Loader als Shell in die Registry schreiben für den nächsten Start
                await ConsoleModeToShellAsync();

                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                if (string.Equals(ex.Message, "Required GCM system service is not ready.", StringComparison.Ordinal))
                {
                    App.StartupTrace("Startup aborted because the privileged GCM service was not ready.");
                    Environment.Exit(2);
                    return;
                }

                App.StartupTrace($"Start() failed: {ex.Message}");
                Debug.WriteLine($"[Start Error] A critical error occurred during startup: {ex.Message}");
                DispatcherQueue.TryEnqueue(() => TransitionToMainUI());
            }
        }

        // Diese neue Methode bündelt alles, was WÄHREND des Videos passieren kann,
        // ohne dass der User es sieht.
        private async Task SetupSystemAndDesktopAsync()
        {
            App.StartupTrace("SetupSystemAndDesktopAsync begin.");
            ShowInAppNotification("Loading system services...");
            await EnsurePrivilegedServiceReadyAsync(ShouldFailFastWhenPrivilegedServiceIsMissing());
            // System-Hooks und Hintergrunddienste aktivieren
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SetupLogging();
            BoostProcessPriority();
            if (!await TrySetUacModeViaServiceAsync(false))
            {
                uac("off");
            }

            // ---> HIER IST DAS NEUE AWAIT <---
            await prestartlist();

            SettingsVerify();
            if (!await TryConfigureKeyboardRedirectViaServiceAsync(true))
            {
                KeyboardRedirector.EnableRedirect();
            }

            // Hintergrund-Tools asynchron starten
            if (IsSteamPluginHostEnabled())
            {
                _ = EnsureSteamPluginHostRunningAsync(notifyIfStarting: false);
            }
            if (!await TryEnsureTouchKeyboardServiceViaServiceAsync())
            {
                EnsureTouchKeyboardServiceIsRunning();
            }
            cssloader();
            preaudio(true, false);

            // Autostart-Apps deaktivieren, falls gewünscht
            try
            {
                if (AppSettings.Load<bool>("usewinpartstartapps"))
                {
                    StartupControl.DisableAllStartupApps();
                }
            }
            catch { }

            // JETZT laden wir den Desktop (explorer.exe) und verstecken die Taskleiste.
            Debug.WriteLine("Starte WinPart-Modus (Desktop laden) im Hintergrund...");
            ShowInAppNotification("Preparing Windows shell...");
            await winpart();
            App.StartupTrace("SetupSystemAndDesktopAsync complete.");
        }

        // Diese Methode kümmert sich am Ende NUR noch um das reine Öffnen des Launchers.
        private async Task StartConfiguredLauncherAsync()
        {
            ApplySteamOnlyMode();
            Debug.WriteLine("Desktop ist bereit. Starte nun Launcher: steam");
            ShowInAppNotification("Starting Steam Big Picture...");
            await StartSteam(true);
        }

        #endregion start
        #endregion start
        #region TaskManager
        /// <summary>
        /// Animates the opacity of a UIElement to make it fade in or out.
        /// </summary>
        /// <param name="element">The UI element to animate.</param>
        /// <param name="targetOpacity">The target opacity (0.0 for fade out, 1.0 for fade in).</param>
        /// <param name="duration">The duration of the animation.</param>
        /// <param name="delay">An optional delay before the animation starts.</param>
        /// <summary>
        /// Animates the opacity of a UIElement to make it fade in or out.
        /// </summary>
        private void AnimateCardVisibility(UIElement element, float toOpacity, TimeSpan duration, TimeSpan delay = default)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            // Create the fade animation
            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.Duration = duration;
            opacityAnimation.DelayTime = delay;
            opacityAnimation.InsertKeyFrame(1.0f, toOpacity, compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.0f), new Vector2(0.2f, 1.0f)));

            // If we are fading in, make the element visible before the animation starts.
            if (toOpacity > 0)
            {
                element.Visibility = Visibility.Visible;
                // The animation will start from the visual's current opacity, which should be 0.
            }

            // Create a batch to know when the animation is complete
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            visual.StartAnimation("Opacity", opacityAnimation);
            batch.End();

            // When the animation batch completes...
            batch.Completed += (s, e) =>
            {
                // If we faded out, collapse the element to remove it from the layout.
                // IMPORTANT: This event runs on a background thread, so we must dispatch back to the UI thread
                // to change UI properties like Visibility.
                if (toOpacity == 0)
                {
                    element.DispatcherQueue.TryEnqueue(() =>
                    {
                        element.Visibility = Visibility.Collapsed;
                    });
                }
            };
        }
        // Add this if you haven't declared it yet, just to be absolutely sure:
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        /// <summary>
        /// This engine strictly filters out ghost windows, shadows, and background processes.
        /// It replicates the logic the actual Windows Taskbar uses to decide what to show.
        /// </summary>
        private bool IsValidTaskbarWindow(IntPtr hWnd)
        {
            // 1. Basic visibility check
            if (!IsWindowVisible(hWnd)) return false;

            // 2. Ignore UWP background ghost windows (Cloaking)
            if (IsCloaked(hWnd)) return false;

            // 3. Windows without a title are usually invisible system helpers
            if (GetWindowTextLength(hWnd) <= 0) return false;

            // 4. Check dimensions (Overlays and ghosts often spawn as 0x0 or 1x1 windows)
            GetWindowRect(hWnd, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width < 10 || height < 10) return false;

            // 5. Read window styles
            long exStyle = (long)GetWindowLongPtr(hWnd, GWL_EXSTYLE);

            // Filter out ToolWindows (like flying popups, hidden helpers)
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;

            // 6. Parent/Owner logic (The classic Alt+Tab rule)
            // If a window has an owner, it shouldn't be on the taskbar, UNLESS it explicitly forces it via WS_EX_APPWINDOW.
            IntPtr ownerHwnd = GetWindow(hWnd, (uint)GetWindowCmd.GW_OWNER);
            bool hasAppWindowStyle = (exStyle & WS_EX_APPWINDOW) != 0;

            if (ownerHwnd != IntPtr.Zero && !hasAppWindowStyle)
                return false;

            // Passed all checks - this is a real, interactable application window!
            return true;
        }
        private async Task RefreshAppListAsync()
        {
            // We always scan to keep the list updated, even while playing
            var processDataList = await Task.Run(() =>
            {
                var dataList = new List<ProcessData>();
                var seenHwnds = new HashSet<IntPtr>();
                var selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                EnumWindows((hWnd, lParam) =>
                {
                    // 1. Exclude our own GCM window
                    if (hWnd == selfHwnd) return true;

                    // 2. Ask our new "Rock Solid" engine if this is a real window
                    if (!IsValidTaskbarWindow(hWnd)) return true;

                    // 3. Get the title
                    int textLen = GetWindowTextLength(hWnd);
                    var titleBuilder = new StringBuilder(textLen + 1);
                    GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                    string windowTitle = titleBuilder.ToString();

                    // 4. Check against your custom title blacklist
                    if (_excludedTitles.Any(t => windowTitle.Contains(t, StringComparison.OrdinalIgnoreCase))) return true;

                    Process proc = null;
                    string exePath = null;
                    string exeName = "";

                    try
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);

                        // Ignore core system processes and ourselves
                        if (pid == 0 || pid == 4 || pid == Process.GetCurrentProcess().Id) return true;

                        proc = Process.GetProcessById((int)pid);
                        exePath = proc.MainModule?.FileName;

                        if (!string.IsNullOrEmpty(exePath))
                            exeName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant().Trim();
                    }
                    catch
                    {
                        // We might not have access rights (e.g. admin processes). 
                        // We still show the window if it passed the Taskbar checks above.
                    }

                    ResolvedGameInfo resolvedGameInfo = ResolveGameInfo(proc, windowTitle, exePath, hWnd);

                    // 5. Check against your custom process blacklist
                    if (!string.IsNullOrEmpty(exeName) && ShouldExcludeProcessWindow(exeName, resolvedGameInfo)) return true;

                    // Prevent duplicates
                    if (!seenHwnds.Add(hWnd)) return true;

                    // SUCCESS: Add to our list
                    dataList.Add(new ProcessData
                    {
                        ProductName = windowTitle,
                        Hwnd = hWnd,
                        Proc = proc,
                        ExePath = exePath,
                        GameInfo = resolvedGameInfo
                    });
                    return true;

                }, IntPtr.Zero);

                return dataList;
            });

            _latestProcessData = processDataList.ToList();

            if (!_isShellUiReady || MainContent == null || MainContent.Visibility != Visibility.Visible)
            {
                _deferredProcessData = processDataList.ToList();
                return;
            }

            UpdateUiFromData(processDataList);
        }

        /// <summary>
        /// Collects all relevant, visible main windows.
        /// </summary>


        private void UpdateLayoutForFocus()
        {
            bool isLauncherRailFocused = _currentFocusArea == FocusArea.Launcher || _currentFocusArea == FocusArea.QuickLaunchers;

            if (isLauncherRailFocused)
            {
                AnimateOverlayOpacity(QuickLauncherPanel, 1.0, false);
            }
            else
            {
                AnimateOverlayOpacity(QuickLauncherPanel, 0.0, false);
            }

            LauncherColumn.Width = new GridLength(1, GridUnitType.Auto);
            CardsColumn.Width = new GridLength(1, GridUnitType.Star);
            ColumnSeparator.Visibility = Visibility.Visible;

            foreach (var card in _launcherAreaButtons)
            {
                card.Opacity = 1.0;
                card.Visibility = Visibility.Visible;
            }

            _ = RefreshAppListAsync();
        }

        private double GetLauncherRailReservedWidth(double scale)
        {
            int launcherCount = Math.Max(1, _launcherAreaButtons?.Count ?? 1);
            double mainCardWidth = (232 + 18) * scale;
            double secondaryCardWidth = (176 + 18) * scale;
            double spacing = Math.Max(0, launcherCount - 1) * (18 * scale);
            double panelLeadIn = (14 + 28) * scale;

            return mainCardWidth + (Math.Max(0, launcherCount - 1) * secondaryCardWidth) + spacing + panelLeadIn;
        }

        private double GetShellLayoutScale()
        {
            GetPhysicalResolution(out int physicalWidth, out int physicalHeight);
            TryGetLogicalViewport(out double logicalWidth, out double logicalHeight);

            double shortSide = Math.Min(logicalWidth, logicalHeight);
            double longSide = Math.Max(logicalWidth, logicalHeight);
            bool ultraHdClass = physicalWidth >= 3400 || physicalHeight >= 1900;

            if (ultraHdClass)
            {
                if (shortSide >= 1000 && longSide >= 1700)
                {
                    return 1.25;
                }

                if (shortSide >= 850 && longSide >= 1450)
                {
                    return 1.08;
                }

                return 0.96;
            }

            if (shortSide <= 820 || longSide <= 1400)
            {
                return 0.94;
            }

            if (shortSide >= 1200 && longSide >= 2200)
            {
                return 1.08;
            }

            return 1.0;
        }

        private void ApplyResponsiveShellSizing(bool rebuildCards = false)
        {
            if (ShellStage == null || LauncherAreaPanel == null || ProgramCardPanel == null || BottomLegendBar == null)
            {
                return;
            }

            double layoutScale = GetShellLayoutScale();
            double scale = layoutScale * GetThemeCardScaleMultiplier();
            double dockScale = layoutScale * GetThemeDockScaleMultiplier();
            double topDockScale = layoutScale * GetThemeTopDockScaleMultiplier();
            bool scaleChanged = Math.Abs(_currentShellLayoutScale - scale) > 0.01;
            _currentShellLayoutScale = scale;
            _currentTopPanelScale = topDockScale;

            ShellStage.Margin = new Thickness(32 * scale, 0, 32 * scale, 72 * scale);
            double shellStageWidth = Math.Max(1180 * scale, 1920 - ShellStage.Margin.Left - ShellStage.Margin.Right);
            ShellStage.Width = shellStageWidth;
            ShellStage.MaxWidth = shellStageWidth;

            LauncherAreaPanel.Spacing = 18 * scale;
            LauncherAreaPanel.Margin = new Thickness(14 * scale, 0, 0, 0);
            ProgramCardPanel.Spacing = 18 * scale;
            ProgramCardPanel.Padding = new Thickness(18 * scale, 10 * scale, 20 * scale, 10 * scale);

            ColumnSeparator.Height = 210 * scale;
            ColumnSeparator.Margin = new Thickness(0, 0, 28 * scale, 0);

            NoCardsMessage.FontSize = 22 * scale;
            NoCardsMessage.Margin = new Thickness(48 * scale, 0, 48 * scale, 0);

            LauncherColumn.Width = new GridLength(1, GridUnitType.Auto);
            CardsColumn.Width = new GridLength(1, GridUnitType.Star);

            double launcherRailWidth = GetLauncherRailReservedWidth(scale);
            double separatorReservation = ColumnSeparator.Width + ColumnSeparator.Margin.Right;
            double minimumProcessRailWidth = 280 * scale;
            double processRailWidth = Math.Max(minimumProcessRailWidth, shellStageWidth - launcherRailWidth - separatorReservation);
            ProgramScrollViewer.Margin = new Thickness(0, 0, 0, 0);
            ProgramScrollViewer.MinWidth = minimumProcessRailWidth;
            ProgramScrollViewer.Width = processRailWidth;
            ProgramScrollViewer.MaxWidth = processRailWidth;

            ApplyThemeToBottomLegend(layoutScale, dockScale);
            ApplyThemeToTopDock(layoutScale, topDockScale);

            if (InfoPanelTransform != null)
            {
                double focusedScale = _currentFocusArea == FocusArea.TopButtons ? topDockScale * 1.05 : topDockScale;
                InfoPanelTransform.ScaleX = focusedScale;
                InfoPanelTransform.ScaleY = focusedScale;
            }

            ApplyTopBarButtonScale(ExitGcmButton, topDockScale);
            ApplyTopBarButtonScale(VolumeButton, topDockScale);
            ApplyTopBarButtonScale(SettingsButton, topDockScale);
            ApplyTopBarButtonScale(AppLauncherButton, topDockScale);
            ApplyTopBarButtonScale(ShutdownButton, topDockScale);

            if (NetworkStatusIcon != null) NetworkStatusIcon.FontSize = 16 * topDockScale;
            if (BatteryIcon != null) BatteryIcon.FontSize = 16 * topDockScale;
            if (BatteryPercentageText != null) BatteryPercentageText.FontSize = 14 * topDockScale;
            if (ControllerBatteryText != null) ControllerBatteryText.FontSize = 14 * topDockScale;
            if (ClockText != null)
            {
                ClockText.FontSize = 20 * topDockScale;

                if (ClockText.Parent is Border clockBorder)
                {
                    clockBorder.CornerRadius = new CornerRadius(10 * topDockScale);
                    clockBorder.Padding = new Thickness(10 * topDockScale, 4 * topDockScale, 10 * topDockScale, 4 * topDockScale);
                    clockBorder.Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(28, 46, 70)));
                }
            }

            if ((rebuildCards || scaleChanged) && !_isRebuildingResponsiveShell)
            {
                _isRebuildingResponsiveShell = true;
                try
                {
                    LoadDynamicLauncherCards();

                    ProgramCardPanel.Children.Clear();
                    _cardCache.Clear();

                    if (_latestProcessData != null && _latestProcessData.Count > 0)
                    {
                        UpdateUiFromData(_latestProcessData);
                    }
                    else
                    {
                        NoCardsMessage.Visibility = Visibility.Visible;
                        ColumnSeparator.Visibility = Visibility.Visible;
                    }
                }
                finally
                {
                    _isRebuildingResponsiveShell = false;
                }
            }
        }

        private static void ApplyTopBarButtonScale(Button button, double scale)
        {
            if (button == null)
            {
                return;
            }

            button.Width = 50 * scale;
            button.Height = 40 * scale;
            button.CornerRadius = new CornerRadius(10 * scale);

            if (button.Content is FontIcon fontIcon)
            {
                fontIcon.FontSize = 18 * scale;
            }
            else if (button.Content is Image image)
            {
                image.Width = 20 * scale;
                image.Height = 20 * scale;
            }
        }

        private class LauncherCardItem
        {
            public string Name { get; set; }
            public string Subtitle { get; set; }
            public string Description { get; set; }
            public string ImagePath { get; set; }
            public string ExePath { get; set; }
            public string Arguments { get; set; }
            public bool IsPrimary { get; set; }
            public IntPtr Hwnd { get; set; }
            public Process Proc { get; set; }
            public ResolvedGameInfo GameInfo { get; set; }
            // We use Action<...> to pass click methods directly.
            public Action<object, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs> TapAction { get; set; }
        }
        /// <summary>
        /// Loads all configured launcher apps from the settings,
        /// creates the UI cards, and adds them to the launcher area.
        /// </summary>
        private void LoadDynamicLauncherCards()
        {
            _launcherAreaButtons = new List<Border>();
            LauncherAreaPanel.Children.Clear();

            ApplySteamOnlyMode();
            string mainLauncherIconPath = "ms-appx:///Assets/steam_logo.png";

            // 1. MAIN LAUNCHER (Wird groß erstellt: 250x250)
            var mainLauncherItem = new LauncherCardItem
            {
                Name = "Steam",
                Subtitle = "Launcher",
                Description = "Zuruck in Steam Big Picture und direkt wieder in die Hauptoberflache springen.",
                ImagePath = mainLauncherIconPath,
                IsPrimary = true,
                TapAction = (s, e) => SwitchToConfiguredLauncher()
            };
            var mainCard = CreateLauncherCard(mainLauncherItem);
            LauncherAreaPanel.Children.Add(mainCard);
            _launcherAreaButtons.Add(mainCard);

            if (_featuredGameProcessData?.GameInfo?.IsGame == true &&
                _featuredGameProcessData.Proc != null &&
                !_featuredGameProcessData.Proc.HasExited)
            {
                var gameLauncherItem = new LauncherCardItem
                {
                    Name = _featuredGameProcessData.GameInfo.DisplayName ?? _featuredGameProcessData.ProductName ?? "Game",
                    Subtitle = "Game",
                    Description = "Aktives Spiel direkt nach vorne holen.",
                    ImagePath = null,
                    ExePath = _featuredGameProcessData.ExePath,
                    Hwnd = _featuredGameProcessData.Hwnd,
                    Proc = _featuredGameProcessData.Proc,
                    GameInfo = _featuredGameProcessData.GameInfo,
                    TapAction = (s, e) => SwitchToFeaturedGame()
                };

                var gameCard = CreateLauncherCard(gameLauncherItem);
                LauncherAreaPanel.Children.Add(gameCard);
                _launcherAreaButtons.Add(gameCard);
            }

            if (_launcherAreaButtons.Count == 0)
            {
                _selectedLauncherAreaIndex = 0;
            }
            else if (_selectedLauncherAreaIndex >= _launcherAreaButtons.Count)
            {
                _selectedLauncherAreaIndex = _launcherAreaButtons.Count - 1;
            }
        }



        /// <summary>
        /// Creates a single, clickable launcher card based on the provided data.
        /// Now handles a custom image for the Main Launcher card with a "LAUNCHER" label.
        /// </summary>
        private Border CreateLauncherCard(LauncherCardItem item)
        {
            double scale = _currentShellLayoutScale;
            var contentGrid = new Grid();

            bool isMainLauncher = item.IsPrimary;
            bool isGameCard = item.GameInfo?.IsGame == true;
            var launcherBackgroundSource = isMainLauncher
                ? new BitmapImage(new Uri("ms-appx:///Assets/steam_launcher_background.jpg"))
                : null;

            var loadedImage = new Image
            {
                Name = "LoadedImage",
                Stretch = Stretch.UniformToFill,
                Opacity = 0.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                RenderTransform = new CompositeTransform()
            };

            var launcherBackgroundImage = isMainLauncher
                ? new Image
                {
                    Source = launcherBackgroundSource,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false
                }
                : null;

            var artworkFrame = new Border
            {
                Margin = new Thickness(5 * scale),
                CornerRadius = new CornerRadius(21 * scale),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 0, 0)),
                Child = isMainLauncher ? launcherBackgroundImage : loadedImage
            };

            var ambientGlow = new Border
            {
                CornerRadius = new CornerRadius(24 * scale),
                Background = new SolidColorBrush(GetThemeAccentColor((byte)(isMainLauncher ? 72 : 44)))
            };

            var glassEffect = new Border
            {
                Name = "GlassBase",
                CornerRadius = new CornerRadius(24 * scale),
                BorderBrush = new SolidColorBrush(GetThemeAccentColor(58)),
                BorderThickness = new Thickness(0.9),
                Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(
                    (byte)(isMainLauncher ? 8 : 20),
                    (byte)(isMainLauncher ? 14 : 28),
                    (byte)(isMainLauncher ? 22 : 48))))
            };

            var tileSection = new TextBlock
            {
                Text = (item.Subtitle ?? (isMainLauncher ? "Launcher" : "Pinned App")).ToUpperInvariant(),
                FontSize = 12 * scale,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(190, 230, 242, 255)),
                CharacterSpacing = (int)(80 * scale),
                Margin = new Thickness(20 * scale, 28 * scale, 20 * scale, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var titleText = new TextBlock
            {
                Text = item.Name.ToUpperInvariant(),
                FontSize = (isMainLauncher ? 20 : 15) * scale,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Bahnschrift"),
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };

            var titlePlate = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = (isMainLauncher ? 58 : 54) * scale,
                Margin = new Thickness(16 * scale, 0, 16 * scale, 14 * scale),
                Padding = new Thickness(16 * scale, 10 * scale, 16 * scale, 10 * scale),
                Background = new SolidColorBrush(GetThemeCardTintColor(176)),
                CornerRadius = new CornerRadius(18 * scale),
                Child = titleText
            };

            BitmapImage defaultIcon = !string.IsNullOrWhiteSpace(item.ImagePath)
                ? new BitmapImage(new Uri(item.ImagePath))
                : GetAppIconAsBitmapImage(item.ExePath) ?? new BitmapImage(new Uri("ms-appx:///Assets/game.png"));

            var iconImage = new Image
            {
                Source = defaultIcon,
                Width = (isMainLauncher ? 104 : 72) * scale,
                Height = (isMainLauncher ? 104 : 72) * scale,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, (isMainLauncher ? 10 : 4) * scale, 0, (isMainLauncher ? 12 : 10) * scale),
                RenderTransform = new CompositeTransform()
            };

            contentGrid.Children.Add(ambientGlow);
            contentGrid.Children.Add(artworkFrame);
            contentGrid.Children.Add(glassEffect);
            AddBubbleReflectionLayers(contentGrid, scale, isMainLauncher);
            contentGrid.Children.Add(tileSection);
            if (!isMainLauncher)
            {
                contentGrid.Children.Add(iconImage);
            }
            if (!isMainLauncher && !isGameCard)
            {
                contentGrid.Children.Add(titlePlate);
            }

            var cardBorder = new Border
            {
                Width = (isMainLauncher ? 232 : 176) * scale,
                Height = (isMainLauncher ? 256 : 188) * scale,
                CornerRadius = new CornerRadius(24 * scale),
                Margin = new Thickness(6 * scale, 0, 12 * scale, 0),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(2),
                Child = contentGrid,
                Tag = item,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            if (!isMainLauncher)
            {
                cardBorder.Loaded += (s, e) => ApplyNativeWinUI3Blur(glassEffect);
            }
            if (item.TapAction != null) cardBorder.Tapped += (s, e) => item.TapAction(s, e);

            if (item.GameInfo?.IsGame == true || (!item.IsPrimary && !string.IsNullOrWhiteSpace(item.ExePath)))
            {
                LoadCardImageAsync(cardBorder, loadedImage, iconImage, titleText, item.Name, item.ExePath, item.Proc, item.GameInfo);
            }

            return cardBorder;
        }

        private void AddBubbleReflectionLayers(Grid contentGrid, double scale, bool isPrimary)
        {
            double innerRadius = 21 * scale;

            var sheen = new Border
            {
                Name = "BubbleSheen",
                Margin = new Thickness(5 * scale),
                CornerRadius = new CornerRadius(innerRadius),
                Background = CreateBubbleSheenBrush(0, true, false),
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            var edgeLight = new Border
            {
                Name = "BubbleEdgeLight",
                Margin = new Thickness(8 * scale),
                CornerRadius = new CornerRadius(innerRadius),
                BorderThickness = new Thickness(0),
                Background = CreateBubbleEdgeBrush(0, true, false),
                Opacity = 0,
                IsHitTestVisible = false
            };

            var lowerReflection = new Border
            {
                Name = "BubbleLowerReflection",
                Margin = new Thickness(14 * scale, 0, 14 * scale, 8 * scale),
                Height = (isPrimary ? 44 : 34) * scale,
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(innerRadius),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop { Offset = 0.0, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                        new GradientStop { Offset = 0.28, Color = Windows.UI.Color.FromArgb(6, 255, 255, 255) },
                        new GradientStop { Offset = 0.64, Color = Windows.UI.Color.FromArgb(28, 255, 255, 255) },
                        new GradientStop { Offset = 1.0, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                    }
                },
                Opacity = 0,
                IsHitTestVisible = false
            };

            var sideGlint = new Border
            {
                Name = "BubbleSideGlint",
                Width = Math.Max(2.0, (isPrimary ? 3.4 : 2.6) * scale),
                Height = (isPrimary ? 154 : 112) * scale,
                Margin = new Thickness(10 * scale, 18 * scale, 10 * scale, 18 * scale),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(3 * scale),
                Background = CreateBubbleSideGlintBrush(1, false),
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            var topGlint = new Border
            {
                Name = "BubbleTopGlint",
                Height = Math.Max(2.0, (isPrimary ? 3.6 : 3.0) * scale),
                Margin = new Thickness(25 * scale, 15 * scale, 25 * scale, 0),
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(3 * scale),
                Background = CreateBubbleTopGlintBrush(0, false),
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            var cornerGlint = new Border
            {
                Name = "BubbleCornerGlint",
                Width = (isPrimary ? 40 : 30) * scale,
                Height = Math.Max(2.0, 3 * scale),
                Margin = new Thickness(12 * scale, 18 * scale, 12 * scale, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(3 * scale),
                Background = CreateBubbleCornerGlintBrush(1, false),
                Opacity = 0,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Windows.Foundation.Point(0.0, 0.5),
                RenderTransform = new CompositeTransform { Rotation = 10 }
            };

            contentGrid.Children.Add(sheen);
            contentGrid.Children.Add(lowerReflection);
            contentGrid.Children.Add(edgeLight);
            contentGrid.Children.Add(sideGlint);
            contentGrid.Children.Add(cornerGlint);
            contentGrid.Children.Add(topGlint);
        }

        private LinearGradientBrush CreateBubbleEdgeBrush(int lightDirection, bool isSelected, bool isNeighborHighlight)
        {
            byte hotAlpha = (byte)(isNeighborHighlight ? 82 : isSelected ? 36 : 46);
            byte midAlpha = (byte)(isNeighborHighlight ? 54 : isSelected ? 22 : 30);
            byte accentAlpha = (byte)(isNeighborHighlight ? 62 : isSelected ? 26 : 34);

            Windows.Foundation.Point start;
            Windows.Foundation.Point end;

            if (lightDirection > 0)
            {
                start = new Windows.Foundation.Point(0, 0.15);
                end = new Windows.Foundation.Point(1, 0.85);
            }
            else if (lightDirection < 0)
            {
                start = new Windows.Foundation.Point(1, 0.15);
                end = new Windows.Foundation.Point(0, 0.85);
            }
            else
            {
                start = new Windows.Foundation.Point(0, 0);
                end = new Windows.Foundation.Point(1, 1);
            }

            return new LinearGradientBrush
            {
                StartPoint = start,
                EndPoint = end,
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.20, Color = Windows.UI.Color.FromArgb(6, 255, 255, 255) },
                    new GradientStop { Offset = 0.40, Color = Windows.UI.Color.FromArgb(hotAlpha, 255, 255, 255) },
                    new GradientStop { Offset = 0.56, Color = GetThemeAccentColor(accentAlpha) },
                    new GradientStop { Offset = 0.76, Color = Windows.UI.Color.FromArgb(midAlpha, 235, 248, 255) },
                    new GradientStop { Offset = 0.92, Color = Windows.UI.Color.FromArgb(4, 255, 255, 255) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateBubbleSheenBrush(int lightDirection, bool isSelected, bool isNeighborHighlight)
        {
            byte bright = (byte)(isNeighborHighlight ? 74 : isSelected ? 30 : 40);
            byte accent = (byte)(isNeighborHighlight ? 58 : isSelected ? 22 : 28);

            Windows.Foundation.Point start = lightDirection < 0
                ? new Windows.Foundation.Point(1, 0)
                : new Windows.Foundation.Point(0, 0);
            Windows.Foundation.Point end = lightDirection < 0
                ? new Windows.Foundation.Point(0, 1)
                : new Windows.Foundation.Point(1, 1);

            return new LinearGradientBrush
            {
                StartPoint = start,
                EndPoint = end,
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.22, Color = Windows.UI.Color.FromArgb(4, 255, 255, 255) },
                    new GradientStop { Offset = 0.43, Color = Windows.UI.Color.FromArgb(bright, 255, 255, 255) },
                    new GradientStop { Offset = 0.58, Color = GetThemeAccentColor(accent) },
                    new GradientStop { Offset = 0.82, Color = Windows.UI.Color.FromArgb(4, 255, 255, 255) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateBubbleSideGlintBrush(int lightDirection, bool isNeighborHighlight)
        {
            byte hot = (byte)(isNeighborHighlight ? 210 : 118);
            byte soft = (byte)(isNeighborHighlight ? 72 : 38);

            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.18, Color = Windows.UI.Color.FromArgb(8, 255, 255, 255) },
                    new GradientStop { Offset = 0.38, Color = Windows.UI.Color.FromArgb(soft, 255, 255, 255) },
                    new GradientStop { Offset = 0.50, Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = 0.62, Color = GetThemeAccentColor((byte)Math.Clamp(soft + 16, 0, 130)) },
                    new GradientStop { Offset = 0.82, Color = Windows.UI.Color.FromArgb(8, 255, 255, 255) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateBubbleCornerGlintBrush(int lightDirection, bool isNeighborHighlight)
        {
            byte hot = (byte)(isNeighborHighlight ? 178 : 104);
            byte soft = (byte)(isNeighborHighlight ? 50 : 26);

            return new LinearGradientBrush
            {
                StartPoint = lightDirection < 0
                    ? new Windows.Foundation.Point(1, 0)
                    : new Windows.Foundation.Point(0, 0),
                EndPoint = lightDirection < 0
                    ? new Windows.Foundation.Point(0, 0)
                    : new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.22, Color = Windows.UI.Color.FromArgb(soft, 255, 255, 255) },
                    new GradientStop { Offset = 0.52, Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = 0.76, Color = GetThemeAccentColor((byte)Math.Clamp(soft + 14, 0, 100)) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateBubbleTopGlintBrush(int lightDirection, bool isNeighborHighlight)
        {
            byte hot = (byte)(isNeighborHighlight ? 120 : 72);
            byte accent = (byte)(isNeighborHighlight ? 56 : 32);

            double center = lightDirection < 0 ? 0.78 : lightDirection > 0 ? 0.22 : 0.5;

            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = Math.Max(0, center - 0.28), Color = Windows.UI.Color.FromArgb(6, 255, 255, 255) },
                    new GradientStop { Offset = Math.Max(0, center - 0.08), Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = Math.Min(1, center + 0.08), Color = GetThemeAccentColor(accent) },
                    new GradientStop { Offset = Math.Min(1, center + 0.28), Color = Windows.UI.Color.FromArgb(5, 255, 255, 255) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }



        private bool _isRefreshRunning = false;

        private void StartAutoTaskRefresh()
        {
            if (_taskRefreshTimer != null) return;

            // Intervall auf 3 Sekunden hochsetzen, wenn wir nicht im Fokus sind
            _taskRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _taskRefreshTimer.Tick += async (s, e) =>
            {
                if (_isRefreshRunning || !_isShellUiReady || !IsWindowInForeground()) return; // Scanne nur, wenn GCM offen ist!

                _isRefreshRunning = true;
                await RefreshAppListAsync();
                _isRefreshRunning = false;
            };
            _taskRefreshTimer.Start();
        }


        /// <summary>
        /// Berechnet die Ähnlichkeit von zwei Strings zwischen 0.0 (komplett anders) und 1.0 (identisch).
        /// </summary>
        public static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0.0;

            // Normalisiere die Strings für einen besseren Vergleich
            source = source.ToLower().Trim();
            target = target.ToLower().Trim();

            if (source == target) return 1.0;

            int stepsToSame = LevenshteinDistance(source, target);
            return (1.0 - ((double)stepsToSame / (double)Math.Max(source.Length, target.Length)));
        }

        /// <summary>
        /// Berechnet die Levenshtein-Distanz zwischen zwei Strings.
        /// </summary>
        private static int LevenshteinDistance(string source, string target)
        {
            int n = source.Length;
            int m = target.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }


        private async Task ShowDebugDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                // CORRECTED: Get the XamlRoot from the Window's Content property, not the Window itself.
                XamlRoot = this.Content.XamlRoot,
                Title = "[DEBUG] " + title,
                Content = message,
                CloseButtonText = "Weiter"
            };
            await dialog.ShowAsync();
        }
        private string CleanGameNameForSearch(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return string.Empty;

            // Remove common trademark symbols
            string cleanedName = rawName.Replace("™", "").Replace("®", "").Replace("©", "");

            // Remove text in brackets (e.g., "(64-bit)") or parentheses
            cleanedName = System.Text.RegularExpressions.Regex.Replace(cleanedName, @"\s*[\(\[].*?[\)\]]", "");

            // Remove common version patterns like "v1.2.3" or "Build 4567"
            cleanedName = System.Text.RegularExpressions.Regex.Replace(cleanedName, @"\s*v\d+(\.\d+)*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleanedName = System.Text.RegularExpressions.Regex.Replace(cleanedName, @"\s*Build\s*\d+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Trim any resulting whitespace from the ends
            return cleanedName.Trim();
        }
        // In gcmloader/MainWindow.xaml.cs

        private List<string> GetAllSteamLibraryPaths()
        {
            var paths = new List<string>();
            string mainSteamPath = GetSteamInstallPath();

            if (string.IsNullOrEmpty(mainSteamPath))
            {
                Logger.Log("[DEBUG] GetAllSteamLibraryPaths: Main Steam install path not found. Aborting.");
                return paths;
            }

            // 1. Add the main install path's steamapps folder
            string mainSteamApps = Path.Combine(mainSteamPath, "steamapps");
            if (Directory.Exists(mainSteamApps))
            {
                paths.Add(mainSteamApps);
                Logger.Log($"[DEBUG] GetAllSteamLibraryPaths: Found main library: {mainSteamApps}");
            }

            // 2. Parse libraryfolders.vdf for all other paths
            string vdfPath = Path.Combine(mainSteamApps, "libraryfolders.vdf");
            if (!File.Exists(vdfPath))
            {
                Logger.Log("[DEBUG] GetAllSteamLibraryPaths: libraryfolders.vdf not found. Only main library will be used.");
                return paths;
            }

            try
            {
                string vdfContent = File.ReadAllText(vdfPath);
                // Regex to find all "path" "D:\\Some\\Path" entries
                var matches = Regex.Matches(vdfContent, "\"path\"\\s+\"([^\"]+)\"");

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        // Path from VDF is escaped, e.g., "D:\\\\SteamLibrary"
                        string libPath = Regex.Unescape(match.Groups[1].Value);

                        // Skip if it's just the main path again
                        if (libPath.Equals(mainSteamPath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string libSteamApps = Path.Combine(libPath, "steamapps");
                        if (Directory.Exists(libSteamApps) && !paths.Contains(libSteamApps))
                        {
                            paths.Add(libSteamApps);
                            Logger.Log($"[DEBUG] GetAllSteamLibraryPaths: Found additional library: {libSteamApps}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[DEBUG] GetAllSteamLibraryPaths: ERROR parsing libraryfolders.vdf: {ex.Message}");
            }

            return paths;
        }



        private void AnimateCrossFade(Image imageToFadeIn, Image imageToFadeOut)
        {
            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(300)); // 0.3s fade

            // 1. Fade-in animation for the new image
            var fadeIn = new DoubleAnimation
            {
                To = 1.0,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, imageToFadeIn);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            storyboard.Children.Add(fadeIn);

            // 2. Fade-out animation for the default icon
            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeOut, imageToFadeOut);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            storyboard.Children.Add(fadeOut);

            // 3. When finished, hide the default icon completely
            storyboard.Completed += (s, e) =>
            {
                imageToFadeOut.Visibility = Visibility.Collapsed;
            };

            storyboard.Begin();
        }

        /// <summary>
        /// Tries to find a Steam AppID by searching all local manifest files
        /// for a matching game name (window title). Used for Anti-Cheat processes.
        /// </summary>
        private string FindAppIdFromGameNameLocallySync(string gameName)
        {
            Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: Searching for AppID for '{gameName}'...");

            // Check cache first
            string cleanedName = CleanGameNameForSearch(gameName);
            if (string.IsNullOrWhiteSpace(cleanedName))
            {
                return null;
            }

            if (_localGameNameCache.TryGetValue(cleanedName, out string cachedAppId))
            {
                Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: Found AppID '{cachedAppId ?? "null"}' in name cache.");
                return cachedAppId;
            }

            // Get all library paths (cached)
            if (_steamLibraryPathsCache == null)
            {
                _steamLibraryPathsCache = GetAllSteamLibraryPaths();
            }

            if (_steamLibraryPathsCache == null || !_steamLibraryPathsCache.Any())
            {
                Logger.Log("[DEBUG] FindAppIdFromGameNameLocally: No Steam library paths found.");
                return null;
            }

            string bestAppId = null;
            double highestSimilarity = 0.0;

            // Search all manifest files in all libraries
            foreach (string libPath in _steamLibraryPathsCache)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(libPath, "appmanifest_*.acf"))
                    {
                        string content = File.ReadAllText(file);

                        // Regex to find the "name" "Some Game Name"
                        var nameMatch = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"");
                        if (nameMatch.Success && nameMatch.Groups.Count > 1)
                        {
                            string manifestGameName = nameMatch.Groups[1].Value;

                            // Check similarity
                            double similarity = CalculateSimilarity(cleanedName, CleanGameNameForSearch(manifestGameName));

                            // We need a high threshold to avoid mismatches (e.g., "Demo" matching "Game")
                            if (similarity > 0.85 && similarity > highestSimilarity)
                            {
                                highestSimilarity = similarity;
                                var appMatch = Regex.Match(Path.GetFileName(file), @"appmanifest_(\d+)\.acf");
                                if (appMatch.Success)
                                {
                                    bestAppId = appMatch.Groups[1].Value;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: Error scanning path '{libPath}': {ex.Message}");
                }
            }

            if (bestAppId != null)
            {
                Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: Found best match for '{cleanedName}' -> AppID {bestAppId} (Similarity: {highestSimilarity:P2})");
                _localGameNameCache[cleanedName] = bestAppId; // Add to cache
                return bestAppId;
            }

            Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: No local manifest match found for '{cleanedName}'.");
            _localGameNameCache[cleanedName] = null; // Add null to cache to prevent re-scan
            return null;
        }

        private Task<string> FindAppIdFromGameNameLocally(string gameName)
        {
            return Task.Run(() => FindAppIdFromGameNameLocallySync(gameName));
        }

        private static bool ContainsKeyword(string text, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private string ResolveGameDisplayName(string windowTitle, string exePath, Process proc)
        {
            string cleanedTitle = CleanGameNameForSearch(windowTitle);
            if (!string.IsNullOrWhiteSpace(cleanedTitle) && !ContainsKeyword(cleanedTitle, _nonGameKeywords))
            {
                return cleanedTitle;
            }

            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(versionInfo.ProductName) && !ContainsKeyword(versionInfo.ProductName, _nonGameKeywords))
                    {
                        return CleanGameNameForSearch(versionInfo.ProductName);
                    }

                    if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription) && !ContainsKeyword(versionInfo.FileDescription, _nonGameKeywords))
                    {
                        return CleanGameNameForSearch(versionInfo.FileDescription);
                    }
                }
                catch { }

                return Path.GetFileNameWithoutExtension(exePath);
            }

            if (proc != null)
            {
                try
                {
                    return proc.ProcessName;
                }
                catch { }
            }

            return cleanedTitle;
        }

        private string BuildGameResolutionCacheKey(string windowTitle, string exePath, Process proc)
        {
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                string cleanedTitleForKey = CleanGameNameForSearch(windowTitle);
                if (!string.IsNullOrWhiteSpace(cleanedTitleForKey))
                {
                    return $"{exePath.Trim()}|{cleanedTitleForKey.ToLowerInvariant()}";
                }

                return exePath.Trim();
            }

            if (proc != null)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(proc.ProcessName))
                    {
                        return $"proc:{proc.ProcessName.Trim().ToLowerInvariant()}";
                    }
                }
                catch { }
            }

            string cleanedTitle = CleanGameNameForSearch(windowTitle);
            return $"title:{cleanedTitle.ToLowerInvariant()}";
        }

        private string GetProcessLineageCacheKey(Process proc)
        {
            if (proc == null)
            {
                return string.Empty;
            }

            try
            {
                return $"{proc.Id}:{proc.StartTime.ToUniversalTime().Ticks}";
            }
            catch
            {
                return $"pid:{proc.Id}";
            }
        }

        private IReadOnlyList<string> GetProcessLineage(Process proc)
        {
            if (proc == null)
            {
                return Array.Empty<string>();
            }

            string cacheKey = GetProcessLineageCacheKey(proc);
            if (_processLineageCache.TryGetValue(cacheKey, out IReadOnlyList<string> cachedLineage))
            {
                return cachedLineage;
            }

            var lineage = new List<string>();

            try
            {
                int currentPid = proc.Id;

                for (int depth = 0; depth < 6 && currentPid > 0; depth++)
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT ParentProcessId, Name FROM Win32_Process WHERE ProcessId = {currentPid}");

                    var processObject = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (processObject == null)
                    {
                        break;
                    }

                    string processName = processObject["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(processName))
                    {
                        lineage.Add(Path.GetFileNameWithoutExtension(processName).ToLowerInvariant());
                    }

                    currentPid = Convert.ToInt32(processObject["ParentProcessId"] ?? 0);
                }
            }
            catch
            {
            }

            _processLineageCache[cacheKey] = lineage;
            return lineage;
        }

        private bool IsWindowLargeGameCandidate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                GetWindowRect(hwnd, out RECT rect);

                double width = Math.Max(0, rect.Right - rect.Left);
                double height = Math.Max(0, rect.Bottom - rect.Top);
                if (width < 640 || height < 360)
                {
                    return false;
                }

                double screenWidth = Math.Max(1, GetScreenWidth());
                double screenHeight = Math.Max(1, GetScreenHeight());
                double windowArea = width * height;
                double screenArea = screenWidth * screenHeight;
                double coverage = windowArea / screenArea;

                return coverage >= 0.45 ||
                       (width >= screenWidth * 0.80 && height >= screenHeight * 0.70);
            }
            catch
            {
                return false;
            }
        }

        private bool ShouldExcludeProcessWindow(string exeName, ResolvedGameInfo gameInfo)
        {
            if (string.IsNullOrWhiteSpace(exeName))
            {
                return false;
            }

            if (gameInfo?.IsGame == true &&
                (exeName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                 exeName.Equals("RuntimeBroker", StringComparison.OrdinalIgnoreCase) ||
                 exeName.Equals("GameLaunchHelper", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (_softExcludedProcessNames.Contains(exeName))
            {
                return gameInfo?.IsGame != true;
            }

            return _excludedProcessNames.Contains(exeName, StringComparer.OrdinalIgnoreCase);
        }

        private ResolvedGameInfo ResolveGameInfo(Process proc, string windowTitle, string exePath, IntPtr hwnd)
        {
            string cacheKey = BuildGameResolutionCacheKey(windowTitle, exePath, proc);
            if (_resolvedGameInfoCache.TryGetValue(cacheKey, out ResolvedGameInfo cachedInfo))
            {
                if (string.IsNullOrWhiteSpace(cachedInfo.DisplayName))
                {
                    cachedInfo.DisplayName = ResolveGameDisplayName(windowTitle, exePath, proc);
                }

                PopulateLocalSteamArtworkPaths(cachedInfo);
                return cachedInfo;
            }

            string cleanedTitle = CleanGameNameForSearch(windowTitle);
            string exeName = string.Empty;
            string productName = string.Empty;
            string fileDescription = string.Empty;

            if (proc != null)
            {
                try { exeName = proc.ProcessName; } catch { }
            }

            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                try
                {
                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    productName = versionInfo.ProductName ?? string.Empty;
                    fileDescription = versionInfo.FileDescription ?? string.Empty;
                }
                catch
                {
                }
            }

            var info = new ResolvedGameInfo
            {
                CacheKey = cacheKey,
                DisplayName = ResolveGameDisplayName(windowTitle, exePath, proc)
            };

            bool largeWindow = IsWindowLargeGameCandidate(hwnd);
            string packageFullName = TryGetPackageFullName(proc);
            bool uwpProcess = !string.IsNullOrWhiteSpace(packageFullName) || IsUwpProcess(proc);
            bool pathSuggestsGame = !string.IsNullOrWhiteSpace(exePath) && _gamePathKeywords.Any(keyword => exePath.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            bool windowsStoreGameCandidate =
                largeWindow &&
                !string.IsNullOrWhiteSpace(exePath) &&
                exePath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
            string stableArtworkKey = GetStableCacheKey(info.DisplayName ?? windowTitle, exePath, proc);
            bool hasCachedArtwork = !string.IsNullOrWhiteSpace(FindCachedImageFile(stableArtworkKey));
            bool titleLooksNonGame = ContainsKeyword(cleanedTitle, _nonGameKeywords) || ContainsKeyword(info.DisplayName, _nonGameKeywords);
            bool exeLooksNonGame = ContainsKeyword(exeName, _nonGameKeywords) || ContainsKeyword(productName, _nonGameKeywords) || ContainsKeyword(fileDescription, _nonGameKeywords);
            bool hardNonGameProcess = _hardNonGameProcessNames.Contains(exeName);
            bool launcherShellLike = ContainsKeyword(cleanedTitle, _gameLauncherShellKeywords) ||
                                     ContainsKeyword(info.DisplayName, _gameLauncherShellKeywords) ||
                                     ContainsKeyword(exeName, _gameLauncherShellKeywords) ||
                                     ContainsKeyword(fileDescription, _gameLauncherShellKeywords);
            IReadOnlyList<string> lineage = GetProcessLineage(proc);
            bool launchedFromSteam = lineage.Any(name => name.Equals("steam", StringComparison.OrdinalIgnoreCase) || name.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase));
            bool launchedFromKnownGameClient = lineage.Any(name => _gameLauncherProcessNames.Contains(name));
            bool launchedFromStoreBootstrap = lineage.Any(name => _softExcludedProcessNames.Contains(name));
            bool packagedGameCandidate = uwpProcess && largeWindow && !titleLooksNonGame && !exeLooksNonGame;
            bool steamLibraryApp = false;

            var reasons = new List<string>();
            int score = 0;

            if (!string.IsNullOrWhiteSpace(exePath) && exePath.Contains(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase))
            {
                string steamAppId = GetSteamAppIdFromExePath(exePath);
                if (!string.IsNullOrWhiteSpace(steamAppId))
                {
                    info.SteamAppId = steamAppId;
                    steamLibraryApp = true;
                    score += 16;
                    reasons.Add("steam-appid-from-path");
                }
            }

            if (pathSuggestsGame)
            {
                score += 7;
                reasons.Add("game-library-path");
            }

            if (windowsStoreGameCandidate || packagedGameCandidate)
            {
                score += 7;
                reasons.Add("store-game-window");
            }

            if (launchedFromSteam)
            {
                score += 4;
                reasons.Add("steam-lineage");
            }

            if (launchedFromKnownGameClient)
            {
                score += 4;
                reasons.Add("game-client-lineage");
            }

            if (launchedFromStoreBootstrap)
            {
                score += 2;
                reasons.Add("launcher-lineage");
            }

            if (hasCachedArtwork)
            {
                score += 6;
                reasons.Add("cached-artwork");
            }

            if (largeWindow)
            {
                score += 3;
                reasons.Add("large-window");
            }

            if (!titleLooksNonGame && !string.IsNullOrWhiteSpace(cleanedTitle))
            {
                score += 2;
                reasons.Add("game-like-title");
            }

            if (!exeLooksNonGame && (!string.IsNullOrWhiteSpace(productName) || !string.IsNullOrWhiteSpace(fileDescription)))
            {
                score += 2;
                reasons.Add("game-like-product");
            }

            if (launcherShellLike)
            {
                score -= 5;
                reasons.Add("launcher-shell");
            }

            if (titleLooksNonGame)
            {
                score -= 4;
                reasons.Add("non-game-title");
            }

            if (exeLooksNonGame)
            {
                score -= 7;
                reasons.Add("non-game-product");
            }

            if (hardNonGameProcess)
            {
                score -= 24;
                reasons.Add("hard-non-game-process");
            }

            if (ShouldExcludeProcessWindow(exeName, null) && !_softExcludedProcessNames.Contains(exeName))
            {
                score -= 10;
                reasons.Add("excluded-process");
            }

            if (string.IsNullOrWhiteSpace(info.SteamAppId) && score >= 2)
            {
                string appIdFromTitle = FindAppIdFromGameNameLocallySync(info.DisplayName ?? cleanedTitle);
                if (!string.IsNullOrWhiteSpace(appIdFromTitle))
                {
                    info.SteamAppId = appIdFromTitle;
                    score += 8;
                    reasons.Add("steam-appid-from-title");
                }
            }

            bool hasSteamAppId = !string.IsNullOrWhiteSpace(info.SteamAppId);
            bool hasStrongGameEvidence =
                hasSteamAppId ||
                steamLibraryApp ||
                hasCachedArtwork ||
                (pathSuggestsGame && !hardNonGameProcess && !exeLooksNonGame) ||
                (packagedGameCandidate && !hardNonGameProcess) ||
                (windowsStoreGameCandidate && !hardNonGameProcess && !titleLooksNonGame) ||
                (launchedFromKnownGameClient && largeWindow && !hardNonGameProcess && !titleLooksNonGame && !exeLooksNonGame);

            bool isProbablyLauncherOnly =
                launcherShellLike &&
                !hasSteamAppId &&
                !pathSuggestsGame &&
                !hasCachedArtwork &&
                !(launchedFromKnownGameClient && largeWindow);

            info.Score = score;
            info.IsGame =
                !hardNonGameProcess &&
                !isProbablyLauncherOnly &&
                hasStrongGameEvidence &&
                (hasSteamAppId || score >= 6 || (largeWindow && (launchedFromSteam || launchedFromStoreBootstrap)));
            info.DetectionSummary = string.Join(", ", reasons);

            if (info.IsGame)
            {
                PopulateLocalSteamArtworkPaths(info);
                if (!string.IsNullOrWhiteSpace(info.PosterImagePath))
                {
                    info.Score += 1;
                }
                if (!string.IsNullOrWhiteSpace(info.HeroImagePath))
                {
                    info.Score += 1;
                }
            }

            _resolvedGameInfoCache[cacheKey] = info;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                _resolvedGameInfoCache[exePath] = info;
            }

            return info;
        }

        private string GetFeaturedGameIdentity(ProcessData processData)
        {
            if (processData?.GameInfo == null || !processData.GameInfo.IsGame)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(processData.GameInfo.SteamAppId))
            {
                return $"steam:{processData.GameInfo.SteamAppId}";
            }

            if (!string.IsNullOrWhiteSpace(processData.ExePath))
            {
                return $"exe:{processData.ExePath}";
            }

            return $"hwnd:{processData.Hwnd}";
        }

        private string GetFeaturedGameDisplayName(ProcessData processData)
        {
            if (processData == null)
            {
                return "Unknown game";
            }

            string displayName = processData.GameInfo?.DisplayName;

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = processData.ProductName;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                try
                {
                    displayName = processData.Proc?.ProcessName;
                }
                catch { }
            }

            return string.IsNullOrWhiteSpace(displayName) ? "Unknown game" : displayName.Trim();
        }

        private ProcessData SelectFeaturedGameProcessData(IEnumerable<ProcessData> processDataList)
        {
            if (processDataList == null)
            {
                return null;
            }

            string currentIdentity = GetFeaturedGameIdentity(_featuredGameProcessData);

            return processDataList
                .Where(data => data?.GameInfo?.IsGame == true)
                .OrderByDescending(data => GetFeaturedGameIdentity(data) == currentIdentity)
                .ThenByDescending(data => data.GameInfo?.Score ?? int.MinValue)
                .ThenByDescending(data => !string.IsNullOrWhiteSpace(data.GameInfo?.SteamAppId))
                .ThenByDescending(data => !string.IsNullOrWhiteSpace(data.GameInfo?.HeroImagePath))
                .ThenByDescending(data => !string.IsNullOrWhiteSpace(data.GameInfo?.PosterImagePath))
                .FirstOrDefault();
        }
        private async Task LoadFromSteamGridDbAsync(
            Border card,                // The main card
            Image loadedImageControl,   // The (still invisible) image to fade in
            Image defaultIconControl,   // The (visible) default icon
            TextBlock titleControl,     // The title field
            string gameName,
            string exePath)
        {
            Logger.Log($"[DEBUG] LoadFromSteamGridDbAsync started. Game: {gameName}");
            try
            {
                string cleanedGameName = CleanGameNameForSearch(gameName);
                if (string.IsNullOrEmpty(cleanedGameName)) { Logger.Log("[DEBUG] SteamGridDB: Cleaned name is empty. Aborting."); return; }
                Logger.Log($"[DEBUG] SteamGridDB: Cleaned name: '{cleanedGameName}'");
                SearchResult searchResult = null;
                if (_gameIdCache.ContainsKey(cleanedGameName))
                {
                    searchResult = _gameIdCache[cleanedGameName];
                    Logger.Log($"[DEBUG] SteamGridDB: ID loaded from cache: {(searchResult?.id.ToString() ?? "null")}");
                }
                else
                {
                    searchResult = await _steamGridHelper.SearchForGameIdAsync(cleanedGameName);
                    _gameIdCache[cleanedGameName] = searchResult;
                    Logger.Log($"[DEBUG] SteamGridDB: API search returned: {(searchResult?.name ?? "NO HIT")}");
                }
                if (searchResult == null) { Logger.Log("[DEBUG] SteamGridDB: No ID found for this game. Aborting."); return; }
                double similarity = CalculateSimilarity(gameName, searchResult.name);
                Logger.Log($"[DEBUG] SteamGridDB: Similarity between '{gameName}' and '{searchResult.name}' is {similarity:P2}");
                if (similarity < 0.5) { Logger.Log("[DEBUG] SteamGridDB: Similarity too low. Aborting."); return; }
                var imageUrl = await _steamGridHelper.GetGridImageUrlAsync(searchResult.id);
                if (string.IsNullOrEmpty(imageUrl)) { Logger.Log($"[DEBUG] SteamGridDB: Game ID found, but no image URL present. Aborting."); return; }
                Logger.Log($"[DEBUG] SteamGridDB: Image URL found: {imageUrl}");
                string localImagePath = Path.Combine(_imageCachePath, $"{searchResult.id}.jpg");
                if (!File.Exists(localImagePath))
                {
                    Logger.Log("[DEBUG] SteamGridDB: Image is not in cache. Downloading...");
                    using (var client = new HttpClient())
                    {
                        var imageData = await client.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(localImagePath, imageData);
                    }
                    Logger.Log("[DEBUG] SteamGridDB: Download and save successful.");
                }
                else
                {
                    Logger.Log("[DEBUG] SteamGridDB: Image already present in cache.");
                }
           


                card.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        var fileBytes = await File.ReadAllBytesAsync(localImagePath);
                        using (var ms = new MemoryStream(fileBytes))
                        {
                            await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                        }

                        // Set the source for the (still invisible) image
                        loadedImageControl.Source = bitmap;
                        if (titleControl != null) titleControl.Text = searchResult.name; // Update the title

                        // Start the cross-fade animation
                        AnimateCrossFade(loadedImageControl, defaultIconControl);

                        Logger.Log($"[DEBUG] SteamGridDB: Image for '{searchResult.name}' loaded and fade-in started.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[DEBUG] SteamGridDB: ERROR displaying downloaded image: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[DEBUG] SteamGridDB: FATAL ERROR in LoadFromSteamGridDbAsync: {ex.Message}");
            }
        }

        private string NormalizeForMatch(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // Nur Buchstaben und Zahlen behalten, alles klein machen
            return new string(text.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant();
        }

        // In deine Helper-Region einfügen/ersetzen
        private string GetExeBasedCacheKey(string exePath, Process proc)
        {
            // 1. Prio: Dateiname der Exe (z.B. "deadspace3")
            if (!string.IsNullOrEmpty(exePath))
            {
                return Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant().Trim();
            }

            // 2. Prio: Prozessname
            if (proc != null)
            {
                try { return proc.ProcessName.ToLowerInvariant().Trim(); } catch { }
            }

            return "unknown_app";
        }
        // Hilfsmethode: Macht jeden String zum validen Dateinamen
        private string CleanFilename(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        // Die Fallback-Suche (Steam lokal -> Online) ausgelagert zur Übersicht
        private async void PerformFallbackSearch(
            Border card, Image img, Image icon, TextBlock txt,
            string gameName, string exePath, Process proc, string savePath, ResolvedGameInfo gameInfo)
        {
            string imagePath = await ResolveAndCacheGameArtworkAsync(gameName, exePath, proc, gameInfo, savePath);
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                await LoadImageToUiAsync(card, img, icon, txt, imagePath, gameInfo?.DisplayName);
            }
        }

        private async Task<string> ResolveAndCacheGameArtworkAsync(
            string gameName,
            string exePath,
            Process proc,
            ResolvedGameInfo gameInfo,
            string targetCachePath)
        {
            if (string.IsNullOrWhiteSpace(targetCachePath))
            {
                return null;
            }

            Directory.CreateDirectory(_imageCachePath);

            if (File.Exists(targetCachePath) && new FileInfo(targetCachePath).Length > 0)
            {
                return targetCachePath;
            }

            string CopyLocalArtwork(string sourcePath)
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    return null;
                }

                try
                {
                    File.Copy(sourcePath, targetCachePath, true);
                    InvalidateArtworkCacheLookup();
                    return targetCachePath;
                }
                catch
                {
                    return null;
                }
            }

            PopulateLocalSteamArtworkPaths(gameInfo);

            string copiedSteamPoster = CopyLocalArtwork(gameInfo?.PosterImagePath);
            if (!string.IsNullOrWhiteSpace(copiedSteamPoster))
            {
                return copiedSteamPoster;
            }

            if (!string.IsNullOrWhiteSpace(exePath))
            {
                string localSteamPath = await FindLocalSteamImageAsync(exePath);
                string copiedLocalSteam = CopyLocalArtwork(localSteamPath);
                if (!string.IsNullOrWhiteSpace(copiedLocalSteam))
                {
                    return copiedLocalSteam;
                }
            }

            string steamAppId = gameInfo?.SteamAppId;
            if (string.IsNullOrWhiteSpace(steamAppId))
            {
                steamAppId = await FindAppIdFromGameNameLocally(gameInfo?.DisplayName ?? gameName);
                if (!string.IsNullOrWhiteSpace(steamAppId) && gameInfo != null)
                {
                    gameInfo.SteamAppId = steamAppId;
                }
            }

            if (!string.IsNullOrWhiteSpace(steamAppId) &&
                await TryDownloadFirstArtworkCandidateAsync(GetSteamArtworkCandidateUrls(steamAppId), targetCachePath))
            {
                return targetCachePath;
            }

            if (_steamGridHelper?.IsApiKeySet == true)
            {
                string searchName = gameInfo?.DisplayName ?? GetSmartSearchName(gameName, exePath);
                string steamGridUrl = await ResolveSteamGridDbArtworkUrlAsync(searchName);
                if (await TryDownloadImageUrlToCacheAsync(steamGridUrl, targetCachePath))
                {
                    return targetCachePath;
                }
            }

            string steamStoreAppId = !string.IsNullOrWhiteSpace(steamAppId)
                ? steamAppId
                : await TryFindSteamStoreAppIdAsync(gameInfo?.DisplayName ?? gameName);

            if (!string.IsNullOrWhiteSpace(steamStoreAppId))
            {
                if (gameInfo != null && string.IsNullOrWhiteSpace(gameInfo.SteamAppId))
                {
                    gameInfo.SteamAppId = steamStoreAppId;
                }

                if (await TryDownloadFirstArtworkCandidateAsync(GetSteamArtworkCandidateUrls(steamStoreAppId), targetCachePath))
                {
                    return targetCachePath;
                }
            }

            foreach (string openFallbackUrl in await SearchCheapSharkArtworkUrlsAsync(gameInfo?.DisplayName ?? gameName))
            {
                if (await TryDownloadImageUrlToCacheAsync(openFallbackUrl, targetCachePath))
                {
                    return targetCachePath;
                }
            }

            return null;
        }

        private async Task<string> ResolveSteamGridDbArtworkUrlAsync(string searchName)
        {
            if (_steamGridHelper?.IsApiKeySet != true || string.IsNullOrWhiteSpace(searchName))
            {
                return null;
            }

            try
            {
                string cleanedName = CleanGameNameForSearch(searchName);
                SearchResult searchResult;

                if (_gameIdCache.TryGetValue(cleanedName, out SearchResult cachedResult))
                {
                    searchResult = cachedResult;
                }
                else
                {
                    searchResult = await _steamGridHelper.SearchForGameIdAsync(cleanedName);
                    _gameIdCache[cleanedName] = searchResult;
                }

                if (searchResult == null)
                {
                    return null;
                }

                double similarity = CalculateSimilarity(cleanedName, CleanGameNameForSearch(searchResult.name));
                if (similarity < 0.4)
                {
                    return null;
                }

                return await _steamGridHelper.GetGridImageUrlAsync(searchResult.id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Artwork] SteamGridDB resolve failed: {ex.Message}");
                return null;
            }
        }

        private async Task<bool> TryDownloadFirstArtworkCandidateAsync(IEnumerable<string> urls, string targetCachePath)
        {
            if (urls == null)
            {
                return false;
            }

            foreach (string url in urls)
            {
                if (await TryDownloadImageUrlToCacheAsync(url, targetCachePath))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> TryDownloadImageUrlToCacheAsync(string imageUrl, string targetCachePath)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || string.IsNullOrWhiteSpace(targetCachePath))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetCachePath));

                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(8)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GCM/1.0");

                using HttpResponseMessage response = await client.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                string mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                    !imageUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !imageUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                    !imageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                    !imageUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync();
                if (data.Length < 1024)
                {
                    return false;
                }

                string tempPath = $"{targetCachePath}.tmp";
                await File.WriteAllBytesAsync(tempPath, data);
                File.Move(tempPath, targetCachePath, true);
                InvalidateArtworkCacheLookup();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Artwork] Download failed '{imageUrl}': {ex.Message}");
                return false;
            }
        }

        private void InvalidateArtworkCacheLookup()
        {
            _verifiedImageCache.Clear();
            _lastCacheRefresh = DateTime.MinValue;
        }

        private async Task EnsureGameArtworkCachedAsync(ProcessData gameData)
        {
            if (gameData?.GameInfo?.IsGame != true)
            {
                return;
            }

            string stableKey = GetStableCacheKey(gameData.ProductName, gameData.ExePath, gameData.Proc);
            if (string.IsNullOrWhiteSpace(stableKey) || !string.IsNullOrWhiteSpace(FindCachedImageFile(stableKey)))
            {
                return;
            }

            lock (_gameArtworkRequestsInFlight)
            {
                if (!_gameArtworkRequestsInFlight.Add(stableKey))
                {
                    return;
                }
            }

            try
            {
                string displayName = GetFeaturedGameDisplayName(gameData);
                ShowInAppNotification($"Preparing artwork: {displayName}");

                string targetCachePath = Path.Combine(_imageCachePath, $"{stableKey}.jpg");
                string resolvedPath = await ResolveAndCacheGameArtworkAsync(
                    displayName,
                    gameData.ExePath,
                    gameData.Proc,
                    gameData.GameInfo,
                    targetCachePath);

                if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                {
                    ShowInAppNotification($"Artwork ready: {displayName}");
                }
            }
            finally
            {
                lock (_gameArtworkRequestsInFlight)
                {
                    _gameArtworkRequestsInFlight.Remove(stableKey);
                }
            }
        }
        private void LoadCardImageAsync(Border card, Image loadedImageControl, Image defaultIconControl, TextBlock titleControl, string gameName, string exePath, Process proc, ResolvedGameInfo gameInfo = null)
        {
            if (!Directory.Exists(_imageCachePath)) Directory.CreateDirectory(_imageCachePath);

            Task.Run(async () =>
            {
                ResolvedGameInfo resolvedGameInfo = gameInfo ?? ResolveGameInfo(proc, gameName, exePath, IntPtr.Zero);

                // 1. Stabilen Namen generieren (z.B. "devenv", "deadspace3")
                string stableKey = GetStableCacheKey(gameName, exePath, proc);

                // 2. PRÜFUNG: Haben wir das Bild schon? (RAM Cache = 0ms)
                string imagePathToLoad = FindCachedImageFile(stableKey);

                if (imagePathToLoad != null)
                {
                    // JA -> Sofort laden
                    await LoadImageToUiAsync(card, loadedImageControl, defaultIconControl, titleControl, imagePathToLoad, null);
                }
                else
                {
                    // NEIN -> Kein Bild im Cache.

                    // WICHTIG: Ist es ein Spiel?
                    if (resolvedGameInfo?.IsGame == true)
                    {
                        // Ja -> Suche online (SteamGridDB)
                        string searchName = resolvedGameInfo.DisplayName ?? GetSmartSearchName(gameName, exePath);
                        string savePath = Path.Combine(_imageCachePath, $"{stableKey}.jpg");

                        card.DispatcherQueue.TryEnqueue(() =>
                        {
                            PerformFallbackSearch(card, loadedImageControl, defaultIconControl, titleControl, searchName, exePath, proc, savePath, resolvedGameInfo);
                        });
                    }
                    else
                    {
                        // Nein (Visual Studio, Opera, Snipping Tool)
                        // -> Wir machen NICHTS. Das Fenster behält sein Standard-Icon.
                        // -> Performance bleibt perfekt.
                        // -> Du kannst jetzt "Start" drücken und manuell ein Bild setzen.
                    }
                }
            });
        }
        private async Task DownloadFromSteamGridDbAndCacheAsync(
            Border card,
            Image loadedImageControl,
            Image defaultIconControl,
            TextBlock titleControl,
            string searchName,
            string targetCachePath)
        {
            try
            {
                // Suche und Download im Hintergrund ausführen!
                await Task.Run(async () =>
                {
                    var searchResult = await _steamGridHelper.SearchForGameIdAsync(searchName);
                    if (searchResult == null) return;

                    double similarity = CalculateSimilarity(searchName, searchResult.name);
                    if (similarity < 0.4) return;

                    var imageUrl = await _steamGridHelper.GetGridImageUrlAsync(searchResult.id);
                    if (string.IsNullOrEmpty(imageUrl)) return;

                    using (var client = new HttpClient())
                    {
                        var data = await client.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(targetCachePath, data);
                    }

                    // Zurück zum UI Thread nur zum Anzeigen
                    card.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await LoadImageToUiAsync(card, loadedImageControl, defaultIconControl, titleControl, targetCachePath, searchResult.name);
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Download] Error: {ex.Message}");
            }
        }

        private async Task<string> DownloadHeroFromSteamGridDbAndCacheAsync(string searchName, string targetCachePath)
        {
            if (_steamGridHelper == null || !_steamGridHelper.IsApiKeySet || string.IsNullOrWhiteSpace(searchName))
            {
                return null;
            }

            try
            {
                var searchResult = await _steamGridHelper.SearchForGameIdAsync(searchName);
                if (searchResult == null)
                {
                    return null;
                }

                double similarity = CalculateSimilarity(CleanGameNameForSearch(searchName), CleanGameNameForSearch(searchResult.name));
                if (similarity < 0.4)
                {
                    return null;
                }

                string imageUrl = await _steamGridHelper.GetHeroImageUrlAsync(searchResult.id);
                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    return null;
                }

                if (!File.Exists(targetCachePath))
                {
                    using var client = new HttpClient();
                    byte[] data = await client.GetByteArrayAsync(imageUrl);
                    await File.WriteAllBytesAsync(targetCachePath, data);
                }

                return targetCachePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Backdrop] SteamGridDB hero download failed: {ex.Message}");
                return null;
            }
        }

        private async Task<SteamStoreBackdropInfo> GetSteamStoreBackdropInfoAsync(string steamAppId)
        {
            if (string.IsNullOrWhiteSpace(steamAppId))
            {
                return null;
            }

            if (_steamStoreBackdropCache.TryGetValue(steamAppId, out SteamStoreBackdropInfo cachedInfo))
            {
                return cachedInfo;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GCM/1.0");

                string json = await client.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={steamAppId}&l=en");
                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty(steamAppId, out JsonElement appElement) ||
                    !appElement.TryGetProperty("success", out JsonElement successElement) ||
                    !successElement.GetBoolean() ||
                    !appElement.TryGetProperty("data", out JsonElement dataElement))
                {
                    _steamStoreBackdropCache[steamAppId] = null;
                    return null;
                }

                var info = new SteamStoreBackdropInfo();

                if (dataElement.TryGetProperty("background_raw", out JsonElement backgroundRawElement))
                {
                    info.BackgroundRawUrl = backgroundRawElement.GetString();
                }

                if (dataElement.TryGetProperty("background", out JsonElement backgroundElement))
                {
                    info.BackgroundUrl = backgroundElement.GetString();
                }

                if (dataElement.TryGetProperty("screenshots", out JsonElement screenshotsElement) &&
                    screenshotsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement screenshot in screenshotsElement.EnumerateArray())
                    {
                        if (screenshot.TryGetProperty("path_full", out JsonElement pathFullElement))
                        {
                            string screenshotUrl = pathFullElement.GetString();
                            if (!string.IsNullOrWhiteSpace(screenshotUrl))
                            {
                                info.ScreenshotUrl = screenshotUrl;
                                break;
                            }
                        }
                    }
                }

                _steamStoreBackdropCache[steamAppId] = info;
                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Backdrop] Steam store backdrop lookup failed: {ex.Message}");
                _steamStoreBackdropCache[steamAppId] = null;
                return null;
            }
        }

        private async Task<string> DownloadBackdropUrlToCacheAsync(string imageUrl, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(_backdropCachePath);
                string cachePath = GetBackdropCacheFilePath(cacheKey);
                if (!File.Exists(cachePath))
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("GCM/1.0");
                    byte[] data = await client.GetByteArrayAsync(imageUrl);
                    await File.WriteAllBytesAsync(cachePath, data);
                }

                return cachePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Backdrop] Failed to cache backdrop '{imageUrl}': {ex.Message}");
                return null;
            }
        }

        private async Task<string> ResolveFeaturedGameBackdropPathAsync(ProcessData featuredGame)
        {
            if (featuredGame?.GameInfo?.IsGame != true)
            {
                return null;
            }

            string cacheKey = $"{GetStableCacheKey(featuredGame.ProductName, featuredGame.ExePath, featuredGame.Proc)}_hero";
            if (_gameBackdropCache.TryGetValue(cacheKey, out string cachedBackdropPath) && File.Exists(cachedBackdropPath))
            {
                return cachedBackdropPath;
            }

            if (_steamGridHelper?.IsApiKeySet == true)
            {
                string steamGridCachePath = GetBackdropCacheFilePath($"{cacheKey}_sgdb");
                if (File.Exists(steamGridCachePath))
                {
                    _gameBackdropCache[cacheKey] = steamGridCachePath;
                    return steamGridCachePath;
                }

                string downloadedHero = await DownloadHeroFromSteamGridDbAndCacheAsync(
                    featuredGame.GameInfo.DisplayName ?? featuredGame.ProductName,
                    steamGridCachePath);

                if (!string.IsNullOrWhiteSpace(downloadedHero) && File.Exists(downloadedHero))
                {
                    _gameBackdropCache[cacheKey] = downloadedHero;
                    return downloadedHero;
                }
            }

            if (!string.IsNullOrWhiteSpace(featuredGame.GameInfo.SteamAppId))
            {
                SteamStoreBackdropInfo steamStoreInfo = await GetSteamStoreBackdropInfoAsync(featuredGame.GameInfo.SteamAppId);
                if (steamStoreInfo != null)
                {
                    string storeBackdropPath =
                        await DownloadBackdropUrlToCacheAsync(steamStoreInfo.BackgroundRawUrl, $"{cacheKey}_steam_bg_raw") ??
                        await DownloadBackdropUrlToCacheAsync(steamStoreInfo.BackgroundUrl, $"{cacheKey}_steam_bg") ??
                        await DownloadBackdropUrlToCacheAsync(steamStoreInfo.ScreenshotUrl, $"{cacheKey}_steam_ss0");

                    if (!string.IsNullOrWhiteSpace(storeBackdropPath) && File.Exists(storeBackdropPath))
                    {
                        _gameBackdropCache[cacheKey] = storeBackdropPath;
                        return storeBackdropPath;
                    }
                }
            }

            PopulateLocalSteamArtworkPaths(featuredGame.GameInfo);

            if (!string.IsNullOrWhiteSpace(featuredGame.GameInfo.HeroImagePath) && File.Exists(featuredGame.GameInfo.HeroImagePath))
            {
                _gameBackdropCache[cacheKey] = featuredGame.GameInfo.HeroImagePath;
                return featuredGame.GameInfo.HeroImagePath;
            }

            return null;
        }

        private async Task UpdateFeaturedGameBackdropAsync(ProcessData featuredGame)
        {
            int loadVersion = Interlocked.Increment(ref _activeGameBackdropLoadVersion);
            string backdropIdentity = GetFeaturedGameIdentity(featuredGame);

            if (string.IsNullOrWhiteSpace(backdropIdentity))
            {
                _activeGameBackdropIdentity = string.Empty;
                DispatcherQueue.TryEnqueue(HideFeaturedGameBackdrop);
                return;
            }

            if (string.Equals(_activeGameBackdropIdentity, backdropIdentity, StringComparison.OrdinalIgnoreCase) &&
                GameBackgroundImage?.Visibility == Visibility.Visible)
            {
                return;
            }

            string backdropPath = await ResolveFeaturedGameBackdropPathAsync(featuredGame);
            if (loadVersion != _activeGameBackdropLoadVersion)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(backdropPath) || !File.Exists(backdropPath))
            {
                _activeGameBackdropIdentity = string.Empty;
                DispatcherQueue.TryEnqueue(HideFeaturedGameBackdrop);
                return;
            }

            _activeGameBackdropIdentity = backdropIdentity;
            await ApplyFeaturedGameBackdropAsync(backdropPath, loadVersion);
        }

        private async Task ApplyFeaturedGameBackdropAsync(string backdropPath, int loadVersion)
        {
            byte[] imageBytes;
            try
            {
                imageBytes = await File.ReadAllBytesAsync(backdropPath);
            }
            catch
            {
                return;
            }

            DispatcherQueue.TryEnqueue(async () =>
            {
                if (GameBackgroundImage == null || loadVersion != _activeGameBackdropLoadVersion)
                {
                    return;
                }

                try
                {
                    var bitmap = new BitmapImage();
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                    }

                    if (loadVersion != _activeGameBackdropLoadVersion)
                    {
                        return;
                    }

                    if (GameBackgroundImage.Visibility == Visibility.Visible && GameBackgroundImage.Opacity > 0.05)
                    {
                        AnimateOverlayOpacity(GameBackgroundImage, 0.0, false);
                        await Task.Delay(220);
                    }

                    if (loadVersion != _activeGameBackdropLoadVersion)
                    {
                        return;
                    }

                    GameBackgroundImage.Source = bitmap;
                    GameBackgroundImage.Visibility = Visibility.Visible;
                    GameBackgroundImage.Opacity = 0.0;
                    AnimateOverlayOpacity(GameBackgroundImage, 1.0, false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Backdrop] Failed to apply featured backdrop: {ex.Message}");
                }
            });
        }

        private void HideFeaturedGameBackdrop()
        {
            if (GameBackgroundImage == null)
            {
                return;
            }

            if (GameBackgroundImage.Visibility == Visibility.Collapsed && GameBackgroundImage.Source == null)
            {
                return;
            }

            AnimateOverlayOpacity(GameBackgroundImage, 0.0, true);
        }

        // --- HELPER METHODEN ---

        /// <summary>
        /// Generiert den Dateinamen für den Cache.
        /// Priorität: 1. Prozessname (Stabil!), 2. Exe-Name, 3. Bereinigter Titel
        /// </summary>
        private string GetStableCacheKey(string windowTitle, string exePath, Process proc)
        {
            // 1. Höchste Priorität: Der Name der EXE-Datei (z.B. "deadspace3")
            if (!string.IsNullOrEmpty(exePath))
            {
                return Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant().Trim();
            }

            // 2. Zweite Priorität: Der interne Prozessname
            if (proc != null)
            {
                try { return proc.ProcessName.ToLowerInvariant().Trim(); } catch { }
            }

            // 3. Letzter Ausweg: Der bereinigte Fenstertitel
            return CleanFilename(windowTitle).ToLowerInvariant().Trim();
        }

        // Ermittelt einen "schönen" Namen für die Suche und Anzeige (z.B. "Google Chrome" statt "chrome")
        private string GetSmartSearchName(string gameName, string exePath)
        {
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    var info = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription;
                    if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName;
                }
                catch { }
                return Path.GetFileNameWithoutExtension(exePath);
            }
            return CleanGameNameForSearch(gameName);
        }



        private string GetCacheFilePathFromKey(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) key = key.Replace(c, '_');
            if (key.Length > 50) key = key.Substring(0, 50);
            return Path.Combine(_imageCachePath, $"{key}.jpg");
        }

        private string GetBackdropCacheFilePath(string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) key = key.Replace(c, '_');
            if (key.Length > 80) key = key.Substring(0, 80);
            return Path.Combine(_backdropCachePath, $"{key}.jpg");
        }

        private void UpdateUiFromData(List<ProcessData> processDataList)
        {
            if (processDataList == null) return;

            _latestProcessData = processDataList.ToList();

            if (!_isShellUiReady || MainContent == null || MainContent.Visibility != Visibility.Visible)
            {
                _deferredProcessData = _latestProcessData.ToList();
                return;
            }

            ProcessData featuredGame = SelectFeaturedGameProcessData(processDataList);
            string featuredIdentity = GetFeaturedGameIdentity(featuredGame);
            string previousFeaturedIdentity = _featuredGameIdentity;

            bool launcherNeedsRefresh = !string.Equals(_featuredGameIdentity, featuredIdentity, StringComparison.OrdinalIgnoreCase);
            _featuredGameIdentity = featuredIdentity;
            _featuredGameProcessData = featuredGame;

            foreach (ProcessData gameData in processDataList.Where(data => data?.GameInfo?.IsGame == true))
            {
                _ = EnsureGameArtworkCachedAsync(gameData);
            }

            if (launcherNeedsRefresh)
            {
                if (featuredGame != null)
                {
                    ShowInAppNotification($"Game detected: {GetFeaturedGameDisplayName(featuredGame)}");
                }
                else if (!string.IsNullOrWhiteSpace(previousFeaturedIdentity))
                {
                    ShowInAppNotification("Game session ended.");
                }

                LoadDynamicLauncherCards();
            }

            List<ProcessData> visibleProcessCards = processDataList;
            if (featuredGame != null)
            {
                visibleProcessCards = processDataList.Where(data => data.Hwnd != featuredGame.Hwnd).ToList();
            }

            // Wir erstellen ein HashSet der NEUEN Handles für schnellen Abgleich
            var scannedHwnds = visibleProcessCards.Select(p => p.Hwnd).ToHashSet();

            // Wir erstellen ein HashSet der BEREITS ANGEZEIGTEN Handles
            var currentUiHwnds = _cardCache.Select(c => c.Hwnd).ToHashSet();

            bool uiChanged = false;

            // ---------------------------------------------------------
            // 1. LÖSCHEN (Nur Karten entfernen, die im neuen Scan FEHLEN)
            // ---------------------------------------------------------
            for (int i = _cardCache.Count - 1; i >= 0; i--)
            {
                var entry = _cardCache[i];

                if (!scannedHwnds.Contains(entry.Hwnd))
                {
                    // Fenster ist weg -> Karte entfernen
                    ProgramCardPanel.Children.Remove(entry.Card);
                    _cardCache.RemoveAt(i);
                    uiChanged = true;

                    // Index-Schutz: Wenn links von uns gelöscht wird, rutschen wir nach links
                    if (_selectedCardIndex > i) _selectedCardIndex--;
                }
            }

            // ---------------------------------------------------------
            // 2. HINZUFÜGEN (Nur was wir NOCH NICHT haben)
            // ---------------------------------------------------------
            foreach (var data in visibleProcessCards)
            {
                // DER FIX: Wenn wir das Fenster schon anzeigen -> ÜBERSPRINGEN (Fass es nicht an!)
                // Das verhindert das Neuladen und Controller-Springen.
                if (currentUiHwnds.Contains(data.Hwnd)) continue;

                // Wenn wir hier sind, ist es ein NEUES Fenster. Erstelle Karte.
                var border = CreateProgramCard(data.ProductName, data.ExePath, data.Proc, data.Hwnd, data.GameInfo);
                var entry = new ProgramCardEntry
                {
                    ProductName = data.ProductName,
                    ExePath = data.ExePath,
                    Hwnd = data.Hwnd,
                    Proc = data.Proc,
                    GameInfo = data.GameInfo,
                    Card = border
                };

                _cardCache.Add(entry);
                ProgramCardPanel.Children.Add(border);
                uiChanged = true;
            }

            // ---------------------------------------------------------
            // 3. UI STATUS
            // ---------------------------------------------------------
            if (uiChanged)
            {
                if (_cardCache.Count > 0)
                {
                    if (_selectedCardIndex >= _cardCache.Count) _selectedCardIndex = _cardCache.Count - 1;
                    if (_selectedCardIndex < 0) _selectedCardIndex = 0;
                }
                else
                {
                    _selectedCardIndex = 0;
                }

                NoCardsMessage.Visibility = _cardCache.Any() ? Visibility.Collapsed : Visibility.Visible;
                HighlightSelectedCard(skipScroll: true, forceAnimation: false);
            }

            if (launcherNeedsRefresh && _currentFocusArea == FocusArea.Launcher)
            {
                UpdateVisualFocus();
                return;
            }

            UpdateSelectionSurface();
        }



        private LauncherCardItem SelectedLauncherItemOrNull()
        {
            if (_launcherAreaButtons == null || _selectedLauncherAreaIndex < 0 || _selectedLauncherAreaIndex >= _launcherAreaButtons.Count)
            {
                return null;
            }

            return _launcherAreaButtons[_selectedLauncherAreaIndex].Tag as LauncherCardItem;
        }

        private Border SelectedLauncherCardOrNull()
        {
            if (_launcherAreaButtons == null || _selectedLauncherAreaIndex < 0 || _selectedLauncherAreaIndex >= _launcherAreaButtons.Count)
            {
                return null;
            }

            return _launcherAreaButtons[_selectedLauncherAreaIndex];
        }

        private bool SelectedLauncherItemRepresentsGame()
        {
            return SelectedLauncherItemOrNull()?.GameInfo?.IsGame == true;
        }

        private bool TryActivateSelectedLauncherItem()
        {
            var item = SelectedLauncherItemOrNull();
            if (item?.TapAction == null)
            {
                return false;
            }

            string gateKey = item.IsPrimary
                ? "launcher-main-card"
                : item.GameInfo?.IsGame == true
                    ? "launcher-featured-game"
                    : $"launcher-{_selectedLauncherAreaIndex}";

            int cooldownMilliseconds = item.IsPrimary ? 900 : 400;
            if (!TryAcquireUiActionGate(gateKey, cooldownMilliseconds))
            {
                return false;
            }

            item.TapAction.Invoke(null, null);
            return true;
        }

        private ProgramCardEntry CreateProgramCardEntryFromLauncherItem(LauncherCardItem item, Border card)
        {
            if (item == null)
            {
                return null;
            }

            return new ProgramCardEntry
            {
                ProductName = item.Name,
                ExePath = item.ExePath,
                Hwnd = item.Hwnd,
                Proc = item.Proc,
                GameInfo = item.GameInfo,
                Card = card
            };
        }

        private ProgramCardEntry SelectedProgramCardEntryOrNull()
        {
            if (_cardCache == null || _selectedCardIndex < 0 || _selectedCardIndex >= _cardCache.Count)
            {
                return null;
            }

            return _cardCache[_selectedCardIndex];
        }

        private void EnsureSelectionSurfaceReferences()
        {
            if (_bottomLegendItemsHost != null &&
                _bottomStatusText != null &&
                _bottomStatusPopup != null &&
                _bottomStatusPopupTransform != null)
            {
                return;
            }

            if (Content is not FrameworkElement root)
            {
                return;
            }

            _bottomLegendBar ??= root.FindName("BottomLegendBar") as Border;
            _bottomLegendItemsHost ??= root.FindName("BottomLegendItemsHost") as StackPanel;
            _bottomStatusPopup ??= root.FindName("BottomStatusPopup") as Border;
            _bottomStatusPopupTransform ??= root.FindName("BottomStatusPopupTransform") as CompositeTransform;
            _bottomStatusText ??= root.FindName("BottomStatusText") as TextBlock;
        }

        private void UpdateSelectionSurface()
        {
            EnsureSelectionSurfaceReferences();

            if (_bottomLegendBar == null || _bottomLegendItemsHost == null)
            {
                return;
            }

            var hints = new List<LegendHint>();

            switch (_currentFocusArea)
            {
                case FocusArea.StartupVideo:
                    AddLegendHint(hints, "A", "Skip");
                    AddLegendHint(hints, "B", "Skip");
                    AddLegendHint(hints, "Start", "Skip");
                    break;

                case FocusArea.Launcher:
                case FocusArea.QuickLaunchers:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Launch");
                    if (_currentFocusArea == FocusArea.Launcher && SelectedLauncherItemRepresentsGame())
                    {
                        AddLegendHint(hints, "B", "Close");
                        AddLegendHint(hints, "Start", "Options");
                    }
                    break;

                case FocusArea.Cards:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Open");
                    if (_cardCache.Any())
                    {
                        AddLegendHint(hints, "B", "Close");
                        AddLegendHint(hints, "Start", "Options");
                    }
                    break;

                case FocusArea.TopButtons:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Open");
                    AddLegendHint(hints, "X", "Audio");
                    AddLegendHint(hints, "DPadDown", "Back");
                    break;

                case FocusArea.SettingsMenu:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Select");
                    AddLegendHint(hints, "X", "Back");
                    AddLegendHint(hints, "B", "Close");
                    break;

                case FocusArea.AudioMenu:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Select");
                    AddLegendHint(hints, "Y", "Master");
                    AddLegendHint(hints, "LeftShoulder", "Prev Tab");
                    AddLegendHint(hints, "RightShoulder", "Next Tab");
                    AddLegendHint(hints, "B", "Close");
                    break;

                case FocusArea.PowerMenu:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Confirm");
                    AddLegendHint(hints, "B", "Back");
                    break;

                case FocusArea.WindowsReturnConfirm:
                    AddLegendHint(hints, "A", "Return to Windows");
                    AddLegendHint(hints, "B", "Stay in GCM");
                    break;

                case FocusArea.AppLauncher:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "LeftShoulder", "Prev Tab");
                    AddLegendHint(hints, "RightShoulder", "Next Tab");
                    AddLegendHint(hints, "A", "Launch");
                    AddLegendHint(hints, "X", "Favorite");
                    AddLegendHint(hints, "B", "Close");
                    break;

                case FocusArea.ImageSelection:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", "Apply");
                    AddLegendHint(hints, "B", "Back");
                    break;

                case FocusArea.GameOptions:
                    AddLegendHint(hints, "Navigate", "Navigate");
                    AddLegendHint(hints, "A", ArtworkSearchPanel.Visibility == Visibility.Visible ? "Apply" : "Select");
                    AddLegendHint(hints, "B", "Back");
                    break;
            }

            string legendSignature = $"{_currentFocusArea}|{_lastActiveControllerType}|{string.Join("|", hints.Select(h => $"{h.IconKey}:{h.Label}"))}";
            Visibility desiredVisibility = hints.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            bool needsRebuild =
                _lastLegendSignature != legendSignature ||
                _bottomLegendItemsHost.Children.Count != hints.Count ||
                _bottomLegendBar.Visibility != desiredVisibility;

            if (!needsRebuild)
            {
                return;
            }

            _bottomLegendItemsHost.Children.Clear();

            foreach (LegendHint hint in hints)
            {
                _bottomLegendItemsHost.Children.Add(CreateLegendChip(hint.IconKey, hint.Label));
            }

            _bottomLegendBar.Visibility = desiredVisibility;
            _lastLegendSignature = legendSignature;
        }

        private class ProgramCardEntry
        {
            public string ProductName;
            public string ExePath;
            public IntPtr Hwnd;
            public Process Proc;
            public ResolvedGameInfo GameInfo;
            public Border Card;
        }

        private List<ProgramCardEntry> _cardCache = new(); // ersetzt Children.Clear()


        public bool TaskManagerVisibility;

        // Internal class to represent an application row
        private class ProgramRow
        {
            public StackPanel RowPanel;   // the horizontal "row"
            public TextBlock NameText;    // the name of the application
            public Button FocusButton;    // Focus button
            public Button KillButton;     // Kill button

            public IntPtr Hwnd;           // window handle
            public Process Proc;          // corresponding Process
        }

        // Index for 2D navigation: _selectedRow (row) and _selectedCol (column)
        private int _selectedRow = 0;     // current row index
        private int _selectedCol = 0;     // 0 = Focus, 1 = Kill
        private DateTime _lastInputTime = DateTime.Now;

        // Stores whether the window is currently in the foreground
        private bool _isForeground = false;

        // Timer to refresh the list every second
        private DispatcherTimer _refreshTimer;

        private List<ProgramRow> _rows = new List<ProgramRow>();

        // AppWindow, for detecting minimized / non-minimized
        private AppWindow _appWindow;

        // ====================================================================
        // Initializes the window and the refresh timer
        // ====================================================================
    

        private void ShowTaskManager()
        {
            App.StartupTrace("ShowTaskManager called.");
            StartClock();
            _selectedCardIndex = 0;

            if (!_isShellUiReady)
            {
                _taskManagerStartupPending = true;
                _ = RefreshAppListAsync();
                return;
            }

            InitializeTaskManagerShell();
        }

        private void InitializeTaskManagerShell()
        {
            if (_hasTaskManagerShellInitialized || !_isShellUiReady)
            {
                return;
            }

            _hasTaskManagerShellInitialized = true;
            _taskManagerStartupPending = false;

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    ProgramCardPanel.Visibility = Visibility.Visible;
                    LoadAllLauncherSettings();
                    ApplyResponsiveShellSizing();
                    UpdateSelectionSurface();
                    UpdateVisualFocus();
                    StartAutoTaskRefresh();

                    if (_deferredProcessData.Count > 0)
                    {
                        UpdateUiFromData(_deferredProcessData);
                        _deferredProcessData = new List<ProcessData>();
                    }
                    else
                    {
                        await RefreshAppListAsync();
                    }
                }
                catch (Exception ex)
                {
                    _hasTaskManagerShellInitialized = false;
                    Debug.WriteLine($"[TaskManager] Deferred startup failed: {ex.Message}");
                }
            });
        }

        private void MarkShellUiReady()
        {
            if (_isShellUiReady)
            {
                return;
            }

            _isShellUiReady = true;

            if (_taskManagerStartupPending || !_hasTaskManagerShellInitialized)
            {
                InitializeTaskManagerShell();
            }
        }




        // ====================================================================
        // Loads the list of applications
        // ====================================================================

        // Replace your existing _excludedTitles array with this one
        private static readonly string[] _excludedTitles = new[]
 {
    // General System Windows
    "Windows® Operating System",
    "System Microsoft® Windows",
    "Windows®-Betriebssystem",
    "Windows Operating System",
    "Windows-Betriebssystem",
    "ApplicationFrameHost",
    "ShellExperienceHost",
    "StartMenuExperienceHost",
    "Einstellungen",
    "Settings",
    "Task-Manager",
    "Task Manager",
    "NAHIMIC",
    "WINDOWS-WIDGET",
    "MSN",
    "Program Manager",      // WICHTIG: Das ist oft der Desktop selbst
    "Microsoft Text Input Application", // Touch Keyboard Ghost

    // Overlays & Ghosts
    "NVIDIA GeForce Overlay",
    "NVIDIA Overlay",
    "GeForce Overlay",
    "NVIDIA Web Helper",
    "GDI+ Window",          // Typischer Ghost-Titel
    "Default IME",          // Input Method Editor Ghost
    "MSCTFIME UI",          // Input Method Editor Ghost
    
    // Specific Apps to always ignore
    "Steam",
    "Big-Picture-Modus",
    "Big Picture Mode",
    "Playnite",
    "Realtek Audio Console",
    "NVIDIA App",
    "AMD Adrenaline",
    "Xbox.Apps.TCUI",
    "Xbox",
    "GeForce NOW"
};

        private static readonly string[] _excludedProcessNames = new[]
{
    "steamwebhelper", "EADesktop", "epicgameslauncher",
    "GalaxyClient", "battle.net", "UbisoftConnect", "start_protected_game",
    "GeForceNOW", "InputApp", "InputHost", "TextInputHost", "ShellExperienceHost",
    "StartMenuExperienceHost", "SearchHost", "XblGameSave", "ApplicationFrameHost",
    "SystemSettings", "LockApp", "SmartScreen", "RuntimeBroker", "taskhostw",
    "devenv", "explorer", "searchapp", "widgets",
    
    // NVIDIA / AMD Overlays
    "nvcontainer",
    "nvidia share",
    "nvidia web helper",
    "amdrsserv",
    "atiesrxx"
};

        [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IApplicationActivationManager
        {
            int ActivateApplication(
                [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [MarshalAs(UnmanagedType.LPWStr)] string arguments,
                ActivateOptions options,
                out uint processId);
        }

        enum ActivateOptions
        {
            None = 0x00000000,
            DesignMode = 0x00000001,
            NoErrorUI = 0x00000002,
            NoSplashScreen = 0x00000004,
        }
        private static readonly Dictionary<string, string> _uwpAppIds = new()
{
    { "XboxApp", "Microsoft.XboxApp_8wekyb3d8bbwe!App" },
    { "Settings", "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel" }
};

        private void ActivateUwpApp(string appUserModelId)
        {
            try
            {
                var mgr = (IApplicationActivationManager)new ApplicationActivationManager();
                mgr.ActivateApplication(appUserModelId, null, ActivateOptions.None, out uint pid);
                Debug.WriteLine($"[UWP] Activated {appUserModelId} with PID {pid}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UWP] Activation failed: {ex.Message}");
            }
        }
       

        private void RestoreUwpAppFromHost(IntPtr appHostHwnd)
        {
            IntPtr realWindow = IntPtr.Zero;

            EnumChildWindows(appHostHwnd, (childHwnd, _) =>
            {
                GetWindowThreadProcessId(childHwnd, out uint pid);

                if (IsWindowVisible(childHwnd) && !IsCloaked(childHwnd))
                {
                    realWindow = childHwnd;
                    return false; // break
                }

                return true;
            }, IntPtr.Zero);

            if (realWindow != IntPtr.Zero)
            {
                ShowWindow(realWindow, SW_SHOWMAXIMIZED);
                SetForegroundWindow(realWindow);
                Debug.WriteLine("[UWP] Maximized child window to bring to front.");
            }
            else
            {
                Debug.WriteLine("[UWP] No visible UWP child window found to maximize.");
            }
        }



      
        const int DWMWA_CLOAKED = 14;

        enum GetWindowCmd : uint
        {
            GW_HWNDFIRST = 0,
            GW_HWNDLAST = 1,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4,
            GW_CHILD = 5,
            GW_ENABLEDPOPUP = 6
        }

        private static bool IsCloaked(IntPtr hWnd)
        {
            const int DWMWA_CLOAKED = 14;
            int isCloaked = 0;
            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out isCloaked, sizeof(int));
            return isCloaked != 0;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);




        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private const uint GW_CHILD = 5;

        private class CardTag
        {
            public Process Process;
            public IntPtr Hwnd;
            public Color BaseColor;
            public string AppUserModelId;
        
        }


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetPackageFullName(IntPtr hProcess, ref int packageFullNameLength, StringBuilder packageFullName);

        private static bool IsUwpProcess(Process proc)
        {
            try
            {
                int length = 0;
                GetPackageFullName(proc.Handle, ref length, null);
                if (length == 0)
                    return false;

                var sb = new StringBuilder(length);
                int result = GetPackageFullName(proc.Handle, ref length, sb);
                return result == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string TryGetPackageFullName(Process proc)
        {
            try
            {
                if (proc == null)
                {
                    return string.Empty;
                }

                int length = 0;
                GetPackageFullName(proc.Handle, ref length, null);
                if (length == 0)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder(length);
                return GetPackageFullName(proc.Handle, ref length, sb) == 0
                    ? sb.ToString()
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private readonly HashSet<string> _nonGameKeywords = new(StringComparer.OrdinalIgnoreCase)
{
    // -- Web Browsers --
    "Edge", "Chrome", "Firefox", "Opera", "Brave",

    // -- System Tools --
    "Explorer", "Einstellungen", "Settings", "Task-Manager", "Manager",
    "Console", "Terminal", "PowerShell", "Registry", "Editor",
    "Rechner", "Calculator", "Snipping Tool", "Ausschneiden und Skizzieren",
    "Systemsteuerung", "Control Panel",
    
    // -- Development --
    "Visual Studio", "VS Code", "Rider", "Android Studio", "Debugger",
    "Codex", "Cursor", "Windows Terminal", "Command Prompt", "Git Bash",
    "Node.js", "npm", "pnpm", "yarn",
    
    // -- Office & Productivity --
    "Word", "Excel", "PowerPoint", "Outlook", "OneNote", "Teams",
    "Slack", "Zoom", "Notepad", "Editor",
    
    // -- Media Players --
    "VLC", "Media Player", "Spotify", "iTunes",
    
    // -- Creative Software --
    "Photoshop", "Illustrator", "Premiere", "After Effects",
    "Blender", "OBS",
    
    // -- Utilities & Launchers (the apps themselves) --
    "7-Zip", "WinRAR", "Discord", "Epic Games", "GOG GALAXY",
    "Ubisoft Connect", "EA", "Battle.net",
    
    // -- Hardware & Drivers --
    "NVIDIA", "GeForce", "AMD Software", "Radeon", "Intel",
    
    // -- Generic Terms --
    "Properties", "Eigenschaften", "Installer", "Updater", "Helper",
    "Dienst", "Host", "Service", "Server", "Launcher", "Runtime", "SDK", "CrashReporter"
};

        private readonly HashSet<string> _gameLauncherShellKeywords = new(StringComparer.OrdinalIgnoreCase)
{
    "launcher", "patcher", "updater", "bootstrap", "boot", "play", "start", "client", "anti-cheat"
};

        private readonly HashSet<string> _softExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
{
    "EADesktop",
    "epicgameslauncher",
    "GalaxyClient",
    "battle.net",
    "UbisoftConnect",
    "start_protected_game",
    "GeForceNOW"
};

        private readonly HashSet<string> _gameLauncherProcessNames = new(StringComparer.OrdinalIgnoreCase)
{
    "steam",
    "steamwebhelper",
    "xbox",
    "xboxapp",
    "xboxpcapp",
    "gamingservices",
    "gamingservicesnet",
    "gamelaunchhelper",
    "gamebar",
    "gamebarftserver",
    "EADesktop",
    "epicgameslauncher",
    "GalaxyClient",
    "battle.net",
    "UbisoftConnect"
};

        private readonly HashSet<string> _hardNonGameProcessNames = new(StringComparer.OrdinalIgnoreCase)
{
    "codex",
    "code",
    "cursor",
    "devenv",
    "rider64",
    "WindowsTerminal",
    "wt",
    "cmd",
    "conhost",
    "powershell",
    "pwsh",
    "node",
    "npm",
    "git-bash",
    "notepad",
    "notepad++",
    "snippingtool",
    "mspaint",
    "spotify",
    "discord",
    "teams",
    "slack",
    "zoom",
    "opera",
    "opera_gx",
    "chrome",
    "msedge",
    "firefox",
    "brave"
};

        private readonly List<string> _gamePathKeywords = new()
{
    "\\steamapps\\common\\",
    "\\XboxGames\\",
    "\\MSIXVC\\",
    "\\ModifiableWindowsApps\\",
    "\\GOG Galaxy\\Games\\",
    "\\Epic Games\\",
    "\\Ubisoft Game Launcher\\games\\",
    "\\Origin Games\\",
    "\\Battle.net\\"
};


        private Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>();
        /// <summary>
        /// Creates a single, clickable launcher card based on the provided data.
        /// Now handles a custom image for the Main Launcher card with a "LAUNCHER" label.
        /// </summary>
        private Border CreateProgramCard(string name, string exePath, Process proc, IntPtr hwnd, ResolvedGameInfo gameInfo = null)
        {
            double scale = _currentShellLayoutScale;
            var contentGrid = new Grid();
            bool isGameCard = gameInfo?.IsGame == true;

            var loadedImage = new Image
            {
                Name = "LoadedImage",
                // UniformToFill ensures the image covers the whole card without distortion.
                Stretch = Stretch.UniformToFill,
                Opacity = 0.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                RenderTransform = new CompositeTransform()
            };

            var artworkFrame = new Border
            {
                Margin = new Thickness(5 * scale),
                CornerRadius = new CornerRadius(21 * scale),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 0, 0)),
                Child = loadedImage
            };

            var ambientGlow = new Border
            {
                CornerRadius = new CornerRadius(24 * scale),
                Background = new SolidColorBrush(GetThemeAccentColor(48))
            };

            var glassEffect = new Border
            {
                Name = "GlassBase",
                CornerRadius = new CornerRadius(24 * scale),
                BorderBrush = new SolidColorBrush(GetThemeAccentColor(58)),
                BorderThickness = new Thickness(0.9),
                Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(20, 28, 48)))
            };

            var titleChip = new TextBlock
            {
                Text = gameInfo?.IsGame == true ? "GAME" : "LIVE PROCESS",
                FontSize = 10.5 * scale,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(188, 232, 242, 255)),
                CharacterSpacing = (int)(65 * scale),
                Margin = new Thickness(20 * scale, 28 * scale, 20 * scale, 0),
                VerticalAlignment = VerticalAlignment.Top
            };

            var iconImage = new Image
            {
                Source = GetAppIconAsBitmapImage(exePath) ?? new BitmapImage(new Uri("ms-appx:///Assets/game.png")),
                Width = 68 * scale,
                Height = 68 * scale,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10 * scale),
                RenderTransform = new CompositeTransform()
            };

            var footerPlate = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 58 * scale,
                Margin = new Thickness(16 * scale, 0, 16 * scale, 14 * scale),
                Padding = new Thickness(15 * scale, 8 * scale, 15 * scale, 10 * scale),
                Background = new SolidColorBrush(GetThemeCardTintColor(176)),
                CornerRadius = new CornerRadius(18 * scale),
                Child = new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = name.ToUpperInvariant(),
                            FontSize = 14 * scale,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Bahnschrift"),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1
                        },
                        new TextBlock
                        {
                            Text = proc?.ProcessName?.ToUpperInvariant() ?? "RUNNING NOW",
                            FontSize = 10 * scale,
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(188, 232, 242, 255)),
                            CharacterSpacing = (int)(70 * scale),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1
                        }
                    }
                }
            };

            contentGrid.Children.Add(ambientGlow);
            contentGrid.Children.Add(artworkFrame);
            contentGrid.Children.Add(glassEffect);
            AddBubbleReflectionLayers(contentGrid, scale, false);
            contentGrid.Children.Add(titleChip);
            contentGrid.Children.Add(iconImage);
            if (!isGameCard)
            {
                contentGrid.Children.Add(footerPlate);
            }

            var cardBorder = new Border
            {
                Width = 192 * scale,
                Height = 228 * scale,
                CornerRadius = new CornerRadius(24 * scale),
                Margin = new Thickness(0, 0, 12 * scale, 0),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(2),
                Child = contentGrid,
                Tag = new CardTag { Process = proc, Hwnd = hwnd },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            // --- EFFEKTE AKTIVIEREN ---
            cardBorder.Loaded += (s, e) => ApplyNativeWinUI3Blur(glassEffect);

            LoadCardImageAsync(cardBorder, loadedImage, iconImage, null, name, exePath, proc, gameInfo);

            return cardBorder;
        }

        private void ApplyNativeWinUI3Blur(Border target)
        {
            if (target == null) return;
            try
            {
                var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(target);
                if (visual == null) return;

                var compositor = visual.Compositor;
                var backdropBrush = compositor.CreateBackdropBrush();
                var spriteVisual = compositor.CreateSpriteVisual();
                spriteVisual.Brush = backdropBrush;

                var bindSize = compositor.CreateExpressionAnimation("host.Size");
                bindSize.SetReferenceParameter("host", visual);
                spriteVisual.StartAnimation("Size", bindSize);

                Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetElementChildVisual(target, spriteVisual);
            }
            catch (Exception ex)
            {
                // Falls DWM (Desktop Window Manager) nicht reagiert
                Debug.WriteLine($"[UI EFFECT ERROR] Blur konnte nicht angewendet werden: {ex.Message}");
            }
        }

        private void ApplyRoundedCompositionClip(FrameworkElement target, double radius)
        {
            // Disabled intentionally: WinUI composition clips can crash during startup
            // when cards are created before the visual tree is fully ready.
        }

        private sealed class LegendHint
        {
            public LegendHint(string iconKey, string label)
            {
                IconKey = iconKey;
                Label = label;
            }

            public string IconKey { get; }
            public string Label { get; }
        }

        private string GetControllerIconAssetPath(string iconKey)
        {
            bool isPlayStation = _lastActiveControllerType == ControllerType.PlayStation;
            string folder = isPlayStation ? "controllericons/playstation" : "controllericons/xbox";

            return iconKey switch
            {
                "Navigate" => isPlayStation ? $"{folder}/T_P5_Dpad.png" : $"{folder}/T_X_Dpad.png",
                "Guide" => isPlayStation ? $"{folder}/Start.png" : $"{folder}/xbox.png",
                _ => $"{folder}/{iconKey}.png"
            };
        }

        private Border CreateLegendChip(string iconKey, string label)
        {
            double scale = _currentShellLayoutScale;
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8 * scale,
                VerticalAlignment = VerticalAlignment.Center
            };

            row.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri($"ms-appx:///Assets/{GetControllerIconAssetPath(iconKey)}")),
                Width = 22 * scale,
                Height = 22 * scale,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            });

            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13 * scale,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            });

            return new Border
            {
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(52, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14 * scale),
                Padding = new Thickness(12 * scale, 8 * scale, 12 * scale, 8 * scale),
                Child = row
            };
        }

        private static void AddLegendHint(ICollection<LegendHint> hints, string iconKey, string label)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                hints.Add(new LegendHint(iconKey, label));
            }
        }

        /// <summary>
        /// Animiert die Rahmenfarbe eines Borders sanft mit einem Storyboard.
        /// </summary>
        private void AnimateBorderColor(Border border, bool isSelected)
        {
            Color targetColor;
            if (isSelected)
            {
                targetColor = GetThemeAccentColor(168);
            }
            else
            {
                targetColor = Microsoft.UI.Colors.Transparent;
            }

            if (border.BorderBrush is not SolidColorBrush brush)
            {
                brush = new SolidColorBrush();
                border.BorderBrush = brush;
            }

            var colorAnimation = new ColorAnimation
            {
                To = targetColor,
                // HIER IST DIE LÖSUNG: Eine kurze Dauer hinzufügen
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(colorAnimation);

            Storyboard.SetTarget(colorAnimation, brush);
            Storyboard.SetTargetProperty(colorAnimation, "Color");

            storyboard.Begin();
        }

        /// <summary>
        /// Aktualisiert die Hervorhebung und ruft die Animationen für Größe und Rahmenfarbe auf.
        /// </summary>
        /// 
        // Bei den anderen Index-Variablen einfügen
        private int _lastSelectedCardIndex = -1; // <--- NEU
        private void HighlightSelectedCard(bool skipScroll = false, bool forceAnimation = true)
        {
            if (ProgramCardPanel.Children.Count == 0) return;

            // 1. Die alte Karte (die den Fokus verliert) sanft zurücksetzen
            if (_lastSelectedCardIndex != -1 && _lastSelectedCardIndex < ProgramCardPanel.Children.Count)
            {
                if (ProgramCardPanel.Children[_lastSelectedCardIndex] is Border oldBorder)
                {
                    AnimateBorderColor(oldBorder, false);
                    AnimateScale(oldBorder, false);
                }
            }

            // 2. Die neue Karte (die den Fokus bekommt) hervorheben
            if (_selectedCardIndex >= 0 && _selectedCardIndex < ProgramCardPanel.Children.Count)
            {
                if (ProgramCardPanel.Children[_selectedCardIndex] is Border newBorder)
                {
                    AnimateBorderColor(newBorder, true);
                    AnimateScale(newBorder, true);

                    // Sofort scrollen, damit die UI dem Stick folgt
                    if (_currentFocusArea == FocusArea.Cards && !skipScroll)
                    {
                        ScrollToCardAnimated(newBorder);
                    }
                }
            }

            _lastSelectedCardIndex = _selectedCardIndex;
            UpdateBubbleReflectionRig();
            UpdateSelectionSurface();
        }

        private void UpdateBubbleReflectionRig()
        {
            bool launcherIsSource = _currentFocusArea == FocusArea.Launcher || _currentFocusArea == FocusArea.QuickLaunchers;
            bool cardsAreSource = _currentFocusArea == FocusArea.Cards;
            bool hasActiveLight = launcherIsSource || cardsAreSource;

            if (_launcherAreaButtons != null)
            {
                for (int i = 0; i < _launcherAreaButtons.Count; i++)
                {
                    int distance;
                    if (launcherIsSource)
                    {
                        distance = i - _selectedLauncherAreaIndex;
                    }
                    else if (cardsAreSource)
                    {
                        // Launcher cards sit left of the process rail, so they catch light on their right edge.
                        distance = -(Math.Max(1, _launcherAreaButtons.Count - i) + Math.Max(0, _selectedCardIndex));
                    }
                    else
                    {
                        distance = 0;
                    }

                    ApplyBubbleReflection(_launcherAreaButtons[i], distance, hasActiveLight);
                }
            }

            if (ProgramCardPanel != null)
            {
                for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
                {
                    if (ProgramCardPanel.Children[i] is not Border card)
                    {
                        continue;
                    }

                    int distance;
                    if (cardsAreSource)
                    {
                        distance = i - _selectedCardIndex;
                    }
                    else if (launcherIsSource)
                    {
                        // Process cards sit right of the launcher rail, so they catch light on their left edge.
                        distance = Math.Max(1, i + 1 + (_launcherAreaButtons?.Count ?? 1) - _selectedLauncherAreaIndex);
                    }
                    else
                    {
                        distance = 0;
                    }

                    ApplyBubbleReflection(card, distance, hasActiveLight);
                }
            }

            UpdateRailReflectionRig(launcherIsSource, cardsAreSource, hasActiveLight);
        }

        private void ApplyBubbleReflection(Border card, int distanceFromLight, bool hasActiveLight)
        {
            if (card == null)
            {
                return;
            }

            int direction = Math.Sign(distanceFromLight);
            int absDistance = Math.Abs(distanceFromLight);
            bool isSelected = hasActiveLight && absDistance == 0;
            double falloff = hasActiveLight ? Math.Max(0, 1.0 - Math.Max(0, absDistance - 1) * 0.34) : 0;
            bool isNeighbor = hasActiveLight && absDistance > 0;
            bool isCloseNeighbor = hasActiveLight && absDistance <= 2 && absDistance > 0;

            double edgeOpacity = isSelected ? 0.14 : 0.0;
            double sheenOpacity = isSelected ? 0.10 : 0.0;
            double lowerOpacity = isSelected ? 0.045 : 0.0;
            double sideGlintOpacity = isCloseNeighbor ? 0.16 + (0.34 * falloff) : 0.0;
            double cornerGlintOpacity = isCloseNeighbor ? 0.18 + (0.30 * falloff) : isSelected ? 0.12 : 0.0;
            double topGlintOpacity = isSelected ? 0.14 : 0.0;

            var edge = FindDescendantByName<Border>(card, "BubbleEdgeLight");
            if (edge != null)
            {
                edge.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                edge.Background = CreateBubbleEdgeBrush(direction, isSelected, isNeighbor);
                AnimateReflectionOpacity(edge, edgeOpacity, 190);
            }

            var sheen = FindDescendantByName<Border>(card, "BubbleSheen");
            if (sheen != null)
            {
                sheen.Background = CreateBubbleSheenBrush(direction, isSelected, isNeighbor);
                AnimateReflectionOpacity(sheen, sheenOpacity, 220);

                if (sheen.RenderTransform is CompositeTransform transform)
                {
                    double cardWidth = Math.Max(120, card.ActualWidth);
                    double targetX = isSelected ? 0 : -direction * cardWidth * 0.10;
                    double targetY = isSelected ? -2 * _currentShellLayoutScale : 0;
                    AnimateReflectionTransform(transform, targetX, targetY, isSelected ? 1.0 : 0.98, 220);
                }
            }

            var lowerReflection = FindDescendantByName<Border>(card, "BubbleLowerReflection");
            if (lowerReflection != null)
            {
                AnimateReflectionOpacity(lowerReflection, lowerOpacity, 220);
            }

            var sideGlint = FindDescendantByName<Border>(card, "BubbleSideGlint");
            if (sideGlint != null)
            {
                sideGlint.HorizontalAlignment = direction < 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                sideGlint.Background = CreateBubbleSideGlintBrush(direction < 0 ? -1 : 1, isNeighbor);
                AnimateReflectionOpacity(sideGlint, sideGlintOpacity, 210);

                if (sideGlint.RenderTransform is CompositeTransform sideTransform)
                {
                    double targetX = direction == 0 ? 0 : direction * 8 * _currentShellLayoutScale;
                    AnimateReflectionTransform(sideTransform, targetX, 0, isCloseNeighbor ? 1.08 : 0.96, 210);
                }
            }

            var cornerGlint = FindDescendantByName<Border>(card, "BubbleCornerGlint");
            if (cornerGlint != null)
            {
                bool lightFromLeft = direction > 0;
                cornerGlint.HorizontalAlignment = direction < 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                cornerGlint.Background = CreateBubbleCornerGlintBrush(direction < 0 ? -1 : 1, isNeighbor);
                AnimateReflectionOpacity(cornerGlint, cornerGlintOpacity, 210);

                if (cornerGlint.RenderTransform is CompositeTransform cornerTransform)
                {
                    cornerTransform.Rotation = direction < 0 ? -10 : 10;
                    cornerTransform.CenterX = lightFromLeft ? 0 : Math.Max(18, cornerGlint.ActualWidth);
                    cornerTransform.CenterY = Math.Max(1, cornerGlint.ActualHeight * 0.5);
                    double targetX = direction == 0 ? 0 : direction * 5 * _currentShellLayoutScale;
                    double targetY = isSelected ? 0 : 2.5 * _currentShellLayoutScale;
                    AnimateReflectionTransform(cornerTransform, targetX, targetY, isCloseNeighbor ? 1.04 : 1.0, 210);
                }
            }

            var topGlint = FindDescendantByName<Border>(card, "BubbleTopGlint");
            if (topGlint != null)
            {
                topGlint.Background = CreateBubbleTopGlintBrush(direction, isNeighbor);
                AnimateReflectionOpacity(topGlint, topGlintOpacity, 220);

                if (topGlint.RenderTransform is CompositeTransform topTransform)
                {
                    double cardWidth = Math.Max(120, card.ActualWidth);
                    double targetX = isSelected ? 0 : -direction * cardWidth * 0.08;
                    AnimateReflectionTransform(topTransform, targetX, 0, isSelected ? 1.0 : 0.98, 220);
                }
            }
        }

        private void EnsureGlassRailReflection(Border rail, double scale, bool isTopRail)
        {
            if (rail == null)
            {
                return;
            }

            Grid host;
            if (rail.Child is Grid existingGrid &&
                existingGrid.Tag is string existingTag &&
                existingTag == "GlassRailReflectionHost")
            {
                host = existingGrid;
            }
            else
            {
                UIElement originalChild = rail.Child;
                rail.Child = null;

                host = new Grid
                {
                    Tag = "GlassRailReflectionHost"
                };

                if (originalChild != null)
                {
                    host.Children.Add(originalChild);
                }

                rail.Child = host;
            }

            double radius = Math.Max(0, rail.CornerRadius.TopLeft);

            Border sheen = host.Children.OfType<Border>().FirstOrDefault(child => child.Name == "RailSheen");
            if (sheen == null)
            {
                sheen = new Border
                {
                    Name = "RailSheen",
                    IsHitTestVisible = false,
                    Opacity = 0,
                    RenderTransform = new CompositeTransform(),
                    RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
                };
                host.Children.Add(sheen);
            }

            sheen.CornerRadius = new CornerRadius(radius);
            sheen.Margin = new Thickness(2 * scale);

            Border edge = host.Children.OfType<Border>().FirstOrDefault(child => child.Name == "RailEdgeLight");
            if (edge == null)
            {
                edge = new Border
                {
                    Name = "RailEdgeLight",
                    IsHitTestVisible = false,
                    Opacity = 0
                };
                host.Children.Add(edge);
            }

            edge.Margin = new Thickness(4 * scale);
            edge.CornerRadius = new CornerRadius(Math.Max(0, radius - (4 * scale)));
            edge.BorderThickness = new Thickness(0);

            Border lightBand = host.Children.OfType<Border>().FirstOrDefault(child => child.Name == "RailLightBand");
            if (lightBand == null)
            {
                lightBand = new Border
                {
                    Name = "RailLightBand",
                    IsHitTestVisible = false,
                    Opacity = 0,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    RenderTransform = new CompositeTransform(),
                    RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
                };
                host.Children.Add(lightBand);
            }

            lightBand.Height = Math.Max(2, 3 * scale);
            lightBand.Margin = isTopRail
                ? new Thickness(32 * scale, 0, 32 * scale, 4 * scale)
                : new Thickness(32 * scale, 4 * scale, 32 * scale, 0);
            lightBand.VerticalAlignment = isTopRail ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            lightBand.CornerRadius = new CornerRadius(3 * scale);

            Border cornerGlint = host.Children.OfType<Border>().FirstOrDefault(child => child.Name == "RailCornerGlint");
            if (cornerGlint == null)
            {
                cornerGlint = new Border
                {
                    Name = "RailCornerGlint",
                    IsHitTestVisible = false,
                    Opacity = 0,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    RenderTransform = new CompositeTransform { Rotation = 7 },
                    RenderTransformOrigin = new Windows.Foundation.Point(0.0, 0.5)
                };
                host.Children.Add(cornerGlint);
            }

            cornerGlint.Width = Math.Max(36, 48 * scale);
            cornerGlint.Height = Math.Max(2, 3 * scale);
            cornerGlint.VerticalAlignment = isTopRail ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            cornerGlint.Margin = isTopRail
                ? new Thickness(20 * scale, 0, 20 * scale, 7 * scale)
                : new Thickness(20 * scale, 7 * scale, 20 * scale, 0);
            cornerGlint.CornerRadius = new CornerRadius(3 * scale);
        }

        private void UpdateRailReflectionRig(bool launcherIsSource, bool cardsAreSource, bool hasActiveLight)
        {
            double lightBias = 0.5;
            double intensity = hasActiveLight ? 1.0 : 0.45;

            if (launcherIsSource)
            {
                int launcherCount = Math.Max(1, _launcherAreaButtons?.Count ?? 1);
                lightBias = 0.18 + (Math.Clamp(_selectedLauncherAreaIndex, 0, launcherCount - 1) / (double)launcherCount) * 0.26;
                intensity = 0.92;
            }
            else if (cardsAreSource)
            {
                int cardCount = Math.Max(1, ProgramCardPanel?.Children.Count ?? 1);
                lightBias = 0.44 + (Math.Clamp(_selectedCardIndex, 0, cardCount - 1) / (double)Math.Max(1, cardCount - 1)) * 0.42;
                intensity = 1.0;
            }
            else if (_currentFocusArea == FocusArea.TopButtons)
            {
                int topCount = Math.Max(1, _topButtons?.Count ?? 1);
                lightBias = 0.36 + (Math.Clamp(_selectedTopButtonIndex, 0, topCount - 1) / (double)Math.Max(1, topCount - 1)) * 0.28;
                intensity = 0.86;
            }

            ApplyRailReflection(infopanelright, lightBias, intensity, true);
            ApplyRailReflection(_bottomLegendBar, lightBias, Math.Max(0.62, intensity * 0.82), false);
        }

        private void ApplyRailReflection(Border rail, double lightBias, double intensity, bool isTopRail)
        {
            if (rail == null)
            {
                return;
            }

            EnsureGlassRailReflection(rail, isTopRail ? _currentTopPanelScale : _currentShellLayoutScale, isTopRail);

            var sheen = FindDescendantByName<Border>(rail, "RailSheen");
            if (sheen != null)
            {
                sheen.Background = CreateRailSheenBrush(lightBias, intensity);
                AnimateReflectionOpacity(sheen, isTopRail ? 0.0 : Math.Clamp(0.02 + (intensity * 0.045), 0, 0.08), 280);

                if (sheen.RenderTransform is CompositeTransform transform)
                {
                    double railWidth = Math.Max(320, rail.ActualWidth);
                    AnimateReflectionTransform(
                        transform,
                        (lightBias - 0.5) * railWidth * 0.24,
                        0,
                        1.0 + (0.018 * intensity),
                        260);
                }
            }

            var edge = FindDescendantByName<Border>(rail, "RailEdgeLight");
            if (edge != null)
            {
                edge.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                edge.Background = CreateRailEdgeBrush(lightBias, intensity);
                AnimateReflectionOpacity(edge, 0.0, 260);
            }

            var band = FindDescendantByName<Border>(rail, "RailLightBand");
            if (band != null)
            {
                band.Background = CreateRailBandBrush(lightBias, intensity);
                AnimateReflectionOpacity(band, Math.Clamp(0.08 + (intensity * 0.18), 0, 0.28), 260);
            }

            var corner = FindDescendantByName<Border>(rail, "RailCornerGlint");
            if (corner != null)
            {
                bool useRightCorner = lightBias > 0.55;
                corner.HorizontalAlignment = useRightCorner ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                corner.Background = CreateRailCornerGlintBrush(useRightCorner ? -1 : 1, intensity);

                if (corner.RenderTransform is CompositeTransform transform)
                {
                    double railScale = isTopRail ? _currentTopPanelScale : _currentShellLayoutScale;
                    transform.Rotation = (useRightCorner ? -7 : 7) * (isTopRail ? -1 : 1);
                    transform.CenterX = useRightCorner ? Math.Max(24, corner.ActualWidth) : 0;
                    transform.CenterY = Math.Max(1, corner.ActualHeight * 0.5);
                    AnimateReflectionTransform(
                        transform,
                        (useRightCorner ? -1 : 1) * Math.Max(2, 4 * railScale),
                        (isTopRail ? -1 : 1) * 1.5 * railScale,
                        1.0,
                        260);
                }

                AnimateReflectionOpacity(corner, Math.Clamp(0.08 + (intensity * 0.20), 0, 0.30), 260);
            }
        }

        private LinearGradientBrush CreateRailSheenBrush(double lightBias, double intensity)
        {
            lightBias = Math.Clamp(lightBias, 0.08, 0.92);
            byte hot = (byte)Math.Clamp(34 + (56 * intensity), 0, 96);
            byte accent = (byte)Math.Clamp(24 + (44 * intensity), 0, 76);

            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(Math.Max(0, lightBias - 0.50), 0),
                EndPoint = new Windows.Foundation.Point(Math.Min(1, lightBias + 0.50), 1),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.22, Color = Windows.UI.Color.FromArgb(6, 255, 255, 255) },
                    new GradientStop { Offset = 0.44, Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = 0.60, Color = GetThemeAccentColor(accent) },
                    new GradientStop { Offset = 0.86, Color = Windows.UI.Color.FromArgb(5, 255, 255, 255) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateRailEdgeBrush(double lightBias, double intensity)
        {
            lightBias = Math.Clamp(lightBias, 0.08, 0.92);
            byte hot = (byte)Math.Clamp(38 + (52 * intensity), 0, 96);
            byte accent = (byte)Math.Clamp(28 + (46 * intensity), 0, 82);

            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(Math.Max(0, lightBias - 0.46), 0),
                EndPoint = new Windows.Foundation.Point(Math.Min(1, lightBias + 0.46), 1),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.24, Color = Windows.UI.Color.FromArgb(5, 255, 255, 255) },
                    new GradientStop { Offset = 0.42, Color = GetThemeAccentColor(accent) },
                    new GradientStop { Offset = 0.56, Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = 0.82, Color = Windows.UI.Color.FromArgb(5, 255, 255, 255) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateRailBandBrush(double lightBias, double intensity)
        {
            lightBias = Math.Clamp(lightBias, 0.08, 0.92);
            byte hot = (byte)Math.Clamp(64 + (78 * intensity), 0, 154);

            return new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = Math.Max(0, lightBias - 0.18), Color = Windows.UI.Color.FromArgb(2, 255, 255, 255) },
                    new GradientStop { Offset = lightBias, Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = Math.Min(1, lightBias + 0.18), Color = GetThemeAccentColor((byte)Math.Clamp(34 + (56 * intensity), 0, 108)) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private LinearGradientBrush CreateRailCornerGlintBrush(int lightDirection, double intensity)
        {
            byte hot = (byte)Math.Clamp(82 + (82 * intensity), 0, 178);
            byte soft = (byte)Math.Clamp(24 + (34 * intensity), 0, 76);

            return new LinearGradientBrush
            {
                StartPoint = lightDirection < 0
                    ? new Windows.Foundation.Point(1, 0)
                    : new Windows.Foundation.Point(0, 0),
                EndPoint = lightDirection < 0
                    ? new Windows.Foundation.Point(0, 0)
                    : new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Offset = 0.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) },
                    new GradientStop { Offset = 0.22, Color = Windows.UI.Color.FromArgb(soft, 255, 255, 255) },
                    new GradientStop { Offset = 0.52, Color = Windows.UI.Color.FromArgb(hot, 255, 255, 255) },
                    new GradientStop { Offset = 0.76, Color = GetThemeAccentColor((byte)Math.Clamp(soft + 18, 0, 108)) },
                    new GradientStop { Offset = 1.00, Color = Windows.UI.Color.FromArgb(0, 255, 255, 255) }
                }
            };
        }

        private T FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }

                T nested = FindDescendantByName<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void AnimateReflectionOpacity(UIElement element, double targetOpacity, int durationMs)
        {
            if (element == null)
            {
                return;
            }

            var animation = new DoubleAnimation
            {
                To = Math.Clamp(targetOpacity, 0.0, 1.0),
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void AnimateReflectionTransform(CompositeTransform transform, double targetX, double targetY, double targetScale, int durationMs)
        {
            if (transform == null)
            {
                return;
            }

            var storyboard = new Storyboard();
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

            void AddTransformAnimation(string property, double value)
            {
                var animation = new DoubleAnimation
                {
                    To = value,
                    Duration = duration,
                    EasingFunction = easing
                };
                Storyboard.SetTarget(animation, transform);
                Storyboard.SetTargetProperty(animation, property);
                storyboard.Children.Add(animation);
            }

            AddTransformAnimation("TranslateX", targetX);
            AddTransformAnimation("TranslateY", targetY);
            AddTransformAnimation("ScaleX", targetScale);
            AddTransformAnimation("ScaleY", targetScale);

            storyboard.Begin();
        }






        private void ScrollToCardAnimated(UIElement card)
        {
            if (card == null || ProgramScrollViewer == null) return;

            try
            {
                var transform = card.TransformToVisual(ProgramCardPanel);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                double cardX = position.X;
                double cardWidth = (card as FrameworkElement).ActualWidth;
                double cardRight = cardX + cardWidth;
                double currentOffset = ProgramScrollViewer.HorizontalOffset;
                double viewportWidth = ProgramScrollViewer.ActualWidth;
                double maxOffset = ProgramScrollViewer.ScrollableWidth;
                double edgePadding = 26 * _currentShellLayoutScale;
                double visibleLeft = currentOffset + edgePadding;
                double visibleRight = currentOffset + viewportWidth - edgePadding;

                if (cardX >= visibleLeft && cardRight <= visibleRight)
                {
                    return;
                }

                double targetOffset = currentOffset;
                if (cardX < visibleLeft)
                {
                    targetOffset = Math.Max(0, cardX - edgePadding);
                }
                else if (cardRight > visibleRight)
                {
                    targetOffset = Math.Min(maxOffset, cardRight - viewportWidth + edgePadding);
                }

                if (Math.Abs(ProgramScrollViewer.HorizontalOffset - targetOffset) < 2)
                {
                    return;
                }

                ProgramScrollViewer.ChangeView(targetOffset, null, null, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SCROLL ERROR] Could not scroll to card: {ex.Message}");
            }
        }










        private void KillApp(Process proc, IntPtr hwnd)
        {
            try
            {
                if (proc.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                {
                    // Nur das Fenster schließen, nicht den Prozess
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                else
                {
                    proc.Kill();
                }
            }
            catch { }


        }

        private bool IsWindowMaximized(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;

            // Get the monitor where the window is located
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            // Get the monitor's information (specifically the work area)
            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(hMonitor, ref monitorInfo);
            RECT monitorWorkRect = monitorInfo.rcWork;

            // Get the window's current rectangle
            GetWindowRect(hwnd, out RECT windowRect);

            // Compare the window's rectangle with the monitor's work area rectangle
            return windowRect.Left == monitorWorkRect.Left &&
                   windowRect.Top == monitorWorkRect.Top &&
                   windowRect.Right == monitorWorkRect.Right &&
                   windowRect.Bottom == monitorWorkRect.Bottom;
        }

        /// <summary>
        /// The main entry point for the shortcut. Checks for Xbox fullscreen, then brings this app to front.
        /// </summary>
        /// 

        const uint WM_KEYUP = 0x0101;
        const uint WM_KEYDOWN = 0x0100;

        private void SendEscapeKey(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                Debug.WriteLine($"[GCM] SendEscapeKey: Invalid window handle.");
                return;
            }

            Debug.WriteLine($"[GCM] Sending ESC keystroke to window: {hWnd}");
            // We are using the received 'hWnd' parameter inside this method
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
            PostMessage(hWnd, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);
        }
        private Process _suspendedGameProcess = null;


        private static double _lastKnownDpi = 0;

        /* * Documentation:
         * This is the fully updated BringTaskManagerToFrontAndFocus method.
         * It now checks the current system DPI before doing anything else.
         * If the DPI has changed since the last time GCM was shown, it triggers 
         * a complete window rebuild to ensure a 100% accurate UI.
         */
        private void ResetParkedSteamState()
        {
            _parkedSteamBigPictureHwnd = IntPtr.Zero;
            _steamBigPictureWasParkedByTaskManager = false;
        }

        private async Task SoftBringWindowToForegroundAsync(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            try
            {
                IntPtr currentForegroundHwnd = GetForegroundWindow();
                uint currentThreadId = GetWindowThreadProcessId(currentForegroundHwnd, out _);
                uint thisThreadId = GetCurrentThreadId();

                AllowSetForegroundWindow(ASFW_ANY);
                if (currentThreadId != thisThreadId)
                {
                    AttachThreadInput(thisThreadId, currentThreadId, true);
                }

                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else
                {
                    ShowWindow(hWnd, SW_SHOW);
                }
                BringWindowToTop(hWnd);
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                SetForegroundWindow(hWnd);
                SetActiveWindow(hWnd);
                SetFocus(hWnd);

                if (currentThreadId != thisThreadId)
                {
                    AttachThreadInput(thisThreadId, currentThreadId, false);
                }

                await Task.Delay(60);
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Focus Soft Error] {ex.Message}");
            }
        }

        private async Task<bool> TryBringGcmToFrontViaTaskViewAsync()
        {
            try
            {
                ClearFocusReturnWatchdog();
                ResetParkedSteamState();

                await SendWinTabForHandoffAsync();
                await Task.Delay(1000);
                await SoftBringWindowToForegroundAsync(WinRT.Interop.WindowNative.GetWindowHandle(this));
                await SendWinTabForHandoffAsync();
                await Task.Delay(260);
                await ConfirmGcmInputFocusWithSafeClickAsync();
                await Task.Delay(80);
                return IsWindowInForeground();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Task View switch to GCM failed: {ex.Message}");
                return false;
            }
        }

        private bool IsModalOverlayVisible()
        {
            return GithubReleaseOverlay?.Visibility == Visibility.Visible ||
                   WindowsReturnConfirmOverlay?.Visibility == Visibility.Visible ||
                   SettingsOverlay?.Visibility == Visibility.Visible ||
                   AppLauncher?.Visibility == Visibility.Visible ||
                   AudioOverlay?.Visibility == Visibility.Visible ||
                   PowerMenu?.Visibility == Visibility.Visible ||
                   GameOptionsOverlay?.Visibility == Visibility.Visible;
        }

        private async Task ConfirmGcmInputFocusWithSafeClickAsync()
        {
            IntPtr gcmHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (gcmHwnd == IntPtr.Zero || !IsWindow(gcmHwnd) || IsModalOverlayVisible())
            {
                return;
            }

            try
            {
                if (!GetWindowRect(gcmHwnd, out RECT rect))
                {
                    return;
                }

                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;
                if (windowWidth <= 0 || windowHeight <= 0)
                {
                    return;
                }

                int clickX = Math.Clamp(rect.Left + 12, 0, Math.Max(0, GetScreenWidth() - 1));
                int clickY = Math.Clamp(rect.Top + 12, 0, Math.Max(0, GetScreenHeight() - 1));

                SetCursorPos(clickX, clickY);
                await Task.Delay(18);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(12);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(18);

                if (this.Content is UIElement root)
                {
                    root.Focus(FocusState.Programmatic);
                }

                Debug.WriteLine("[Focus] Safe click confirmed GCM input focus.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Focus Safe Click Error] {ex.Message}");
            }
            finally
            {
                ParkMouseCursor();
            }
        }

        private async Task<bool> TryReturnToSteamViaTaskViewAsync()
        {
            ClearFocusReturnWatchdog();
            ResetParkedSteamState();

            try
            {
                Process.Start(new ProcessStartInfo("steam://open/gamepadui") { UseShellExecute = true });
            }
            catch
            {
            }

            IntPtr steamHwnd = FindSteamBigPictureWindow();
            if (steamHwnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return await TrySwitchToWindowViaTaskViewAsync(
                    steamHwnd,
                    FocusReturnTarget.SteamBigPicture,
                    maximizeTarget: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Task View switch back to Steam failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TrySwitchToWindowViaTaskViewAsync(
            IntPtr targetHwnd,
            FocusReturnTarget focusTarget,
            bool maximizeTarget = false)
        {
            if (targetHwnd == IntPtr.Zero || !IsWindow(targetHwnd))
            {
                return false;
            }

            ClearFocusReturnWatchdog();
            ResetParkedSteamState();

            try
            {
                MakeSelfNonTopmost();

                if (focusTarget == FocusReturnTarget.SteamBigPicture)
                {
                    _lastKnownSteamBigPictureHwnd = targetHwnd;
                }
                else if (focusTarget == FocusReturnTarget.GameWindow)
                {
                    _lastKnownGameHwnd = targetHwnd;
                }

                await SendWinTabForHandoffAsync();
                await Task.Delay(1000);

                if (IsIconic(targetHwnd))
                {
                    ShowWindow(targetHwnd, SW_RESTORE);
                }
                else if (maximizeTarget)
                {
                    ShowWindow(targetHwnd, SW_SHOWMAXIMIZED);
                }
                else
                {
                    ShowWindow(targetHwnd, SW_SHOW);
                }

                await SoftBringWindowToForegroundAsync(targetHwnd);

                // Close Task View with the same gesture once the target is selected.
                await SendWinTabForHandoffAsync();
                await Task.Delay(260);

                return GetForegroundWindow() == targetHwnd;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Task View switch to target failed: {ex.Message}");
                return false;
            }
        }

        public void BringTaskManagerToFrontAndFocus()
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                UpdateControllerBatteryStatus();
                ApplySteamOnlyMode();

                if (IsWindowInForeground())
                {
                    if (!await TryReturnToSteamViaTaskViewAsync())
                    {
                        await StartSteam(false);
                    }
                    return;
                }

                if (!await TryBringGcmToFrontViaTaskViewAsync())
                {
                    Debug.WriteLine("[GCM] Task View handoff to GCM did not report focus; skipping fallback by design.");
                }
            });
        }

        private async Task ParkSteamBigPictureForTaskManagerAsync()
        {
            IntPtr steamHwnd = FindSteamBigPictureWindow();
            if (steamHwnd == IntPtr.Zero)
            {
                _parkedSteamBigPictureHwnd = IntPtr.Zero;
                _steamBigPictureWasParkedByTaskManager = false;
                return;
            }

            try
            {
                _parkedSteamBigPictureHwnd = steamHwnd;
                _lastKnownSteamBigPictureHwnd = steamHwnd;
                _steamBigPictureWasParkedByTaskManager = true;
                ShowWindow(steamHwnd, SW_MINIMIZE);
                await Task.Delay(80);
                ShowWindow(steamHwnd, SW_HIDE);
                SetWindowPos(steamHwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                await Task.Delay(120);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Failed to park Steam Big Picture: {ex.Message}");
            }
        }

        private async Task<bool> RestoreParkedSteamBigPictureAsync()
        {
            if (!_steamBigPictureWasParkedByTaskManager)
            {
                return false;
            }

            IntPtr steamHwnd = _parkedSteamBigPictureHwnd;
            if (steamHwnd == IntPtr.Zero)
            {
                steamHwnd = FindSteamBigPictureWindow();
            }

            if (steamHwnd == IntPtr.Zero)
            {
                _parkedSteamBigPictureHwnd = IntPtr.Zero;
                _steamBigPictureWasParkedByTaskManager = false;
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo("steam://open/gamepadui") { UseShellExecute = true });
            }
            catch
            {
            }

            try
            {
                MakeSelfNonTopmost();
                IntPtr selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                SetWindowPos(selfHwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                ShowWindow(selfHwnd, SW_MINIMIZE);
                await Task.Delay(80);
                ShowWindow(steamHwnd, SW_SHOW);
                ShowWindow(steamHwnd, SW_RESTORE);
                ShowWindow(steamHwnd, SW_SHOWMAXIMIZED);
                await Task.Delay(160);
                await ForcefullyBringToForeground(steamHwnd);
                _lastKnownSteamBigPictureHwnd = steamHwnd;
                _parkedSteamBigPictureHwnd = IntPtr.Zero;
                _steamBigPictureWasParkedByTaskManager = false;
                ArmFocusReturnWatchdog(FocusReturnTarget.SteamBigPicture, TimeSpan.FromSeconds(15));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Failed to restore parked Steam Big Picture: {ex.Message}");
                return false;
            }
        }




        // Added 'forceRestart' parameter, defaulting to false
        private async Task StartXbox(bool forceRestart = false)
        {
            try
            {
                // Push GCM to the absolute background so Xbox can take over smoothly
                IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                SetWindowPos(myHwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                // Check if Xbox processes are currently running
                var xboxProcesses = Process.GetProcessesByName("XboxApp")
                                           .Concat(Process.GetProcessesByName("XboxPcApp"))
                                           .ToList();
                bool isRunning = xboxProcesses.Any();

                // --- CASE A: COLD BOOT (forceRestart = true) ---
                if (forceRestart)
                {
                    Debug.WriteLine("[GCM] Brute-Force Xbox Start: Killing old instances (Cold Boot)...");
                    if (isRunning)
                    {
                        foreach (var proc in xboxProcesses)
                        {
                            try { if (!proc.HasExited) proc.Kill(); } catch { }
                        }
                        // Give Windows time to release the file locks and window handles
                        await Task.Delay(1000);
                    }

                    // Auto-hide taskbar so the app maximizes without a gap at the bottom
                    TaskbarManager.EnableAutoHide();
                    Process.Start(new ProcessStartInfo("xbox:") { UseShellExecute = true });
                }
                // --- CASE B: WARM BOOT (forceRestart = false) ---
                else
                {
                    if (isRunning)
                    {
                        Debug.WriteLine("[GCM] Xbox is already running. Switching focus (Warm Boot)...");
                        // Calling the protocol again acts as a wake-up command for UWP apps
                        Process.Start(new ProcessStartInfo("xbox:") { UseShellExecute = true });
                    }
                    else
                    {
                        Debug.WriteLine("[GCM] Xbox was not running. Starting normally...");
                        TaskbarManager.EnableAutoHide();
                        Process.Start(new ProcessStartInfo("xbox:") { UseShellExecute = true });
                    }
                }

                // --- WAIT FOR THE WINDOW ---
                // If it's a warm boot and already running, we don't need to wait as long
                int timeoutSeconds = (forceRestart || !isRunning) ? 15 : 5;
                IntPtr xboxHwnd = await FindXboxWindowHandleAsync(timeoutSeconds);

                if (xboxHwnd == IntPtr.Zero)
                {
                    Debug.WriteLine("[GCM] Timeout! Xbox window could not be found.");
                    return;
                }

                Debug.WriteLine($"[GCM] Xbox window ready. Applying Nuclear-Focus...");

                // Force the Xbox window to the front using our unbreakable method
                await ForcefullyBringToForeground(xboxHwnd);

                // Ensure the window is maximized
                if (!IsWindowMaximized(xboxHwnd))
                {
                    Debug.WriteLine("[GCM] Xbox window is not maximized. Maximizing now...");
                    MaximizeXboxWindow(xboxHwnd);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Error in StartXbox: {ex.Message}");
            }
        }

        private BitmapImage GetAppIconAsBitmapImage(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return null;

            // Cache-Check: Haben wir das Icon für diese EXE schonmal geladen?
            if (_iconCache.TryGetValue(exePath, out var cachedIcon)) return cachedIcon;

            try
            {
                using (var icon = Icon.ExtractAssociatedIcon(exePath))
                {
                    using (var bmp = icon.ToBitmap())
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Seek(0, SeekOrigin.Begin);

                        var bmpImage = new BitmapImage();
                        bmpImage.SetSource(ms.AsRandomAccessStream());

                        // Im Cache speichern
                        _iconCache[exePath] = bmpImage;
                        return bmpImage;
                    }
                }
            }
            catch { return null; }
        }




        private const uint GW_OWNER = 4;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private string GetWindowTitle(IntPtr hWnd)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            if (GetWindowText(hWnd, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return string.Empty;
        }


        public static bool IsTaskActive(string taskName)
        {
            using (TaskService ts = new TaskService())
            {
                var task = ts.FindTask(taskName, true);
                return task != null && task.Enabled;
            }
        }

        // Plays the navigation sound (e.g. moving between cards)
        private void PlayNavigationSound() => PlayCachedSound("nav");

        // Plays the activation sound (e.g. pressing A / launching an app)
        private async void PlayActivationSound()
        {
            PlayCachedSound("play");
            // Intensity 0.4 (40%), Duration 200ms
            await TriggerVibration(0, 0.4f, 200);
        }

        // Plays the deactivation sound (e.g. closing an app / pressing B)
        private async void PlaydeactivationSound()
        {
            PlayCachedSound("pause");
            // Intensity 0.2 (20%), Duration 200ms
            await TriggerVibration(0, 0.2f, 200);
        }

        private int GetUiSoundCooldownMilliseconds(string soundKey)
        {
            return soundKey switch
            {
                "nav" => 85,
                "play" => 140,
                "pause" => 140,
                _ => 100
            };
        }

        private double GetUiSoundVolume(string soundKey)
        {
            return soundKey switch
            {
                "nav" => 0.42,
                "play" => 0.58,
                "pause" => 0.54,
                _ => 0.5
            };
        }

        private void EnsureUiSoundPlayers()
        {
            foreach (var entry in _soundCache)
            {
                if (_uiSoundPlayers.ContainsKey(entry.Key))
                {
                    continue;
                }

                var player = new MediaPlayer
                {
                    AutoPlay = false,
                    IsLoopingEnabled = false,
                    Volume = GetUiSoundVolume(entry.Key),
                    AudioCategory = MediaPlayerAudioCategory.Other,
                    Source = MediaSource.CreateFromUri(entry.Value)
                };

                _uiSoundPlayers[entry.Key] = player;
            }
        }

        private void PlayCachedSound(string soundKey)
        {
            if (!DispatcherQueue.HasThreadAccess)
            {
                DispatcherQueue.TryEnqueue(() => PlayCachedSound(soundKey));
                return;
            }

            EnsureUiSoundPlayers();

            if (!_uiSoundPlayers.TryGetValue(soundKey, out var player))
            {
                return;
            }

            try
            {
                DateTime now = DateTime.UtcNow;
                if (_uiSoundLastPlayedUtc.TryGetValue(soundKey, out var lastPlayedUtc) &&
                    (now - lastPlayedUtc).TotalMilliseconds < GetUiSoundCooldownMilliseconds(soundKey))
                {
                    return;
                }

                _uiSoundLastPlayedUtc[soundKey] = now;
                player.Volume = GetUiSoundVolume(soundKey);
                player.Pause();
                player.PlaybackSession.Position = TimeSpan.Zero;
                player.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundError] Failed to play {soundKey}: {ex.Message}");
            }
        }


        #endregion // TaskManager
        #region Gamepad/Keyboard_Navigation
        public static void DisableWindowsControllerShortcuts()
        {
            try
            {
                // 1. Verhindert, dass der Guide-Button die Game Bar oder die Taskansicht (Alt+Tab) öffnet
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar"))
                {
                    if (key != null)
                    {
                        // Schaltet "Spieleleiste mit dieser Taste auf einem Controller öffnen" aus
                        key.SetValue("UseNexusForGameBarEnabled", 0, Microsoft.Win32.RegistryValueKind.DWord);
                        key.SetValue("ShowStartupPanel", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }

                // 2. Schaltet das GameDVR Overlay (Hintergrundaufzeichnung) auf Systemebene ab
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR"))
                {
                    if (key != null)
                    {
                        key.SetValue("AppCaptureEnabled", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }

                Debug.WriteLine("[System] Windows Controller Shortcuts (Game Bar / Task View) erfolgreich deaktiviert.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[System] Fehler beim Deaktivieren der Controller Shortcuts: {ex.Message}");
            }
        }
        public static void NukeWindowsGuideButton()
        {
            try
            {
                // 1. Töte die Game Bar Prozesse, die versteckt im Hintergrund lauern
                string[] processesToKill = { "GameBar", "GameBarFTServer", "bcastdvr" };
                foreach (string p in processesToKill)
                {
                    foreach (var proc in Process.GetProcessesByName(p))
                    {
                        try { proc.Kill(); } catch { }
                    }
                }

                // 2. Deaktiviere den Dienst, der den Guide-Button an Windows weiterleitet!
                // Da GCM "Secret XInput #100" nutzt, können WIR den Button trotzdem noch lesen!
                if (IsAdministrator())
                {
                    var psiDisable = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = "config XboxGipSvc start= disabled",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psiDisable)?.WaitForExit();

                    var psiStop = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = "stop XboxGipSvc",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(psiStop)?.WaitForExit();

                    Debug.WriteLine("[System] Windows Xbox service disabled. Guide button is reserved for GCM.");
                }
                else
                {
                    Debug.WriteLine("[System] Local mode active. Skipping XboxGipSvc changes because no administrator token is available.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[System] Fehler beim Blockieren des Guide-Buttons: {ex.Message}");
            }
        }

        #region shortcuts
        #region shortcut overlay
        // --- HELPER: Macht aus den Config-Strings schöne Anzeige-Namen ---
        private string GetNiceKeyName(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Equals("None", StringComparison.OrdinalIgnoreCase))
                return null;

            key = key.Trim();

            // Übersetzungen für schönere Namen im Overlay
            if (key.Equals("Back", StringComparison.OrdinalIgnoreCase)) return "Select";
            if (key.Equals("Guide", StringComparison.OrdinalIgnoreCase)) return "Xbox";
            if (key.Equals("LeftShoulder", StringComparison.OrdinalIgnoreCase)) return "LB";
            if (key.Equals("RightShoulder", StringComparison.OrdinalIgnoreCase)) return "RB";
            if (key.Equals("LeftThumb", StringComparison.OrdinalIgnoreCase)) return "LS";
            if (key.Equals("RightThumb", StringComparison.OrdinalIgnoreCase)) return "RS";
            if (key.Equals("DPadUp", StringComparison.OrdinalIgnoreCase)) return "D-Up";
            if (key.Equals("DPadDown", StringComparison.OrdinalIgnoreCase)) return "D-Down";
            if (key.Equals("DPadLeft", StringComparison.OrdinalIgnoreCase)) return "D-Left";
            if (key.Equals("DPadRight", StringComparison.OrdinalIgnoreCase)) return "D-Right";

            // Fallback: Wenn es keine Abkürzung braucht (z.B. "A", "B", "Start")
            return key;
        }

        // --- OVERLAY LOGIC ---
        private BlankWindow1 _globalShortcutOverlay = null;
        private bool _isOverlayClosing = false; // Neu: Unser Türsteher für die Animation

        private void ToggleGlobalShortcutOverlay()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_globalShortcutOverlay != null)
                {
                    CloseGlobalShortcutOverlay();
                }
                else
                {
                    var displayList = new List<ShortcutDisplayItem>();
                    foreach (var shortcut in _runtimeShortcuts)
                    {
                        string actionText = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(shortcut.FunctionName);
                        string keysText = shortcut.DisplayText;

                        if (shortcut.HoldDurationSeconds > 0)
                            keysText = $"Hold [ {keysText} ] for {shortcut.HoldDurationSeconds}s";
                        else
                            keysText = $"Press [ {keysText} ]";

                        displayList.Add(new ShortcutDisplayItem { KeysDisplay = keysText, ActionName = actionText });
                    }

                    // 1. Fenster erstellen
                    _globalShortcutOverlay = new BlankWindow1(displayList);

                    // 2. NEU: Handle des neuen Fensters abrufen und aus Alt+Tab entfernen
                    IntPtr overlayHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_globalShortcutOverlay);
                    HideWindowFromAltTab(overlayHwnd);

                    PlayNavigationSound();
                }
            });
        }

        private void CloseGlobalShortcutOverlay()
        {
            // Only trigger close if it's not already currently closing
            if (_globalShortcutOverlay != null && !_isOverlayClosing)
            {
                _isOverlayClosing = true;
                PlaydeactivationSound();

                // Pass the callback to set null ONLY after the animation has finished
                _globalShortcutOverlay.CloseAnimated(() =>
                {
                    _globalShortcutOverlay = null;
                    _isOverlayClosing = false;
                });
            }
        }





        #endregion shortcut overlay
        #region alt tab
        private const byte VK_MENU = 0x12; // ALT key
        private const byte VK_TAB = 0x09;
        private const byte VK_SPACE = 0x20;
        private const byte VK_R = 0x52;    // R


        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private bool isAltTabMode = false;
        private bool debounceAltTabInput = false;
        private DateTime altTabStartedTime;
        // Constants
        private const int ALT_TAB_DEBOUNCE_MS = 300; // debounce time in milliseconds
        private void SendWinTab(bool silent = false)
        {
            MakeSelfNonTopmost(); // <-- HINZUGEFÜGT
            if (!silent)
            {
                SendOverlayNotification("Shortcut: Task View");
            }
            keybd_event(0x5B, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private async Task SendWinTabForHandoffAsync()
        {
            MakeSelfNonTopmost();
            keybd_event(0x5B, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            await Task.Delay(120);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }



        #endregion alt tab
        #region performance overlay shortcut AHK
        private void TriggerPerformanceOverlay()
        {
            // User notification
            SendOverlayNotification("Toggle Performance Overlay");

            try
            {
                // Simulate Alt + R
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero); // Alt down
                keybd_event(VK_R, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);    // R down

                keybd_event(VK_R, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);      // R up
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);   // Alt up

                Console.WriteLine("Performance overlay shortcut (Alt+R) sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending performance overlay shortcut: {ex.Message}");
            }
        }

        #endregion performance overlay shortcut AHK
        #region audio management

        private void VolumeUp()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                float currentVol = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Min(1.0f, currentVol + 0.05f);

                int newVolPercent = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                SendOverlayNotification($"Volume Up: {newVolPercent}%");
            }
            catch (Exception ex) { Debug.WriteLine($"[Shortcut Error] Volume Up failed: {ex.Message}"); }
        }

        private void VolumeDown()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                float currentVol = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Max(0.0f, currentVol - 0.05f);

                int newVolPercent = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                SendOverlayNotification($"Volume Down: {newVolPercent}%");
            }
            catch (Exception ex) { Debug.WriteLine($"[Shortcut Error] Volume Down failed: {ex.Message}"); }
        }

        private void KillCurrentProcess()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0 || pid == Process.GetCurrentProcess().Id) return;

                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName;

                // Verhindert das Schließen von kritischen Systemprozessen oder GCM selbst
                if (name.ToLower() == "explorer" || name.ToLower() == "gcmloader") return;

                proc.Kill(true); // true schließt auch Child-Prozesse
                SendOverlayNotification($"Terminated: {name}");
                Debug.WriteLine($"[Shortcut] Process {name} killed by user shortcut.");

                // UI Liste aktualisieren, falls GCM gerade sichtbar ist
                _ = RefreshAppListAsync();
            }
            catch (Exception ex) { Debug.WriteLine($"[Shortcut Error] Kill Process failed: {ex.Message}"); }
        }

        private bool _isMasterVolumeFocused = false;

        public static void SwitchToNextAudioDevice()
        {
            try
            {
               
                var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                string currentName = defaultDevice.FriendlyName;

                // Get all active playback devices and exclude Steam-related devices
                List<MMDevice> devices = enumerator
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .Where(d => !d.FriendlyName.ToLower().Contains("steam streaming"))
                    .ToList();

                List<string> deviceNames = devices.Select(d => d.FriendlyName).ToList();

                int currentIndex = deviceNames.FindIndex(name => name.Equals(currentName, StringComparison.OrdinalIgnoreCase));
                int nextIndex = (currentIndex + 1) % deviceNames.Count;
                string rawDeviceName = deviceNames[nextIndex];
                string cleanedDeviceName = rawDeviceName.Split('(')[0].Trim();
                NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{cleanedDeviceName}\"");
                Console.WriteLine($"Switched to audio device: {cleanedDeviceName}");
                //usernotification
                SendOverlayNotification("Switched to:" + cleanedDeviceName);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error switching audio device: {ex.Message}");
            }
        }
        #endregion audio management
        #region shortcut overlay

        #endregion shortcut overlay
        #region shortcut xbox bar
        public static void xboxbar()
        {
            // Definiere die virtuellen Tastencodes
            const byte VK_LWIN = 0x5B; // Linke Windows-Taste
            const byte G_KEY = 0x47;   // 'G'-Taste

            // Simuliere den Tastendruck
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero); // Win-Taste herunterdrücken
            keybd_event(G_KEY, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);   // G-Taste herunterdrücken
            keybd_event(G_KEY, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);     // G-Taste loslassen
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);   // Win-Taste loslassen
        }
        #endregion xbox bar
        #region backtowin
        [DllImport("user32.dll")]
        private static extern void LockWorkStation();

        private const byte VK_LWIN = 0x5B;
        private const byte D_KEY = 0x44;

        public static void MinimizeAllViaShortcut()
        {
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            keybd_event(D_KEY, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            keybd_event(D_KEY, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        private void Triggerbacktowin()
        {
            MakeSelfNonTopmost(); 
            SendOverlayNotification("Shortcut: Back to Windows");
            BackToWindows();
        }
        #endregion backtowin
        #region gamebarkeyboard

        #endregion gamebarkeyboard
        #endregion shortcuts








        /// <summary>
        /// Handles all controller-as-a-mouse logic, including held clicks and right-stick scrolling.
        /// </summary>

        private void HandleMouseControl(State state, int index)
        {
            var gp = state.Gamepad;
            var btns = (GamepadButtonFlags)gp.Buttons;
            const int deadzone = 5000;

            // 1. MAUSZEIGER (Linker Stick)
            float moveX = (Math.Abs((float)gp.LeftThumbX) > deadzone) ? (gp.LeftThumbX / 32767f) * 35f : 0;
            float moveY = (Math.Abs((float)gp.LeftThumbY) > deadzone) ? (gp.LeftThumbY / 32767f) * 35f : 0;

            if (moveX != 0 || moveY != 0)
            {
                GetCursorPos(out POINT p);
                SetCursorPos(p.X + (int)moveX, p.Y - (int)moveY);
            }

            // 2. SCROLLEN (Rechter Stick)
            if (Math.Abs((float)gp.RightThumbY) > deadzone)
            {
                if ((DateTime.Now - _lastScrollTime).TotalMilliseconds > 40)
                {
                    int scrollAmount = gp.RightThumbY > 0 ? 120 : -120;
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)scrollAmount, UIntPtr.Zero);
                    _lastScrollTime = DateTime.Now;
                }
            }

            // 3. MAUSKLICKS (A = Links, X = Rechts)

            // LINKER KLICK (A) - Direkte Statusabfrage für Drag & Drop
            bool isAPressed = (btns & GamepadButtonFlags.A) != 0;
            bool wasAPressed = (_lastButtonStates[index] & GamepadButtonFlags.A) != 0;

            if (isAPressed && !wasAPressed)
            {
                // A wurde gerade eben gedrückt
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            }
            else if (!isAPressed && wasAPressed)
            {
                // A wurde gerade eben losgelassen
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }

            // RECHTER KLICK (X) - Einfacher Klick
            bool isXPressed = (btns & GamepadButtonFlags.X) != 0;
            bool wasXPressed = (_lastButtonStates[index] & GamepadButtonFlags.X) != 0;

            if (isXPressed && !wasXPressed)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            }

            // Y = Tastatur (nur beim Drücken)
            bool isYPressed = (btns & GamepadButtonFlags.Y) != 0;
            bool wasYPressed = (_lastButtonStates[index] & GamepadButtonFlags.Y) != 0;
            if (isYPressed && !wasYPressed) ToggleTouchKeyboard();

            // WICHTIG: Den State erst ganz am Ende speichern!
            _lastButtonStates[index] = btns;
        }
        /// <summary>
        /// Shows the modern Windows 11 Touch Keyboard by starting its process.
        /// This method is safe to call even if the keyboard is already open.
        /// </summary>
        /// <summary>
        /// Hides (kills) the modern Windows Touch Keyboard process. This remains the same.
        /// </summary>
        private void HideOnScreenKeyboard()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("TabTip"))
                {
                    proc.Kill();
                }
                Debug.WriteLine("[GCM] Closed modern Touch Keyboard process.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Failed to close modern Touch Keyboard: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the modern keyboard using a robust multi-step approach.
        /// </summary>


        // Add this new class to your project
        public static class KeyboardRedirector
        {
            // The registry key that allows us to intercept an executable launch.
            private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\osk.exe";
            private const string DebuggerValueName = "Debugger";

            /// <summary>
            /// Redirects the osk.exe shortcut (Ctrl+Win+O) to the modern touch keyboard.
            /// NOTE: This requires administrator privileges to write to HKEY_LOCAL_MACHINE.
            /// </summary>
            public static void EnableRedirect()
            {
                try
                {
                    // The path to the modern keyboard executable.
                    string targetPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                        @"microsoft shared\ink\TabTip.exe");

                    if (!File.Exists(targetPath))
                    {
                        Debug.WriteLine("[KeyboardRedirect] TabTip.exe not found. Cannot create redirect.");
                        return;
                    }

                    // Create the registry key if it doesn't exist.
                    using (RegistryKey key = Registry.LocalMachine.CreateSubKey(KeyPath))
                    {
                        // Set the "Debugger" value to our target path. This is the Windows mechanism for redirection.
                        key.SetValue(DebuggerValueName, targetPath, RegistryValueKind.String);
                    }
                    Debug.WriteLine("[KeyboardRedirect] osk.exe is now redirected to TabTip.exe.");
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("[KeyboardRedirect] ERROR: Administrator privileges are required to set up the keyboard redirect.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[KeyboardRedirect] An unexpected error occurred: {ex.Message}");
                }
            }

            /// <summary>
            /// Removes the redirect, restoring the default behavior of osk.exe.
            /// NOTE: This requires administrator privileges.
            /// </summary>
            public static void DisableRedirect()
            {
                try
                {
                    // Check if the key exists before trying to delete it.
                    using (RegistryKey parentKey = Registry.LocalMachine.OpenSubKey(Path.GetDirectoryName(KeyPath), true))
                    {
                        if (parentKey?.OpenSubKey(Path.GetFileName(KeyPath)) != null)
                        {
                            parentKey.DeleteSubKey(Path.GetFileName(KeyPath));
                            Debug.WriteLine("[KeyboardRedirect] osk.exe redirect has been removed.");
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("[KeyboardRedirect] ERROR: Administrator privileges are required to remove the keyboard redirect.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[KeyboardRedirect] An unexpected error occurred while removing the redirect: {ex.Message}");
                }
            }
        }


        /// <summary>
        /// The most reliable method to toggle the modern touch keyboard.
        /// It uses the same undocumented COM interface as the AHK script.
        /// </summary>
        /// <summary>
     
        private async void ToggleTouchKeyboard()
        {
            try
            {
                // Step 1: Ensure the TabTip process is running.
                bool isKeyboardRunning = Process.GetProcessesByName("TabTip").Any();

                if (!isKeyboardRunning)
                {
                    Debug.WriteLine("[Keyboard] TabTip.exe is not running. Starting it now...");
                    string keyboardPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                        @"microsoft shared\ink\TabTip.exe");

                    if (File.Exists(keyboardPath))
                    {
                        var psi = new ProcessStartInfo(keyboardPath) { UseShellExecute = true };
                        Process.Start(psi);

                        // CRITICAL: Wait for the process to initialize before trying to send a command to it.
                        await Task.Delay(500);
                    }
                    else
                    {
                        Debug.WriteLine("[Keyboard] ERROR: TabTip.exe could not be found.");
                        return; // Exit if we can't continue.
                    }
                }

                // Step 2: Now that the process is guaranteed to be running, call the COM toggle.
                // This part of the code is now reached every single time you press 'Y'.
                Debug.WriteLine("[Keyboard] Process is running. Using COM to toggle visibility...");
                var uiHostNoLaunch = (ITipInvocation)new UIHostNoLaunch();
                uiHostNoLaunch.Toggle(GetDesktopWindow());
                Marshal.ReleaseComObject(uiHostNoLaunch);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Keyboard] An error occurred while toggling the keyboard: {ex.Message}");
            }
        }

        // Make sure you have this P/Invoke declaration in your class if you don't already.
        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();


        /// <summary>
        /// Shows the redirected keyboard by simulating the Ctrl+Win+O shortcut.
        /// </summary>
        /// 
        // Add these with your other virtual-key constants like VK_MENU, VK_TAB, etc.
        // The virtual key code for the Control key.
        private const byte VK_CONTROL = 0x11;
        // The virtual key code for the 'O' key.
        private const byte O_KEY = 0x47;
        private void SetupGamepad()
        {
            // Wir erhöhen auf 10, dann haben wir massig Platz für alle
            _lastButtonStates = new GamepadButtonFlags[10];
            _lastShortcutButtons = new GamepadButtonFlags[10];
            _nextAllowedInputTime = new DateTime[10];
            _isStickCentered = new bool[10];

            Task.Factory.StartNew(() => XboxInputLoop(), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => PlayStationInputLoop(), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => PlayStationEdgeInputLoop(), TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(() => SteamDeckPuckInputLoop(), TaskCreationOptions.LongRunning);
        }


        private DateTime[] _mouseModeTimer = new DateTime[10]; // Timer für jeden Controller (0-3 Xbox, 4 PS, etc.)
        private bool[] _mouseModeTriggered = new bool[10];     // Merker, ob bereits ausgelöst wurde
        private bool _mouseToggleLocked = false; // Verhindert, dass der Maus-Modus "flattert"
        // Updates the UI labels to show Xbox or PlayStation icons/text
        
        
        private bool _debugMsgShown = false;



        private uint[] _lastXboxPacketNumbers = new uint[4];
        private Controller[] _xboxControllers = new Controller[4];
        private DateTime _comboStartTime = DateTime.MinValue;
        private bool _comboIsActive = false;
        // --- XINPUT SECRET GUIDE BUTTON ACCESS ---

        [StructLayout(LayoutKind.Explicit)]
        struct XInputGamepadSecret
        {
            [FieldOffset(0)] public ushort wButtons; // Hier versteckt sich der Guide Button (0x0400)
            [FieldOffset(2)] public byte bLeftTrigger;
            [FieldOffset(3)] public byte bRightTrigger;
            [FieldOffset(4)] public short sThumbLX;
            [FieldOffset(6)] public short sThumbLY;
            [FieldOffset(8)] public short sThumbRX;
            [FieldOffset(10)] public short sThumbRY;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct XInputStateSecret
        {
            [FieldOffset(0)] public uint dwPacketNumber;
            [FieldOffset(4)] public XInputGamepadSecret Gamepad;
        }

        // EntryPoint #100 ist der undokumentierte Zugang zum Guide-Button in xinput1_4.dll
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern int XInputGetStateSecret(int dwUserIndex, out XInputStateSecret pState);

        private const int XINPUT_GUIDE_BUTTON = 0x0400; // Das Bit für die Xbox-Taste




        private async Task XboxInputLoop()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            const int menuDeadzone = 18000;

            // Konstante für den Guide Button Code
            const int GUIDE_BIT = 0x0400;

            while (!_isExiting)
            {
                for (int i = 0; i < 4; i++)
                {
                    // 1. Controller Initialisieren
                    if (_xboxControllers[i] == null) _xboxControllers[i] = new Controller((UserIndex)i);

                    if (_xboxControllers[i].IsConnected)
                    {
                        try
                        {
                            // A. Standard Tasten lesen (A, B, X, Y...)
                            var state = _xboxControllers[i].GetState();
                            var gp = state.Gamepad;
                            GamepadButtonFlags btns = (GamepadButtonFlags)gp.Buttons;

                            // B. Guide Button lesen (Secret Methode) und reinmischen
                            XInputStateSecret stateSecret;
                            if (XInputGetStateSecret(i, out stateSecret) == 0)
                            {
                                bool isGuideDown = (stateSecret.Gamepad.wButtons & GUIDE_BIT) != 0;

                                if (isGuideDown)
                                {
                                    // Wir fügen den Guide-Button zur normalen Tastenliste hinzu!
                                    // Damit denkt dein Programm, "Guide" sei ein ganz normaler Knopf.
                                    btns |= (GamepadButtonFlags)GUIDE_BIT;
                                }
                            }

                            // C. Shortcuts verarbeiten (Jetzt inkl. Guide Button!)
                            HandleShortcuts(btns, i);


                            // --- Ab hier dein normaler UI/Maus Code (unverändert) ---

                            // Maus Modus Toggle (Start + Back)
                            bool comboPressed = (btns & GamepadButtonFlags.DPadDown) != 0 &&
                                                (btns & GamepadButtonFlags.RightThumb) != 0;

                            if (comboPressed)
                            {
                                if (_mouseModeTimer[i] == DateTime.MinValue)
                                {
                                    _mouseModeTimer[i] = DateTime.Now;
                                    _mouseModeTriggered[i] = false;
                                }
                                else if (!_mouseModeTriggered[i] && (DateTime.Now - _mouseModeTimer[i]).TotalSeconds >= 2.0)
                                {
                                    _isMouseModeActive = !_isMouseModeActive;
                                    if (!_isMouseModeActive) ParkMouseCursor();
                                    else
                                    {
                                        while (ShowCursor(true) < 0) ;
                                        _isCursorVisible = true;
                                        SetCursorPos(GetScreenWidth() / 2, GetScreenHeight() / 2);
                                    }
                                    _ = TriggerVibration(i, 0.5f, 300);
                                    SendOverlayNotification(_isMouseModeActive ? "Mouse Mode: ON" : "Mouse Mode: OFF");
                                    _mouseModeTriggered[i] = true;
                                }
                            }
                            else
                            {
                                _mouseModeTimer[i] = DateTime.MinValue;
                                _mouseModeTriggered[i] = false;
                            }

                            // Sticks
                            int xDir = 0;
                            if (gp.LeftThumbX < -menuDeadzone) xDir = -1;
                            else if (gp.LeftThumbX > menuDeadzone) xDir = 1;

                            int yDir = 0;
                            if (gp.LeftThumbY < -menuDeadzone) yDir = -1;
                            else if (gp.LeftThumbY > menuDeadzone) yDir = 1;

                            if (_isMouseModeActive)
                            {
                                if (!_isCursorVisible)
                                {
                                    while (ShowCursor(true) < 0) ;
                                    _isCursorVisible = true;
                                }
                                HandleMouseControl(state, i);
                            }
                            else
                            {
                                if (btns != GamepadButtonFlags.None || xDir != 0 || yDir != 0)
                                {
                                    ParkMouseCursor();
                                }

                                if (IsWindowInForeground())
                                {
                                    var newPresses = btns & ~_lastButtonStates[i];
                                    if (newPresses != GamepadButtonFlags.None)
                                    {
                                        DispatcherQueue.TryEnqueue(() => HandleGamepadInput(newPresses, false, false, false, false, i));
                                    }

                                    bool isStickMoving = (xDir != 0 || yDir != 0);
                                    if (isStickMoving)
                                    {
                                        if (DateTime.Now > _nextAllowedInputTime[i])
                                        {
                                            DispatcherQueue.TryEnqueue(() => HandleGamepadInput(GamepadButtonFlags.None, xDir == -1, xDir == 1, yDir == 1, yDir == -1, i));
                                            _nextAllowedInputTime[i] = _isStickCentered[i] ? DateTime.Now.AddMilliseconds(400) : DateTime.Now.AddMilliseconds(150);
                                            _isStickCentered[i] = false;
                                        }
                                    }
                                    else
                                    {
                                        _nextAllowedInputTime[i] = DateTime.MinValue;
                                        _isStickCentered[i] = true;
                                    }
                                    _lastButtonStates[i] = btns;
                                }
                            }
                        }
                        catch { _xboxControllers[i] = null; }
                    }
                }

                Thread.Sleep(8);
            }
        }

        private async Task SteamDeckPuckInputLoop()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            const short menuDeadzone = 10000;

            while (!_isExiting)
            {
                if (_steamDeckPuckStream == null)
                {
                    TryConnectSteamDeckPuck();

                    if (_steamDeckPuckStream == null)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }

                try
                {
                    int bytesRead = _steamDeckPuckStream.Read(_steamDeckPuckInputBuffer);
                    if (bytesRead <= 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_steamDeckPuckInputBuffer[0] != 0x45 || bytesRead < 18)
                    {
                        Thread.Yield();
                        continue;
                    }

                    uint rawButtons = BitConverter.ToUInt32(_steamDeckPuckInputBuffer, 2);
                    GamepadButtonFlags buttons = MapSteamDeckPuckButtons(rawButtons);

                    short leftThumbX = BitConverter.ToInt16(_steamDeckPuckInputBuffer, 10);
                    short leftThumbY = BitConverter.ToInt16(_steamDeckPuckInputBuffer, 12);

                    HandleShortcuts(buttons, SteamDeckPuckControllerIndex);

                    int xDir = 0;
                    if (leftThumbX < -menuDeadzone) xDir = -1;
                    else if (leftThumbX > menuDeadzone) xDir = 1;

                    int yDir = 0;
                    if (leftThumbY < -menuDeadzone) yDir = -1;
                    else if (leftThumbY > menuDeadzone) yDir = 1;

                    if (buttons != GamepadButtonFlags.None || xDir != 0 || yDir != 0)
                    {
                        ApplyActiveControllerType(ControllerType.SteamController);
                        ParkMouseCursor();
                    }

                    if (IsWindowInForeground())
                    {
                        DispatcherQueue.TryEnqueue(() => ProcessSteamDeckPuckSmoothNavigation(buttons, xDir, yDir));
                    }

                    Thread.Yield();
                }
                catch (System.TimeoutException)
                {
                    Thread.Yield();
                }
                catch
                {
                    _steamDeckPuckStream?.Dispose();
                    _steamDeckPuckStream = null;
                    _steamDeckPuckDevice = null;
                    _lastSteamDeckPuckButtonState = GamepadButtonFlags.None;
                    _mouseModeTimer[SteamDeckPuckControllerIndex] = DateTime.MinValue;
                    _mouseModeTriggered[SteamDeckPuckControllerIndex] = false;
                    _steamDeckPuckNextAllowedInputTime = DateTime.MinValue;
                    _steamDeckPuckStickCentered = true;
                }
            }
        }

        private void ProcessSteamDeckPuckSmoothNavigation(GamepadButtonFlags buttons, int xDir, int yDir)
        {
            if (!_isMouseModeActive && (buttons != GamepadButtonFlags.None || xDir != 0 || yDir != 0))
            {
                ParkMouseCursor();
            }

            var newPresses = buttons & ~_lastSteamDeckPuckButtonState;
            if (newPresses != GamepadButtonFlags.None)
            {
                HandleGamepadInput(newPresses, false, false, false, false, SteamDeckPuckControllerIndex);
            }

            if (xDir != 0 || yDir != 0)
            {
                if (DateTime.Now > _steamDeckPuckNextAllowedInputTime)
                {
                    HandleGamepadInput(GamepadButtonFlags.None, xDir == -1, xDir == 1, yDir == 1, yDir == -1, SteamDeckPuckControllerIndex);
                    _steamDeckPuckNextAllowedInputTime = _steamDeckPuckStickCentered
                        ? DateTime.Now.AddMilliseconds(400)
                        : DateTime.Now.AddMilliseconds(150);
                    _steamDeckPuckStickCentered = false;
                }
            }
            else
            {
                _steamDeckPuckNextAllowedInputTime = DateTime.MinValue;
                _steamDeckPuckStickCentered = true;
            }

            _lastSteamDeckPuckButtonState = buttons;
        }

        private void TryConnectSteamDeckPuck()
        {
            try
            {
                var candidates = DeviceList.Local
                    .GetHidDevices(SteamDeckPuckVendorId, SteamDeckPuckProductId)
                    .Where(device => device.MaxInputReportLength >= 54)
                    .OrderByDescending(device => ContainsIgnoreCase(device.DevicePath, "mi_02"))
                    .ThenByDescending(device => ContainsIgnoreCase(device.DevicePath, "col03"))
                    .ThenByDescending(device => device.MaxInputReportLength)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    if (!candidate.TryOpen(out var stream))
                    {
                        continue;
                    }

                    try
                    {
                        stream.ReadTimeout = 50;

                        bool isSteamDeckStateStream = false;
                        byte[] probeBuffer = new byte[Math.Max(64, candidate.MaxInputReportLength)];
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            int probeBytes = stream.Read(probeBuffer);
                            if (probeBytes > 0 && probeBuffer[0] == 0x45)
                            {
                                isSteamDeckStateStream = true;
                                break;
                            }
                        }

                        if (!isSteamDeckStateStream)
                        {
                            stream.Dispose();
                            continue;
                        }

                        _steamDeckPuckStream = stream;
                        _steamDeckPuckDevice = candidate;
                        Debug.WriteLine($"[SteamDeck] Puck connected: {candidate.ProductName} ({candidate.DevicePath})");
                        return;
                    }
                    catch
                    {
                        stream.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamDeck] Failed to connect puck input: {ex.Message}");
            }
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static GamepadButtonFlags MapSteamDeckPuckButtons(uint rawButtons)
        {
            GamepadButtonFlags mapped = GamepadButtonFlags.None;

            if ((rawButtons & SteamDeckButtonA) != 0) mapped |= GamepadButtonFlags.A;
            if ((rawButtons & SteamDeckButtonB) != 0) mapped |= GamepadButtonFlags.B;
            if ((rawButtons & SteamDeckButtonX) != 0) mapped |= GamepadButtonFlags.X;
            if ((rawButtons & SteamDeckButtonY) != 0) mapped |= GamepadButtonFlags.Y;
            if ((rawButtons & SteamDeckButtonView) != 0) mapped |= GamepadButtonFlags.Start;
            if ((rawButtons & SteamDeckButtonMenu) != 0) mapped |= GamepadButtonFlags.Back;
            if ((rawButtons & SteamDeckButtonSteam) != 0) mapped |= (GamepadButtonFlags)XINPUT_GUIDE_BUTTON;
            if ((rawButtons & SteamDeckButtonLeftShoulder) != 0) mapped |= GamepadButtonFlags.LeftShoulder;
            if ((rawButtons & SteamDeckButtonRightShoulder) != 0) mapped |= GamepadButtonFlags.RightShoulder;
            if ((rawButtons & SteamDeckButtonLeftStick) != 0) mapped |= GamepadButtonFlags.LeftThumb;
            if ((rawButtons & SteamDeckButtonRightStick) != 0) mapped |= GamepadButtonFlags.RightThumb;
            if ((rawButtons & SteamDeckButtonDPadUp) != 0) mapped |= GamepadButtonFlags.DPadUp;
            if ((rawButtons & SteamDeckButtonDPadRight) != 0) mapped |= GamepadButtonFlags.DPadRight;
            if ((rawButtons & SteamDeckButtonDPadDown) != 0) mapped |= GamepadButtonFlags.DPadDown;
            if ((rawButtons & SteamDeckButtonDPadLeft) != 0) mapped |= GamepadButtonFlags.DPadLeft;

            return mapped;
        }

        private List<HidStream> _activePs5Streams = new List<HidStream>();
        private GamepadButtonFlags[] _multiPs5LastButtons = new GamepadButtonFlags[10];
        private GamepadButtonFlags[] _lastPs5Buttons = new GamepadButtonFlags[10];
        private DateTime _nextScanTime = DateTime.MinValue;
        private DateTime[] _nextStickMove = new DateTime[10]; // Timer für jeden Controller


        #region dualsense edge

        #region psdualsense

        // --- DualSense Edge (NEU & SEPARAT) ---
        private HidDevice _edgeDevice;        // Eigener Slot!
        private HidStream _edgeStream;        // Eigener Stream!
        private byte[] _edgeInputBuffer = new byte[64]; // Eigener Buffer!
        // Seperater Status nur für den Edge, damit er den normalen Controller nicht stört
        private GamepadButtonFlags _lastEdgeButtonState = GamepadButtonFlags.None;
        private DateTime _edgeNextAllowedInputTime = DateTime.MinValue;
        private bool _edgeStickCentered = true;

        #endregion

        #region dedizierter DualSense Edge Loop
        // Separater Speicher für Edge Shortcuts und Maus-Klicks
        private GamepadButtonFlags _lastEdgeShortcutButtons = GamepadButtonFlags.None;
        private GamepadButtonFlags _lastEdgeMouseButtons = GamepadButtonFlags.None;
        private async Task PlayStationEdgeInputLoop()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            while (!_isExiting)
            {
                // Verbindung (wie gehabt, nur kürzer gefasst für Übersicht)
                if (_edgeStream == null)
                {
                    /* Dein bestehender Verbindungscode hier... oder wir lassen ihn laufen, falls er schon geht */
                    // Um sicherzugehen, hier der komplette Verbindungsblock:
                    try
                    {
                        var loader = DeviceList.Local;
                        var edgeDev = loader.GetHidDevices(0x054C).FirstOrDefault(d => d.ProductID == 0x0DF2);
                        if (edgeDev != null && edgeDev.TryOpen(out var tempStream))
                        {
                            _edgeStream = tempStream;
                            _edgeStream.ReadTimeout = 1;
                            _edgeDevice = edgeDev;
                            Debug.WriteLine("[Edge] Verbunden.");
                        }
                    }
                    catch { _edgeStream = null; }
                    if (_edgeStream == null) { Thread.Sleep(1000); continue; }
                }

                try
                {
                    int bytesRead = _edgeStream.Read(_edgeInputBuffer);
                    if (bytesRead > 0)
                    {
                        byte reportId = _edgeInputBuffer[0];
                        int start = (reportId == 0x31) ? 2 : 1;
                        if (reportId != 0x31 && reportId != 0x01) { Thread.Yield(); continue; }

                        GamepadButtonFlags edgeButtons = GamepadButtonFlags.None;
                        int btnOffset = (reportId == 0x31) ? 7 : 4;

                        // --- Parsing (Identisch zum Standard, plus Paddles) ---
                        // D-Pad
                        byte b1 = _edgeInputBuffer[start + btnOffset];
                        byte dpadVal = (byte)(b1 & 0x0F);
                        if (dpadVal == 0 || dpadVal == 1 || dpadVal == 7) edgeButtons |= GamepadButtonFlags.DPadUp;
                        if (dpadVal == 1 || dpadVal == 2 || dpadVal == 3) edgeButtons |= GamepadButtonFlags.DPadRight;
                        if (dpadVal == 3 || dpadVal == 4 || dpadVal == 5) edgeButtons |= GamepadButtonFlags.DPadDown;
                        if (dpadVal == 5 || dpadVal == 6 || dpadVal == 7) edgeButtons |= GamepadButtonFlags.DPadLeft;

                        // Buttons
                        if ((b1 & 0x10) != 0) edgeButtons |= GamepadButtonFlags.X;
                        if ((b1 & 0x20) != 0) edgeButtons |= GamepadButtonFlags.A;
                        if ((b1 & 0x40) != 0) edgeButtons |= GamepadButtonFlags.B;
                        if ((b1 & 0x80) != 0) edgeButtons |= GamepadButtonFlags.Y;

                        byte b2 = _edgeInputBuffer[start + btnOffset + 1];
                        if ((b2 & 0x01) != 0) edgeButtons |= GamepadButtonFlags.LeftShoulder;
                        if ((b2 & 0x02) != 0) edgeButtons |= GamepadButtonFlags.RightShoulder;
                        if ((b2 & 0x10) != 0) edgeButtons |= GamepadButtonFlags.Back;
                        if ((b2 & 0x20) != 0) edgeButtons |= GamepadButtonFlags.Start; // WICHTIG für Menü!
                        if ((b2 & 0x40) != 0) edgeButtons |= GamepadButtonFlags.LeftThumb;
                        if ((b2 & 0x80) != 0) edgeButtons |= GamepadButtonFlags.RightThumb;

                        // Paddles (Rücktasten)
                        byte bEdge = _edgeInputBuffer[start + btnOffset + 4];
                        if ((bEdge & 0x04) != 0) edgeButtons |= GamepadButtonFlags.A;
                        if ((bEdge & 0x08) != 0) edgeButtons |= GamepadButtonFlags.B;

                        // Sticks
                        float lx = (_edgeInputBuffer[start + 0] - 128) / 128f;
                        float ly = (_edgeInputBuffer[start + 1] - 128) / 128f;
                        int xDir = lx < -0.3f ? -1 : (lx > 0.3f ? 1 : 0);
                        int yDir = ly < -0.3f ? 1 : (ly > 0.3f ? -1 : 0);

                        // --- Verarbeitung ---
                        HandleUniversalToggle(edgeButtons);
                        HandleEdgeShortcuts(edgeButtons);

                        if (_isMouseModeActive)
                        {
                            HandleMouseControl(new State
                            {
                                Gamepad = new SharpDX.XInput.Gamepad
                                {
                                    Buttons = (SharpDX.XInput.GamepadButtonFlags)edgeButtons,
                                    LeftThumbX = (short)(lx * 32767),
                                    LeftThumbY = (short)(-ly * 32767)
                                }
                            }, 5); // Index 5 für Edge Maus
                        }
                        else if (IsWindowInForeground())
                        {
                            DispatcherQueue.TryEnqueue(() => ProcessEdgeSmoothNavigation(edgeButtons, xDir, yDir));
                        }
                        Thread.Yield();
                    }
                    else { Thread.Sleep(1); }
                }
                catch { _edgeStream?.Dispose(); _edgeStream = null; }
            }
        }

        // --- Die vereinfachte Methode für Edge ---
        private void ProcessEdgeSmoothNavigation(GamepadButtonFlags buttons, int xDir, int yDir)
        {
            // 1. Buttons (One-Shot)
            var newPresses = buttons & ~_lastEdgeButtonState;
            if (newPresses != GamepadButtonFlags.None)
            {
                // ALLES an HandleGamepadInput weiterleiten (Controller Index 4 für PS/Edge UI Logik)
                HandleGamepadInput(newPresses, false, false, false, false, 4);
            }

            // 2. Stick Navigation
            if (xDir == 0 && yDir == 0)
            {
                _edgeNextAllowedInputTime = DateTime.MinValue;
                _edgeStickCentered = true;
            }
            else
            {
                if (DateTime.Now > _edgeNextAllowedInputTime)
                {
                    HandleGamepadInput(GamepadButtonFlags.None, xDir == -1, xDir == 1, yDir == 1, yDir == -1, 4);

                    if (_edgeStickCentered)
                    {
                        _edgeNextAllowedInputTime = DateTime.Now.AddMilliseconds(400);
                        _edgeStickCentered = false;
                    }
                    else
                    {
                        _edgeNextAllowedInputTime = DateTime.Now.AddMilliseconds(150);
                    }
                }
            }
            _lastEdgeButtonState = buttons;
        }
        
        private void HandleEdgeShortcuts(GamepadButtonFlags currentButtons)
        {
            // Wir nutzen Index 6 für den Edge Controller, um Konflikte mit Xbox (0-3),
            // Standard-PS5 (4) und dem Edge-Mausmodus (5) im State-Tracking zu vermeiden.
            int controllerIndex = 6;

            // Berechne, welche Tasten in diesem Frame NEU gedrückt wurden
            var newPresses = currentButtons & ~_lastEdgeShortcutButtons;
            _lastEdgeShortcutButtons = currentButtons;

            foreach (var shortcut in _runtimeShortcuts)
            {
                // 1. Prüfen, ob die geforderten Tasten (Bitmaske) gedrückt gehalten werden
                bool requirementsMet = (currentButtons & shortcut.RequiredButtons) == shortcut.RequiredButtons;

                if (requirementsMet)
                {
                    // FALL A: Zeitbasiertes Auslösen (HoldDuration > 0)
                    if (shortcut.HoldDurationSeconds > 0)
                    {
                        // Timer starten, falls noch nicht aktiv
                        if (shortcut.HoldStartTimes[controllerIndex] == DateTime.MaxValue)
                        {
                            shortcut.HoldStartTimes[controllerIndex] = DateTime.Now;
                        }

                        // Prüfen, ob die Zeit abgelaufen ist
                        var elapsedSeconds = (DateTime.Now - shortcut.HoldStartTimes[controllerIndex]).TotalSeconds;

                        if (elapsedSeconds >= shortcut.HoldDurationSeconds && !shortcut.HasTriggered[controllerIndex])
                        {
                            // Aktion ausführen
                            ExecuteShortcutAction(shortcut.FunctionName, controllerIndex, true);
                            shortcut.HasTriggered[controllerIndex] = true; // Sperren, damit es nicht mehrfach feuert
                        }
                    }
                    // FALL B: Sofortiges Auslösen (HoldDuration == 0)
                    else
                    {
                        // Bei Sofort-Aktionen muss mindestens eine der nötigen Tasten "neu" sein,
                        // damit wir nicht 60-mal pro Sekunde feuern.
                        if ((newPresses & shortcut.RequiredButtons) != GamepadButtonFlags.None)
                        {
                            ExecuteShortcutAction(shortcut.FunctionName, controllerIndex, false);
                        }
                    }
                }
                else
                {
                    // Reset, wenn Tasten losgelassen wurden (nur für Hold-Shortcuts relevant)
                    if (shortcut.HoldDurationSeconds > 0)
                    {
                        shortcut.HoldStartTimes[controllerIndex] = DateTime.MaxValue;
                        shortcut.HasTriggered[controllerIndex] = false;
                    }
                }
            }
        }
        #endregion


        #endregion dualsense edge

        #region standard ps controller dualsense
        // Füge diese Variable oben in deine Klasse zu den anderen Variablen hinzu:
        private DateTime _lastPs5StickDispatch = DateTime.MinValue;
        private GamepadButtonFlags _bgLastPs5Buttons = GamepadButtonFlags.None; // Eigener Speicher für den BG-Thread

        private async Task PlayStationInputLoop()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            while (!_isExiting)
            {
                // Verbindung herstellen (Original Logik)
                if (_ps5Stream == null)
                {
                    try
                    {
                        var loader = DeviceList.Local;
                        var ps5Dev = loader.GetHidDevices(0x054C).FirstOrDefault(d => d.ProductID == 0x0CE6 || d.ProductID == 0x0DF2);

                        if (ps5Dev != null && ps5Dev.TryOpen(out var tempStream))
                        {
                            _ps5Stream = tempStream;
                            _ps5Stream.ReadTimeout = 50;
                            _ps5Device = ps5Dev;
                            Debug.WriteLine($"[PS5] Verbunden: {ps5Dev.ProductName}");
                        }
                    }
                    catch { _ps5Stream = null; }

                    if (_ps5Stream == null)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }

                // Daten lesen
                try
                {
                    int bytesRead = _ps5Stream.Read(_hidInputBuffer);
                    if (bytesRead > 0)
                    {
                        byte reportId = _hidInputBuffer[0];
                        int startIdx = (reportId == 0x01) ? 1 : (reportId == 0x31 ? 2 : -1);

                        if (startIdx == -1) { Thread.Yield(); continue; }

                        // --- Buttons Parsen ---
                        GamepadButtonFlags psBtn = GamepadButtonFlags.None;
                        int btnOffset = (reportId == 0x31) ? 7 : 4;
                        byte b1 = _hidInputBuffer[startIdx + btnOffset];
                        byte dpad = (byte)(b1 & 0x0F);

                        if (dpad == 0 || dpad == 1 || dpad == 7) psBtn |= GamepadButtonFlags.DPadUp;
                        if (dpad == 1 || dpad == 2 || dpad == 3) psBtn |= GamepadButtonFlags.DPadRight;
                        if (dpad == 3 || dpad == 4 || dpad == 5) psBtn |= GamepadButtonFlags.DPadDown;
                        if (dpad == 5 || dpad == 6 || dpad == 7) psBtn |= GamepadButtonFlags.DPadLeft;

                        if ((b1 & 0x10) != 0) psBtn |= GamepadButtonFlags.X;
                        if ((b1 & 0x20) != 0) psBtn |= GamepadButtonFlags.A;
                        if ((b1 & 0x40) != 0) psBtn |= GamepadButtonFlags.B;
                        if ((b1 & 0x80) != 0) psBtn |= GamepadButtonFlags.Y;

                        byte b2 = _hidInputBuffer[startIdx + btnOffset + 1];
                        if ((b2 & 0x01) != 0) psBtn |= GamepadButtonFlags.LeftShoulder;
                        if ((b2 & 0x02) != 0) psBtn |= GamepadButtonFlags.RightShoulder;
                        if ((b2 & 0x10) != 0) psBtn |= GamepadButtonFlags.Back;
                        if ((b2 & 0x20) != 0) psBtn |= GamepadButtonFlags.Start;
                        if ((b2 & 0x40) != 0) psBtn |= GamepadButtonFlags.LeftThumb;
                        if ((b2 & 0x80) != 0) psBtn |= GamepadButtonFlags.RightThumb;

                        // --- Sticks ---
                        float lx = (_hidInputBuffer[startIdx + 0] - 128) / 128f;
                        float ly = (_hidInputBuffer[startIdx + 1] - 128) / 128f;
                        int xDir = lx < -0.3f ? -1 : (lx > 0.3f ? 1 : 0);
                        int yDir = ly < -0.3f ? 1 : (ly > 0.3f ? -1 : 0);

                        // --- Original Verarbeitung ---
                        HandleUniversalToggle(psBtn);
                        HandleShortcuts(psBtn, 4);

                        if (_isMouseModeActive)
                        {
                            // Maus-Modus direkt aufrufen (kein UI Thread nötig -> schnell)
                            HandleMouseControl(new State
                            {
                                Gamepad = new SharpDX.XInput.Gamepad
                                {
                                    Buttons = (SharpDX.XInput.GamepadButtonFlags)psBtn,
                                    LeftThumbX = (short)(lx * 32767),
                                    LeftThumbY = (short)(-ly * 32767)
                                }
                            }, 4);
                        }
                        else if (IsWindowInForeground())
                        {
                            // UI Navigation
                            DispatcherQueue.TryEnqueue(() => ProcessPs5SmoothNavigation(psBtn, xDir, yDir));
                        }

                        // Originales Yield für maximale Frequenz
                        Thread.Yield();
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch
                {
                    _ps5Stream?.Dispose();
                    _ps5Stream = null;
                }
            }
        }

        private void ProcessPs5SmoothNavigation(GamepadButtonFlags buttons, int xDir, int yDir)
        {
            // Originale Logik: Maus parken bei Eingabe
            if (!_isMouseModeActive && (buttons != GamepadButtonFlags.None || xDir != 0 || yDir != 0))
            {
                ParkMouseCursor();
            }

            // Buttons (One-Shot)
            var newPresses = buttons & ~_lastPs5ButtonState;
            if (newPresses != GamepadButtonFlags.None)
            {
                // Hier rufen wir das neue zentrale HandleGamepadInput auf, 
                // das jetzt das Menü kennt. Da es im Dispatcher läuft, ist es sicher.
                HandleGamepadInput(newPresses, false, false, false, false, 4);
            }

            // Stick Navigation (Originale Logik mit deinen Timern)
            if (xDir != 0 || yDir != 0)
            {
                if (DateTime.Now > _ps5NextAllowedInputTime)
                {
                    HandleGamepadInput(GamepadButtonFlags.None, xDir == -1, xDir == 1, yDir == 1, yDir == -1, 4);

                    _ps5NextAllowedInputTime = _ps5StickCentered ? DateTime.Now.AddMilliseconds(400) : DateTime.Now.AddMilliseconds(150);
                    _ps5StickCentered = false;
                }
            }
            else
            {
                _ps5NextAllowedInputTime = DateTime.MinValue;
                _ps5StickCentered = true;
            }

            _lastPs5ButtonState = buttons;
        }

        

        

        // Hilfsmethode für das Raster-Scrolling im App-Launcher
        private void HandleAppLauncherNavigation(int xDir, int yDir)
        {
            if (AppGridView.Items.Count == 0) return;

            int columns = 4; // Standardfall
            try
            {
                if (AppGridView.ActualWidth > 0 && AppGridView.ContainerFromIndex(0) is GridViewItem container)
                    columns = Math.Max(1, (int)Math.Floor(AppGridView.ActualWidth / container.ActualWidth));
            }
            catch { }

            int currentIndex = AppGridView.SelectedIndex;
            int newIndex = currentIndex;

            if (yDir == 1) newIndex = Math.Max(0, currentIndex - columns); // Hoch
            else if (yDir == -1) newIndex = Math.Min(AppGridView.Items.Count - 1, currentIndex + columns); // Runter
            else if (xDir == 1) newIndex = (currentIndex + 1) % AppGridView.Items.Count; // Rechts
            else if (xDir == -1) newIndex = (currentIndex - 1 + AppGridView.Items.Count) % AppGridView.Items.Count; // Links

            if (newIndex != currentIndex)
            {
                AppGridView.SelectedIndex = newIndex;
                AppGridView.ScrollIntoView(AppGridView.SelectedItem);
                PlayNavigationSound();
            }
        }
        private void HandleUniversalToggle(GamepadButtonFlags buttons)
        {
            bool isComboPressed = buttons.HasFlag(GamepadButtonFlags.Back) && buttons.HasFlag(GamepadButtonFlags.Start);

            if (isComboPressed)
            {
                if (!_mouseToggleLocked)
                {
                    _isMouseModeActive = !_isMouseModeActive;
                    _mouseToggleLocked = true;

                    if (!_isMouseModeActive)
                    {
                        // Sofort beim Ausschalten parken
                        ParkMouseCursor();
                    }
                    else
                    {
                        // Beim Einschalten: Maus in die Mitte des Bildschirms holen und zeigen
                        while (ShowCursor(true) < 0) ;
                        _isCursorVisible = true;
                        SetCursorPos(GetScreenWidth() / 2, GetScreenHeight() / 2);
                    }

                    ControllerLogger.Log("System", $"Mouse Mode changed to: {_isMouseModeActive}");
                    SendOverlayNotification(_isMouseModeActive ? "Mouse Mode: ON" : "Mouse Mode: OFF");
                }
            }
            else
            {
                _mouseToggleLocked = false;
            }
        }

        #endregion standard ps controller dualsense

        private DateTime[] _nextAllowedInputTime = new DateTime[5] { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };
        private bool[] _isStickCentered = new bool[5] { true, true, true, true, true };
        private void ProcessUINavigation(GamepadButtonFlags buttons, int xDir, int yDir, int index)
        {
            if (!IsWindowInForeground()) return;

            // Buttons A, B, X, Y sofort
            var newPresses = buttons & ~_lastButtonStates[index];
            if (newPresses != GamepadButtonFlags.None)
            {
                DispatcherQueue.TryEnqueue(() => HandleGamepadInput(newPresses, false, false, false, false, index));
            }

            // Stick Smooth Logik
            bool isCurrentlyMoving = (xDir != 0 || yDir != 0);
            if (!isCurrentlyMoving)
            {
                _nextAllowedInputTime[index] = DateTime.MinValue;
                _isStickCentered[index] = true;
            }
            else
            {
                if (DateTime.Now > _nextAllowedInputTime[index])
                {
                    DispatcherQueue.TryEnqueue(() => {
                        HandleGamepadInput(GamepadButtonFlags.None, xDir == -1, xDir == 1, yDir == 1, yDir == -1, index);
                    });

                    _nextAllowedInputTime[index] = _isStickCentered[index] ? DateTime.Now.AddMilliseconds(400) : DateTime.Now.AddMilliseconds(150);
                    _isStickCentered[index] = false;
                }
            }
            _lastButtonStates[index] = buttons;
        }





        // Fix for recognizing pressed buttons via bitwise and allowing held + second key combo
        private static readonly Dictionary<string, GamepadButtonFlags> _buttonMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = GamepadButtonFlags.A,
            ["B"] = GamepadButtonFlags.B,
            ["X"] = GamepadButtonFlags.X,
            ["Y"] = GamepadButtonFlags.Y,
            ["Start"] = GamepadButtonFlags.Start,
            ["Back"] = GamepadButtonFlags.Back,
            ["DPadUp"] = GamepadButtonFlags.DPadUp,
            ["DPadDown"] = GamepadButtonFlags.DPadDown,
            ["DPadLeft"] = GamepadButtonFlags.DPadLeft,
            ["DPadRight"] = GamepadButtonFlags.DPadRight,
            ["LeftShoulder"] = GamepadButtonFlags.LeftShoulder,
            ["RightShoulder"] = GamepadButtonFlags.RightShoulder,
            ["LeftThumb"] = GamepadButtonFlags.LeftThumb,  // Hattest du vergessen, sicherheitshalber dazu
            ["RightThumb"] = GamepadButtonFlags.RightThumb, // Hattest du vergessen, sicherheitshalber dazu

            // --- NEU: Der Guide Button ---
            // 0x0400 ist der interne Hex-Code für die Xbox-Taste
            ["Guide"] = (GamepadButtonFlags)0x0400,
            ["Xbox"] = (GamepadButtonFlags)0x0400
        };

        private bool IsButtonPressed(GamepadButtonFlags state, string key)
        {
            key = key?.Trim();
            if (string.IsNullOrEmpty(key)) return false;

            if (!_buttonMap.TryGetValue(key, out var button))
            {
                try { button = (GamepadButtonFlags)Enum.Parse(typeof(GamepadButtonFlags), key, true); }
                catch { return false; }
            }
            
            return (state & button) != 0;
        }

       
        private HashSet<(string, string)> _triggeredCombos = new();
        private Dictionary<string, DateTime> _heldButtonTimestamps = new();
        private readonly TimeSpan _comboTimeout = TimeSpan.FromMilliseconds(1000);


        private void LogGamepadInit(string message)
        {
            try
            {
                string logDir = Path.Combine(AppContext.BaseDirectory, "log");
                Directory.CreateDirectory(logDir); // erstellt Ordner, falls nicht da

                string logFile = Path.Combine(logDir, "gamepadinitial.txt");

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.AppendAllText(logFile, $"[{timestamp}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging darf niemals crashen
            }
        }



        private void LoadShortcutsFromSettings()
        {
            _runtimeShortcuts.Clear();
            _shortcutActions.Clear();

            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "settings.toml");

            if (!File.Exists(settingsFilePath))
            {
                LogGamepadInit("settings.toml not found. No shortcuts loaded.");
                return;
            }

            try
            {
                var settingsModel = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();

                // 1. Helper to parse string keys to GamepadButtonFlags
                GamepadButtonFlags ParseKey(string key)
                {
                    if (string.IsNullOrWhiteSpace(key) || key.Equals("None", StringComparison.OrdinalIgnoreCase))
                        return GamepadButtonFlags.None;

                    if (_buttonMap.TryGetValue(key, out var flag))
                        return flag;

                    // Fallback try enum parse
                    if (Enum.TryParse(key, true, out GamepadButtonFlags result))
                        return result;

                    return GamepadButtonFlags.None;
                }

                void AddShortcutToList(string k1, string k2, string func, double duration, bool enabled)
                {
                    if (!enabled || string.IsNullOrEmpty(func)) return;

                    var flags1 = ParseKey(k1);
                    var flags2 = ParseKey(k2);

                    // If Key1 is invalid, skip. Key2 can be None.
                    if (flags1 == GamepadButtonFlags.None) return;

                    // --- NEU: Wir bauen den Display-Text exakt in der Reihenfolge von k1 und k2 ---
                    var textParts = new List<string>();

                    string niceK1 = GetNiceKeyName(k1);
                    if (niceK1 != null) textParts.Add(niceK1);

                    string niceK2 = GetNiceKeyName(k2);
                    if (niceK2 != null) textParts.Add(niceK2);

                    string finalDisplayText = string.Join(" + ", textParts);
                    // -------------------------------------------------------------------------------

                    var newShortcut = new RuntimeShortcut
                    {
                        RequiredButtons = flags1 | flags2, // Combine bitmasks for the logic
                        FunctionName = func,
                        HoldDurationSeconds = duration,
                        DisplayText = finalDisplayText     // <-- Speichert den Text für unser Overlay!
                    };

                    _runtimeShortcuts.Add(newShortcut);
                    LogGamepadInit($"[OK] Added Shortcut: {k1} + {k2} ({duration}s) -> {func}");
                }

                // 2. Load Custom Shortcuts
                if (settingsModel.TryGetValue("shortcuts", out var shortcutsObj) && shortcutsObj is TomlTableArray shortcutsArray)
                {
                    foreach (TomlTable table in shortcutsArray)
                    {
                        string k1 = table["key1"]?.ToString();
                        string k2 = table["key2"]?.ToString(); // Can be "None" or null
                        string func = table["function"]?.ToString();
                        bool enabled = Convert.ToBoolean(table["enabled"]);

                        // Load Hold Duration (default to 0.0 if missing)
                        double duration = 0.0;
                        if (table.ContainsKey("hold_duration"))
                        {
                            duration = Convert.ToDouble(table["hold_duration"]);
                        }

                        AddShortcutToList(k1, k2, func, duration, enabled);
                    }
                }

                // 3. Load Seamless Switch (Legacy support via same system)
                if (settingsModel.TryGetValue("winmode_shortcut", out var winObj) && winObj is TomlTable winTable)
                {
                    string k1 = winTable["key1"]?.ToString();
                    string k2 = winTable["key2"]?.ToString();
                    bool enabled = Convert.ToBoolean(winTable["enabled"]);

                    // Seamless switch is usually instant (0.0s hold)
                    AddShortcutToList(k1, k2, "winmodechange", 0.0, enabled);
                }

                // 4. Map Functions to Actions
                // 4. Map Functions to Actions
                _shortcutActions["taskmanager"] = BringTaskManagerToFrontAndFocus;
                _shortcutActions["switch tab"] = () => SendWinTab();
                _shortcutActions["audio switch"] = SwitchToNextAudioDevice;
                _shortcutActions["performance overlay"] = TriggerPerformanceOverlay;
                _shortcutActions["xbox bar"] = xboxbar;
                _shortcutActions["xbox keyboard"] = ToggleTouchKeyboard;
                _shortcutActions["volume up"] = VolumeUp;
                _shortcutActions["shortcut overlay"] = ToggleGlobalShortcutOverlay;//VolumeUp;
                _shortcutActions["volume down"] = VolumeDown;
                _shortcutActions["kill process"] = KillCurrentProcess;
            }
            catch (Exception ex)
            {
                LogGamepadInit($"[ERROR] Failed to load shortcuts: {ex.Message}");
            }
        }


        // ########## ANFANG DES KOMPLETTEN CODE-BLOCKS ##########

        #region Gamepad Navigation

        private void EnsureTouchKeyboardServiceIsRunning()
        {
            const string serviceName = "TabletInputService";
            try
            {
                // Get a controller for the service.
                using (ServiceController service = new ServiceController(serviceName))
                {
                    // Check if the service is stopped.
                    if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        Debug.WriteLine($"[GCM] Touch Keyboard service ('{serviceName}') is stopped. Starting it now...");

                        // Start the service and wait for it to be running.
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));

                        Debug.WriteLine($"[GCM] Service '{serviceName}' started successfully.");
                    }
                    else
                    {
                        Debug.WriteLine($"[GCM] Touch Keyboard service ('{serviceName}') is already running.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log an error if the service cannot be found or started (e.g., due to permissions).
                Debug.WriteLine($"[GCM] ERROR: Could not start the touch keyboard service ('{serviceName}'). On-screen keyboard may not be available. Details: {ex.Message}");
            }
        }



        // --- State variables to remember previous focus (for navigation) ---
        private FocusArea _previousFocusArea = FocusArea.Cards; // Used to track which area to return to from TopButtons
        private int _previousLauncherAreaIndex = -1;
        private int _previousCardIndex = -1;
        private int _previousTopButtonIndex = -1;


        /// <summary>
        /// Processes gamepad input against the list of loaded runtime shortcuts.
        /// Handles both instant combinations and time-based hold triggers.
        /// </summary>
        /// <param name="currentButtons">The current state of all buttons.</param>
        /// <param name="index">Controller index (0-3 for Xbox, 4 for PS).</param>
        private void HandleShortcuts(GamepadButtonFlags currentButtons, int index)
        {
            // Safety check for array bounds
            if (index < 0 || index >= 10) return;

            // Calculate which buttons were *just* pressed in this frame
            // (Used for instant triggers so they don't fire repeatedly)
            var newPresses = currentButtons & ~_lastShortcutButtons[index];
            _lastShortcutButtons[index] = currentButtons;

            foreach (var shortcut in _runtimeShortcuts)
            {
                if (index == SteamDeckPuckControllerIndex &&
                    !string.Equals(shortcut.FunctionName, "taskmanager", StringComparison.OrdinalIgnoreCase))
                {
                    if (shortcut.HoldDurationSeconds > 0)
                    {
                        shortcut.HoldStartTimes[index] = DateTime.MaxValue;
                        shortcut.HasTriggered[index] = false;
                    }

                    continue;
                }

                // 1. Check if the required buttons for this shortcut are currently held down
                bool requirementsMet = (currentButtons & shortcut.RequiredButtons) == shortcut.RequiredButtons;

                if (requirementsMet)
                {
                    // CASE A: Time-based Hold (Duration > 0)
                    if (shortcut.HoldDurationSeconds > 0)
                    {
                        // Start timer if not already started
                        if (shortcut.HoldStartTimes[index] == DateTime.MaxValue)
                        {
                            shortcut.HoldStartTimes[index] = DateTime.Now;
                        }

                        // Check if enough time has passed
                        var elapsedSeconds = (DateTime.Now - shortcut.HoldStartTimes[index]).TotalSeconds;

                        if (elapsedSeconds >= shortcut.HoldDurationSeconds && !shortcut.HasTriggered[index])
                        {
                            // FIRE ACTION
                            ExecuteShortcutAction(shortcut.FunctionName, index, true);
                            shortcut.HasTriggered[index] = true; // Prevent re-firing while holding
                        }
                    }
                    // CASE B: Instant Trigger (Duration == 0)
                    else
                    {
                        // For instant triggers, at least one of the required buttons must be "newly pressed"
                        // This prevents the action from firing every 10ms while you hold the buttons.
                        // We check if (NewPresses overlaps with RequiredButtons)
                        if ((newPresses & shortcut.RequiredButtons) != GamepadButtonFlags.None)
                        {
                            ExecuteShortcutAction(shortcut.FunctionName, index, false);
                        }
                    }
                }
                else
                {
                    // Reset state if buttons are released
                    if (shortcut.HoldDurationSeconds > 0)
                    {
                        shortcut.HoldStartTimes[index] = DateTime.MaxValue;
                        shortcut.HasTriggered[index] = false;
                    }
                }
            }
        }

        // Helper to execute the action safely on the UI thread
        private void ExecuteShortcutAction(string functionName, int controllerIndex, bool isHoldAction)
        {
            if (_shortcutActions.TryGetValue(functionName, out var action))
            {
                string triggerType = isHoldAction ? "Held" : "Pressed";
                Debug.WriteLine($"[Shortcut] {triggerType} {functionName} on Controller {controllerIndex}");

                DispatcherQueue.TryEnqueue(() =>
                {
                    action.Invoke();

                    // Optional: Custom text for Hold actions
                    string msg = isHoldAction ? $"{functionName} (Held)" : $"Shortcut: {functionName}";
                    SendOverlayNotification(msg);

                    // Vibration feedback
                    _ = TriggerVibration(controllerIndex, 0.5f, 250);
                });
            }
        }

        /// <summary>
        /// Die zentrale Methode, die alle Gamepad-Eingaben für Shortcuts und UI-Navigation verarbeitet.
        /// </summary>
        /// 
        private bool _ignoreNextInputFrame = false;
        /// <summary>
        /// Startet den Computer neu.
        /// </summary>
        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Neustart-Button geklickt...");
            // Der Parameter /r steht für "restart"
            Process.Start("shutdown", "/r /t 0");
        }


        /// Die zentrale Methode zur Verarbeitung der Gamepad-Eingaben für die UI-Navigation.

        private void HandleGamepadInput(GamepadButtonFlags newPresses, bool stickMovedLeft, bool stickMovedRight, bool stickMovedUp, bool stickMovedDown, int controllerIndex)
        {
            if (_globalShortcutOverlay != null || _isOverlayClosing)
            {
                // Only trigger close if we aren't already closing
                if ((newPresses & GamepadButtonFlags.B) != 0 && !_isOverlayClosing)
                {
                    DispatcherQueue.TryEnqueue(() => CloseGlobalShortcutOverlay());
                }

                return; // Block other inputs to the list underneath
            }

            // Sicherheitscheck: Nur verarbeiten, wenn das Fenster im Fokus ist
            if (!IsWindowInForeground()) return;

            ApplyActiveControllerType(ResolveControllerTypeForIndex(controllerIndex));

            bool navigated = false;

            switch (_currentFocusArea)
            {

                case FocusArea.StartupVideo:
                    // Wenn B, A oder Start gedrückt wird -> Video abbrechen!
                    if ((newPresses & GamepadButtonFlags.B) != 0 ||
                        (newPresses & GamepadButtonFlags.A) != 0 ||
                        (newPresses & GamepadButtonFlags.Start) != 0)
                    {
                        Debug.WriteLine("[StartupVideo] Video durch User übersprungen!");
                        DispatcherQueue.TryEnqueue(() => TransitionToMainUI());
                    }
                    return;

                case FocusArea.WindowsReturnConfirm:
                    if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        ConfirmReturnToWindows();
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        CloseWindowsReturnConfirm();
                        PlaydeactivationSound();
                    }
                    return;

                // --- 1. TopButtons (Leiste oben) ---
                case FocusArea.TopButtons:
                    if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                    {
                        // Wechsel zurück zum vorherigen Hauptbereich
                        _currentFocusArea = _previousFocusArea;
                        _previousTopButtonIndex = _selectedTopButtonIndex;

                        if (_currentFocusArea == FocusArea.Cards)
                            _selectedCardIndex = _previousCardIndex != -1 ? _previousCardIndex : 0;
                        else if (_currentFocusArea == FocusArea.Launcher)
                            _selectedLauncherAreaIndex = _previousLauncherAreaIndex != -1 ? _previousLauncherAreaIndex : 0;

                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) && _topButtons.Any())
                    {
                        _selectedTopButtonIndex = (_selectedTopButtonIndex + 1) % _topButtons.Count;
                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft) && _topButtons.Any())
                    {
                        _selectedTopButtonIndex = (_selectedTopButtonIndex - 1 + _topButtons.Count) % _topButtons.Count;
                        navigated = true;
                    }

                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        if (ClickSelectedTopButton())
                        {
                            PlayActivationSound();
                        }
                    }
                    else if ((newPresses & GamepadButtonFlags.X) != 0)
                    {
                        // Öffnet das integrierte Audio-Menü
                        if (TryToggleAudioFlyout("audio-top-shortcut"))
                        {
                            PlayActivationSound();
                        }
                    }
                    break;

                // --- 2. Cards (Laufende Apps) ---
                case FocusArea.Cards:
                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        _previousFocusArea = FocusArea.Cards;
                        _previousCardIndex = _selectedCardIndex;
                        _currentFocusArea = FocusArea.TopButtons;
                        _selectedTopButtonIndex = _previousTopButtonIndex != -1 ? _previousTopButtonIndex : 0;
                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) && ProgramCardPanel.Children.Any())
                    {
                        if (_selectedCardIndex < ProgramCardPanel.Children.Count - 1)
                        {
                            _selectedCardIndex++;
                            navigated = true;
                        }
                    }
                    else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft)
                    {
                        if (ProgramCardPanel.Children.Any() && _selectedCardIndex > 0)
                        {
                            _selectedCardIndex--;
                            navigated = true;
                        }
                        else
                        {
                            // Am Anfang der Liste -> Wechsel zum Mini-Launcher links
                            _previousFocusArea = FocusArea.Cards;
                            _currentFocusArea = FocusArea.Launcher;
                            _selectedLauncherAreaIndex = _launcherAreaButtons.Any() ? _launcherAreaButtons.Count - 1 : 0;
                            navigated = true;
                        }
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        TriggerCardAction(_selectedCardIndex, true);
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        TriggerCardAction(_selectedCardIndex, false);
                        PlaydeactivationSound();
                    }
                    // --- START TASTE (GAME OPTIONS) ---
                    else if ((newPresses & GamepadButtonFlags.Start) != 0)
                    {
                        if (_cardCache != null && _cardCache.Count > _selectedCardIndex && _selectedCardIndex >= 0)
                        {
                            var entry = _cardCache[_selectedCardIndex];

                            // NEU: Öffne das Options-Menü statt direkt die Bildsuche
                            DispatcherQueue.TryEnqueue(() => OpenGameOptions(entry));

                            PlayActivationSound();
                        }
                    }
                    break;

                case FocusArea.QuickLaunchers:
                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        // Von den Quadraten noch weiter nach oben -> Top-Leiste
                        _previousFocusArea = FocusArea.QuickLaunchers;
                        _currentFocusArea = FocusArea.TopButtons;
                        _selectedTopButtonIndex = _previousTopButtonIndex != -1 ? _previousTopButtonIndex : 0;
                        navigated = true;
                    }
                    else if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                    {
                        // Nach unten -> Zurück zur großen Main-Launcher Karte
                        _currentFocusArea = FocusArea.Launcher;
                        _selectedLauncherAreaIndex = 0;
                        navigated = true;
                    }
                    else if ((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight)
                    {
                        _selectedQuickLauncherIndex = Math.Min(_quickLauncherButtons.Count - 1, _selectedQuickLauncherIndex + 1);
                        navigated = true;
                    }
                    else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft)
                    {
                        _selectedQuickLauncherIndex = Math.Max(0, _selectedQuickLauncherIndex - 1);
                        navigated = true;
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        // Starten!
                        string targetLauncher = _quickLauncherButtons[_selectedQuickLauncherIndex].Tag.ToString();
                        if (TryAcquireUiActionGate("quick-launcher-activate", 900))
                        {
                            SwitchToSpecificLauncher(targetLauncher);
                            PlayActivationSound();
                        }
                    }
                    break;

                // --- 3. Launcher (Mini-Launcher links) ---
                case FocusArea.Launcher:
                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        if (_selectedLauncherAreaIndex == 0)
                        {
                            if (_quickLauncherButtons != null && _quickLauncherButtons.Any() && QuickLauncherPanel.Visibility == Visibility.Visible)
                            {
                                // Wir sind auf der Hauptkarte -> Gehe zu den Quick-Launch Quadraten
                                _currentFocusArea = FocusArea.QuickLaunchers;
                                _selectedQuickLauncherIndex = 0;
                            }
                            else
                            {
                                _previousFocusArea = FocusArea.Launcher;
                                _previousLauncherAreaIndex = _selectedLauncherAreaIndex;
                                _currentFocusArea = FocusArea.TopButtons;
                                _selectedTopButtonIndex = _previousTopButtonIndex != -1 ? _previousTopButtonIndex : 0;
                            }
                        }
                        else
                        {
                            // Wir sind auf DISCORD oder APP 1-5 -> Gehe direkt zur Top-Leiste
                            _previousFocusArea = FocusArea.Launcher;
                            _previousLauncherAreaIndex = _selectedLauncherAreaIndex;
                            _currentFocusArea = FocusArea.TopButtons;
                            _selectedTopButtonIndex = _previousTopButtonIndex != -1 ? _previousTopButtonIndex : 0;
                        }
                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) && _launcherAreaButtons.Any())
                    {
                        if (_selectedLauncherAreaIndex == _launcherAreaButtons.Count - 1)
                        {
                            // Ende der Liste -> Wechsel zu den Cards
                            _currentFocusArea = FocusArea.Cards;
                            _selectedCardIndex = 0;
                            navigated = true;
                        }
                        else
                        {
                            _selectedLauncherAreaIndex++;
                            navigated = true;
                        }
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft) && _selectedLauncherAreaIndex > 0)
                    {
                        _selectedLauncherAreaIndex--;
                        navigated = true;
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        if (_selectedLauncherAreaIndex < _launcherAreaButtons.Count && TryActivateSelectedLauncherItem())
                        {
                            PlayActivationSound();
                        }
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        if (SelectedLauncherItemRepresentsGame())
                        {
                            CloseFeaturedGame();
                            PlaydeactivationSound();
                        }
                    }
                    else if ((newPresses & GamepadButtonFlags.Start) != 0)
                    {
                        if (SelectedLauncherItemRepresentsGame())
                        {
                            var item = SelectedLauncherItemOrNull();
                            var card = SelectedLauncherCardOrNull();
                            DispatcherQueue.TryEnqueue(() => OpenGameOptions(CreateProgramCardEntryFromLauncherItem(item, card)));
                            PlayActivationSound();
                        }
                    }
                    break;

                // --- 4. AudioMenu (Audio-Geräte Flyout) ---
                case FocusArea.AudioMenu:
                    // --- GLOBAL AUDIO MENU ACTIONS (Work everywhere in this menu) ---

                    // RB/LB Tab Switch - Now outside the focus checks to be always accessible
                    if ((newPresses & GamepadButtonFlags.RightShoulder) != 0 && !_isAudioMixerMode)
                    {
                        ToggleAudioTab(true);
                        PlayNavigationSound();
                        return;
                    }
                    else if ((newPresses & GamepadButtonFlags.LeftShoulder) != 0 && _isAudioMixerMode)
                    {
                        ToggleAudioTab(false);
                        PlayNavigationSound();
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.Y) != 0)
                    {
                        _isMasterVolumeFocused = !_isMasterVolumeFocused;
                        UpdateAudioVisualFocus();
                        PlayNavigationSound();
                        return;
                    }

                    // B = Close Menu
                    if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        CloseAudioFlyout();
                        PlaydeactivationSound();
                        return;
                    }

                    // =========================================================
                    // MODUS 1: MASTER SLIDER FOCUSED
                    // =========================================================
                    if (_isMasterVolumeFocused)
                    {
                        if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight))
                        {
                            if (MasterVolumeSlider.Value < 100) MasterVolumeSlider.Value += 5;
                        }
                        else if (((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft))
                        {
                            if (MasterVolumeSlider.Value > 0) MasterVolumeSlider.Value -= 5;
                        }
                        else if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                        {
                            _isMasterVolumeFocused = false;
                            // Ensure indices are valid
                            if (!_isAudioMixerMode) _selectedAudioDeviceIndex = 0;
                            else _selectedMixerIndex = 0;

                            UpdateAudioVisualFocus();
                            PlayNavigationSound();
                            return;
                        }
                    }
                    // =========================================================
                    // MODUS 2: LIST SELECTION (Devices or Mixer)
                    // =========================================================
                    else
                    {
                        if (!_isAudioMixerMode) // --- DEVICES LIST ---
                        {
                            if (((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown) && _audioDeviceButtons.Any())
                            {
                                _selectedAudioDeviceIndex = (_selectedAudioDeviceIndex + 1) % _audioDeviceButtons.Count;
                                navigated = true;
                            }
                            else if (((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp) && _audioDeviceButtons.Any())
                            {
                                if (_selectedAudioDeviceIndex == 0)
                                {
                                    _isMasterVolumeFocused = true;
                                }
                                else
                                {
                                    _selectedAudioDeviceIndex--;
                                }
                                navigated = true;
                            }
                            else if ((newPresses & GamepadButtonFlags.A) != 0)
                            {
                                if (_audioDeviceButtons.Count > _selectedAudioDeviceIndex)
                                {
                                    if (TrySelectAudioDevice(_audioDeviceButtons[_selectedAudioDeviceIndex].Tag.ToString()))
                                    {
                                        PlayActivationSound();
                                    }
                                }
                            }
                        }
                        else // --- MIXER LIST ---
                        {
                            if (((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown) && _audioMixerRows.Any())
                            {
                                _selectedMixerIndex = (_selectedMixerIndex + 1) % _audioMixerRows.Count;
                                navigated = true;
                            }
                            else if (((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp) && _audioMixerRows.Any())
                            {
                                if (_selectedMixerIndex == 0)
                                {
                                    _isMasterVolumeFocused = true;
                                }
                                else
                                {
                                    _selectedMixerIndex--;
                                }
                                navigated = true;
                            }
                            else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft)
                                AdjustSessionVolume(_selectedMixerIndex, -0.05f);
                            else if ((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight)
                                AdjustSessionVolume(_selectedMixerIndex, 0.05f);
                        }
                    }

                    if (navigated)
                    {
                        UpdateAudioVisualFocus();
                        PlayNavigationSound();

                        // Auto-Scroll implementation for the lists
                        if (_isAudioMixerMode && _audioMixerRows.Count > _selectedMixerIndex)
                            ScrollToAudioItemAnimated(_audioMixerRows[_selectedMixerIndex], AudioMixerScrollViewer, MixerListStackPanel);
                        else if (!_isAudioMixerMode && _audioDeviceButtons.Count > _selectedAudioDeviceIndex)
                            ScrollToAudioItemAnimated(_audioDeviceButtons[_selectedAudioDeviceIndex], AudioDevicesScrollViewer, SimpleAudioList);
                        return;
                    }
                    break;

                // --- 5. PowerMenu (Shutdown/Restart) ---
                case FocusArea.PowerMenu:
                    if (((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown) && _powerMenuItems.Any())
                    {
                        _selectedPowerMenuItemIndex = (_selectedPowerMenuItemIndex + 1) % _powerMenuItems.Count;
                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp) && _powerMenuItems.Any())
                    {
                        _selectedPowerMenuItemIndex = (_selectedPowerMenuItemIndex - 1 + _powerMenuItems.Count) % _powerMenuItems.Count;
                        navigated = true;
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0 && _powerMenuItems.Count > _selectedPowerMenuItemIndex)
                    {
                        _powerMenuItems[_selectedPowerMenuItemIndex].Focus(FocusState.Programmatic);
                        // Trigger Click logic for the button
                        var btn = _powerMenuItems[_selectedPowerMenuItemIndex];
                        if (btn == ShutdownMenuItem) ShutdownMenuItem_Click(null, null);
                        else if (btn == RestartMenuItem) RestartMenuItem_Click(null, null);
                        else if (btn == SleepMenuItem) SleepMenuItem_Click(null, null);
                        else if (btn == LogOffMenuItem) LogOffMenuItem_Click(null, null);
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        PowerMenu.Visibility = Visibility.Collapsed;
                        _currentFocusArea = FocusArea.TopButtons;
                        navigated = true;
                        PlaydeactivationSound();
                    }
                    break;

                // --- 6. AppLauncher (Fullscreen Grid) ---
                // --- 6. AppLauncher (Fullscreen Grid) ---
                case FocusArea.AppLauncher:
                    // B = Schließen (wie bisher)
                    if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        ToggleAppLauncher_Click(null, null);
                        PlaydeactivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.X) != 0)
                    {
                        ToggleSelectedAppFavorite();
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.LeftShoulder) != 0)
                    {
                        CycleAppLauncherFilter(-1);
                        PlayNavigationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.RightShoulder) != 0 ||
                             (newPresses & GamepadButtonFlags.Y) != 0)
                    {
                        CycleAppLauncherFilter(1);
                        PlayNavigationSound();
                    }
                    // A = App starten
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        if (AppGridView.SelectedItem is AppInfo selectedApp)
                        {
                            LaunchApp(selectedApp);
                        }
                    }
                    // Navigation (D-Pad & Stick) (NEU)
                    else
                    {
                        int xDir = 0;
                        int yDir = 0;

                        // D-Pad Erfassung
                        if ((newPresses & GamepadButtonFlags.DPadUp) != 0) yDir = 1;
                        else if ((newPresses & GamepadButtonFlags.DPadDown) != 0) yDir = -1;
                        else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0) xDir = -1;
                        else if ((newPresses & GamepadButtonFlags.DPadRight) != 0) xDir = 1;

                        // Stick Erfassung (wird von der Input-Loop übergeben)
                        if (xDir == 0 && yDir == 0)
                        {
                            if (stickMovedUp) yDir = 1;
                            else if (stickMovedDown) yDir = -1;
                            else if (stickMovedLeft) xDir = -1;
                            else if (stickMovedRight) xDir = 1;
                        }

                        // Wenn eine Bewegung erkannt wurde, an die Hilfsmethode leiten
                        if (xDir != 0 || yDir != 0)
                        {
                            HandleAppLauncherNavigation(xDir, yDir);
                        }
                    }
                    break;

                case FocusArea.SettingsMenu:
                    if (TryHandleSettingsCaptureInput(newPresses, stickMovedLeft, stickMovedRight, stickMovedUp, stickMovedDown))
                    {
                        PlayActivationSound();
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        CloseSettingsOverlay();
                        PlaydeactivationSound();
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                    {
                        MoveSettingsSelection(1);
                        PlayNavigationSound();
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        MoveSettingsSelection(-1);
                        PlayNavigationSound();
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        ActivateSelectedSettingsRow();
                        PlayActivationSound();
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft)
                    {
                        if (FocusSettingsCategories())
                        {
                            PlayNavigationSound();
                        }
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight)
                    {
                        if (AdvanceSettingsSelection())
                        {
                            PlayNavigationSound();
                        }
                        return;
                    }

                    if ((newPresses & GamepadButtonFlags.X) != 0)
                    {
                        if (RewindSettingsSelection())
                        {
                            PlayNavigationSound();
                        }
                        return;
                    }
                    break;

                // --- 7. ImageSelection (Bildauswahl Overlay) ---
                case FocusArea.ImageSelection:
                    // Prüfen ob wir überhaupt Ergebnisse haben
                    if (ImageResultsGrid.Items.Count > 0)
                    {
                        // Spalten berechnen (Standardmäßig ca. 5 im GridView, kann variieren)
                        int columns = 5;

                        if ((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight)
                        {
                            _selectedImageGridIndex = Math.Min(ImageResultsGrid.Items.Count - 1, _selectedImageGridIndex + 1);
                        }
                        else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft)
                        {
                            _selectedImageGridIndex = Math.Max(0, _selectedImageGridIndex - 1);
                        }
                        else if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                        {
                            _selectedImageGridIndex = Math.Min(ImageResultsGrid.Items.Count - 1, _selectedImageGridIndex + columns);
                        }
                        else if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                        {
                            _selectedImageGridIndex = Math.Max(0, _selectedImageGridIndex - columns);
                        }

                        // A = Bild auswählen & laden
                        else if ((newPresses & GamepadButtonFlags.A) != 0)
                        {
                            if (_selectedImageGridIndex >= 0 && _selectedImageGridIndex < _currentImageSearchResults.Count)
                            {
                                string url = _currentImageSearchResults[_selectedImageGridIndex];
                                DownloadAndApplyImage(url);
                                PlayActivationSound();
                            }
                        }

                        // Visuelles Feedback (Scrollen und Selektieren)
                        ImageResultsGrid.SelectedIndex = _selectedImageGridIndex;
                        ImageResultsGrid.ScrollIntoView(ImageResultsGrid.SelectedItem);
                    }

                    // B = Abbrechen / Schließen
                    if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        CloseImageSelectorButton_Click(null, null);
                        PlaydeactivationSound();
                    }
                    break;
                case FocusArea.GameOptions:

                    // --- SZENARIO A: WIR SIND IN DER BILDERSUCHE ---
                    if (ArtworkSearchPanel.Visibility == Visibility.Visible)
                    {
                        // B = Zurück zum Hauptmenü
                        if ((newPresses & GamepadButtonFlags.B) != 0)
                        {
                            BtnBackToOptions_Click(null, null);
                            PlaydeactivationSound();
                            return;
                        }

                        // Navigation im Bilder-Raster (Grid)
                        if (ImageResultsGrid.Items.Count > 0)
                        {
                            int columns = 5; // Ungefähre Spaltenzahl

                            if ((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight)
                            {
                                _selectedImageGridIndex = Math.Min(ImageResultsGrid.Items.Count - 1, _selectedImageGridIndex + 1);
                                navigated = true;
                            }
                            else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft)
                            {
                                _selectedImageGridIndex = Math.Max(0, _selectedImageGridIndex - 1);
                                navigated = true;
                            }
                            else if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                            {
                                _selectedImageGridIndex = Math.Min(ImageResultsGrid.Items.Count - 1, _selectedImageGridIndex + columns);
                                navigated = true;
                            }
                            else if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                            {
                                _selectedImageGridIndex = Math.Max(0, _selectedImageGridIndex - columns);
                                navigated = true;
                            }

                            // A = Bild auswählen & laden
                            else if ((newPresses & GamepadButtonFlags.A) != 0)
                            {
                                if (_selectedImageGridIndex >= 0 && _selectedImageGridIndex < _currentImageSearchResults.Count)
                                {
                                    string url = _currentImageSearchResults[_selectedImageGridIndex];
                                    DownloadAndApplyImage(url);
                                    PlayActivationSound();
                                }
                            }

                            if (navigated)
                            {
                                ImageResultsGrid.SelectedIndex = _selectedImageGridIndex;
                                ImageResultsGrid.ScrollIntoView(ImageResultsGrid.SelectedItem);
                                PlayNavigationSound();
                                // Hier returnen wir, damit wir unten nicht UpdateVisualFocus für das Hauptmenü aufrufen
                                return;
                            }
                        }
                    }

                    // --- SZENARIO B: WIR SIND IM HAUPTMENÜ (Suspend / Artwork) ---
                    else
                    {
                        // B = Menü ganz schließen
                        if ((newPresses & GamepadButtonFlags.B) != 0)
                        {
                            CloseGameOptions();
                            PlaydeactivationSound();
                        }
                        // HOCH / RUNTER = Auswahl wechseln
                        else if (((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp))
                        {
                            _selectedGameOptionIndex = 0; // Hoch -> Suspend Button
                            UpdateGameOptionsFocus();
                            PlayNavigationSound();
                        }
                        else if (((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown))
                        {
                            _selectedGameOptionIndex = 1; // Runter -> Artwork Button
                            UpdateGameOptionsFocus();
                            PlayNavigationSound();
                        }
                        // A = Button klicken
                        else if ((newPresses & GamepadButtonFlags.A) != 0)
                        {
                            if (_selectedGameOptionIndex == 0) BtnSuspendGame_Click(null, null);
                            else BtnChangeArtwork_Click(null, null);

                            PlayActivationSound();
                        }
                    }
                    break;

                case FocusArea.GithubReleasePrompt:
                    if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        GithubReleaseOpenButton_Click(null, null);
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        GithubReleaseLaterButton_Click(null, null);
                        PlaydeactivationSound();
                    }
                    break;
            }

            if (navigated)
            {
                UpdateVisualFocus();
                PlayNavigationSound();
            }
        }

        private static bool IsPlayStationControllerIndex(int controllerIndex)
        {
            return controllerIndex is 4 or 5 or 6;
        }

        private GamepadButtonFlags[] _lastButtonStates = new GamepadButtonFlags[5];
        private DateTime[] _lastInputTimePerController = new DateTime[5];
        private int[] _lastStickXDirections = new int[4];
        private int[] _lastStickYDirections = new int[4];


        private Windows.Gaming.Input.Gamepad _rawXboxGamepad = null;
        private bool _lastGuideButtonPressed = false;

        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            if (PowerMenu.Visibility == Visibility.Visible)
            {
                PowerMenu.Visibility = Visibility.Collapsed;
                _currentFocusArea = FocusArea.TopButtons;
            }
            else
            {
                // Menü anzeigen
                PowerMenu.Visibility = Visibility.Visible;

                // Fokus auf das Sleep-Menü setzen
                _currentFocusArea = FocusArea.PowerMenu;
                _selectedPowerMenuItemIndex = 0; // Sleep ist Standard (Index 0)

                UpdateVisualFocus();
            }
        }

        private void PowerMenu_BackdropTapped(object sender, TappedRoutedEventArgs e)
        {
            PowerMenu.Visibility = Visibility.Collapsed;
            _currentFocusArea = FocusArea.TopButtons;
            UpdateVisualFocus();
        }

        /// <summary>
        /// Updates the UI performantly by only changing the old and new focused elements.
        /// </summary>
        private void UpdateGameOptionsFocus()
        {
            // 1. Reset Styles (Standard Background)
            BtnSuspendGame.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF
            BtnSuspendGame.BorderThickness = new Thickness(1);

            BtnChangeArtwork.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(51, 255, 255, 255));
            BtnChangeArtwork.BorderThickness = new Thickness(1);

            // 2. Highlight Selection (Akzentfarbe)
            var accentBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];

            if (_selectedGameOptionIndex == 0)
            {
                BtnSuspendGame.BorderBrush = accentBrush;
                BtnSuspendGame.BorderThickness = new Thickness(2);
                BtnSuspendGame.Focus(FocusState.Programmatic);
            }
            else
            {
                BtnChangeArtwork.BorderBrush = accentBrush;
                BtnChangeArtwork.BorderThickness = new Thickness(2);
                BtnChangeArtwork.Focus(FocusState.Programmatic);
            }
        }
        private void UpdateVisualFocus(bool isInitial = false)
        {
            UpdateLayoutForFocus();

            // --- RESET PHASE ---
            _launcherAreaButtons.ForEach(b => { AnimateScale(b, false); AnimateBorderColor(b, false); _quickLauncherButtons?.ForEach(b => { AnimateScale(b, false); AnimateBorderColor(b, false); }); });
            _topButtons.ForEach(b => {
                b.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                b.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                b.BorderThickness = new Thickness(0);
            });
            _powerMenuItems.ForEach(b => { b.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); });
            StyleWindowsReturnConfirmChoices();

            if (_currentFocusArea != FocusArea.Cards)
            {
                for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
                {
                    if (ProgramCardPanel.Children[i] is Border card) { AnimateScale(card, false); AnimateBorderColor(card, false); }
                }
            }

            foreach (var btn in _audioDeviceButtons)
            {
                // Korrektur: Nutze Windows.UI.Color oder direkt Microsoft.UI.Colors
                btn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                AnimateScale(btn, false);
            }

            // --- HIGHLIGHT PHASE ---
            switch (_currentFocusArea)
            {
                case FocusArea.Launcher:
                    AnimateInfoPanelFocus(false);
                    if (_launcherAreaButtons.Count > _selectedLauncherAreaIndex)
                    {
                        var selectedButton = _launcherAreaButtons[_selectedLauncherAreaIndex];
                        AnimateScale(selectedButton, true);
                        AnimateBorderColor(selectedButton, true);
                    }
                    break;

                case FocusArea.TopButtons:
                    AnimateInfoPanelFocus(true);
                    if (_topButtons.Count > _selectedTopButtonIndex)
                    {
                        var selectedButton = _topButtons[_selectedTopButtonIndex];
                        selectedButton.Background = new SolidColorBrush(GetThemeAccentColor(42));
                        selectedButton.BorderBrush = new SolidColorBrush(GetThemeAccentColor(210));
                        selectedButton.BorderThickness = new Thickness(2);
                    }
                    break;

                case FocusArea.Cards:
                    AnimateInfoPanelFocus(false);
                    HighlightSelectedCard();
                    return;

                case FocusArea.AudioMenu:
                    if (_audioDeviceButtons.Count > _selectedAudioDeviceIndex)
                    {
                        var activeBtn = _audioDeviceButtons[_selectedAudioDeviceIndex];
                        // Benutze hier Color.FromArgb aus Windows.UI
                        activeBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 255, 255, 255));
                        AnimateScale(activeBtn, true);
                    }
                    break;

                case FocusArea.PowerMenu:
                    if (_powerMenuItems.Count > _selectedPowerMenuItemIndex)
                    {
                        var selectedButton = _powerMenuItems[_selectedPowerMenuItemIndex];
                        selectedButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 255, 255, 255));
                    }
                    break;

                case FocusArea.QuickLaunchers:
                    AnimateInfoPanelFocus(false);
                    if (_quickLauncherButtons != null && _quickLauncherButtons.Count > _selectedQuickLauncherIndex)
                    {
                        var selectedQuick = _quickLauncherButtons[_selectedQuickLauncherIndex];
                        AnimateScale(selectedQuick, true);
                        AnimateBorderColor(selectedQuick, true);
                    }
                    break;

                case FocusArea.SettingsMenu:
                    UpdateSettingsVisualFocus();
                    break;

                case FocusArea.WindowsReturnConfirm:
                    StyleWindowsReturnConfirmChoices();
                    break;

                case FocusArea.GithubReleasePrompt:
                    StyleGithubReleasePromptChoices();
                    break;
            }

            UpdateBubbleReflectionRig();
            UpdateSelectionSurface();
        }

        #endregion

        // ########## ENDE DES KOMPLETTEN CODE-BLOCKS ##########


        /// <summary>
        /// Animiert die Skalierung eines UI-Elements performant.
        /// </summary>
        /// 



        private void AnimateScale(UIElement element, bool isSelected)
        {
            if (element is not Border border) return;

            if (border.RenderTransform is not CompositeTransform transform)
            {
                transform = new CompositeTransform();
                border.RenderTransform = transform;
                border.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }

            var duration = TimeSpan.FromMilliseconds(170);
            var sb = new Storyboard();
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            bool isProcessCard = border.Tag is CardTag;
            double scaleFactor = isProcessCard ? 1.012 : 1.026;
            double translateY = isProcessCard ? 0 : -4;

            // 1. SKALIERUNG
            var animX = new DoubleAnimation { To = isSelected ? scaleFactor : 1.0, Duration = duration, EasingFunction = easing };
            var animY = new DoubleAnimation { To = isSelected ? scaleFactor : 1.0, Duration = duration, EasingFunction = easing };
            var animTrans = new DoubleAnimation { To = isSelected ? translateY : 0, Duration = duration, EasingFunction = easing };

            Storyboard.SetTarget(animX, transform); Storyboard.SetTargetProperty(animX, "ScaleX");
            Storyboard.SetTarget(animY, transform); Storyboard.SetTargetProperty(animY, "ScaleY");
            Storyboard.SetTarget(animTrans, transform); Storyboard.SetTargetProperty(animTrans, "TranslateY");
            sb.Children.Add(animX); sb.Children.Add(animY); sb.Children.Add(animTrans);

            sb.Begin();
        }


        /// <summary>
        /// Führt die Klick-Aktion für den aktuell ausgewählten oberen Button aus.
        /// </summary>
        private bool ClickSelectedTopButton()
        {
            if (_topButtons.Count > _selectedTopButtonIndex)
            {
                var buttonToClick = _topButtons[_selectedTopButtonIndex];
                string gateKey = buttonToClick == VolumeButton
                    ? "top-volume-button"
                    : buttonToClick == SettingsButton
                        ? "top-settings-button"
                        : buttonToClick == AppLauncherButton
                            ? "top-app-launcher-button"
                            : buttonToClick == ShutdownButton
                                ? "top-shutdown-button"
                                : "top-exit-button";

                if (!TryAcquireUiActionGate(gateKey, buttonToClick == VolumeButton ? 280 : 220))
                {
                    return false;
                }

                if (buttonToClick == ExitGcmButton)
                {
                    ExitGcmButton_Click_1(null, null);
                    return true;
                }
                else if (buttonToClick == VolumeButton) // NEU: Reaktion auf A-Taste
                {
                    return TryToggleAudioFlyout("audio-top-button");
                }
                else if (buttonToClick == SettingsButton)
                {
                    SettingsButton_Click(null, null);
                    return true;
                }
                else if (buttonToClick == AppLauncherButton)
                {
                    ToggleAppLauncher_Click(null, null);
                    return true;
                }
                else if (buttonToClick == ShutdownButton)
                {
                    _currentFocusArea = FocusArea.PowerMenu;
                    _selectedPowerMenuItemIndex = 0;
                    PowerButton_Click(null, null);
                    UpdateVisualFocus();
                    return true;
                }
            }

            return false;
        }
        private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!IsWindowInForeground()) return;

            switch (e.Key)
            {
                case VirtualKey.Left:
                    if (_selectedCardIndex > 0)
                    {
                        _selectedCardIndex--;
                        HighlightSelectedCard();
                        PlayNavigationSound();
                    }
                    break;

                case VirtualKey.Right:
                    if (_selectedCardIndex < ProgramCardPanel.Children.Count - 1)
                    {
                        _selectedCardIndex++;
                        HighlightSelectedCard();
                        PlayNavigationSound();
                    }
                    break;

                case VirtualKey.Enter:
                    TriggerCardAction(_selectedCardIndex, true);
                    PlayActivationSound();
                    break;

                case VirtualKey.Escape:
                    TriggerCardAction(_selectedCardIndex, false);
                    PlaydeactivationSound();
                    break;
            }
        }
        private void RestoreUwpAppWindow(IntPtr hwnd)
        {
            IntPtr childWindow = IntPtr.Zero;

            // Suche nach sichtbarem, nicht verdecktem Kindfenster
            EnumChildWindows(hwnd, (child, _) =>
            {
                if (IsWindowVisible(child) && !IsCloaked(child))
                {
                    childWindow = child;
                    return false; // abbrechen sobald gefunden
                }
                return true;
            }, IntPtr.Zero);

            if (childWindow != IntPtr.Zero)
            {
                ShowWindow(childWindow, SW_RESTORE); // oder SW_SHOWMAXIMIZED
                SetForegroundWindow(childWindow);
                Debug.WriteLine("[UWP] Child window brought to front.");
            }
            else
            {
                Debug.WriteLine("[UWP] No visible child window found.");
            }
        }

        private void AnimateInfoPanelFocus(bool hasFocus)
        {
            var sb = new Storyboard();
            var animX = new DoubleAnimation();
            var animY = new DoubleAnimation();
            double baseScale = _currentTopPanelScale;

            animX.Duration = animY.Duration = new Duration(TimeSpan.FromMilliseconds(400));

            // Bounce-Effekt beim Aktivieren
            if (hasFocus)
            {
                animX.To = animY.To = baseScale * 1.05;
                animX.EasingFunction = animY.EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
            }
            else
            {
                animX.To = animY.To = baseScale;
                animX.EasingFunction = animY.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            }

            Storyboard.SetTarget(animX, InfoPanelTransform);
            Storyboard.SetTargetProperty(animX, "ScaleX");
            Storyboard.SetTarget(animY, InfoPanelTransform);
            Storyboard.SetTargetProperty(animY, "ScaleY");

            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Begin();
        }


        /// <summary>
        /// Sucht nach allen laufenden Anwendungen und aktualisiert die UI-Karten.
        /// </summary>
        // Variable to prevent multiple scans running at once
        private bool _isScanning = false;
        [Flags]
        internal enum WindowStylesEx : uint
        {
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_NOACTIVATE = 0x08000000, // Wichtig für Overlays
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_TRANSPARENT = 0x00000020
        }

        private async Task CloseProcessWindowAsync(Process proc, IntPtr hwnd)
        {
            try
            {
                if (proc != null && !proc.HasExited)
                {
                    proc.Kill();
                }
                else if (hwnd != IntPtr.Zero)
                {
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not kill process, sending close message instead: {ex.Message}");
                if (hwnd != IntPtr.Zero)
                {
                    PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }

            await Task.Delay(500);
            await RefreshAppListAsync();
        }

 
        private async void TriggerCardAction(int index, bool launch)
        {
            if (index < 0 || index >= _cardCache.Count) return;

            var entry = _cardCache[index];
            if (!(entry.Card.Tag is CardTag tag)) return;

            if (launch)
            {
                // --- CINEMATIC MANUAL RESUME LOGIC ---
                // Check if the target process is actually asleep
                bool isSuspended = tag.Process != null && !tag.Process.HasExited && ProcessSuspender.IsProcessSuspended(tag.Process.Id);

                if (isSuspended)
                {
                    LogToAppData($"[Manual Resume] Detected suspended state for {tag.Process.ProcessName}. Starting wakeup sequence...");

                    // 1. Notify the user what is happening
                    SendOverlayNotification($"Waking up: {tag.Process.ProcessName}...");

                    // 2. Visual Delay (1.5 seconds) - Gives the feeling of "booting up"
                    await Task.Delay(1500);

                    // 3. Execute Resume (Unfreeze)
                    ToggleGameSuspend(tag.Process, false);

                    // Reset auto-resume flag since we handled it manually here
                    _suspendedGamePid = 0;

                    // 4. Audio Feedback & Success Message
                    PlayActivationSound(); // Nice "Ping" sound
                    SendOverlayNotification("Game Resumed!");

                    // 5. Technical Delay: Give the process 500ms to process the resume signal 
                    // and repaint its window before we force it to the foreground.
                    await Task.Delay(500);
                }
                // -------------------------------------

                // Standard switching logic (happens instantly if not suspended)
                MakeSelfNonTopmost();
                if (!await TrySwitchToWindowViaTaskViewAsync(
                        tag.Hwnd,
                        FocusReturnTarget.GameWindow))
                {
                    Debug.WriteLine("[GCM] Process handoff did not report focus; skipping fallback by design.");
                }
            }
            else // B-Button to Close (Logic remains identical)
            {
                await CloseProcessWindowAsync(tag.Process, tag.Hwnd);
            }
        }




        [DllImport("user32.dll")]
        private static extern bool SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        private bool _altPressed = false;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool ShowWindowbtf(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOW = 5; // Used for showing/activating
        public static void BringToFrontAndFocus(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            try
            {
                // --- NEUE INTELLIGENTE LOGIK ---

                // Step 1: Check if the target window is minimized (iconic).
                if (IsIconic(hWnd))
                {
                    // If it is minimized, we MUST restore it to make it visible.
                    ShowWindowbtf(hWnd, SW_RESTORE);
                }

                // Step 2: Now, bring it to the foreground. This part is safe for already
                // visible windows and will not cause a state change/flicker.
                IntPtr foregroundHwnd = GetForegroundWindow();
                if (foregroundHwnd == hWnd) return; // It's already the foreground window.

                uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
                uint ourThreadId = GetCurrentThreadId();

                AttachThreadInput(ourThreadId, foregroundThreadId, true);

                SetForegroundWindow(hWnd);
                SetActiveWindow(hWnd);

                AttachThreadInput(ourThreadId, foregroundThreadId, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BringToFrontAndFocus failed: {ex.Message}");
            }
        }
        public async Task ForceGcmToFront()
        {
            try
            {
                IntPtr gcmHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                IntPtr currentForegroundHwnd = GetForegroundWindow();

                // Wenn wir schon im Fokus sind, nur XAML auffrischen und abbrechen
                if (gcmHwnd == currentForegroundHwnd)
                {
                    if (this.Content is UIElement rootElement)
                    {
                        rootElement.Focus(FocusState.Programmatic);
                    }
                    return;
                }

                uint currentThreadId = GetWindowThreadProcessId(currentForegroundHwnd, out _);
                uint thisThreadId = GetCurrentThreadId();

                // 1. Erlaubnis & Thread-Link
                AllowSetForegroundWindow(ASFW_ANY);
                if (currentThreadId != thisThreadId)
                {
                    AttachThreadInput(thisThreadId, currentThreadId, true);
                }

                // 2. Fenster zeigen und nach ganz oben zwingen
                ShowWindow(gcmHwnd, 9); // SW_RESTORE = 9
                BringWindowToTop(gcmHwnd);
                // HWND_TOPMOST, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
                SetWindowPos(gcmHwnd, HWND_TOPMOST, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0040);

                // 3. API Fokus (WICHTIG: SetFocus hinzugefügt)
                SetForegroundWindow(gcmHwnd);
                SetActiveWindow(gcmHwnd);
                SetFocus(gcmHwnd);

                // 4. Input wieder lösen (BEVOR wir den physischen Klick machen!)
                if (currentThreadId != thisThreadId)
                {
                    AttachThreadInput(thisThreadId, currentThreadId, false);
                }

                // 5. Kurze Pause für die Windows DWM Rendering-Pipeline
                await Task.Delay(50);

                // --- 6. DER PHYSIKALISCHE "JAB" ---
                // Jetzt, wo der Thread gelöst ist, funktioniert der Klick zu 100%
                int screenHeight = GetScreenHeight();
                SetCursorPos(0, screenHeight - 5);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                await Task.Delay(10);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                // 7. XAML-Fokus sicherstellen
                if (this.Content is UIElement root)
                {
                    root.Focus(FocusState.Programmatic);
                }

                // 8. TopMost nach kurzem Delay lösen, damit wir nicht andere Popups blockieren
                await Task.Delay(100);
                SetWindowPos(gcmHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0040);

                ParkMouseCursor();
                Debug.WriteLine("[Focus] GCM successfully forced to foreground with input focus.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Focus Error] {ex.Message}");
            }
        }


        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        const uint INPUT_MOUSE = 0;
        const uint MOUSEEVENTF_MOVE = 0x0001;
        const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        const uint MOUSEEVENTF_LEFTUP = 0x0004;
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
      

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        #endregion
        #region Startupvideo

        #region Startup Video Logic

        // --- EINSTELLUNGEN LESEN ---

        private static bool IsGcmVideoEnabled()
        {
            try { return AppSettings.Load<bool>("usestartupvideo"); }
            catch { return false; }
        }

        private static bool IsSteamInjectionEnabled()
        {
            try { return AppSettings.Load<bool>("usesteamstartupvideo"); }
            catch { return false; }
        }

        private void ForceStartupVideoFullscreen()
        {
            try
            {
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                ShowWindow(hwnd, SW_RESTORE);

                try
                {
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow.Presenter.Kind != Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                    {
                        appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                    }
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"Startup fullscreen presenter fallback: {ex.Message}");
                }

                long style = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
                style &= ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
                style |= WS_POPUP;
                SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)style);

                SetWindowPos(
                    hwnd,
                    HWND_TOPMOST,
                    0,
                    0,
                    GetScreenWidth(),
                    GetScreenHeight(),
                    SWP_SHOWWINDOW | 0x0020);

                App.StartupTrace("Startup fullscreen forced.");
            }
            catch (Exception ex)
            {
                App.StartupTrace($"Startup fullscreen force failed: {ex.Message}");
                Debug.WriteLine($"[StartupVideo] Fullscreen force failed: {ex.Message}");
            }
        }

        // --- GCM PLAYER (Startet direkt beim App-Start) ---

        private void PlayStartupVideo()
        {
            try
            {
                App.StartupTrace("PlayStartupVideo entered.");
                if (!IsGcmVideoEnabled() || IsSteamInjectionEnabled())
                {
                    App.StartupTrace("Startup video skipped.");
                    TransitionToMainUI();
                    return;
                }

                string videoPath = "";
                try { videoPath = AppSettings.Load<string>("startupvideo_path"); } catch { }

                if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                {
                    Debug.WriteLine("[StartupVideo] Videodatei nicht gefunden.");
                    App.StartupTrace("Startup video path missing. Transitioning directly to UI.");
                    TransitionToMainUI();
                    return;
                }

                ForceStartupVideoFullscreen();

                // ALLES andere ausblenden, Hintergrund auf Tiefschwarz setzen
                MainContent.Visibility = Visibility.Collapsed;
                FocusLossOverlay.Visibility = Visibility.Collapsed;

                StartupVideoPlayer.Visibility = Visibility.Visible;
                StartupVideoPlayer.HorizontalAlignment = HorizontalAlignment.Stretch;
                StartupVideoPlayer.VerticalAlignment = VerticalAlignment.Stretch;
                // WICHTIG: Stretch auf UniformToFill, damit keine Ränder entstehen (Letterboxing vermeiden)
                StartupVideoPlayer.Stretch = Stretch.UniformToFill;

                _startupMediaPlayer = new MediaPlayer { AutoPlay = true };
                _startupMediaPlayer.Source = MediaSource.CreateFromUri(new Uri(videoPath, UriKind.Absolute));

                var timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
                timeoutTimer.Tick += (s, e) => {
                    timeoutTimer.Stop();
                    if (!startupVideoFinished) TransitionToMainUI();
                };
                timeoutTimer.Start();

                _startupMediaPlayer.MediaEnded += OnStartupVideoEnded;
                StartupVideoPlayer.SetMediaPlayer(_startupMediaPlayer);

                // NEU: Fokus-Status auf Video setzen, um Controller-Eingaben abzufangen
                _currentFocusArea = FocusArea.StartupVideo;
            }
            catch (Exception ex)
            {
                App.StartupTrace($"PlayStartupVideo failed: {ex.Message}");
                Debug.WriteLine("[StartupVideo] Fehler: " + ex.Message);
                TransitionToMainUI();
            }
        }

        private void OnStartupVideoEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() => TransitionToMainUI());
        }

        private void TransitionToMainUI()
        {
            if (startupVideoFinished || _isTransitioningToMainUi) return; // Verhindert doppeltes Ausführen

            App.StartupTrace("TransitionToMainUI begin.");
            _isTransitioningToMainUi = true;

            try
            {
                try
                {
                    if (_startupMediaPlayer != null)
                    {
                        _startupMediaPlayer.MediaEnded -= OnStartupVideoEnded;
                        _startupMediaPlayer.Pause();
                        _startupMediaPlayer.Dispose();
                        _startupMediaPlayer = null;
                    }

                    if (StartupVideoPlayer != null)
                    {
                        StartupVideoPlayer.SetMediaPlayer(null);
                        StartupVideoPlayer.Visibility = Visibility.Collapsed;
                    }

                    App.StartupTrace("TransitionToMainUI startup video cleaned.");
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"TransitionToMainUI video cleanup failed: {ex}");
                }

                try
                {
                    MainContent.Opacity = 1.0;
                    MainContent.Visibility = Visibility.Visible;

                    FocusLossOverlay.Opacity = 1.0;
                    FocusLossOverlay.Visibility = Visibility.Collapsed;
                    _isOverlayActive = false;
                    App.StartupTrace("TransitionToMainUI main content visible.");
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"TransitionToMainUI visibility failed: {ex}");
                }

                try
                {
                    _currentFocusArea = FocusArea.Cards;
                    MarkShellUiReady();
                    ApplyResponsiveShellSizing();
                    UpdateVisualFocus();
                    App.StartupTrace("TransitionToMainUI shell ready.");
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"TransitionToMainUI shell failed: {ex}");
                }

                try
                {
                    IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(myHwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow.Presenter.Kind != Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                    {
                        appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                    }

                    App.StartupTrace("TransitionToMainUI fullscreen ready.");
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"TransitionToMainUI fullscreen failed: {ex}");
                }

                try
                {
                    SetBackgroundImage(GetScreenWidth(), GetScreenHeight());
                    App.StartupTrace("TransitionToMainUI background scheduled.");
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"TransitionToMainUI background failed: {ex}");
                }

                try
                {
                    IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    SetWindowPos(myHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
                catch (Exception ex)
                {
                    App.StartupTrace($"TransitionToMainUI topmost reset failed: {ex}");
                }

                startupVideoFinished = true;
                App.StartupTrace("TransitionToMainUI complete.");
            }
            catch (Exception ex)
            {
                App.StartupTrace($"TransitionToMainUI failed fatally but was contained: {ex}");
                try
                {
                    MainContent.Visibility = Visibility.Visible;
                    FocusLossOverlay.Visibility = Visibility.Collapsed;
                }
                catch { }
                startupVideoFinished = true;
            }
            finally
            {
                _isTransitioningToMainUi = false;
            }
        }

        // --- STEAM INJECTION ---

        public static void RenameSteamStartupVideo_Start()
        {
            try
            {
                if (!IsGcmVideoEnabled()) return;
                if (!IsSteamInjectionEnabled()) return;

                // Auto-Detect statt Settings!
                string steamPath = AutoDetectLauncherPath("steam");
                if (string.IsNullOrEmpty(steamPath)) return;

                string moviesPath = Path.Combine(Path.GetDirectoryName(steamPath), "steamui", "movies");
                if (!Directory.Exists(moviesPath)) return;

                string myVideo = Path.Combine(moviesPath, "GCM_vid.webm");
                string steamOriginal = Path.Combine(moviesPath, "bigpicture_startup.webm");
                string steamBackup = Path.Combine(moviesPath, "bigpicture_startup.old.webm");

                if (File.Exists(steamBackup))
                {
                    if (File.Exists(steamOriginal)) File.Delete(steamOriginal);
                    File.Move(steamBackup, steamOriginal);
                }

                if (!File.Exists(myVideo)) return;

                if (File.Exists(steamOriginal))
                {
                    File.Move(steamOriginal, steamBackup);
                }
                File.Copy(myVideo, steamOriginal, true);
                Debug.WriteLine("[SteamInjection] Swap erfolgreich.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInjection] Start-Fehler: {ex.Message}");
            }
        }

        public static void RenameSteamStartupVideo_End()
        {
            try
            {
                // Auto-Detect statt Settings!
                string steamPath = AutoDetectLauncherPath("steam");
                if (string.IsNullOrEmpty(steamPath)) return;

                string moviesPath = Path.Combine(Path.GetDirectoryName(steamPath), "steamui", "movies");

                string steamOriginal = Path.Combine(moviesPath, "bigpicture_startup.webm");
                string steamBackup = Path.Combine(moviesPath, "bigpicture_startup.old.webm");

                if (File.Exists(steamBackup))
                {
                    if (File.Exists(steamOriginal)) File.Delete(steamOriginal);
                    File.Move(steamBackup, steamOriginal);
                    Debug.WriteLine("[SteamInjection] Restore erfolgreich.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SteamInjection] End-Fehler: {ex.Message}");
            }
        }

        #endregion

        #endregion Startupvideo

        #region shurdownmenuitem
        private void ShutdownMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Shutdown-Button geklickt. Räume auf und fahre den Computer herunter...");

            // ZUERST die Aufräum-Methode aufrufen
            RenameSteamStartupVideo_End();

            // DANN den Computer herunterfahren
            Process.Start("shutdown", "/s /t 0");
        }

        private async void SleepMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Logging start
            LogToAppData("========================================");
            LogToAppData("=== Sleep Sequence Initiated (Cinematic) ===");

            // 1. Clean up UI immediately
            PowerMenu.Visibility = Visibility.Collapsed;
            _currentFocusArea = FocusArea.TopButtons;
            UpdateVisualFocus();

            // 2. Find Game
            var gameProc = FindActiveGameProcess();

            if (gameProc != null)
            {
                // Store PID for resume
                _suspendedGamePid = gameProc.Id;

                // --- STEP 1: NOTIFY SUSPEND ---
                string gameName = gameProc.ProcessName;
                SendOverlayNotification($"Suspending: {gameName}...");
                LogToAppData($"[Sleep] User notified. Suspending {gameName}...");

                // Freeze the game
                ToggleGameSuspend(gameProc, true);

                // --- WAIT: Visual pause to let the user read "Suspending..." ---
                // and to give the system time to calm down the process (CPU -> 0%)
                await Task.Delay(2500); // 2.5 Sekunden warten
            }
            else
            {
                // Fallback message if no game is running
                SendOverlayNotification("Preparing Sleep Mode...");
                LogToAppData("[Sleep] No game found. Skipping suspend.");
                await Task.Delay(1500);
            }

            // 3. Play Sound (Sound effect right before the final message)
            PlaydeactivationSound();

            // --- STEP 2: NOTIFY SLEEP ---
            SendOverlayNotification("Entering Sleep Mode...");
            LogToAppData("[Sleep] Displaying 'Entering Sleep Mode' notification.");

            // --- WAIT: Let the user see the final message before black screen ---
            await Task.Delay(1500); // 1.5 Sekunden warten

            // 4. Execute System Sleep
            LogToAppData("[Sleep] Sending S3 Suspend command now.");

            // false = Sleep (S3), false = no force, false = wake allowed
            bool success = SetSuspendState(false, false, false);

            if (!success)
            {
                string err = "[Sleep] Error: SetSuspendState failed. Check Admin/Hibernate.";
                Debug.WriteLine(err);
                LogToAppData(err);

                // SAFETY: If sleep fails, wake the game up so the user isn't stuck
                if (gameProc != null)
                {
                    SendOverlayNotification("Sleep failed! Resuming Game...");
                    ToggleGameSuspend(gameProc, false); // Resume
                    _suspendedGamePid = 0;
                }
            }
            else
            {
                LogToAppData("[Sleep] Good night. System sleep command sent.");
            }
        }

        private async void LogOffMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PowerMenu.Visibility = Visibility.Collapsed;

            // Wichtige Aufräum-Aktion vor dem Abmelden ausführen
            RenameSteamStartupVideo_End();

            await Task.Delay(200);

            // Meldet den aktuellen Benutzer ab
            Process.Start("shutdown", "/l");
        }

        // In MainWindow.xaml.cs


        private async void ToggleAppLauncher_Click(object sender, RoutedEventArgs e)
        {
            if (AppLauncher.Visibility == Visibility.Visible)
            {
                AppLauncher.Visibility = Visibility.Collapsed;
                _currentFocusArea = FocusArea.TopButtons;
            }
            else
            {
                AppLauncher.Visibility = Visibility.Visible;
                _currentFocusArea = FocusArea.AppLauncher;

                // WICHTIG: Apps nur laden, wenn die Liste noch leer ist
                if (AllInstalledApps.Count == 0)
                {
                    await LoadInstalledAppsAsync();
                }
                else
                {
                    LoadAppLauncherFavorites();
                    foreach (AppInfo app in AllInstalledApps)
                    {
                        app.IsFavorite = _favoriteAppIds.Contains(app.StableId);
                    }
                    RefreshAppLauncherView();
                }

                if (AppGridView.Items.Count > 0)
                {
                    AppGridView.SelectedIndex = 0;
                }
            }
            UpdateVisualFocus();
        }

        private void AppGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppInfo app)
            {
               
                LaunchApp(app);
            }
        }

        private void LaunchApp(AppInfo app)
        {
            if (app == null) return;

            try
            {
                ClearFocusReturnWatchdog();

                if (app.LaunchKind.Equals("Packaged", StringComparison.OrdinalIgnoreCase))
                {
                    ActivateUwpApp(app.LaunchTarget);
                }
                else if (app.LaunchKind.Equals("Shortcut", StringComparison.OrdinalIgnoreCase) ||
                         app.FilePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    // For .lnk shortcuts use IShellDispatch2 which runs at medium integrity
                    // even when called from an elevated process.
                    dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                    shell.ShellExecute(app.FilePath);
                }
                else
                {
                    StartProcessAsNonAdmin(string.IsNullOrWhiteSpace(app.LaunchTarget) ? app.FilePath : app.LaunchTarget);
                }

                // Clean up the UI after launching.
                AppLauncher.Visibility = Visibility.Collapsed;
                _currentFocusArea = FocusArea.Cards;
                UpdateVisualFocus();
                PlayActivationSound();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppLauncher] Failed to launch app '{app.FilePath}': {ex.Message}");
            }
        }

        private async void RestartMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PowerMenu.Visibility = Visibility.Collapsed;

            // Wichtige Aufräum-Aktion vor dem Neustart ausführen
            RenameSteamStartupVideo_End();

            // Kurze Verzögerung, um sicherzustellen, dass der vorherige Befehl abgeschlossen ist
            await Task.Delay(200);

            // Startet den Computer neu
            Process.Start("shutdown", "/r /t 0");
        }

        private void ShutdownButton_Click_1(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Shutdown-Button geklickt. Räume auf und fahre den Computer herunter...");

            // ZUERST die Aufräum-Methode aufrufen
            RenameSteamStartupVideo_End();

            // DANN den Computer herunterfahren
            Process.Start("shutdown", "/s /t 0");
        }
        #endregion shurdownmenuitem

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleSettingsOverlay();
        }


        #endregion methodes
        #region aftersleepwindow
        // --- DISABLE LOGIN ON WAKEUP ---

        public static void DisableLoginOnWakeup()
        {
            try
            {
                // Die GUID für "Kennwort bei Reaktivierung anfordern"
                // (Das ist eine Standard-Windows-GUID, die sich nicht ändert)
                string lockGuid = "0e796bdb-100d-47d6-a2d5-f7d2daa51f51";

                // 1. Einstellung für Netzbetrieb (AC) auf 0 (Deaktiviert) setzen
                RunPowerCfg($"/setacvalueindex SCHEME_CURRENT SUB_NONE {lockGuid} 0");

                // 2. Einstellung für Akkubetrieb (DC) auf 0 (Deaktiviert) setzen
                RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT SUB_NONE {lockGuid} 0");

                // 3. Änderungen sofort anwenden
                RunPowerCfg("/SetActive SCHEME_CURRENT");

                Debug.WriteLine("[System] Login on Wakeup has been disabled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[System] Failed to disable login on wake: {ex.Message}");
            }

            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Power\PowerSettings\0e796bdb-100d-47d6-a2d5-f7d2daa51f51", true))
                {
                    if (key != null)
                    {
                        key.SetValue("ACSettingIndex", 0, RegistryValueKind.DWord);
                        key.SetValue("DCSettingIndex", 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        // Hilfsmethode, um powercfg.exe unsichtbar auszuführen
        private static void RunPowerCfg(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true
            };

            using var p = Process.Start(psi);
            p.WaitForExit();
        }
        #endregion aftersleepwindow
        #region sleep game
        // Checks if a process is suspended (Logic ported from Nyrna's C++ code)



        // Powerful Process Suspender that mimics Nyrna's logic (Recursive Tree Suspension)
        public static class ProcessSuspender
        {
            [DllImport("ntdll.dll")]
            private static extern uint NtSuspendProcess(IntPtr processHandle);

            [DllImport("ntdll.dll")]
            private static extern uint NtResumeProcess(IntPtr processHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll")]
            private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

            [DllImport("kernel32.dll")]
            private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            [DllImport("kernel32.dll")]
            private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            private const uint TH32CS_SNAPPROCESS = 0x00000002;
            private const uint PROCESS_SUSPEND_RESUME = 0x0800;

            [StructLayout(LayoutKind.Sequential)]
            private struct PROCESSENTRY32
            {
                public uint dwSize;
                public uint cntUsage;
                public uint th32ProcessID;
                public IntPtr th32DefaultHeapID;
                public uint th32ModuleID;
                public uint cntThreads;
                public uint th32ParentProcessID;
                public int pcPriClassBase;
                public uint dwFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public string szExeFile;
            }

            // --- PUBLIC METHODS ---

            // Friert den Prozess UND alle Kinder ein (wie Nyrna)
            public static void SuspendRecursive(int pid)
            {
                // 1. Erst den Papa einfrieren
                SuspendSingleProcess(pid);

                // 2. Kinder suchen und auch einfrieren
                var children = GetChildProcesses(pid);
                foreach (var childPid in children)
                {
                    SuspendRecursive(childPid);
                }
            }

            public static bool IsProcessSuspended(int pid)
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (process == null || process.HasExited) return false;

                    process.Refresh();

                    if (process.Threads.Count > 0)
                    {
                        // Wir nehmen Thread[0] als Indikator für den Hauptprozess
                        var mainThread = process.Threads[0];
                        if (mainThread.ThreadState == System.Diagnostics.ThreadState.Wait &&
                            mainThread.WaitReason == System.Diagnostics.ThreadWaitReason.Suspended)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    return false;
                }
                return false;
            }


            // Taut den Prozess UND alle Kinder wieder auf
            public static void ResumeRecursive(int pid)
            {
                // 1. Papa aufwecken
                ResumeSingleProcess(pid);

                // 2. Kinder aufwecken
                var children = GetChildProcesses(pid);
                foreach (var childPid in children)
                {
                    ResumeRecursive(childPid);
                }
            }

            // --- INTERNE HELFER ---

            private static void SuspendSingleProcess(int pid)
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    handle = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
                    if (handle != IntPtr.Zero)
                    {
                        NtSuspendProcess(handle);
                        Debug.WriteLine($"[ProcessSuspender] Suspended PID: {pid}");
                    }
                }
                catch { }
                finally
                {
                    if (handle != IntPtr.Zero) CloseHandle(handle);
                }
            }

            private static void ResumeSingleProcess(int pid)
            {
                IntPtr handle = IntPtr.Zero;
                try
                {
                    handle = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
                    if (handle != IntPtr.Zero)
                    {
                        NtResumeProcess(handle);
                        Debug.WriteLine($"[ProcessSuspender] Resumed PID: {pid}");
                    }
                }
                catch { }
                finally
                {
                    if (handle != IntPtr.Zero) CloseHandle(handle);
                }
            }

            private static List<int> GetChildProcesses(int parentPid)
            {
                var children = new List<int>();
                IntPtr snapshot = IntPtr.Zero;

                try
                {
                    snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
                    if (snapshot != IntPtr.Zero)
                    {
                        PROCESSENTRY32 procEntry = new PROCESSENTRY32();
                        procEntry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));

                        if (Process32First(snapshot, ref procEntry))
                        {
                            do
                            {
                                if (procEntry.th32ParentProcessID == parentPid)
                                {
                                    children.Add((int)procEntry.th32ProcessID);
                                }
                            }
                            while (Process32Next(snapshot, ref procEntry));
                        }
                    }
                }
                catch { }
                finally
                {
                    if (snapshot != IntPtr.Zero) CloseHandle(snapshot);
                }

                return children;
            }
        }


        private int _suspendedGamePid = 0;

        // Event Handler: Triggered when Windows wakes up or goes to sleep
        private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                LogToAppData("[System] System is waking up (Resume event).");

                // Haben wir ein Spiel eingefroren?
                if (_suspendedGamePid != 0)
                {
                    try
                    {
                        // Versuche, den Prozess zu finden
                        var proc = Process.GetProcessById(_suspendedGamePid);

                        // Doppelte Sicherheit: Existiert er noch und läuft er?
                        if (proc != null && !proc.HasExited)
                        {
                            LogToAppData($"[Auto-Resume] Process found ({proc.ProcessName}). Resuming now...");
                            SendOverlayNotification($"Resuming: {proc.ProcessName}");

                            // WICHTIG: false = RESUME (Unfreeze)
                            ToggleGameSuspend(proc, false);

                            // Reset
                            _suspendedGamePid = 0;
                        }
                        else
                        {
                            LogToAppData("[Auto-Resume] Game process has exited during sleep.");
                            _suspendedGamePid = 0;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process.GetProcessById wirft ArgumentException, wenn PID nicht existiert
                        LogToAppData("[Auto-Resume] Process ID not found (Game closed?).");
                        _suspendedGamePid = 0;
                    }
                    catch (Exception ex)
                    {
                        LogToAppData($"[Auto-Resume] Unexpected error: {ex.Message}");
                        _suspendedGamePid = 0;
                    }
                }
            }
        }

        // Writes logs to a text file in AppData for debugging
        private void LogToAppData(string message)
        {
            try
            {
                // Path: %AppData%\gcmsettings\logs\sleep_debug.txt
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "logs");

                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                string logFile = Path.Combine(logDir, "sleep_debug.txt");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                File.AppendAllText(logFile, $"[{timestamp}] {message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LogToAppData Error] {ex.Message}");
            }
        }


        private bool IsProtectedProcess(Process proc)
        {
            if (proc == null) return true;

            string name = proc.ProcessName.ToLower();

            // List of process names that are essential or prone to errors when frozen
            string[] blacklist = {
        "discord", "spotify", "steam", "steamwebhelper",
        "nvcontainer", "nvidia share", "nvidia web helper",
        "explorer", "gcmloader", "taskhostw", "searchhost",
        "chrome", "firefox", "msedge", "opera", "browser"
    };

            // Check against blacklist and existing keyword lists
            return blacklist.Any(b => name.Contains(b)) ||
                   _autoMouseApps.Any(a => name.Contains(a.ToLower())) ||
                   _nonGameKeywords.Any(k => name.Contains(k.ToLower()));
        }

        private Process FindActiveGameProcess()
        {
            if (_featuredGameProcessData?.Proc != null &&
                !_featuredGameProcessData.Proc.HasExited &&
                !IsProtectedProcess(_featuredGameProcessData.Proc))
            {
                LogToAppData($"[ProcessManager] Featured game target: {_featuredGameProcessData.Proc.ProcessName}");
                return _featuredGameProcessData.Proc;
            }

            if (_cardCache == null || _cardCache.Count == 0) return null;

            // 1. Pass: Look for a confirmed game (based on install path)
            foreach (var entry in _cardCache)
            {
                if (entry.Proc != null && !entry.Proc.HasExited && !IsProtectedProcess(entry.Proc))
                {
                    if (IsLikelyGame(entry.Proc))
                    {
                        LogToAppData($"[ProcessManager] Confirmed game target: {entry.Proc.ProcessName}");
                        return entry.Proc;
                    }
                }
            }

            // 2. Pass: Smart Fallback (Anything not protected and not a system app)
            var fallback = _cardCache.FirstOrDefault(c =>
                c.Proc != null &&
                !c.Proc.HasExited &&
                !IsProtectedProcess(c.Proc) &&
                c.Proc.ProcessName.ToLower() != "explorer"
            );

            if (fallback != null)
            {
                LogToAppData($"[ProcessManager] Fallback target found: {fallback.Proc.ProcessName}");
                return fallback.Proc;
            }

            return null;
        }

        private void ToggleGameSuspend(Process gameProc, bool suspend)
        {
            if (gameProc == null || gameProc.HasExited) return;

            try
            {
                // Final guard against suspending critical apps
                if (IsProtectedProcess(gameProc))
                {
                    LogToAppData($"[ProcessManager] Suspension blocked: {gameProc.ProcessName} is protected.");
                    return;
                }

                string actionName = suspend ? "Freeze" : "Wake";
                LogToAppData($"[ProcessManager] {actionName} initiated for {gameProc.ProcessName} (PID: {gameProc.Id})");

                if (suspend)
                {
                    // Freeze the entire process tree recursively
                    ProcessSuspender.SuspendRecursive(gameProc.Id);
                    SendOverlayNotification($"Game Frozen: {gameProc.ProcessName}");
                }
                else
                {
                    // Resume the entire process tree recursively
                    ProcessSuspender.ResumeRecursive(gameProc.Id);
                    SendOverlayNotification($"Game Resumed: {gameProc.ProcessName}");
                }
            }
            catch (Exception ex)
            {
                // If one process fails, we log it but don't stop the whole program
                LogToAppData($"[ProcessManager] ERROR during {gameProc.ProcessName} toggle: {ex.Message}");
                Debug.WriteLine($"[ProcessManager] Failed to {suspend} {gameProc.ProcessName}. Continuing...");
            }
        }
        #endregion sleep game

        private void LauncherTileRow_Loaded(object sender, RoutedEventArgs e)
        {
           
        }


        private void ExitGcmButton_Click_1(object sender, RoutedEventArgs e)
        {
            ShowWindowsReturnConfirm();
        }

        private void ShowGithubReleasePrompt(Version currentVersion)
        {
            if (_availableGithubReleaseUpdate == null || GithubReleaseOverlay == null)
            {
                return;
            }

            _githubReleaseReturnFocusArea = _currentFocusArea == FocusArea.GithubReleasePrompt
                ? FocusArea.Cards
                : _currentFocusArea;

            UpdateGithubReleasePromptIcons();

            if (GithubReleaseTitleText != null)
            {
                GithubReleaseTitleText.Text = _availableGithubReleaseUpdate.DisplayTitle.ToUpperInvariant();
            }

            if (GithubReleaseMessageText != null)
            {
                GithubReleaseMessageText.Text =
                    $"GitHub release {_availableGithubReleaseUpdate.VersionText} is available. " +
                    $"You are currently on {FormatVersion(currentVersion)}. Open the release page to install it manually.";
            }

            GithubReleaseOverlay.Visibility = Visibility.Visible;
            _currentFocusArea = FocusArea.GithubReleasePrompt;
            StyleGithubReleasePromptChoices();

            if (this.Content is UIElement root)
            {
                root.Focus(FocusState.Programmatic);
            }

            UpdateVisualFocus();
        }

        private void UpdateGithubReleasePromptIcons()
        {
            string confirmIcon = $"ms-appx:///Assets/{GetControllerIconAssetPath("A")}";
            string cancelIcon = $"ms-appx:///Assets/{GetControllerIconAssetPath("B")}";

            if (GithubReleaseOpenIcon != null)
            {
                GithubReleaseOpenIcon.Source = new BitmapImage(new Uri(confirmIcon));
            }

            if (GithubReleaseLaterIcon != null)
            {
                GithubReleaseLaterIcon.Source = new BitmapImage(new Uri(cancelIcon));
            }
        }

        private void StyleGithubReleasePromptChoices()
        {
            if (GithubReleaseOpenButton != null)
            {
                GithubReleaseOpenButton.Background = new SolidColorBrush(GetThemeAccentColor(52));
                GithubReleaseOpenButton.BorderBrush = new SolidColorBrush(GetThemeAccentColor(138));
                GithubReleaseOpenButton.BorderThickness = new Thickness(1.35);
            }

            if (GithubReleaseLaterButton != null)
            {
                GithubReleaseLaterButton.Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(24, 38, 58)));
                GithubReleaseLaterButton.BorderBrush = new SolidColorBrush(GetThemeAccentColor(54));
                GithubReleaseLaterButton.BorderThickness = new Thickness(1.0);
            }
        }

        private void CloseGithubReleasePrompt()
        {
            if (GithubReleaseOverlay != null)
            {
                GithubReleaseOverlay.Visibility = Visibility.Collapsed;
            }

            _currentFocusArea = _githubReleaseReturnFocusArea;
            UpdateVisualFocus();
        }

        private void OpenGithubReleasePromptPage()
        {
            if (_availableGithubReleaseUpdate == null || string.IsNullOrWhiteSpace(_availableGithubReleaseUpdate.HtmlUrl))
            {
                CloseGithubReleasePrompt();
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _availableGithubReleaseUpdate.HtmlUrl,
                    UseShellExecute = true
                });

                SendOverlayNotification($"Release page opened: {_availableGithubReleaseUpdate.VersionText}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Update] Failed to open release page: {ex.Message}");
            }

            CloseGithubReleasePrompt();
        }

        private void GithubReleaseOpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenGithubReleasePromptPage();
        }

        private void GithubReleaseLaterButton_Click(object sender, RoutedEventArgs e)
        {
            CloseGithubReleasePrompt();
        }

        private void GithubReleaseOverlay_BackdropTapped(object sender, TappedRoutedEventArgs e)
        {
            CloseGithubReleasePrompt();
        }

        private void GithubReleaseContent_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ShowWindowsReturnConfirm()
        {
            UpdateWindowsReturnConfirmIcons();
            WindowsReturnConfirmOverlay.Visibility = Visibility.Visible;
            _currentFocusArea = FocusArea.WindowsReturnConfirm;
            StyleWindowsReturnConfirmChoices();
            if (this.Content is UIElement root)
            {
                root.Focus(FocusState.Programmatic);
            }
            UpdateVisualFocus();
        }

        private void UpdateWindowsReturnConfirmIcons()
        {
            string confirmIcon = $"ms-appx:///Assets/{GetControllerIconAssetPath("A")}";
            string cancelIcon = $"ms-appx:///Assets/{GetControllerIconAssetPath("B")}";

            if (WindowsReturnConfirmIcon != null)
            {
                WindowsReturnConfirmIcon.Source = new BitmapImage(new Uri(confirmIcon));
            }

            if (WindowsReturnCancelIcon != null)
            {
                WindowsReturnCancelIcon.Source = new BitmapImage(new Uri(cancelIcon));
            }
        }

        private void StyleWindowsReturnConfirmChoices()
        {
            if (WindowsReturnConfirmButton != null)
            {
                WindowsReturnConfirmButton.Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(30, 46, 68)));
                WindowsReturnConfirmButton.BorderBrush = new SolidColorBrush(GetThemeAccentColor(82));
                WindowsReturnConfirmButton.BorderThickness = new Thickness(1.2);
            }

            if (WindowsReturnCancelButton != null)
            {
                WindowsReturnCancelButton.Background = new SolidColorBrush(GetThemeCardTintColor(GetThemeGlassAlpha(24, 38, 58)));
                WindowsReturnCancelButton.BorderBrush = new SolidColorBrush(GetThemeAccentColor(58));
                WindowsReturnCancelButton.BorderThickness = new Thickness(1.2);
            }
        }

        private void CloseWindowsReturnConfirm()
        {
            WindowsReturnConfirmOverlay.Visibility = Visibility.Collapsed;
            _currentFocusArea = FocusArea.TopButtons;
            _selectedTopButtonIndex = _topButtons.IndexOf(ExitGcmButton);
            if (_selectedTopButtonIndex < 0)
            {
                _selectedTopButtonIndex = 0;
            }
            UpdateVisualFocus();
        }

        private void ConfirmReturnToWindows()
        {
            WindowsReturnConfirmOverlay.Visibility = Visibility.Collapsed;
            ClearFocusReturnWatchdog();
            BackToWindows();
        }

        private void WindowsReturnConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmReturnToWindows();
        }

        private void WindowsReturnCancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWindowsReturnConfirm();
        }

        private void WindowsReturnConfirmOverlay_BackdropTapped(object sender, TappedRoutedEventArgs e)
        {
            CloseWindowsReturnConfirm();
        }

        private void WindowsReturnConfirmContent_Tapped(object sender, TappedRoutedEventArgs e)
        {
            e.Handled = true;
        }
    }

    //nircmd code
    namespace NirCmdUtil
    {
        public static class NirCmdHelper
        {
            /// <summary>
            /// Executes a command using nircmd.exe.
            /// </summary>
            /// <param name="command">The command to pass to nircmd (e.g., "changesysvolume 5000")</param>
            public static void ExecuteCommand(string command)
            {
                // Determine the path to nircmd.exe in the current directory
                string nircmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nircmd.exe");

                if (!File.Exists(nircmdPath))
                {
                    throw new FileNotFoundException("nircmd.exe was not found in the current directory.");
                }

                // Configure ProcessStartInfo for nircmd
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = nircmdPath,
                    Arguments = command,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process { StartInfo = psi })
                {
                    process.Start();

                    // Optionally capture output and error streams
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        throw new Exception("Error executing nircmd command: " + error);
                    }
                }
            }
        }
    }
   
    //startup apps controll
    public static class StartupControl
    {
        private const string HKCU_Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string HKLM_Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string HKCU_Backup = @"Software\gcm\BackupStartup\HKCU";
        private const string HKLM_Backup = @"Software\gcm\BackupStartup\HKLM";
        private static readonly string StartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static readonly string StartupBackupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcm\\StartupBackup");

        public static void DisableAllStartupApps()
        {
            DisableRegistryStartup(Registry.CurrentUser, HKCU_Run, HKCU_Backup);
            DisableRegistryStartup(Registry.LocalMachine, HKLM_Run, HKLM_Backup);
            BackupAndClearStartupFolder();
        }

        public static void RestoreStartupApps()
        {
            RestoreRegistryStartup(Registry.CurrentUser, HKCU_Run, HKCU_Backup);
            RestoreRegistryStartup(Registry.LocalMachine, HKLM_Run, HKLM_Backup);
            RestoreStartupFolder();
        }

        private static void DisableRegistryStartup(RegistryKey root, string runPath, string backupPath)
        {
            using RegistryKey runKey = root.OpenSubKey(runPath, writable: true);
            using RegistryKey backupKey = root.CreateSubKey(backupPath);
            if (runKey == null || backupKey == null) return;

            foreach (string valueName in runKey.GetValueNames())
            {
                object value = runKey.GetValue(valueName);
                RegistryValueKind kind = runKey.GetValueKind(valueName);
                backupKey.SetValue(valueName, value, kind);
                runKey.DeleteValue(valueName);
            }
        }

        private static void RestoreRegistryStartup(RegistryKey root, string runPath, string backupPath)
        {
            using RegistryKey runKey = root.OpenSubKey(runPath, writable: true);
            using RegistryKey backupKey = root.OpenSubKey(backupPath, writable: true);
            if (runKey == null || backupKey == null) return;

            foreach (string valueName in backupKey.GetValueNames())
            {
                object value = backupKey.GetValue(valueName);
                RegistryValueKind kind = backupKey.GetValueKind(valueName);
                runKey.SetValue(valueName, value, kind);
            }
            root.DeleteSubKeyTree(backupPath);
        }

        private static void BackupAndClearStartupFolder()
        {
            if (!Directory.Exists(StartupFolder)) return;
            Directory.CreateDirectory(StartupBackupFolder);

            foreach (string file in Directory.GetFiles(StartupFolder))
            {
                string destFile = Path.Combine(StartupBackupFolder, Path.GetFileName(file));
                File.Move(file, destFile, overwrite: true);
            }
        }

        private static void RestoreStartupFolder()
        {
            if (!Directory.Exists(StartupBackupFolder)) return;
            Directory.CreateDirectory(StartupFolder);

            foreach (string file in Directory.GetFiles(StartupBackupFolder))
            {
                string destFile = Path.Combine(StartupFolder, Path.GetFileName(file));
                File.Move(file, destFile, overwrite: true);
            }

            Directory.Delete(StartupBackupFolder, recursive: true);
        }
    }
    // Taskbar
    /// <summary>
    /// Steuert die Sichtbarkeit der Taskleiste UND anderer Shell-Elemente (Startmenü, etc.)
    /// </summary>
    public static class TaskbarVisibility
    {
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
          int X, int Y, int cx, int cy, uint uFlags);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        // AKTUALISIERT: Flags hinzugefügt, um Neuzeichnen/Größenänderung zu verhindern
        const uint SWP_HIDEWINDOW = 0x0080;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        // NEU: Eine Hilfsmethode, um das Finden und Verstecken zu bündeln
        private static void HideWindowByClass(string className)
        {
            IntPtr windowHandle = FindWindow(className, null);
            if (windowHandle != IntPtr.Zero)
            {
                ShowWindow(windowHandle, SW_HIDE);
                SetWindowPos(windowHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_HIDEWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
            }
        }

        private static void HideWindowByTitle(string windowTitle)
        {
            IntPtr windowHandle = FindWindow(null, windowTitle);
            if (windowHandle != IntPtr.Zero)
            {
                ShowWindow(windowHandle, SW_HIDE);
                SetWindowPos(windowHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_HIDEWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
            }
        }

        // AKTUALISIERT: Diese Methode versteckt jetzt ALLES
        // This method now intelligently hides components based on settings
        // AKTUALISIERT: Versteckt NUR die Taskleiste, lässt aber Startmenü/Suche/Lautstärke in Ruhe
        public static void HideTaskbar()
        {
            // Load the settings
            bool enableTaskbar = false;

            try { enableTaskbar = AppSettings.Load<bool>("enable_taskbar"); } catch { }

            // Wenn die Taskleiste aktiviert sein soll, machen wir hier gar nichts
            if (enableTaskbar) return;

            // --- 1. Taskbar Hiding 
            // Main Taskbar
            HideWindowByClass("Shell_TrayWnd");

            // Taskbar on secondary monitors (Der Balken auf anderen Monitoren)
            HideWindowByClass("Shell_SecondaryTrayWnd");


            /*
            HideWindowByTitle("Search");
            HideWindowByTitle("Suche");
            HideWindowByClass("Windows.UI.Core.CoreWindow"); // startmenu
            HideWindowByClass("ControlCenter.Internal.Flyout"); // Info-Center
            HideWindowByClass("NativeHWNDHost"); // Oft Widgets oder Suche
            */
        }

        public static void ShowTaskbar()
        {
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero)
            {
                ShowWindow(taskbarHandle, SW_SHOW);
                // AKTUALISIERT: Flags hinzugefügt
                SetWindowPos(taskbarHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
            }

            // NEU: Auch die sekundäre Taskleiste wieder anzeigen
            IntPtr taskbarHandleSecondary = FindWindow("Shell_SecondaryTrayWnd", null);
            if (taskbarHandleSecondary != IntPtr.Zero)
            {
                ShowWindow(taskbarHandleSecondary, SW_SHOW);
                SetWindowPos(taskbarHandleSecondary, HWND_BOTTOM, 0, 0, 0, 0, SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
            }
        }
    }


    public static class ControllerLogger
    {
        private static readonly string LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
        private static readonly string Ps5LogPath = Path.Combine(LogFolder, "log_playstation.txt");
        private static readonly string XboxLogPath = Path.Combine(LogFolder, "log_xbox.txt");
        private static readonly long MaxLogSize = 10 * 1024 * 1024; // 10 MB

        public static void InitializeLogs()
        {
            try
            {
                if (File.Exists(Ps5LogPath)) File.Delete(Ps5LogPath);
                if (File.Exists(XboxLogPath)) File.Delete(XboxLogPath);
                Log("System", "Logs initialized and cleared.");
            }
            catch { }
        }

        public static void Log(string controller, string message)
        {
            string path = controller.ToLower().Contains("ps") || controller.ToLower().Contains("play") ? Ps5LogPath : XboxLogPath;
            try
            {
                FileInfo fi = new FileInfo(path);
                if (fi.Exists && fi.Length > MaxLogSize) File.Delete(path);

                string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(path, logLine);
            }
            catch { /* Nicht crashen wegen Logging */ }
        }
    }

    /// <summary>
    /// Eine Hilfsklasse, um den "Automatisch ausblenden"-Zustand der Windows-Taskleiste programmatisch zu steuern.
    /// </summary>
    public static class TaskbarManager
    {
        // Notwendige P/Invoke-Definitionen für die Windows Shell API
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        // Konstanten für die Befehle an SHAppBarMessage
        private const uint ABM_GETSTATE = 0x00000004; // Status abfragen
        private const uint ABM_SETSTATE = 0x0000000A; // Status setzen
        private const int ABS_AUTOHIDE = 0x0000001;  // Der Flag für "Automatisch ausblenden"

        private static bool? _originalState = null;

        /// <summary>
        /// Speichert den ursprünglichen Zustand und aktiviert dann das automatische Ausblenden.
        /// </summary>
        public static void EnableAutoHide()
        {
            // Speichere die ursprüngliche Einstellung des Benutzers, aber nur einmal.
            if (_originalState == null)
            {
                _originalState = GetAutoHideState();
            }

            // Aktiviere das automatische Ausblenden
            SetAutoHide(true);
        }

        /// <summary>
        /// Stellt den ursprünglichen Zustand der Einstellung wieder her, den der Benutzer vor dem App-Start hatte.
        /// </summary>
        public static void RestoreOriginalState()
        {
            // Wenn wir einen Zustand gespeichert haben, stelle ihn wieder her.
            if (_originalState.HasValue)
            {
                SetAutoHide(_originalState.Value);
            }
        }

        /// <summary>
        /// Fragt den aktuellen Zustand von "Automatisch ausblenden" ab.
        /// </summary>
        private static bool GetAutoHideState()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            uint state = (uint)SHAppBarMessage(ABM_GETSTATE, ref data);
            return (state & ABS_AUTOHIDE) == ABS_AUTOHIDE;
        }

        /// <summary>
        /// Setzt den Zustand von "Automatisch ausblenden".
        /// </summary>
        /// <param name="enable">True, um es zu aktivieren, False zum Deaktivieren.</param>
        private static void SetAutoHide(bool enable)
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));

            uint currentState = (uint)SHAppBarMessage(ABM_GETSTATE, ref data);

            if (enable)
            {
                // Füge den AutoHide-Flag hinzu
                data.lParam = (int)(currentState | ABS_AUTOHIDE);
            }
            else
            {
                // Entferne den AutoHide-Flag
                data.lParam = (int)(currentState & ~ABS_AUTOHIDE);
            }

            // Wende den neuen Zustand an
            SHAppBarMessage(ABM_SETSTATE, ref data);
            Debug.WriteLine($"Taskleiste 'Automatisch ausblenden' gesetzt auf: {enable}");
        }
    }
    /// <summary>
    /// Helperclass for applauncher.
    /// </summary>
    public class AppInfo
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string LaunchTarget { get; set; }
        public string LaunchKind { get; set; } = "Executable";
        public string SourceLabel { get; set; } = "DESKTOP";
        public string StableId { get; set; }
        public bool IsFavorite { get; set; }
        public string FavoriteGlyph => IsFavorite ? "★" : "☆";
        public double FavoriteOpacity => IsFavorite ? 1.0 : 0.38;
        public BitmapImage Icon { get; set; }
    }

   

}
