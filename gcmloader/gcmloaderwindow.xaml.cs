using Discord.WebSocket;
using GAMINGCONSOLEMODE;
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
using NAudio.CoreAudioApi.Interfaces;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NAudio.CoreAudioApi;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using HidSharp;
using Windows.UI;
using Windows.Gaming.Input;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using DualSenseAPI;
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
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Networking.Connectivity;
using Windows.System;
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
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                int volume = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);

                // Event kurz abbestellen, damit wir keine Endlosschleife erzeugen
                MasterVolumeSlider.ValueChanged -= MasterVolumeSlider_ValueChanged;
                MasterVolumeSlider.Value = volume;
                MasterVolumeSlider.ValueChanged += MasterVolumeSlider_ValueChanged;

                MasterVolumeText.Text = $"{volume}%";
                UpdateVolumeIcon(volume);
            }
            catch { }
        }

        // Wird aufgerufen, wenn der Slider bewegt wird
        private void MasterVolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            try
            {
                int newVolume = (int)e.NewValue;
                //MasterVolumeText.Text = $"{newVolume}%";
                UpdateVolumeIcon(newVolume);

                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                device.AudioEndpointVolume.MasterVolumeLevelScalar = newVolume / 100.0f;
            }
            catch { }
        }

        private void UpdateVolumeIcon(int volume)
        {
            if (volume == 0) MasterVolumeIcon.Glyph = "\uE74F"; // Mute
            else if (volume < 33) MasterVolumeIcon.Glyph = "\uE993"; // Low
            else if (volume < 66) MasterVolumeIcon.Glyph = "\uE994"; // Mid
            else MasterVolumeIcon.Glyph = "\uE995"; // High
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

                RefreshMixerList();
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

            HashSet<uint> processedPids = new HashSet<uint>();

            try
            {
                var enumerator = new MMDeviceEnumerator();
                MMDevice device;
                try
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                }
                catch
                {
                    return;
                }

                var sessionManager = device.AudioSessionManager;
                sessionManager.RefreshSessions();

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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioMixer CRITICAL ERROR]: {ex.Message}");
            }
        }

        // Creates a single row for the mixer
        private Border CreateMixerRow(string name, BitmapImage icon, AudioSessionControl session)
        {
            var border = new Border
            {
                Height = 70,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 0, 15, 0),
                // WICHTIG: Margin sorgt dafür, dass beim Zoomen nichts abgeschnitten wird
                Margin = new Thickness(12, 4, 12, 4),
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



        // Fokus Visualisierung für Audio Menü (MIT AUTO-SCROLLING)
        private void UpdateAudioVisualFocus()
        {
            // 1. Reset aller Listen-Elemente
            foreach (var btn in _audioDeviceButtons)
            {
                AnimateScale(btn, false);
                btn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
            foreach (var row in _audioMixerRows)
            {
                AnimateScale(row, false);
                row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }

            // 2. Reset Master Slider
            MasterVolumeContainer.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            MasterVolumeContainer.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255)); // Standard dunkel

            // 3. Highlight Logik
            if (_isMasterVolumeFocused)
            {
                // --- MASTER SLIDER FOKUS ---
                // Hellerer Hintergrund + Akzent-Rahmen
                MasterVolumeContainer.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
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
                        active.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255));
                        AnimateScale(active, true);
                        // Auto-Scroll Logic hier einfügen wenn nötig...
                    }
                }
                else
                {
                    if (_audioDeviceButtons.Count > _selectedAudioDeviceIndex && _selectedAudioDeviceIndex >= 0)
                    {
                        var active = _audioDeviceButtons[_selectedAudioDeviceIndex];
                        active.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255));
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
        private int _selectedAudioDeviceIndex = 0;

        // --- High-End Animation for the entire Sound Panel ---
        private async void OpenAudioFlyout()
{
    try
    {
        // 1. UI Reset
        ToggleAudioTab(false);
        _isMasterVolumeFocused = false;
        UpdateMasterVolumeUI();
        
        SimpleAudioList.Children.Clear();
        _audioDeviceButtons.Clear();

        // 2. Sichtbarkeit setzen (damit das Layout gerechnet wird), aber Opacity bleibt 0
        AudioOverlay.Visibility = Visibility.Visible;
        _currentFocusArea = FocusArea.AudioMenu;
        _selectedAudioDeviceIndex = 0;

        // --- HIER FEHLTE DIE LOGIK: Lade die Audio-Geräte ---
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

        MMDevice defaultDevice = null;
        try { defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); } catch { }

        foreach (var device in devices)
        {
            var btn = new Button
            {
                Tag = device.FriendlyName,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 60,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255)),
                BorderThickness = new Thickness(0)
            };

            var contentStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 15 };

            string glyph = device.FriendlyName.ToLower().Contains("headset") || device.FriendlyName.ToLower().Contains("kopfhörer") ? "\uE76B" : "\uE7F5";
            contentStack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });

            contentStack.Children.Add(new TextBlock
            {
                Text = device.FriendlyName,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (defaultDevice != null && device.ID == defaultDevice.ID)
            {
                btn.BorderThickness = new Thickness(2);
                btn.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
            }

            btn.Content = contentStack;
            btn.Click += (s, e) => SetAudioDevice(device.FriendlyName);

            _audioDeviceButtons.Add(btn);
            SimpleAudioList.Children.Add(btn);
        }
        // --------------------------------------------------

        // FIX FÜR DAS ABSCHNEIDEN: Dem System Zeit geben, die Buttons ins UI zu zeichnen!
        await Task.Delay(10);
        SimpleAudioList.UpdateLayout();

        // 3. Animation starten
        var sb = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(450);
        var easing = new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseOut };

        // Fade In
        var fadeIn = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(250) };
        Storyboard.SetTarget(fadeIn, AudioOverlay);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        // Slide Up
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
        sb.Begin();

        UpdateVisualFocus();
    }
    catch (Exception ex) 
    { 
        Debug.WriteLine("Audio Error: " + ex.Message); 
    }
}

        private void CloseAudioFlyout()
        {
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
                AudioOverlay.Visibility = Visibility.Collapsed;
                _currentFocusArea = FocusArea.TopButtons;
                UpdateVisualFocus();
            };

            sb.Begin();
        }

        private void ToggleAudioFlyout()
        {
         
            if (AudioOverlay.Visibility == Visibility.Visible) CloseAudioFlyout();
            else OpenAudioFlyout();
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
            PlayActivationSound();
            CloseAudioFlyout();
        }

        #endregion soundcontrol
        #region psdualsense
        // PS5 HID Hardware
        private HidDevice _ps5Device;
        private HidStream _ps5Stream;
        private byte[] _hidInputBuffer = new byte[64];

        // Navigation State für PlayStation (Index 4 reserviert)
        private GamepadButtonFlags _lastPs5ButtonState = GamepadButtonFlags.None;
        private DateTime _ps5NextAllowedInputTime = DateTime.MinValue;
        private bool _ps5StickCentered = true;
        private GamepadButtonFlags[] _lastShortcutButtons = new GamepadButtonFlags[5];

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

            // Give the system a tiny moment to stabilize the new resolution
            Task.Run(async () => {
                await Task.Delay(500);
                this.DispatcherQueue.TryEnqueue(() => {
                    ForceDpiRedraw();
                });
            });
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
                    // We use the Dispatcher with a tiny delay. If we redraw too fast, 
                    // the system might still be reporting the OLD scale values.
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);
                        Debug.WriteLine("[GCM] Scaling change detected via XamlRoot. Redrawing...");
                        ForceDpiRedraw();
                    });
                };
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
                int pWidth = Vanara.PInvoke.User32.GetSystemMetrics(
                                 Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
                int pHeight = Vanara.PInvoke.User32.GetSystemMetrics(
                                 Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);
                if (pWidth > 0 && pHeight > 0)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, pWidth, pHeight, 0x0040 | 0x0020);
                }
                UpdateScale();
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
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
            return dpi / 96.0;
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
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                uint dpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
                double dpiScale = dpi / 96.0;
                if (dpiScale <= 0) return;
                int pWidth = Vanara.PInvoke.User32.GetSystemMetrics(
                                 Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
                int pHeight = Vanara.PInvoke.User32.GetSystemMetrics(
                                 Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);
                if (pWidth <= 0 || pHeight <= 0) return;
                double logicalW = pWidth / dpiScale;
                double logicalH = pHeight / dpiScale;
                const double baseW = 1920.0;
                const double baseH = 1080.0;
                double ratio = Math.Min(logicalW / baseW, logicalH / baseH);
                if (ratio <= 0) return;
                MainContent.Width = baseW;
                MainContent.Height = baseH;
                MainContent.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center;
                MainContent.VerticalAlignment = VerticalAlignment.Center;
                MainContent.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                MainContent.RenderTransform = new ScaleTransform
                {
                    ScaleX = ratio,
                    ScaleY = ratio
                };
                Debug.WriteLine($"[Scale] {pWidth}x{pHeight} / {dpiScale:F2} = {logicalW:F0}x{logicalH:F0} → {ratio:F4}");
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
            // Wir geben Windows 500ms Zeit, um die neuen Skalierungswerte in die Registry zu schreiben
            Task.Run(async () =>
            {
                await Task.Delay(500);

                this.DispatcherQueue.TryEnqueue(async () =>
                {
                    Debug.WriteLine("[GCM] Automatische Skalierungsänderung erkannt. Führe Hard-Resync aus...");

                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                    // Aktuelle DPI speichern
                    uint currentDpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
                    _lastKnownDpi = currentDpi;

                    if (MainContent != null)
                    {
                        // 1. Layout-Cache leeren
                        MainContent.Visibility = Visibility.Collapsed;

                        // 2. Fenster physisch anpassen (Ohne Fokus zu klauen!)
                        int pWidth = Vanara.PInvoke.User32.GetSystemMetrics(Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
                        int pHeight = Vanara.PInvoke.User32.GetSystemMetrics(Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);

                        // 0x0040 = SWP_SHOWWINDOW
                        // 0x0020 = SWP_FRAMECHANGED (Zwingt Windows zum DPI-Refresh)
                        // 0x0010 = SWP_NOACTIVATE (Klaut keinen Fokus!)
                        // 0x0004 = SWP_NOZORDER (Bleibt im Hintergrund, falls es dort ist)
                        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, pWidth, pHeight, 0x0040 | 0x0020 | 0x0010 | 0x0004);

                        // 3. Dem Buffer Zeit geben
                        await Task.Delay(100);

                        // 4. Unsere magische Skalierungs-Logik anwenden
                        UpdateScale();

                        // 5. UI wieder einblenden
                        MainContent.Visibility = Visibility.Visible;
                    }

                    // 6. XAML Engine zwingen, neu zu zeichnen
                    if (this.Content is FrameworkElement root)
                    {
                        root.InvalidateMeasure();
                        root.UpdateLayout();
                    }
                });
            });
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
        public enum ControllerType { Xbox, PlayStation }
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
                        bitmap.DecodePixelWidth = 300; // Performance!

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


        private async Task<string> FindLocalSteamImageAsync(string exePath)
        {
            Logger.Log($"[DEBUG] FindLocalSteamImageAsync: Searching strict vertical cover for: {exePath}");
            try
            {
                // 1. AppID ermitteln (nötig für den Steam Cache)
                string steamAppId = await Task.Run(() => GetSteamAppIdFromExePath(exePath));
                string steamInstallPath = GetSteamInstallPath();

                if (string.IsNullOrEmpty(steamInstallPath) || string.IsNullOrEmpty(steamAppId))
                {
                    return null;
                }

                // 2. Der Pfad zum Steam Library Cache
                // Hier liegen die von Steam selbst heruntergeladenen Cover
                string appCacheDirectory = Path.Combine(steamInstallPath, "appcache", "librarycache");

                // Steam speichert die Hochkant-Cover (600x900) meistens direkt in diesem Ordner
                // Dateiname ist meistens: {AppID}_library_600x900.jpg
                string directVerticalPath = Path.Combine(appCacheDirectory, $"{steamAppId}_library_600x900.jpg");

                if (File.Exists(directVerticalPath))
                {
                    Logger.Log($"[DEBUG] Success! Found official Steam vertical cover: {directVerticalPath}");
                    return directVerticalPath;
                }

                // 3. Fallback: Manchmal liegen sie in Unterordnern (alte Logik, aber strenger gefiltert)
                // Wir suchen im Cache-Ordner, aber NUR nach Dateien, die "600x900" im Namen haben.
                // Wir ignorieren "header.jpg", "hero.jpg", etc., da diese das Bild verzerren würden.
                string strictSearchPath = Path.Combine(steamInstallPath, "appcache", "librarycache", steamAppId);
                if (Directory.Exists(strictSearchPath))
                {
                    // Suche rekursiv nach der korrekten Auflösung
                    var files = Directory.GetFiles(strictSearchPath, "*600x900.jpg", SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        Logger.Log($"[DEBUG] Found vertical cover in subfolder: {files[0]}");
                        return files[0];
                    }
                }

                Logger.Log("[DEBUG] No local VERTICAL image found. (Ignoring banners to prevent distortion)");
                return null; // Zwingt das System, online bei SteamGridDB zu suchen
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

        private List<Border> _launcherAreaButtons;
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
        private bool _lastFocusState = true;

        private DispatcherTimer _focusCheckTimer;
        private DispatcherTimer _minimizeGracePeriodTimer;
        private bool _isOverlayActive = false;
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
        private enum FocusArea { Launcher, QuickLaunchers, Cards, TopButtons, PowerMenu, AppLauncher, AudioMenu, ImageSelection, GameOptions, StartupVideo }
        private List<Border> _quickLauncherButtons;
        private int _selectedQuickLauncherIndex = 0;
        private FocusArea _currentFocusArea = FocusArea.Cards;

        // Index und Liste für die oberen Buttons
        private int _selectedTopButtonIndex = 0;
        private List<Button> _topButtons;
        private List<Button> _powerMenuItems; 
        private int _selectedPowerMenuItemIndex = 0; 



        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_OVERLAPPEDWINDOW = 0x00CF0000L;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
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
            this.InitializeComponent();

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

            // Safely load the launcher setting
            string currentLauncher = "steam";
            try { currentLauncher = AppSettings.Load<string>("launcher"); }
            catch (Exception ex) { Debug.WriteLine($"[Startup] Could not load launcher setting: {ex.Message}"); }

            string bootLogoPath = currentLauncher switch
            {
                "steam" => "ms-appx:///Assets/steam_logo.png",
                "playnite" => "ms-appx:///Assets/playnite_logo.png",
                "xbox" => "ms-appx:///Assets/xbox_logo.png",
                "gfn" => "ms-appx:///Assets/geforcenow.png",
                _ => "ms-appx:///Assets/gcm_ui_logo.png"
            };

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
                    FocusSink.Focus(FocusState.Programmatic);

                    if (!_isVideoPlaybackInitiated)
                    {
                        _isVideoPlaybackInitiated = true;
                        PlayStartupVideo();
                    }
                };
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _soundCache["nav"] = new Uri(Path.Combine(baseDir, "Assets\\nav.wav"));
            _soundCache["play"] = new Uri(Path.Combine(baseDir, "Assets\\play.wav"));
            _soundCache["pause"] = new Uri(Path.Combine(baseDir, "Assets\\pause.wav"));

            _autoMouseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _autoMouseTimer.Tick += AutoMouseEngine_Tick;
            _autoMouseTimer.Start();

            try
            {
                string apiKey = AppSettings.Load<string>("steamgriddb_api_key");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _steamGridHelper = new SteamGridDBHelper(null);
                }
                else
                {
                    _steamGridHelper = new SteamGridDBHelper(apiKey);
                    Directory.CreateDirectory(_imageCachePath);
                }
            }
            catch { _steamGridHelper = new SteamGridDBHelper(null); }

            var controllerBatteryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            controllerBatteryTimer.Tick += (s, e) => UpdateControllerBatteryStatus();
            controllerBatteryTimer.Start();
            UpdateControllerBatteryStatus();

            MinimizeAllToDesktop();
            LoadDynamicLauncherCards();
            _quickLauncherButtons = new List<Border> { QuickSteam, QuickPlaynite, QuickXbox, QuickGfn };
            QuickLauncherPanel.Visibility = Visibility.Visible; // Damit es Layout-Platz einnimmt

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

            StartAsynctasks();
            _appStartTime = DateTime.UtcNow;
            SetupWindowEngine();

            // Wir rufen hier Activate auf, was wiederum MainWindow_Activated triggert
            this.Activate();
        }

        #region App Launcher Logic
        private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Get the current search text
            string searchText = AppSearchBox.Text;

            // Create a new, filtered list from the master "AllInstalledApps" list
            var filteredList = AllInstalledApps
                .Where(app => app.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Assign the filtered list as the new data source for the GridView
            AppGridView.ItemsSource = filteredList;

            // Show the "No results" message only if the search is active but finds nothing
            if (string.IsNullOrEmpty(searchText))
            {
                NoSearchResultsText.Visibility = Visibility.Collapsed;
                // Show the original "No apps" message if the master list is empty
                NoAppsFoundText.Visibility = (AllInstalledApps.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                NoSearchResultsText.Visibility = (filteredList.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                NoAppsFoundText.Visibility = Visibility.Collapsed;
            }
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
            Debug.WriteLine("[AppLauncher] Starting ROBUST HYBRID scan for all applications...");
            isAppListLoaded = false;
            AllInstalledApps.Clear();

            DispatcherQueue.TryEnqueue(() =>
            {
                AppLoadingRing.IsActive = true;
                AppLoadingRing.Visibility = Visibility.Visible;
                NoAppsFoundText.Visibility = Visibility.Collapsed;
            });

            var appData = await Task.Run(() =>
            {
                var discoveredApps = new List<(string Name, string FilePath)>();
                var seenDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // =====================================================================
                // TEIL 1: REGISTRY-SCAN (stabil und schnell)
                // =====================================================================
                // (Dieser Teil bleibt unverändert und funktioniert ja bei dir)
                string[] registryPaths = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
                RegistryKey[] rootKeys = { Registry.CurrentUser, Registry.LocalMachine };
                foreach (var rootKey in rootKeys) { /* ... Deine komplette Registry-Logik von vorhin hier ... */ }

                // =================================================================================
                // TEIL 2: MANUELLER STARTMENÜ-SCAN (immun gegen "Access Denied")
                // =================================================================================
                Debug.WriteLine("[AppLauncher] ----- Starting Part 2: Manual & Robust Start Menu (.lnk) Scan -----");
                string[] startMenuPaths = {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

                foreach (var path in startMenuPaths)
                {
                    // Wir starten die neue, rekursive Suchfunktion
                    ScanFolderForLnks(path, discoveredApps, seenDisplayNames);
                }

                return discoveredApps.OrderBy(a => a.Name).ToList();
            });

            foreach (var app in appData)
            {
                AllInstalledApps.Add(new AppInfo
                {
                    Name = app.Name,
                    FilePath = app.FilePath,
                    Icon = GetAppIconAsBitmapImage(app.FilePath)
                });
            }

            Debug.WriteLine($"[AppLauncher] Hybrid Scan finished. Total found: {AllInstalledApps.Count} applications.");
            isAppListLoaded = true;

            DispatcherQueue.TryEnqueue(() =>
            {
                AppLoadingRing.IsActive = false;
                AppLoadingRing.Visibility = Visibility.Collapsed;
                NoAppsFoundText.Visibility = AllInstalledApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (AppLauncher.Visibility == Visibility.Visible && AppGridView.Items.Count > 0)
                {
                    if (AppGridView.SelectedIndex == -1) AppGridView.SelectedIndex = 0;
                }
            });
        }

        private void ScanFolderForLnks(string folderPath, List<(string Name, string FilePath)> discoveredApps, HashSet<string> seenDisplayNames)
        {
            try
            {
                // Schritt 1: Hole alle .lnk-Dateien NUR in der aktuellen Ordnerebene
                foreach (var lnkFile in Directory.GetFiles(folderPath, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(lnkFile);
                    if (!string.IsNullOrWhiteSpace(name) && !name.ToLower().Contains("uninstall") && seenDisplayNames.Add(name))
                    {
                        discoveredApps.Add((name, lnkFile));
                    }
                }

                // Schritt 2: Gehe in jeden Unterordner und rufe diese Funktion für ihn erneut auf
                foreach (var subFolderPath in Directory.GetDirectories(folderPath))
                {
                    ScanFolderForLnks(subFolderPath, discoveredApps, seenDisplayNames);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Das ist der entscheidende Punkt: Wenn wir auf einen geschützten Ordner stoßen,
                // protokollieren wir das und machen einfach weiter, anstatt abzubrechen.
                Debug.WriteLine($"[AppLauncher] SKIPPED protected folder: {folderPath}");
            }
            catch (Exception ex)
            {
                // Fange andere mögliche Dateisystemfehler ab
                Debug.WriteLine($"[AppLauncher] Error scanning folder '{folderPath}': {ex.Message}");
            }
        }

        // NEU: Unsere eigene Methode zum Auslesen von .lnk-Dateien
        private string ResolveLnkShortcut(string lnkPath)
        {
            var shellLink = (IShellLinkW)new ShellLink();
            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(lnkPath, 0); // STGM_READ

            var sb = new StringBuilder(1024);
            shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);

            return sb.ToString();
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
                if (!string.IsNullOrEmpty(exePath) && _gamePathKeywords.Any(keyword => exePath.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
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
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // VRR / Schwarzbild Fix: Erzwinge Standard-Desktop-Rendering (bleibt wie es ist)
            int disableFullscreenTransform = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref disableFullscreenTransform, sizeof(int));
            int policy = DWMFLIP_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_FLIP3D_POLICY, ref policy, sizeof(int));

            // --- DEINE NEUE LOGIK: DER SMARTE SKALIERUNGS-CHECK ---
            uint currentDpi = Vanara.PInvoke.User32.GetDpiForWindow(hwnd);
            int currentWidth = Vanara.PInvoke.User32.GetSystemMetrics(Vanara.PInvoke.User32.SystemMetric.SM_CXSCREEN);
            int currentHeight = Vanara.PInvoke.User32.GetSystemMetrics(Vanara.PInvoke.User32.SystemMetric.SM_CYSCREEN);

            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Hat sich die Skalierung oder Auflösung seit dem letzten Mal geändert? (Oder ist es der allererste Start?)
            if (_lastCheckedDpi == 0 || _lastCheckedDpi != currentDpi || _lastCheckedWidth != currentWidth || _lastCheckedHeight != currentHeight)
            {
                Debug.WriteLine("[GCM] Skalierungsänderung erkannt. Wende Fullscreen neu an...");

                // Wenn er schon im Fullscreen ist, zwingen wir ihn kurz raus und wieder rein.
                // Das sorgt dafür, dass sich die Skalierung repariert.
                if (appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                {
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
                }
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);

                // Neue Werte speichern, damit er beim nächsten Mal nicht mehr zuckt
                _lastCheckedDpi = currentDpi;
                _lastCheckedWidth = currentWidth;
                _lastCheckedHeight = currentHeight;
            }
            else
            {
                // Skalierung ist exakt gleich geblieben!
                // Wir setzen Fullscreen NUR, wenn das Fenster (warum auch immer) nicht mehr im Fullscreen sein sollte.
                // -> KEIN BLINDES NEU-SETZEN MEHR = KEIN ZUCKEN MEHR!
                if (appWindow.Presenter.Kind != Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen)
                {
                    appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                }
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
            bool isSuspended = ProcessSuspender.IsProcessSuspended(entry.Proc.Id);
            if (isSuspended)
            {
                TxtSuspendTitle.Text = "Resume Game";
                TxtSuspendDesc.Text = "Continue playing where you left off";
                IconSuspend.Glyph = "\uE768"; // Play Icon
            }
            else
            {
                TxtSuspendTitle.Text = "Suspend Game";
                TxtSuspendDesc.Text = "Freezes the game to save resources";
                IconSuspend.Glyph = "\uE769"; // Pause Icon
            }

            // Fokus auf ersten Button
            BtnSuspendGame.Focus(FocusState.Programmatic);
            UpdateVisualFocus();
        }

        private void BtnSuspendGame_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditingCardEntry == null) return;

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
            ImageSearchBox.Text = _currentEditingCardEntry.ProductName;
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
            _currentFocusArea = FocusArea.Cards;
            _currentEditingCardEntry = null;
            UpdateVisualFocus();
        }

        private void GameOptionsOverlay_BackdropTapped(object sender, TappedRoutedEventArgs e)
        {
            CloseGameOptions();
        }


        /// <summary>
        /// Handles the search button click. Fetches vertical covers from SteamGridDB.
        /// </summary>
        private async void ImageSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Content.XamlRoot == null) return;

            if (_steamGridHelper == null || !_steamGridHelper.IsApiKeySet)
            {
                NoImagesFoundText.Text = "Error: API Key is missing in settings.toml";
                NoImagesFoundText.Visibility = Visibility.Visible;
                return;
            }

            string searchTerm = ImageSearchBox.Text;
            if (string.IsNullOrWhiteSpace(searchTerm)) return;

            ImageSearchProgress.Visibility = Visibility.Visible;
            NoImagesFoundText.Visibility = Visibility.Collapsed;
            ImageResultsGrid.ItemsSource = null;

            try
            {
                string cleanedName = CleanGameNameForSearch(searchTerm);

                // Debug-Text während der Suche
                NoImagesFoundText.Text = $"Searching ID for '{cleanedName}'...";
                NoImagesFoundText.Visibility = Visibility.Visible;

                var searchResult = await _steamGridHelper.SearchForGameIdAsync(cleanedName);

                if (searchResult != null)
                {
                    var urls = await _steamGridHelper.GetVerticalImagesForGameAsync(searchResult.id);
                    _currentImageSearchResults = urls;

                    if (urls.Count > 0)
                    {
                        ImageResultsGrid.ItemsSource = urls;
                        _selectedImageGridIndex = 0;
                        ImageResultsGrid.SelectedIndex = 0;
                        ImageResultsGrid.Focus(FocusState.Programmatic);
                        NoImagesFoundText.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        NoImagesFoundText.Text = $"Game found (ID: {searchResult.id}), but no vertical images.";
                        NoImagesFoundText.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    NoImagesFoundText.Text = $"Game '{cleanedName}' not found on SteamGridDB.";
                    NoImagesFoundText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                // HIER wird der echte Fehler angezeigt!
                NoImagesFoundText.Text = $"API Error: {ex.Message}";
                NoImagesFoundText.Visibility = Visibility.Visible;
            }
            finally
            {
                ImageSearchProgress.Visibility = Visibility.Collapsed;
            }
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
            _currentFocusArea = FocusArea.Cards; // Return focus to cards
            _currentEditingCardEntry = null;
            UpdateVisualFocus(); // Refresh highlights
        }

        #endregion
        #region methodes for code

        public static void SendOverlayNotification(string message)
        {
            try
            {
                bool shortcutpopup = AppSettings.Load<bool>("shortcutpopup");

                if (shortcutpopup)
                {
                    // 1. Sound abspielen
                    try
                    {
                        string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "shortcut.wav");
                        if (System.IO.File.Exists(soundPath))
                        {
                            new System.Media.SoundPlayer(soundPath).Play();
                        }
                    }
                    catch { }

                    // 2. Das neue Popup-Fenster erstellen und anzeigen
                    // WICHTIG: Wir nutzen App.m_window.DispatcherQueue, um auf den UI-Thread zu kommen.
                    if (App.m_window != null)
                    {
                        App.m_window.ShowInAppNotification(message);
                    }
                }
            }
            catch
            {
                AppSettings.Save("shortcutpopup", true);
            }
        }

        public void ShowInAppNotification(string message)
        {
            // Wir müssen sicherstellen, dass wir auf dem UI-Thread sind
            this.DispatcherQueue.TryEnqueue(() =>
            {
                // 1. Das neue Notification-Control erstellen
                var notification = new InAppNotification(message);

                // 2. Event abonnieren: Wenn die Animation fertig ist...
                notification.AnimationFinished += (s, args) =>
                {
                    // ...entfernen wir das Element wieder aus der Liste (Speicher freigeben)
                    NotificationStack.Children.Remove(notification);
                };

                // 3. Zum StackPanel hinzufügen (erscheint sofort und animiert sich rein)
                NotificationStack.Children.Add(notification);
            });
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
        #region lossless
        private const int SW_MINIMIZEE = 6;

        // NOTE: This method is now 'async Task'
        public static async Task StartLosslessScaling()
        {
            try
            {
                // 1. Your existing checks remain the same - this is good practice.
                if (!AppSettings.Load<bool>("lossless"))
                {
                    Console.WriteLine("Lossless Scaling auto-start is disabled. Skipping.");
                    return;
                }

                string losslessPath = AppSettings.Load<string>("losslesspath");
                if (string.IsNullOrEmpty(losslessPath) || !File.Exists(losslessPath))
                {
                    Console.WriteLine($"Error: The path to Lossless Scaling is invalid: {losslessPath}");
                    return;
                }

                string processName = Path.GetFileNameWithoutExtension(losslessPath);
                if (Process.GetProcessesByName(processName).Any())
                {
                    Console.WriteLine("Lossless Scaling is already running.");
                    return;
                }

                // 2. Start the process WITHOUT the WindowStyle suggestion.
                Console.WriteLine("Starting Lossless Scaling...");
                Process process = Process.Start(losslessPath);

                // 3. Wait for the process to create its main window.
                // We give it up to 5 seconds to appear.
                for (int i = 0; i < 50; i++)
                {
                    // The Refresh() is important to get the latest process details.
                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        break; // Window found, exit the loop.
                    }
                    await Task.Delay(100);
                }

                // 4. Forcefully minimize the window.
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    Console.WriteLine("Lossless Scaling window found. Forcing it to minimize.");
                    ShowWindow(process.MainWindowHandle, SW_MINIMIZEE);
                }
                else
                {
                    Console.WriteLine("Could not find the main window for Lossless Scaling after starting it.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred while starting Lossless Scaling: {ex.Message}");
            }
        }
        #endregion lossless
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
        public void prestartlist()
        {
            try
            {
                bool prestartlist = AppSettings.Load<bool>("usepreloadlist");

                if (prestartlist == true)
                {
                    string prestartlistpath = AppSettings.Load<string>("prealoadlistpath");

                    if (File.Exists(prestartlistpath))
                    {
                        string[] lines = File.ReadAllLines(prestartlistpath);

                        foreach (var line in lines)
                        {
                            string entry = line.Trim();

                            // Skip empty lines or comments
                            if (string.IsNullOrWhiteSpace(entry) || entry.StartsWith("#"))
                                continue;

                            try
                            {
                                // Open URLs in default browser
                                if (entry.StartsWith("http://") || entry.StartsWith("https://"))
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = entry,
                                        UseShellExecute = true
                                    });
                                }
                                // Run executable files
                                else if (entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (File.Exists(entry))
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = entry,
                                            UseShellExecute = true
                                        });
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Executable not found: {entry}");
                                    }
                                }
                                // Open other files (e.g., images, txt, etc.)
                                else if (File.Exists(entry))
                                {
                                    Process.Start(new ProcessStartInfo
                                    {
                                        FileName = entry,
                                        UseShellExecute = true
                                    });
                                }
                                else
                                {
                                    Console.WriteLine($"File not found: {entry}");
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue
                                Console.WriteLine($"Error with entry '{entry}': {ex.Message}");
                            }
                        }

                        Console.WriteLine("Finished running preload list.");
                    }
                    else
                    {
                        Console.WriteLine("prestartlist not found");
                    }
                }
                else
                {
                    Console.WriteLine("no prestartlist set");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error in preload list processing: {ex.Message}");
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
                    BackgroundImage.Source = null;
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

                    // Sicherer Bild-Ladevorgang
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.UriSource = new Uri(cleanPath, UriKind.Absolute);
                        BackgroundImage.Source = bitmap;
                        BackgroundImage.Visibility = Visibility.Visible;
                        BackgroundVideoPlayer.Visibility = Visibility.Collapsed;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[Wallpaper] Fehler beim Laden des Bildes: " + ex.Message);
                        // Letzter Notnagel: Hintergrund schwarz/leer lassen statt Absturz
                        BackgroundImage.Source = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Wallpaper Critical Error] {ex.Message}");
            }
        }

        private void SetupLiveWallpaper(string videoPath)
        {
            try
            {
                StopLiveWallpaper();

                var player = new Windows.Media.Playback.MediaPlayer();

                // OPTIMIERUNG 1: Video-Eigenschaften für Performance setzen
                player.IsVideoFrameServerEnabled = false; // Wir brauchen keinen Zugriff auf einzelne Frames
                player.AudioCategory = MediaPlayerAudioCategory.Other;

                // OPTIMIERUNG 2: Hardware-Dekodierung bevorzugen
                // Das entlastet die CPU, auf der auch dein Fenster-Scanner läuft
                player.Source = MediaSource.CreateFromUri(new Uri(videoPath, UriKind.Absolute));

                player.IsLoopingEnabled = true;
                player.IsMuted = true;

                BackgroundVideoPlayer.SetMediaPlayer(player);

                // WICHTIG: Den Player-Typ auf 'Hardware' zwingen (über das UI Element)
                BackgroundVideoPlayer.Opacity = 1.0;

                player.Play();
            }
            catch (Exception ex) { Debug.WriteLine($"[Wallpaper] Error: {ex.Message}"); }
        }

        private void StopLiveWallpaper()
        {
            try
            {
                var player = BackgroundVideoPlayer.MediaPlayer;
                if (player != null)
                {
                    player.Pause();
                    BackgroundVideoPlayer.SetMediaPlayer(null);
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
        public static void displayfusion(string art)
        {

            try
            {
                // Function
                // Full path to DisplayFusionCommand.exe
                string displayFusionCommandPath = @"C:\Program Files\DisplayFusion\DisplayFusionCommand.exe";

                // Check if DisplayFusionCommand.exe exists
                if (!File.Exists(displayFusionCommandPath))
                {
                    Console.WriteLine("DisplayFusionCommand.exe not found at the expected location or not set");
                    return;
                }
                // Check if action is "start"
                if (art == "start")
                {
                    bool usedisplayfusion = AppSettings.Load<bool>("usedisplayfusion");

                    if (usedisplayfusion == true)
                    {
                        // Get start profile
                        string startprofil = AppSettings.Load<string>("usedisplayfusion_start");

                        if (!string.IsNullOrEmpty(startprofil))
                        {
                            // Command to load the profile using DisplayFusion Command Line
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = displayFusionCommandPath,
                                Arguments = $"-monitorloadprofile \"{startprofil}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            });

                            Console.WriteLine($"Loaded DisplayFusion profile: {startprofil}");
                            // sleep 
                        }
                        else
                        {
                            Console.WriteLine("No start profile configured. Skipping...");
                        }
                    }
                    else
                    {
                        Console.WriteLine("DisplayFusion integration is disabled.");
                    }
                }
                else
                {
                    // Action is "end"
                    bool usedisplayfusion = AppSettings.Load<bool>("usedisplayfusion");

                    if (usedisplayfusion == true)
                    {
                        // Get end profile
                        string endprofil = AppSettings.Load<string>("usedisplayfusion_end");

                        if (!string.IsNullOrEmpty(endprofil))
                        {
                            // Command to load the profile using DisplayFusion Command Line
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = displayFusionCommandPath,
                                Arguments = $"-monitorloadprofile \"{endprofil}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            });

                            Console.WriteLine($"Loaded DisplayFusion profile: {endprofil}");

                        }
                        else
                        {
                            Console.WriteLine("No end profile configured. Skipping...");
                        }
                    }
                    else
                    {
                        Console.WriteLine("DisplayFusion integration is disabled.");
                    }
                }
            }
            catch
            {
                Console.WriteLine("DisplayFusion problem-");
            }
        }
        static void IsJoyxoffInstalledAndStart()
        {
            try
            {
                bool joyxofftogglestatus = AppSettings.Load<bool>("usejoyxoff");
                if (joyxofftogglestatus == true)
                {
                    string joyxoffExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Joyxoff", "Joyxoff.exe");
                    try
                    {
                        if (File.Exists(joyxoffExePath))
                        {
                            Process.Start(joyxoffExePath);
                        }
                    }
                    catch
                    {

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
            // Restore essential settings before restarting Explorer
            TaskbarManager.RestoreOriginalState();
            TaskManagerReEnableServices();
            MakeSelfNonTopmost();
            Console.WriteLine("Exit-Button clicked. Restoring desktop and exiting app...");

            // Unregister to prevent crashes during shutdown
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            try
            {
                // 1. Reset shell to explorer.exe in Registry
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true))
                {
                    if (key != null)
                    {
                        key.SetValue("Shell", "explorer.exe", RegistryValueKind.String);
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
            displayfusion("end");
            KillProcess("PluginLoader_noconsole.exe");
            Console.WriteLine("PluginLoader_noconsole killed");
            CleanupLogging();
            preaudio(false, true);

            // Restore UAC settings
            try
            {
                if (AppSettings.Load<bool>("uac"))
                {
                    uac("on");
                }
            }
            catch { uac("on"); }

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
            MakeSelfNonTopmost();
            Debug.WriteLine($"[GCM] Quick-Launch ausgelöst für: '{launcherId}'...");

            try
            {
                switch (launcherId)
                {
                    case "steam":
                        await StartSteam(false); // <--- false: Sanfter Wechsel!
                        break;
                    case "gfn":
                        await StartGfn();
                        break;
                    case "xbox":
                        await StartXbox();
                        break;
                    case "playnite":
                        // FIX: Kein Protokoll-Befehl mehr! Direkt unsere Fullscreen-Methode aufrufen.
                        await StartPlaynite();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler beim Quick-Launch: {ex.Message}");
            }
        }

        private async void SwitchToConfiguredLauncher()
        {
            MakeSelfNonTopmost();

            string launcher = AppSettings.Load<string>("launcher");
            Debug.WriteLine($"[GCM] Wechsle zu konfiguriertem Launcher: '{launcher}'...");

            try
            {
                switch (launcher)
                {
                    case "steam":
                        await StartSteam(false); 
                        return;

                    case "gfn":
                        await StartGfn();
                        return;

                    case "playnite":
                        // FIX: Auch hier jeden Protokoll-Befehl entfernt!
                        await StartPlaynite();
                        return;

                    case "custom":
                        string customPath = AppSettings.Load<string>("customlauncherpath");
                        string customProcessName = Path.GetFileNameWithoutExtension(customPath);
                        Process customProc = Process.GetProcessesByName(customProcessName).FirstOrDefault();
                        if (customProc != null && customProc.MainWindowHandle != IntPtr.Zero)
                        {
                            await ForcefullyBringToForeground(customProc.MainWindowHandle);
                        }
                        else
                        {
                            await StartOtherLauncher();
                        }
                        return;

                    case "xbox":
                        await StartXbox(false);
                        return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler während des Launcher-Wechsels: {ex.Message}");
            }
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
        private async void ConsoleModeToShell()
        {
            // Die Pfade zur Windows Shell Registry
            const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            const string valueName = "Shell";

            try
            {
                // 1. SCHUTZ: Prüfen, ob wir Admin-Rechte haben. Ohne diese stürzt die App bei Registry-Schreibzugriffen ab.
                if (!IsAdministrator())
                {
                    Debug.WriteLine("[ConsoleMode] ERROR: Keine Administratorrechte. Überspringe Shell-Registrierung.");
                    return;
                }

                // 2. PFAD ERMITTELN: Pfad der aktuellen .exe holen und in Anführungszeichen setzen
                string targetExecutable = Process.GetCurrentProcess().MainModule.FileName;
                if (!targetExecutable.StartsWith("\""))
                {
                    targetExecutable = $"\"{targetExecutable}\"";
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

                string launcher = "steam"; // Fallback
                try { launcher = AppSettings.Load<string>("launcher"); } catch { }

                switch (launcher)
                {
                    case "steam":
                        string steamPath = "";
                        try { steamPath = AppSettings.Load<string>("steamlauncherpath"); } catch { }
                        if (!string.IsNullOrEmpty(steamPath) && !File.Exists(steamPath))
                            Debug.WriteLine("[Settings] The Steam path in settings is invalid, but continuing startup.");
                        break;

                    case "playnite":
                        string playnitePath = "";
                        try { playnitePath = AppSettings.Load<string>("playnitelauncherpath"); } catch { }
                        if (!string.IsNullOrEmpty(playnitePath) && !File.Exists(playnitePath))
                            Debug.WriteLine("[Settings] The Playnite path in settings is invalid, but continuing startup.");
                        break;

                    case "custom":
                        string customPath = "";
                        try { customPath = AppSettings.Load<string>("customlauncherpath"); } catch { }
                        if (!string.IsNullOrEmpty(customPath) && !File.Exists(customPath))
                            Debug.WriteLine("[Settings] The Custom Launcher path is invalid, but continuing startup.");
                        break;

                    case "xbox":
                        // Xbox needs no path, started via protocol
                        break;

                    case "gfn":
                        string gfnPath = "";
                        try { gfnPath = AppSettings.Load<string>("gfnlauncherpath"); } catch { }
                        if (!string.IsNullOrEmpty(gfnPath) && !File.Exists(gfnPath))
                            Debug.WriteLine("[Settings] The GeForce Now path is invalid, but continuing startup.");
                        break;

                    default:
                        Debug.WriteLine($"[Settings] Unknown launcher '{launcher}', continuing with default behavior.");
                        break;
                }

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

            }
            catch (Exception ex)
            {

            }
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
        public static async Task winpart()
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

                        Console.WriteLine("Starting explorer.exe...");

                        // Check if explorer is already running
                        bool explorerRunning = Process.GetProcessesByName("explorer").Any();
                        TaskManagerDebloatServices();

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

        public static void TaskManagerDebloatServices()
        {
            var debloatList = IsHandheld() ? _debloatServicesHandheld : _debloatServicesDesktop;

            foreach (var (serviceName, processName) in debloatList)
            {
                if (!string.IsNullOrWhiteSpace(processName))
                {
                    // Wenn Prozessname bekannt → nutze volle Methode
                    DisableServiceAndKillProcess(serviceName, processName);
                }
                else
                {
                    // Nur Dienst deaktivieren
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
        public static void TaskManagerReEnableServices()
        {
            // Choose correct list based on device mode
            var servicesToEnable = IsHandheld() ? _debloatServicesHandheld : _debloatServicesDesktop;

            // Set each service to start automatically (do not start now)
            foreach (var (serviceName, _) in servicesToEnable)
            {
                SetServiceStartupToAuto(serviceName);
            }
        }
        public static void SetServiceStartupToAuto(string serviceName)
        {
            try
            {
                // Use sc.exe to set service to automatic start
                Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"config \"{serviceName}\" start= auto",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Verb = "runas"
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
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"config \"{serviceName}\" start= disabled",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Verb = "runas" // Admin!
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


        public static void DisableServiceAndKillProcess(string serviceName, string processName)
        {
            try
            {
                // Stop and disable service
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
                        RedirectStandardOutput = true,
                        Verb = "runas"
                    })?.WaitForExit();
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
            string launcher = AppSettings.Load<string>("launcher");
            Debug.WriteLine($"WinPart abgeschlossen. Starte nun Launcher: {launcher}");

            switch (launcher)
            {
                case "steam":
                    await StartSteam();
                    break;
                case "playnite":
                    await StartPlaynite();
                    break;
                case "custom":
                    await StartOtherLauncher();
                    break;
                case "gfn":
                    await StartGfn();
                    break;
                case "xbox":
                    await StartXbox(true);
                    break;
                default:
                    AppSettings.Save("launcher", "steam");
                    await StartSteam();
                    break;
            }

            // 4. GCM Loader als Shell in die Registry schreiben für den nächsten Start
            ConsoleModeToShell();
        }
        #endregion winparts

        #endregion functions
        #region launcher
        private const uint SWP_NOACTIVATE = 0x0010;
        // Wir fügen den Parameter 'forceRestart' hinzu. Standard ist 'false'.
        private async Task StartSteam(bool forceRestart = false)
        {
            try
            {
                string steamExePath = AutoDetectLauncherPath("steam");
                if (string.IsNullOrWhiteSpace(steamExePath))
                    throw new FileNotFoundException("Steam konnte auf diesem System nicht automatisch gefunden werden.");

                // GCM IN DEN HINTERGRUND SCHIEBEN, um Platz für Steam zu machen
                IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                SetWindowPos(myHwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                bool steamLiefSchon = false;

                // --- FALL A: KALTSTART BEIM BOOTEN (forceRestart = true) ---
                if (forceRestart)
                {
                    Debug.WriteLine("[GCM] Brute-Force Steam Start: Beende alte Instanzen (Kaltstart angefordert)...");
                    var steamProcs = Process.GetProcessesByName("steam")
                                            .Concat(Process.GetProcessesByName("steamwebhelper"))
                                            .ToList();

                    if (steamProcs.Any())
                    {
                        foreach (var proc in steamProcs)
                        {
                            try { if (!proc.HasExited) proc.Kill(); } catch { }
                        }
                        await Task.Delay(1500);
                    }

                    Debug.WriteLine("[GCM] Steam Kaltstart im Big Picture Modus...");
                    RenameSteamStartupVideo_Start();
                    Process.Start(new ProcessStartInfo(steamExePath) { Arguments = "-gamepadui", UseShellExecute = true });
                }
                // --- FALL B: WARMSTART ÜBER DAS MENÜ (forceRestart = false) ---
                else
                {
                    Process steamProc = Process.GetProcessesByName("steam").FirstOrDefault();
                    if (steamProc != null)
                    {
                        Debug.WriteLine("[GCM] Steam läuft bereits. Trigger Big Picture Protokoll (Warmstart)...");
                        Process.Start(new ProcessStartInfo("steam://open/gamepadui") { UseShellExecute = true });
                        steamLiefSchon = true;
                    }
                    else
                    {
                        Debug.WriteLine("[GCM] Steam lief nicht. Normaler Start...");
                        Process.Start(new ProcessStartInfo(steamExePath) { Arguments = "-gamepadui", UseShellExecute = true });
                    }
                }

                // --- AUF DAS FENSTER WARTEN ---
                IntPtr steamHwnd = IntPtr.Zero;
                int attempts = 0;
                // Wenn es nur ein Warmstart ist, müssen wir nicht so lange warten
                int maxAttempts = steamLiefSchon ? 20 : 60;

                while (attempts < maxAttempts)
                {
                    steamHwnd = FindSteamBigPictureWindow();
                    if (steamHwnd != IntPtr.Zero)
                    {
                        await Task.Delay(800); // UI aufbauen lassen
                        break;
                    }
                    await Task.Delay(250);
                    attempts++;
                }

                // --- FENSTER MAXIMIEREN UND IN DEN VORDERGRUND ZWINGEN ---
                if (steamHwnd != IntPtr.Zero)
                {
                    Debug.WriteLine($"[GCM] Steam BP Fenster bereit. Wende Nuclear-Fokus an...");
                    await ForcefullyBringToForeground(steamHwnd);
                    ShowWindow(steamHwnd, 3); // SW_SHOWMAXIMIZED
                }
                else
                {
                    Debug.WriteLine("[GCM] Timeout! Steam BP Fenster wurde nicht gefunden.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler in StartSteam: {ex.Message}");
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
                Process.Start(new ProcessStartInfo(playnitePath)
                {
                    WorkingDirectory = Path.GetDirectoryName(playnitePath), // Verhindert Pfad-Fehler
                    Arguments = "--startfullscreen --hidesplashscreen",     // Zwingt Fullscreen
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Fehler in StartPlaynite: {ex.Message}");
                await messagebox("Playnite Fullscreen konnte nicht gestartet werden. Bitte prüfe die Installation.");
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
                    throw new FileNotFoundException("GeForce Now konnte auf diesem System nicht automatisch gefunden werden.");
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
                Process.Start(launcherPath);
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

                // 3. ERST JETZT, wo das Video weg ist und der Desktop 100% bereit ist,
                // starten wir den eigentlichen Launcher. Er poppt jetzt nahezu sofort auf!
                await StartConfiguredLauncherAsync();

                // 4. GCM Loader als Shell in die Registry schreiben für den nächsten Start
                ConsoleModeToShell();

                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Start Error] A critical error occurred during startup: {ex.Message}");
                DispatcherQueue.TryEnqueue(() => TransitionToMainUI());
            }
        }

        // Diese neue Methode bündelt alles, was WÄHREND des Videos passieren kann,
        // ohne dass der User es sieht.
        private async Task SetupSystemAndDesktopAsync()
        {
            // System-Hooks und Hintergrunddienste aktivieren
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SetupLogging();
            BoostProcessPriority();
            uac("off");

            SettingsVerify();
            KeyboardRedirector.EnableRedirect();

            // Hintergrund-Tools asynchron starten
            _ = Task.Run(() => RunBoilrNoUI());
            displayfusion("start");
            IsJoyxoffInstalledAndStart();
            EnsureTouchKeyboardServiceIsRunning();
            cssloader();
            preaudio(true, false);
            prestartlist();
            await StartLosslessScaling();

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
            // Da das Video im Vordergrund läuft, sieht der User keine aufblitzenden Fenster!
            Debug.WriteLine("Starte WinPart-Modus (Desktop laden) im Hintergrund...");
            await winpart();
        }

        // Diese Methode kümmert sich am Ende NUR noch um das reine Öffnen des Launchers.
        private async Task StartConfiguredLauncherAsync()
        {
            string launcher = "steam"; // Fallback
            try { launcher = AppSettings.Load<string>("launcher"); } catch { }

            Debug.WriteLine($"Desktop ist bereit. Starte nun Launcher: {launcher}");

            switch (launcher)
            {
                case "steam":
                    await StartSteam(true); // <--- HIER: true für Kaltstart beim Boot!
                    break;
                case "playnite":
                    await StartPlaynite();
                    break;
                case "custom":
                    await StartOtherLauncher();
                    break;
                case "gfn":
                    await StartGfn();
                    break;
                case "xbox":
                    await StartXbox(true);
                    break;
                default:
                    AppSettings.Save("launcher", "steam");
                    await StartSteam(true); // <--- HIER AUCH: true
                    break;
            }
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

                    // 5. Check against your custom process blacklist
                    if (!string.IsNullOrEmpty(exeName) && _excludedProcessNames.Contains(exeName)) return true;

                    // Prevent duplicates
                    if (!seenHwnds.Add(hWnd)) return true;

                    // SUCCESS: Add to our list
                    dataList.Add(new ProcessData { ProductName = windowTitle, Hwnd = hWnd, Proc = proc, ExePath = exePath });
                    return true;

                }, IntPtr.Zero);

                return dataList;
            });

            UpdateUiFromData(processDataList);
        }

        /// <summary>
        /// Collects all relevant, visible main windows.
        /// </summary>


        private void UpdateLayoutForFocus()
        {
            if (_currentFocusArea == FocusArea.Launcher || _currentFocusArea == FocusArea.QuickLaunchers)
            {
                AnimateOverlayOpacity(QuickLauncherPanel, 1.0, false);
            }
            else
            {
                AnimateOverlayOpacity(QuickLauncherPanel, 0.0, false);
            }

            if (_currentFocusArea == FocusArea.Launcher)
            {
                // This part for the Launcher focus remains unchanged.
                LauncherColumn.Width = new GridLength(1, GridUnitType.Star);
                CardsColumn.Width = new GridLength(0);
                ColumnSeparator.Visibility = Visibility.Collapsed;

                for (int i = 0; i < _launcherAreaButtons.Count; i++)
                {
                    var card = _launcherAreaButtons[i];
                    if (i > 1)
                    {
                        AnimateCardVisibility(card, 1.0f, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(50 * (i - 1)));
                    }
                }
            }
            else
            {
                // This part for Cards/TopButtons focus is now updated.
                LauncherColumn.Width = new GridLength(1, GridUnitType.Auto);
                CardsColumn.Width = new GridLength(1, GridUnitType.Star);
                ColumnSeparator.Visibility = Visibility.Visible;

                // *** THIS IS THE KEY CHANGE ***
                // Instead of using potentially stale data, we trigger a fresh UI refresh.
                // We use "_ = " to call the async method without waiting for it, keeping the UI responsive.
                _ = RefreshAppListAsync();

                // This loop for hiding extra launcher cards remains unchanged.
                for (int i = 0; i < _launcherAreaButtons.Count; i++)
                {
                    var card = _launcherAreaButtons[i];
                    if (i > 1)
                    {
                        AnimateCardVisibility(card, 0.0f, TimeSpan.FromMilliseconds(150));
                    }
                }
            }
        }

        public class LauncherCardItem
        {
            public string Name { get; set; }
            public string ImagePath { get; set; }
            public string ExePath { get; set; }
            public string Arguments { get; set; }
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

            string launcher = AppSettings.Load<string>("launcher");
            string mainLauncherIconPath = launcher switch
            {
                "steam" => "ms-appx:///Assets/steam_logo.png",
                "playnite" => "ms-appx:///Assets/playnite_logo.png",
                "xbox" => "ms-appx:///Assets/xbox_logo.png",
                "gfn" => "ms-appx:///Assets/geforcenow.png", 
                _ => "ms-appx:///Assets/ownlauncher.png"
            };

            // 1. MAIN LAUNCHER (Wird groß erstellt: 250x250)
            var mainLauncherItem = new LauncherCardItem
            {
                Name = "Main Launcher",
                ImagePath = mainLauncherIconPath,
                TapAction = (s, e) => SwitchToConfiguredLauncher()
            };
            var mainCard = CreateLauncherCard(mainLauncherItem);
            LauncherAreaPanel.Children.Add(mainCard);
            _launcherAreaButtons.Add(mainCard);

            try
            {
                bool exePath = AppSettings.Load<bool>("show_discord");
                if (exePath == true)
                {
                    // 2. DISCORD (Wird klein erstellt: 170x170)
                   var discordItem = new LauncherCardItem
                    {
                      Name = "Discord",
                    ImagePath = "ms-appx:///Assets/discord.png",
                     TapAction = (s, e) => { MakeSelfNonTopmost(); StartDiscord(); PlayActivationSound(); }
                    };
                    var discordCard = CreateLauncherCard(discordItem);
                   LauncherAreaPanel.Children.Add(discordCard);
                   _launcherAreaButtons.Add(discordCard);
                }
                else
                {
                   

                }
            }
            catch
            {
                var discordItem = new LauncherCardItem
                {
                    Name = "Discord",
                    ImagePath = "ms-appx:///Assets/discord.png",
                    TapAction = (s, e) => { MakeSelfNonTopmost(); StartDiscord(); PlayActivationSound(); }
                };
                var discordCard = CreateLauncherCard(discordItem);
                LauncherAreaPanel.Children.Add(discordCard);
                _launcherAreaButtons.Add(discordCard);

                AppSettings.Save("show_discord", true);
            }
            // 3. DIE 5 CUSTOM CARDS (Alle klein: 170x170)
            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    string exePath = AppSettings.Load<string>($"button{i}link");
                    string imagePath = AppSettings.Load<string>($"button{i}image");
                    string args = AppSettings.Load<string>($"button{i}args") ?? "";

                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        var customItem = new LauncherCardItem
                        {
                            Name = $"App {i}",
                            ImagePath = imagePath,
                            TapAction = (s, e) =>
                            {
                                MakeSelfNonTopmost();
                                Process.Start(new ProcessStartInfo(exePath) { Arguments = args, UseShellExecute = true });
                                PlayActivationSound();
                            }
                        };
                        var customCard = CreateLauncherCard(customItem);
                        LauncherAreaPanel.Children.Add(customCard);
                        _launcherAreaButtons.Add(customCard);
                    }
                }
                catch { }
            }
        }



        /// <summary>
        /// Creates a single, clickable launcher card based on the provided data.
        /// Now handles a custom image for the Main Launcher card with a "LAUNCHER" label.
        /// </summary>
        private Border CreateLauncherCard(LauncherCardItem item)
        {
            var contentGrid = new Grid();

            // LOGIK: Ist es der Haupt-Launcher?
            bool isMainLauncher = (item.Name == "Main Launcher");

            // 1. DIE GLASS-SCHICHT (Clear Glass Look)
            var glassEffect = new Border
            {
                Name = "GlassBase",
                CornerRadius = new CornerRadius(15),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1.0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 255, 255, 255))
            };

            // 2. TITEL-BEREICH (Oben)
            var titleBlurLayer = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                Height = isMainLauncher ? 55 : 40, // Kleinerer Balken für Apps
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(130, 20, 20, 20)),
                CornerRadius = new CornerRadius(15, 15, 0, 0),
                Child = new TextBlock
                {
                    Text = item.Name.ToUpper(),
                    FontSize = isMainLauncher ? 12 : 10, // Kleinere Schrift für Apps
                    FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1
                }
            };

            // 3. ICON (Zentral)
            var iconImage = new Image
            {
                Source = new BitmapImage(new Uri(item.ImagePath ?? "ms-appx:///Assets/game.png")),
                Width = isMainLauncher ? 95 : 65,  // Deutlicher Größenunterschied
                Height = isMainLauncher ? 95 : 65,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, isMainLauncher ? 20 : 10, 0, 0),
                RenderTransform = new CompositeTransform()
            };

            contentGrid.Children.Add(glassEffect);
            contentGrid.Children.Add(titleBlurLayer);
            contentGrid.Children.Add(iconImage);

            var cardBorder = new Border
            {
                // MASSE: Launcher groß, Rest klein
                Width = isMainLauncher ? 250 : 170,
                Height = isMainLauncher ? 250 : 170,
                CornerRadius = new CornerRadius(15),
                Margin = new Thickness(6, 0, 6, 0), // Näher zusammenrücken
                Child = contentGrid,
                Tag = item,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            cardBorder.Loaded += (s, e) => ApplyNativeWinUI3Blur(glassEffect);
            if (item.TapAction != null) cardBorder.Tapped += (s, e) => item.TapAction(s, e);

            return cardBorder;
        }



        private bool _isRefreshRunning = false;

        private void StartAutoTaskRefresh()
        {
            if (_taskRefreshTimer != null) return;

            // Intervall auf 3 Sekunden hochsetzen, wenn wir nicht im Fokus sind
            _taskRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _taskRefreshTimer.Tick += async (s, e) =>
            {
                if (_isRefreshRunning || !IsWindowInForeground()) return; // Scanne nur, wenn GCM offen ist!

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
        private async Task<string> FindAppIdFromGameNameLocally(string gameName)
        {
            Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: Searching for AppID for '{gameName}'...");

            // Check cache first
            string cleanedName = CleanGameNameForSearch(gameName);
            if (_localGameNameCache.TryGetValue(cleanedName, out string cachedAppId))
            {
                Logger.Log($"[DEBUG] FindAppIdFromGameNameLocally: Found AppID '{cachedAppId ?? "null"}' in name cache.");
                return cachedAppId;
            }

            // Get all library paths (cached)
            if (_steamLibraryPathsCache == null)
            {
                _steamLibraryPathsCache = await Task.Run(() => GetAllSteamLibraryPaths());
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
                        string content = await File.ReadAllTextAsync(file);

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
            string gameName, string exePath, string savePath)
        {
            // 1. Steam Lokal
            if (!string.IsNullOrEmpty(exePath))
            {
                string localPath = await FindLocalSteamImageAsync(exePath);
                if (!string.IsNullOrEmpty(localPath))
                {
                    try
                    {
                        File.Copy(localPath, savePath, true);
                        await LoadImageToUiAsync(card, img, icon, txt, savePath, null);
                        return;
                    }
                    catch { }
                }
            }

            // 2. Online (SteamGridDB)
            if (_steamGridHelper != null && _steamGridHelper.IsApiKeySet)
            {
                // Schönen Namen suchen
                string searchName = GetSmartSearchName(gameName, exePath);

                // Downloaden
                await DownloadFromSteamGridDbAndCacheAsync(card, img, icon, txt, searchName, savePath);
            }
        }
        private void LoadCardImageAsync(Border card, Image loadedImageControl, Image defaultIconControl, TextBlock titleControl, string gameName, string exePath, Process proc)
        {
            if (!Directory.Exists(_imageCachePath)) Directory.CreateDirectory(_imageCachePath);

            Task.Run(async () =>
            {
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
                    if (IsLikelyGame(proc))
                    {
                        // Ja -> Suche online (SteamGridDB)
                        string searchName = GetSmartSearchName(gameName, exePath);
                        string savePath = Path.Combine(_imageCachePath, $"{stableKey}.jpg");

                        card.DispatcherQueue.TryEnqueue(() =>
                        {
                            PerformFallbackSearch(card, loadedImageControl, defaultIconControl, titleControl, searchName, exePath, savePath);
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

        private void UpdateUiFromData(List<ProcessData> processDataList)
        {
            if (processDataList == null) return;

            // Wir erstellen ein HashSet der NEUEN Handles für schnellen Abgleich
            var scannedHwnds = processDataList.Select(p => p.Hwnd).ToHashSet();

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
            foreach (var data in processDataList)
            {
                // DER FIX: Wenn wir das Fenster schon anzeigen -> ÜBERSPRINGEN (Fass es nicht an!)
                // Das verhindert das Neuladen und Controller-Springen.
                if (currentUiHwnds.Contains(data.Hwnd)) continue;

                // Wenn wir hier sind, ist es ein NEUES Fenster. Erstelle Karte.
                var border = CreateProgramCard(data.ProductName, data.ExePath, data.Proc, data.Hwnd);
                var entry = new ProgramCardEntry
                {
                    ProductName = data.ProductName,
                    ExePath = data.ExePath,
                    Hwnd = data.Hwnd,
                    Proc = data.Proc,
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
                // Index im gültigen Bereich halten
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
            }
        }



        private class ProgramCardEntry
        {
            public string ProductName;
            public string ExePath;
            public IntPtr Hwnd;
            public Process Proc;
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
            // Creates a timer for 10 seconds
            var hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                ProgramCardPanel.Visibility = Visibility.Visible;

                // ⏳ Wichtig: kleiner Delay mit DispatcherTimer
                var focusTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };

                focusTimer.Tick += (s2, e2) =>
                {
                    focusTimer.Stop();

                    var firstCard = ProgramCardPanel.Children.OfType<StackPanel>().FirstOrDefault();
                    if (firstCard?.Children.Count >= 3 && firstCard.Children[2] is StackPanel buttons)
                    {
                        if (buttons.Children[0] is Button launchButton)
                        {
                            Debug.WriteLine("→ Setze Fokus auf Launch-Button");
                            launchButton.Focus(FocusState.Programmatic);
                        }
                    }

                    LoadAllLauncherSettings();
                };

                focusTimer.Start();
                StartAutoTaskRefresh();

            };

            hideTimer.Start();
            StartClock();

            DispatcherQueue.TryEnqueue(() =>
            {
                _selectedCardIndex = 0;
                HighlightSelectedCard(); // beim Öffnen markieren
            });

            DispatcherQueue.TryEnqueue(() =>
            {
                _selectedCardIndex = 0;
                UpdateVisualFocus(); // Stellt sicher, dass die erste Karte hervorgehoben wird
            });

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

        private readonly List<string> _gamePathKeywords = new()
{
    "\\steamapps\\common\\",
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
        private Border CreateProgramCard(string name, string exePath, Process proc, IntPtr hwnd)
        {
            var contentGrid = new Grid();

            var loadedImage = new Image
            {
                Name = "LoadedImage",
                // UniformToFill ensures the image covers the whole card without distortion.
                Stretch = Stretch.UniformToFill,
                Opacity = 0.0,
                RenderTransform = new CompositeTransform()
            };

            // 2. DIE GLASS-SCHICHT (Der "Clear Glass" Container)
            var glassEffect = new Border
            {
                Name = "GlassBase",
                CornerRadius = new CornerRadius(10),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1.0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(25, 255, 255, 255))
            };

            // 3. TITEL-BEREICH (Exakt wie Launcher Card, aber etwas "Cleaner")
            var titleBlurLayer = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                Height = 55,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(130, 20, 20, 20)),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Child = new TextBlock
                {
                    Text = name.ToUpper(),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 12, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1
                }
            };

            // 4. ICON
            var iconImage = new Image
            {
                Source = GetAppIconAsBitmapImage(exePath) ?? new BitmapImage(new Uri("ms-appx:///Assets/game.png")),
                Width = 80,
                Height = 80,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0),
                RenderTransform = new CompositeTransform()
            };

            // 5. BANNER-BEREICH (Buttons)
            var bannerContainer = new Border
            {
                Name = "ButtonBanner",
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 50,
                Opacity = 0,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(90, 10, 10, 10)),
                CornerRadius = new CornerRadius(0, 0, 10, 10),
                Child = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 15 }
            };
            SetupButtonLayout((StackPanel)bannerContainer.Child, "Xbox");

            // Ebenen stapeln
            contentGrid.Children.Add(loadedImage);
            contentGrid.Children.Add(glassEffect);
            contentGrid.Children.Add(titleBlurLayer);
            contentGrid.Children.Add(iconImage);
            contentGrid.Children.Add(bannerContainer);

            var cardBorder = new Border
            {
                Width = 220,
                Height = 280,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10, 0, 10, 0),
                Child = contentGrid,
                Tag = new CardTag { Process = proc, Hwnd = hwnd },
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new CompositeTransform()
            };

            // --- EFFEKTE AKTIVIEREN ---
            cardBorder.Loaded += (s, e) => ApplyNativeWinUI3Blur(glassEffect);

            // FIX: Hier übergeben wir jetzt 'proc' als letzten Parameter!
            LoadCardImageAsync(cardBorder, loadedImage, iconImage, null, name, exePath, proc);

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

        private void SetupButtonLayout(StackPanel banner, string type)
        {
            banner.Children.Clear();

            if (type == "Xbox")
            {
                // WICHTIG: Ersetze die Dateinamen durch deine exakten PNG-Namen!
                banner.Children.Add(CreateImageButton("controllericons/xbox/a.png", "Play"));
                banner.Children.Add(CreateImageButton("controllericons/xbox/b.png", "Close"));

                // NEU: Das Start-Symbol ganz rechts für das Optionen-Menü
                banner.Children.Add(CreateImageButton("controllericons/xbox/start.png", ""));
            }
            else // PlayStation Style
            {
                // WICHTIG: Ersetze die Dateinamen durch deine exakten PNG-Namen!
                banner.Children.Add(CreateImageButton("controllericons/playstation/cross.png", "Play"));
                banner.Children.Add(CreateImageButton("controllericons/playstation/circle.png", "Close"));

                // NEU: Das Start-Symbol für PlayStation
                banner.Children.Add(CreateImageButton("controllericons/playstation/start.png", ""));
            }
        }

        // Hilfsmethode, die das Bild aus dem relativen Pfad lädt und den Text daneben setzt
        private StackPanel CreateImageButton(string relativeImagePath, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            // Das PNG laden (ms-appx:///Assets/ + der Pfad, den wir oben übergeben haben)
            var iconImage = new Image
            {
                Source = new BitmapImage(new Uri($"ms-appx:///Assets/{relativeImagePath}")),
                Width = 24,  // Passe diese Werte an, falls die Icons zu groß/klein sind
                Height = 24,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };

            var textBlock = new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };

            sp.Children.Add(iconImage);
            sp.Children.Add(textBlock);

            return sp;
        }

        private StackPanel CreateControllerButton(string symbol, string colorHex, string label, bool isPS = false)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            var btnCircle = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(HexToColor(colorHex)),
                Child = new TextBlock
                {
                    Text = symbol,
                    FontSize = isPS ? 10 : 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                    Foreground = new SolidColorBrush(isPS ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            sp.Children.Add(btnCircle);
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            });
            return sp;
        }

        private Windows.UI.Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            byte r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }

        private StackPanel CreateXboxHint(string button, string colorHex, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };

            // Der farbige Kreis mit dem Buchstaben
            var btnCircle = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(HexToColor(colorHex)),
                Child = new TextBlock
                {
                    Text = button,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.ExtraBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            sp.Children.Add(btnCircle);
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            });
            return sp;
        }

        // Hilfsfunktion für Farben
        

        // 3. HILFSMETHODE FÜR DIE BUTTON-ANZEIGE
        private StackPanel CreateKeyHint(string glyph, string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            sp.Children.Add(new FontIcon
            {
                Glyph = glyph,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons")
            });
            sp.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            });
            return sp;
        }

        // Hilfsmethode für die Shortcut-Anzeigen (X Start / O Close)
        private StackPanel CreateKeyHint(string glyph, string color, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            sp.Children.Add(new FontIcon { Glyph = glyph, FontSize = 14, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        /// <summary>
        /// Animiert die Rahmenfarbe eines Borders sanft mit einem Storyboard.
        /// </summary>
        private void AnimateBorderColor(Border border, bool isSelected)
        {
            Color targetColor;
            if (isSelected)
            {
                // Wir machen die Hervorhebung etwas heller und sichtbarer
                targetColor = Microsoft.UI.Colors.WhiteSmoke;
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
            // Sicherheitscheck: Falls die Liste leer ist, abbrechen
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

            // 3. Den aktuellen Index für das nächste Mal speichern
            _lastSelectedCardIndex = _selectedCardIndex;
        }






        private void ScrollToCardAnimated(UIElement card)
        {
            if (card == null || ProgramScrollViewer == null) return;

            try
            {
                // Hole die Position der Karte innerhalb des scrollbaren Bereichs (des Panels).
                var transform = card.TransformToVisual(ProgramCardPanel);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                // Berechne die Zielposition, um die Karte in der Mitte des sichtbaren Bereichs zu zentrieren.
                double cardX = position.X;
                double cardWidth = (card as FrameworkElement).ActualWidth;
                double viewportWidth = ProgramScrollViewer.ActualWidth;

                double targetOffset = cardX - (viewportWidth / 2) + (cardWidth / 2);

                // Stelle sicher, dass wir nicht über die Grenzen hinaus scrollen.
                double maxOffset = ProgramScrollViewer.ScrollableWidth;
                targetOffset = Math.Max(0, Math.Min(targetOffset, maxOffset));

                // Scrolle zur Zielposition. Der letzte Parameter "false" aktiviert die sanfte Animation.
                ProgramScrollViewer.ChangeView(targetOffset, null, null, false);
            }
            catch (Exception ex)
            {
                // Fängt seltene Layout-Fehler ab, um einen Absturz zu verhindern.
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
        public void BringTaskManagerToFrontAndFocus()
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

                // 1. GCM in den Vordergrund bringen (JETZT WARTEN WIR DARAUF!)
                await ForceGcmToFront();
                UpdateControllerBatteryStatus();

                // 2. Launcher-Logik (Steam minimieren etc.)
                try
                {
                    string launcher = AppSettings.Load<string>("launcher");
                    if (launcher == "steam")
                    {
                        IntPtr steamHwnd = FindSteamBigPictureWindow();
                        if (steamHwnd != IntPtr.Zero)
                        {
                            // Steam-spezifische Korrektur: Falls Steam sich kurzzeitig wehrt,
                            // setzen wir GCM zur Sicherheit nach 50ms nochmal in den Fokus.
                            await Task.Delay(50);
                            await ForceGcmToFront();
                        }
                    }
                    else if (launcher == "playnite" || launcher == "custom")
                    {
                        string procToFind = launcher == "playnite"
                            ? "Playnite.FullscreenApp"
                            : Path.GetFileNameWithoutExtension(AppSettings.Load<string>("customlauncherpath"));

                        Process proc = Process.GetProcessesByName(procToFind).FirstOrDefault();
                        if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
                            ShowWindow(proc.MainWindowHandle, 6); // SW_MINIMIZE
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"[GCM] Launcher logic error: {ex.Message}"); }
            });
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

        private void PlayCachedSound(string soundKey)
        {
            if (!_soundCache.TryGetValue(soundKey, out var soundUri)) return;

            try
            {
                // Create a new MediaPlayer for each trigger to allow overlapping sounds
                var player = new MediaPlayer();
                player.Source = MediaSource.CreateFromUri(soundUri);

                // Dispose resources once the sound has finished playing
                player.MediaEnded += (s, e) => {
                    player.Dispose();
                };

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
                var psiDisable = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "config XboxGipSvc start= disabled",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas" // Braucht Admin-Rechte
                };
                Process.Start(psiDisable)?.WaitForExit();

                var psiStop = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "stop XboxGipSvc",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };
                Process.Start(psiStop)?.WaitForExit();

                Debug.WriteLine("[System] Windows Xbox-Dienst blockiert. Guide-Button gehört jetzt GCM!");
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
        private void SendWinTab()
        {
            MakeSelfNonTopmost(); // <-- HINZUGEFÜGT
            SendOverlayNotification("Shortcut: Task View");
            keybd_event(0x5B, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
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
        #region lossless scalling
        // Das Schlüsselwort "async" ist hier erforderlich
        public static void LosslessScaling()
        {
            // User notification
            SendOverlayNotification("Toggle Scaling");
            LosslessScalingController.TriggerScaling();
        }
        #endregion lossless scalling
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
                _shortcutActions["switch tab"] = SendWinTab;
                _shortcutActions["audio switch"] = SwitchToNextAudioDevice;
                _shortcutActions["performance overlay"] = TriggerPerformanceOverlay;
                _shortcutActions["xbox bar"] = xboxbar;
                _shortcutActions["lossless scaling"] = LosslessScaling;
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
                        ClickSelectedTopButton();
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.X) != 0)
                    {
                        // Öffnet das integrierte Audio-Menü
                        OpenAudioFlyout();
                        PlayActivationSound();
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
                        _selectedCardIndex = (_selectedCardIndex + 1) % ProgramCardPanel.Children.Count;
                        navigated = true;
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
                        SwitchToSpecificLauncher(targetLauncher);
                        PlayActivationSound();
                    }
                    break;

                // --- 3. Launcher (Mini-Launcher links) ---
                case FocusArea.Launcher:
                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        if (_selectedLauncherAreaIndex == 0)
                        {
                            // Wir sind auf der Hauptkarte -> Gehe zu den 4 kleinen Quadraten
                            _currentFocusArea = FocusArea.QuickLaunchers;
                            _selectedQuickLauncherIndex = 0;
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
                        if (_selectedLauncherAreaIndex < _launcherAreaButtons.Count)
                        {
                            var item = _launcherAreaButtons[_selectedLauncherAreaIndex].Tag as LauncherCardItem;
                            item?.TapAction?.Invoke(null, null);
                        }
                        PlayActivationSound();
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
                                    SetAudioDevice(_audioDeviceButtons[_selectedAudioDeviceIndex].Tag.ToString());
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
                    // A = App starten (NEU)
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
            }

            if (navigated)
            {
                UpdateVisualFocus();
                PlayNavigationSound();
            }
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
                b.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                b.BorderThickness = new Thickness(0);
            });
            _powerMenuItems.ForEach(b => { b.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); });

            for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
            {
                if (ProgramCardPanel.Children[i] is Border card) { AnimateScale(card, false); AnimateBorderColor(card, false); }
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
                        selectedButton.BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlHighlightAccentBrush"];
                        selectedButton.BorderThickness = new Thickness(2);
                    }
                    break;

                case FocusArea.Cards:
                    AnimateInfoPanelFocus(false);
                    if (ProgramCardPanel.Children.Count > _selectedCardIndex)
                    {
                        if (ProgramCardPanel.Children[_selectedCardIndex] is Border card)
                        {
                            AnimateScale(card, true);
                            AnimateBorderColor(card, true);
                            ScrollToCardAnimated(card);
                        }
                    }
                    break;

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
            }
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

            // Banner (A/B Buttons) suchen für Karten
            FrameworkElement banner = null;
            if (border.Child is Grid g)
            {
                banner = g.Children.OfType<FrameworkElement>().FirstOrDefault(x => x.Name == "ButtonBanner");
            }

            var duration = TimeSpan.FromMilliseconds(120);
            var sb = new Storyboard();

            // WICHTIG: Hier von 1.08 auf 1.02 reduziert -> Verhindert Abschneiden!
            double scaleFactor = 1.02;

            // 1. SKALIERUNG
            var animX = new DoubleAnimation { To = isSelected ? scaleFactor : 1.0, Duration = duration };
            var animY = new DoubleAnimation { To = isSelected ? scaleFactor : 1.0, Duration = duration };
            // TranslateY leicht reduzieren für Mixer, da die Zeilen flacher sind
            var animTrans = new DoubleAnimation { To = isSelected ? -2 : 0, Duration = duration };

            Storyboard.SetTarget(animX, transform); Storyboard.SetTargetProperty(animX, "ScaleX");
            Storyboard.SetTarget(animY, transform); Storyboard.SetTargetProperty(animY, "ScaleY");
            Storyboard.SetTarget(animTrans, transform); Storyboard.SetTargetProperty(animTrans, "TranslateY");
            sb.Children.Add(animX); sb.Children.Add(animY); sb.Children.Add(animTrans);

            // 2. BANNER-LOGIK (Nur für Main Cards relevant)
            if (banner != null)
            {
                if (isSelected)
                {
                    var animOp = new DoubleAnimation { To = 1.0, Duration = duration };
                    Storyboard.SetTarget(animOp, banner);
                    Storyboard.SetTargetProperty(animOp, "Opacity");
                    sb.Children.Add(animOp);
                }
                else
                {
                    banner.Opacity = 0;
                }
            }

            sb.Begin();
        }


        /// <summary>
        /// Führt die Klick-Aktion für den aktuell ausgewählten oberen Button aus.
        /// </summary>
        private void ClickSelectedTopButton()
        {
            if (_topButtons.Count > _selectedTopButtonIndex)
            {
                var buttonToClick = _topButtons[_selectedTopButtonIndex];

                if (buttonToClick == ExitGcmButton)
                {
                    ExitGcmButton_Click_1(null, null);
                }
                else if (buttonToClick == VolumeButton) // NEU: Reaktion auf A-Taste
                {
                    ToggleAudioFlyout();
                }
                else if (buttonToClick == SettingsButton)
                {
                    SettingsButton_Click(null, null);
                }
                else if (buttonToClick == AppLauncherButton)
                {
                    ToggleAppLauncher_Click(null, null);
                }
                else if (buttonToClick == ShutdownButton)
                {
                    _currentFocusArea = FocusArea.PowerMenu;
                    _selectedPowerMenuItemIndex = 0;
                    PowerButton_Click(null, null);
                    UpdateVisualFocus();
                }
            }
        }
        private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!IsWindowInForeground()) return;

            switch (e.Key)
            {
                case VirtualKey.Left:
                    _selectedCardIndex--;
                    if (_selectedCardIndex < 0)
                        _selectedCardIndex = ProgramCardPanel.Children.Count - 1;
                    HighlightSelectedCard();
                    PlayNavigationSound();
                    break;

                case VirtualKey.Right:
                    _selectedCardIndex++;
                    if (_selectedCardIndex >= ProgramCardPanel.Children.Count)
                        _selectedCardIndex = 0;
                    HighlightSelectedCard();
                    PlayNavigationSound();
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

            animX.Duration = animY.Duration = new Duration(TimeSpan.FromMilliseconds(400));

            // Bounce-Effekt beim Aktivieren
            if (hasFocus)
            {
                animX.To = animY.To = 1.05; // 5% größer
                animX.EasingFunction = animY.EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
            }
            else
            {
                animX.To = animY.To = 1.0; // Normalgröße
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

 
        private async void TriggerCardAction(int index, bool launch)
        {
            if (index < 0 || index >= _cardCache.Count) return;

            var entry = _cardCache[index];
            if (!(entry.Card.Tag is CardTag tag)) return;

            if (launch)
            {
                // --- CINEMATIC MANUAL RESUME LOGIC ---
                // Check if the target process is actually asleep
                bool isSuspended = ProcessSuspender.IsProcessSuspended(tag.Process.Id);

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
                TaskManagerBringWindowToForeground(tag.Hwnd);
            }
            else // B-Button to Close (Logic remains identical)
            {
                try
                {
                    if (tag.Process != null && !tag.Process.HasExited)
                    {
                        tag.Process.Kill();
                    }
                    else
                    {
                        PostMessage(tag.Hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not kill process, sending close message instead: {ex.Message}");
                    PostMessage(tag.Hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }

                await Task.Delay(500);
                await RefreshAppListAsync();
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

        // --- GCM PLAYER (Startet direkt beim App-Start) ---

        private void PlayStartupVideo()
        {
            try
            {
                if (!IsGcmVideoEnabled() || IsSteamInjectionEnabled())
                {
                    TransitionToMainUI();
                    return;
                }

                string videoPath = "";
                try { videoPath = AppSettings.Load<string>("startupvideo_path"); } catch { }

                if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                {
                    Debug.WriteLine("[StartupVideo] Videodatei nicht gefunden.");
                    TransitionToMainUI();
                    return;
                }

                // ALLES andere ausblenden, Hintergrund auf Tiefschwarz setzen
                MainContent.Visibility = Visibility.Collapsed;
                FocusLossOverlay.Visibility = Visibility.Collapsed;

                StartupVideoPlayer.Visibility = Visibility.Visible;
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
            if (startupVideoFinished) return; // Verhindert doppeltes Ausführen

            SetBackgroundImage(GetScreenWidth(), GetScreenHeight());

            if (_startupMediaPlayer != null)
            {
                _startupMediaPlayer.MediaEnded -= OnStartupVideoEnded;
                _startupMediaPlayer.Pause(); // Sicherstellen, dass der Ton sofort stoppt
                _startupMediaPlayer.Dispose();
                _startupMediaPlayer = null;
            }

            StartupVideoPlayer.SetMediaPlayer(null);
            StartupVideoPlayer.Visibility = Visibility.Collapsed;

            MainContent.Opacity = 1.0;
            MainContent.Visibility = Visibility.Visible;

            FocusLossOverlay.Opacity = 1.0;
            FocusLossOverlay.Visibility = Visibility.Collapsed; // Normalerweise ausgeblendet, bis Fokus verloren geht
            _isOverlayActive = false;

            // Fokus auf die Karten setzen
            _currentFocusArea = FocusArea.Cards;
            UpdateVisualFocus();

            // WICHTIG: Das Fenster aus dem "Immer im Vordergrund"-Modus befreien!
            IntPtr myHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetWindowPos(myHwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

            startupVideoFinished = true;
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
                // ### THIS IS THE FIX ###
                // We now handle .lnk files correctly, just like the mouse click does.
                if (app.FilePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    // Use ShellExecute to let Windows handle the shortcut file.
                    Process.Start(new ProcessStartInfo(app.FilePath) { UseShellExecute = true });
                }
                else
                {
                    // For regular .exe files, the direct start is fine.
                    Process.Start(app.FilePath);
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
            try
            {
                // This command launches the main Windows Settings page.
                Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Could not open Windows Settings: {ex.Message}");
            }
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
            // Schritt 2: "explorer.exe" wieder als Standard-Shell in der Registry eintragen
            BackToWindows();
          
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
        public BitmapImage Icon { get; set; }
    }

   

}
