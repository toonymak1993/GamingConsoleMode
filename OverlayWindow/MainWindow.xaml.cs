using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SharpDX.XInput;
using Tomlyn;
using System.Globalization;
using System.Windows.Data;
using Tomlyn.Model;

namespace OverlayWindow
{
    public partial class MainWindow : Window
    {
        private Controller _controller;
        private State _previousState;
        private DispatcherTimer _gamepadTimer;
        public ObservableCollection<Shortcut> Shortcuts { get; set; }
        private Expander[] _categoryExpanders;
        // FÜGE DIESE ZEILE HIER EIN:
        private int currentExpanderIndex = 0;


        public MainWindow()
        {
            InitializeComponent();

            // Verhindert, dass mehrere Instanzen des Overlays laufen
            if (IsAnotherInstanceRunning())
            {
                this.Close();
                return;
            }

            Shortcuts = new ObservableCollection<Shortcut>();
            ShortcutList.ItemsSource = Shortcuts;

            // Lade die Shortcuts EINMAL beim Start aus der TOML-Datei
            LoadShortcutsFromToml();

            StartPipeServer();
            this.Visibility = Visibility.Hidden;

            _categoryExpanders = new[] { ShortcutExpander };
            InitGamepadControl();
        }

        private bool IsAnotherInstanceRunning()
        {
            var current = Process.GetCurrentProcess();
            return Process.GetProcessesByName(current.ProcessName).Any(p => p.Id != current.Id);
        }

        /// <summary>
        /// GEÄNDERT: Liest jetzt einmalig die settings.toml und lädt nur aktivierte Shortcuts.
        /// </summary>
        private void LoadShortcutsFromToml()
        {
            Shortcuts.Clear();
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "settings.toml");

            if (!File.Exists(settingsFilePath)) return;

