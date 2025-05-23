using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace wingamepad
{
    public partial class App : Application
    {
        private Controller _xinputController;
        private bool _controllerConnected = false;
        private GamepadButtonFlags _lastButtonState = GamepadButtonFlags.None;

        private readonly TimeSpan _comboTimeout = TimeSpan.FromMilliseconds(1000);
        private Dictionary<(string, string), string> _activeShortcuts = new();
        private HashSet<(string, string)> _triggeredCombos = new();
        private Dictionary<string, DateTime> _heldButtonTimestamps = new();

        private string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wingamepadlog.txt");

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
            ["RightShoulder"] = GamepadButtonFlags.RightShoulder
        };
        private bool IsAnotherInstanceRunning()
        {
            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName)
                                .Where(p => p.Id != current.Id);
            return others.Any();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Overwrite the log file at each startup
            File.WriteAllText(_logFilePath, string.Empty);

            Log("Application started.");
            LoadWinmodeShortcuts();
            SetupGamepadWatcher();

            if (IsAnotherInstanceRunning())
            {
                Environment.Exit(0);
            }
        }

        private void LoadWinmodeShortcuts()
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "shortcutswin");
            if (!Directory.Exists(folderPath)) return;

            _activeShortcuts.Clear();

            foreach (var filePath in Directory.GetFiles(folderPath, "winmode_*.json"))
            {
                try
                {
                    string content = File.ReadAllText(filePath);
                    var entry = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                    if (entry == null) continue;

                    if (entry.TryGetValue("Key1", out var k1) &&
                        entry.TryGetValue("Key2", out var k2) &&
                        entry.TryGetValue("Function", out var fn))
                    {
                        string key1 = k1?.ToString()?.Trim();
                        string key2 = k2?.ToString()?.Trim();
                        string function = fn?.ToString()?.Trim();

                        if (!string.IsNullOrEmpty(key1) && !string.IsNullOrEmpty(key2) && !string.IsNullOrEmpty(function))
                        {
                            _activeShortcuts[(key1, key2)] = function;
                            Log($"Loaded shortcut: {key1} + {key2} -> {function}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error reading shortcut file: {filePath} - {ex.Message}");
                }
            }
        }

        private void SetupGamepadWatcher()
        {
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            timer.Tick += (s, e) =>
            {
                if (_xinputController == null || !_xinputController.IsConnected)
                {
                    _xinputController = GetConnectedController();
                    _controllerConnected = _xinputController != null;
                }

                if (_controllerConnected)
                {
                    var state = _xinputController.GetState();
                    var buttons = state.Gamepad.Buttons;

                    foreach (var pair in _activeShortcuts)
                    {
                        var key1 = pair.Key.Item1;
                        var key2 = pair.Key.Item2;
                        var function = pair.Value;

                        bool key1Pressed = IsButtonPressed(buttons, key1);
                        bool key2Pressed = IsButtonPressed(buttons, key2);

                        if (key1Pressed && !_heldButtonTimestamps.ContainsKey(key1))
                            _heldButtonTimestamps[key1] = DateTime.UtcNow;

                        if (_heldButtonTimestamps.TryGetValue(key1, out var heldTime))
                        {
                            if (DateTime.UtcNow - heldTime < _comboTimeout && key2Pressed)
                            {
                                if (!_triggeredCombos.Contains(pair.Key))
                                {
                                    _triggeredCombos.Add(pair.Key);
                                    Log($"Shortcut triggered: {key1} + {key2} -> {function}");
                                    ExecuteFunction(function);
                                }
                            }
                            else if (!key1Pressed)
                            {
                                _heldButtonTimestamps.Remove(key1);
                                _triggeredCombos.Remove(pair.Key);
                            }
                        }
                    }

                    _lastButtonState = buttons;
                }
            };

            timer.Start();
        }

        private Controller GetConnectedController()
        {
            for (int i = 0; i < 4; i++)
            {
                var controller = new Controller((UserIndex)i);
                if (controller.IsConnected)
                    return controller;
            }
            return null;
        }

        private bool IsButtonPressed(GamepadButtonFlags state, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            return _buttonMap.TryGetValue(key, out var button) && (state & button) != 0;
        }

        private void ExecuteFunction(string function)
        {
            if (function == "winmodechange")
            {
                TriggerWinModeChange();
            }
            else if (function == "winmode_overlay")
            {
                //for later
            }
            else if (function == "winmode_toggle")
            {
                //for later
            }
            else
            {
                Log($"Unknown winmode function: {function}");
            }
        }

        private void TriggerWinModeChange()
        {
            try
            {
                string loaderPath = @"C:\\Program Files (x86)\\GCMcrew\\GCM\\GCM\\gcmloader.exe";

                Log($"Executing winmodechange: launching {loaderPath}");

                if (File.Exists(loaderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = loaderPath,
                        UseShellExecute = true,
                        Verb = "runas"
                    });

                    Log("Application exiting after winmodechange trigger.");
                    Application.Current.Shutdown();
                }
                else
                {
                    Log("gcmloader.exe not found at fixed path.");
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to launch gcmloader: {ex.Message}");
            }
        }


        private void TriggerOverlay()
        {
            //for later
        }

        private void TriggerModeToggle()
        {
            //for later
        }

        private void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch { /* fail silently */ }
        }
    }
}
