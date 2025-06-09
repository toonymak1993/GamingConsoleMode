using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SharpDX.XInput;

namespace OverlayWindow
{
    public partial class MainWindow : Window
    {
        private Controller controller = new Controller(UserIndex.One);
        private State previousState;
        private DispatcherTimer gamepadTimer;
        private DispatcherTimer timer;
        private readonly string shortcutPath;
        public ObservableCollection<Shortcut> Shortcuts { get; set; }
        private int currentExpanderIndex = 0;
        private Expander[] categoryExpanders;

        public MainWindow()
        {
            InitializeComponent();

            if (IsAnotherInstanceRunning())
            {
                this.Close();
                return;
            }

            shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "shortcuts");
            Shortcuts = new ObservableCollection<Shortcut>();
            ShortcutList.ItemsSource = Shortcuts;

            LoadShortcuts();
            StartShortcutWatcher();
            StartPipeServer();

            this.Visibility = Visibility.Hidden;

            // Gamepad UI setup
            categoryExpanders = new[] { ShortcutExpander };
            InitGamepadControl();

           
        }


        private bool IsAnotherInstanceRunning()
        {
            var current = Process.GetCurrentProcess();
            return Process.GetProcessesByName(current.ProcessName).Any(p => p.Id != current.Id);
        }

