using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Tomlyn;
using Tomlyn.Model;
using Notification.Wpf;
using System.Windows.Media.Imaging;

namespace wingamepad
{
    public partial class App : Application
    {
        private Controller _xinputController;
        private bool _controllerConnected = false;
        private Dictionary<(string, string), string> _activeShortcuts = new();
        private HashSet<(string, string)> _triggeredCombos = new();
        private Dictionary<string, DateTime> _heldButtonTimestamps = new();
        private readonly TimeSpan _comboTimeout = TimeSpan.FromMilliseconds(1000);
        private readonly NotificationManager _notificationManager = new();

        // Stellt sicher, dass die App nur einmal läuft
        private System.Threading.Mutex _mutex;

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
            ["LeftThumb"] = GamepadButtonFlags.LeftThumb,
            ["RightThumb"] = GamepadButtonFlags.RightThumb
        };

        /// <summary>
        /// Die Haupt-Startmethode der Anwendung.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Stellt sicher, dass nur eine Instanz der App läuft
            _mutex = new System.Threading.Mutex(true, "wingamepad-single-instance-mutex", out bool createdNew);
            if (!createdNew)
            {
                // Eine andere Instanz läuft bereits. Schließe diese neue Instanz sofort.
                Application.Current.Shutdown();
                return;
            }

            // Lade die Shortcuts. Wenn die Methode 'false' zurückgibt, ist etwas nicht konfiguriert.
            if (!LoadShortcutsFromToml())
            {
                // Schließe die App leise, wenn keine gültigen/aktivierten Shortcuts gefunden wurden.
                Application.Current.Shutdown();
                return;
            }

            SetupGamepadWatcher();
        }

        /// <summary>
        /// Liest die settings.toml und lädt die aktivierten Shortcuts.
        /// Gibt 'true' zurück, wenn alles erfolgreich war, sonst 'false'.
        /// </summary>
        private bool LoadShortcutsFromToml()
        {
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "settings.toml");

            if (!File.Exists(settingsFilePath))
            {
                // Datei nicht gefunden -> beenden
                return false;
            }

            try
            {
                var model = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();

                // Prüfe, ob der Seamless Switch überhaupt global aktiviert ist
                if (!model.TryGetValue("useseamlessswitchtogcm", out var useSeamlessObj) || !Convert.ToBoolean(useSeamlessObj))
                {
                    // Feature ist deaktiviert -> beenden
                    return false;
                }

                // Lade den Shortcut selbst
                if (model.TryGetValue("winmode_shortcut", out var shortcutObj) && shortcutObj is TomlTable table)
                {
                    string key1 = table["key1"]?.ToString();
                    string key2 = table["key2"]?.ToString();
                    bool enabled = Convert.ToBoolean(table["enabled"]);

                    if (enabled && !string.IsNullOrEmpty(key1) && !string.IsNullOrEmpty(key2))
                    {
                        // Füge den Shortcut hinzu. Die Funktion ist immer "winmodechange".
                        _activeShortcuts[(key1, key2)] = "winmodechange";
                        return true; // Erfolgreich geladen
                    }
                }
            }
            catch
            {
                // Bei jedem Fehler (Datei kaputt, Key nicht gefunden etc.) -> beenden
                return false;
            }

            // Kein gültiger, aktivierter Shortcut gefunden -> beenden
            return false;
        }

        /// <summary>
        /// Startet den Timer, der den Gamepad-Status überwacht.
        /// </summary>
        private void SetupGamepadWatcher()
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (s, e) =>
            {
                var newController = GetConnectedController();
                bool justConnected = newController != null && _xinputController == null;

                _xinputController = newController;
                _controllerConnected = _xinputController != null;

                if (!_controllerConnected) return;

                if (justConnected)
                {
                    ShowControllerToast();
                }

                HandleShortcutDetection(_xinputController.GetState().Gamepad.Buttons);
            };
            timer.Start();
        }

        /// <summary>
        /// Prüft, ob eine Shortcut-Kombination gedrückt wurde.
        /// </summary>
        private void HandleShortcutDetection(GamepadButtonFlags currentButtons)
        {
            foreach (var pair in _activeShortcuts)
            {
                var (key1, key2) = pair.Key;
                bool key1Pressed = IsButtonPressed(currentButtons, key1);
                bool key2Pressed = IsButtonPressed(currentButtons, key2);

                if (key1Pressed && !_heldButtonTimestamps.ContainsKey(key1))
                    _heldButtonTimestamps[key1] = DateTime.UtcNow;

                if (_heldButtonTimestamps.TryGetValue(key1, out var heldTime))
                {
                    if (DateTime.UtcNow - heldTime < _comboTimeout && key2Pressed)
                    {
                        if (!_triggeredCombos.Contains(pair.Key))
                        {
                            _triggeredCombos.Add(pair.Key);
                            TriggerWinModeChange(); // Funktion direkt aufrufen
                        }
                    }
                    else if (!key1Pressed)
                    {
                        _heldButtonTimestamps.Remove(key1);
                        _triggeredCombos.Remove(pair.Key);
                    }
                }
            }
        }

        /// <summary>
        /// Führt die Aktion zum Starten des GCM Loaders aus und beendet sich selbst.
        /// </summary>
        private void TriggerWinModeChange()
        {
            try
            {
                string loaderPath = @"C:\Program Files (x86)\GCM\GCM\gcmloader.exe";
                if (File.Exists(loaderPath))
                {
                    Process.Start(new ProcessStartInfo(loaderPath) { UseShellExecute = true, Verb = "runas" });
                    Application.Current.Shutdown();
                }
            }
            catch { /* Bei Fehler leise beenden */ }
        }

        /// <summary>
        /// Zeigt eine Benachrichtigung an, wenn ein Controller verbunden wird.
        /// </summary>
        private void ShowControllerToast()
        {
            if (_activeShortcuts.Count == 0) return;

            var first = _activeShortcuts.First();
            string shortcutText = $"{first.Key.Item1} + {first.Key.Item2}";
            var iconUri = new Uri(@"C:\Program Files (x86)\GCM\GCM\logo.ico", UriKind.Absolute);

            _notificationManager.Show(
                "Gamepad connected",
                $"Press {shortcutText} to start GCM",
                NotificationType.Information,
                expirationTime: TimeSpan.FromSeconds(7),
                icon: File.Exists(iconUri.LocalPath) ? new BitmapImage(iconUri) : null
            );
        }

        // Hilfsmethoden
        private Controller GetConnectedController()
        {
            for (int i = 0; i < 4; i++)
            {
                var controller = new Controller((UserIndex)i);
                if (controller.IsConnected) return controller;
            }
            return null;
        }

        private bool IsButtonPressed(GamepadButtonFlags state, string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _buttonMap.TryGetValue(key, out var button) && (state & button) != 0;
        }
    }
}