            try
            {
                var model = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();
                if (model.TryGetValue("shortcuts", out var shortcutsObj) && shortcutsObj is TomlTableArray shortcutsArray)
                {
                    foreach (TomlTable shortcutTable in shortcutsArray)
                    {
                        var shortcut = new Shortcut
                        {
                            Key1 = shortcutTable["key1"]?.ToString(),
                            Key2 = shortcutTable["key2"]?.ToString(),
                            Function = shortcutTable["function"]?.ToString(),
                            Enabled = Convert.ToBoolean(shortcutTable["enabled"])
                        };

                        if (shortcut.Enabled) // Nur aktivierte Shortcuts hinzufügen
                        {
                            Shortcuts.Add(shortcut);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading shortcuts from TOML: {ex.Message}");
            }
        }

        private void StartPipeServer()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream("GCMOverlayPipe", PipeDirection.In);
                        server.WaitForConnection();

                        using var reader = new StreamReader(server);
                        var command = reader.ReadLine();

                        Dispatcher.Invoke(() =>
                        {
                            if (command == "TOGGLE") ToggleOverlay();
                            else if (command != null && command.StartsWith("NOTIFY:")) ShowToast(command.Substring(7));
                        });
                    }
                    catch { /* Ignoriere Fehler und versuche es erneut */ }
                }
            });
        }

        private void ToggleOverlay()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double targetLeft = screenWidth - this.Width;

            if (this.Visibility == Visibility.Visible)
            {
                var slideOut = new DoubleAnimation(screenWidth, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                slideOut.Completed += (s, e) => this.Visibility = Visibility.Hidden;
                this.BeginAnimation(Window.LeftProperty, slideOut);
            }
            else
            {
                this.Left = screenWidth;
                this.Visibility = Visibility.Visible;
                this.Topmost = true;

                // Zuverlässigere Methode, um das Fenster in den Vordergrund zu zwingen
                Dispatcher.InvokeAsync(() =>
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    NativeMethods.SetForegroundWindow(helper.Handle);
                    this.Activate();
                    this.Focus();
                }, DispatcherPriority.ApplicationIdle);

                var slideIn = new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                this.BeginAnimation(Window.LeftProperty, slideIn);
            }
        }

        private void ShowToast(string message)
        {
            var popup = new ToastPopup(message, "🎮");
            popup.Show();
        }

        private void InitGamepadControl()
        {
            // Effizienteres Intervall für die Gamepad-Abfrage
            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _gamepadTimer.Tick += GamepadTick;

            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    _controller = new Controller(UserIndex.One);
                    _gamepadTimer.Start();
                    FocusFirstExpander();
                }
                else
                {
                    _gamepadTimer.Stop();
                }
            };
        }

        private void FocusFirstExpander()
        {
            if (_categoryExpanders.Any())
            {
                currentExpanderIndex = 0;
                HighlightCurrentExpander();
            }
        }

        /// <summary>
        /// BEREINIGT: Verarbeitet Gamepad-Eingaben nur bei neuen Tastendrücken.
        /// </summary>
        private void GamepadTick(object sender, EventArgs e)
        {
            if (!_controller.IsConnected) return;

            var state = _controller.GetState();
            if (state.PacketNumber == _previousState.PacketNumber) return;

            var currentButtons = state.Gamepad.Buttons;
            var prevButtons = _previousState.Gamepad.Buttons;

            // DPad Down
            if (currentButtons.HasFlag(GamepadButtonFlags.DPadDown) && !prevButtons.HasFlag(GamepadButtonFlags.DPadDown))
            {
                FocusNextCategory();
            }

            // DPad Up
            if (currentButtons.HasFlag(GamepadButtonFlags.DPadUp) && !prevButtons.HasFlag(GamepadButtonFlags.DPadUp))
            {
                FocusPreviousCategory();
            }

            // A Button
            if (currentButtons.HasFlag(GamepadButtonFlags.A) && !prevButtons.HasFlag(GamepadButtonFlags.A))
            {
                ToggleCurrentCategory();
            }

            // B Button
            if (currentButtons.HasFlag(GamepadButtonFlags.B) && !prevButtons.HasFlag(GamepadButtonFlags.B))
            {
                CollapseCurrentCategory();
            }

            _previousState = state;
        }

        private void FocusNextCategory()
        {
            currentExpanderIndex = (currentExpanderIndex + 1) % _categoryExpanders.Length;
            HighlightCurrentExpander();
        }

        private void FocusPreviousCategory()
        {
            currentExpanderIndex = (currentExpanderIndex - 1 + _categoryExpanders.Length) % _categoryExpanders.Length;
            HighlightCurrentExpander();
        }

        private void ToggleCurrentCategory()
        {
            if (!_categoryExpanders.Any()) return;
            var exp = _categoryExpanders[currentExpanderIndex];
            exp.IsExpanded = !exp.IsExpanded;
        }

        private void CollapseCurrentCategory()
        {
            if (!_categoryExpanders.Any()) return;
            _categoryExpanders[currentExpanderIndex].IsExpanded = false;
        }

        private void HighlightCurrentExpander()
        {
            for (int i = 0; i < _categoryExpanders.Length; i++)
            {
                _categoryExpanders[i].BorderThickness = (i == currentExpanderIndex) ? new Thickness(2) : new Thickness(0);
                _categoryExpanders[i].BorderBrush = (i == currentExpanderIndex) ? System.Windows.Media.Brushes.WhiteSmoke : System.Windows.Media.Brushes.Transparent;
            }
            _categoryExpanders[currentExpanderIndex].Focus();
        }

        /// <summary>
        /// ENTFERNT: Diese Methode hat das Overlay zu aggressiv geschlossen.
        /// Das Fenster bleibt jetzt offen, auch wenn es den Fokus verliert.
        /// </summary>
        //private void Window_Deactivated(object sender, EventArgs e)
        //{
        //    if (this.Visibility == Visibility.Visible)
        //    {
        //        ToggleOverlay();
        //    }
        //}
    }



    // Die Shortcut-Klasse bleibt unverändert
    public class Shortcut
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Function { get; set; }
        public bool Enabled { get; set; }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    public class ToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string s ? s.ToUpper() : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