        private void StartShortcutWatcher()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0) };
            timer.Tick += (s, e) => LoadShortcuts();
            timer.Start();
        }

        private void LoadShortcuts()
        {
            try
            {
                Shortcuts.Clear();
                var files = Directory.GetFiles(shortcutPath, "*.json");
                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    var shortcut = JsonSerializer.Deserialize<Shortcut>(content);
                    if (shortcut != null && shortcut.Enabled)
                        Shortcuts.Add(shortcut);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shortcut load error: " + ex.Message);
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
                            if (command == "TOGGLE")
                                ToggleOverlay();
                            else if (command.StartsWith("NOTIFY:"))
                                ShowToast(command.Substring(7));
                        });
                    }
                    catch { }
                }
            });
        }

        private void ToggleOverlay()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double targetLeft = screenWidth - this.Width;

            if (this.Visibility == Visibility.Visible)
            {
                var slideOut = new DoubleAnimation
                {
                    To = screenWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                slideOut.Completed += (s, e) => this.Visibility = Visibility.Hidden;
                this.BeginAnimation(Window.LeftProperty, slideOut);
            }
            else
            {
                this.Left = screenWidth; // Start off-screen
                this.Visibility = Visibility.Visible;
                this.Topmost = true;
                this.Show();

                // Focus handling (robust native version)
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                NativeMethods.SetForegroundWindow(helper.Handle); // Native call

                this.Focus();
                Keyboard.Focus(this);

                Debug.WriteLine($"[DEBUG] Overlay is active: {this.IsActive}");
                Debug.WriteLine($"[DEBUG] Focused element: {Keyboard.FocusedElement}");

                var slideIn = new DoubleAnimation
                {
                    To = targetLeft,
                    Duration = TimeSpan.FromMilliseconds(300),
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
            gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            gamepadTimer.Tick += GamepadTick;

            // Controller-Polling nur aktiv, wenn sichtbar
            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    gamepadTimer.Start();
                    FocusFirstExpander();
                }
                else
                {
                    gamepadTimer.Stop();
                }
            };
        }

        private void FocusFirstExpander()
        {
            if (categoryExpanders.Length > 0)
            {
                currentExpanderIndex = 0;
                HighlightCurrentExpander();
            }
        }

        private bool aWasPressed = false;
        private bool bWasPressed = false;
        private bool upWasPressed = false;
        private bool downWasPressed = false;

        private int lastPacketNumber = -1;

        private void GamepadTick(object sender, EventArgs e)
        {
            if (!controller.IsConnected)
            {
                Debug.WriteLine("[DEBUG] Controller not connected");
                controller = new Controller(UserIndex.One);
                return;
            }

            var state = controller.GetState();

            // Debug packet number
            if (state.PacketNumber == lastPacketNumber)
            {
                Debug.WriteLine("[DEBUG] No new input (PacketNumber unchanged)");
                return;
            }

            Debug.WriteLine($"[DEBUG] PacketNumber changed: {lastPacketNumber} -> {state.PacketNumber}");
            lastPacketNumber = state.PacketNumber;

            var buttons = state.Gamepad.Buttons;
            var prevButtons = previousState.Gamepad.Buttons;

            Debug.WriteLine($"[DEBUG] Current Buttons: {buttons}");

            // DPad Down
            if ((buttons & GamepadButtonFlags.DPadDown) != 0)
                Console.WriteLine("[DEBUG] DPadDown PRESSED");
            if ((buttons & GamepadButtonFlags.DPadDown) != 0 && (prevButtons & GamepadButtonFlags.DPadDown) == 0)
            {
                Debug.WriteLine("[DEBUG] DPadDown NEW PRESS → FocusNextCategory()");
                FocusNextCategory();
            }

            // DPad Up
            if ((buttons & GamepadButtonFlags.DPadUp) != 0)
                Debug.WriteLine("[DEBUG] DPadUp PRESSED");
            if ((buttons & GamepadButtonFlags.DPadUp) != 0 && (prevButtons & GamepadButtonFlags.DPadUp) == 0)
            {
                Debug.WriteLine("[DEBUG] DPadUp NEW PRESS → FocusPreviousCategory()");
                FocusPreviousCategory();
            }

            // A Button
            if ((buttons & GamepadButtonFlags.A) != 0)
                Debug.WriteLine("[DEBUG] A PRESSED");
            if ((buttons & GamepadButtonFlags.A) != 0 && (prevButtons & GamepadButtonFlags.A) == 0)
            {
                Debug.WriteLine("[DEBUG] A NEW PRESS → ToggleCurrentCategory()");
                ToggleCurrentCategory();
            }

            // B Button
            if ((buttons & GamepadButtonFlags.B) != 0)
                Console.WriteLine("[DEBUG] B PRESSED");
            if ((buttons & GamepadButtonFlags.B) != 0 && (prevButtons & GamepadButtonFlags.B) == 0)
            {
                Console.WriteLine("[DEBUG] B NEW PRESS → CollapseCurrentCategory()");
                CollapseCurrentCategory();
            }

            previousState = state;
        }





        private void FocusNextCategory()
        {
            currentExpanderIndex = (currentExpanderIndex + 1) % categoryExpanders.Length;
            HighlightCurrentExpander();
        }

        private void FocusPreviousCategory()
        {
            currentExpanderIndex = (currentExpanderIndex - 1 + categoryExpanders.Length) % categoryExpanders.Length;
            HighlightCurrentExpander();
        }

        private void ToggleCurrentCategory()
        {
            if (categoryExpanders.Length == 0) return;
            var exp = categoryExpanders[currentExpanderIndex];
            exp.IsExpanded = !exp.IsExpanded;
        }

        private void CollapseCurrentCategory()
        {
            if (categoryExpanders.Length == 0) return;
            categoryExpanders[currentExpanderIndex].IsExpanded = false;
        }

        private void HighlightCurrentExpander()
        {
            for (int i = 0; i < categoryExpanders.Length; i++)
            {
                categoryExpanders[i].BorderThickness = (i == currentExpanderIndex) ? new Thickness(3) : new Thickness(1);
                categoryExpanders[i].BorderBrush = (i == currentExpanderIndex) ? Brushes.DodgerBlue : Brushes.Gray;
            }

            categoryExpanders[currentExpanderIndex].Focus();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                Debug.WriteLine("[DEBUG] Lost focus → overlay will close");
                ToggleOverlay();
            }
        }
    }

    public class Shortcut
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Function { get; set; }
        public bool Enabled { get; set; }
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);
}
