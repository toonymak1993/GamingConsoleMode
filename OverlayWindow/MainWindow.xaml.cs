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
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OverlayWindow
{
    public partial class MainWindow : Window
    {

        private bool isInNotificationMode = false;


        private readonly StackPanel toastContainer = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 0, 0, 0),
            IsHitTestVisible = false
        };


        private readonly string shortcutPath;
        public ObservableCollection<Shortcut> Shortcuts { get; set; }

        private DispatcherTimer timer;
        private bool isWelcomeMode = false;
        private DispatcherTimer welcomeAutoCloseTimer;
        private ProgressBar welcomeProgressBar;

        // Check if another instance of this overlay is already running
        private bool IsAnotherInstanceRunning()
        {
            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName)
                                .Where(p => p.Id != current.Id);
            return others.Any();
        }

        public MainWindow()
        {
            InitializeComponent();

            if (IsAnotherInstanceRunning())
            {
                this.Close();
                return;
            }
            RootPanel.Children.Add(toastContainer);

            // Setup shortcut loading
            shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "shortcuts");
            Shortcuts = new ObservableCollection<Shortcut>();
            ShortcutCarousel.Items.Clear();
            ShortcutCarousel.ItemsSource = Shortcuts;

            // Styled welcome bar
            welcomeProgressBar = new ProgressBar
            {
                Height = 6,
                Margin = new Thickness(20),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visibility = Visibility.Collapsed
            };
            RootPanel.Children.Insert(1, welcomeProgressBar);

            // Load shortcuts without showing anything
            LoadShortcuts();

            // Start watchers
            StartShortcutWatcher();
            StartPipeServer();

            // Ensure overlay remains hidden until a Pipe command comes
            this.Visibility = Visibility.Hidden;
        }


        // Monitor shortcut directory continuously
        private void StartShortcutWatcher()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0) };
            timer.Tick += (s, e) => LoadShortcuts();
            timer.Start();
        }

        // Load all .json shortcut definitions
        private void LoadShortcuts()
        {
            try
            {
                Shortcuts.Clear();
                var files = Directory.GetFiles(shortcutPath, "*.json");

                foreach (var file in files)
                {
                    string content = File.ReadAllText(file);
                    var shortcut = JsonSerializer.Deserialize<Shortcut>(content);
                    if (shortcut != null && shortcut.Enabled)
                    {
                        Shortcuts.Add(shortcut);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading shortcuts: " + ex.Message);
            }
        }

        // Start named pipe server to receive overlay commands (TOGGLE, WELCOME)
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
                            {
                                ToggleOverlay();
                            }
                            else if (command == "WELCOME")
                            {
                                ToggleWelcomeMode();
                            }
                            else if (command.StartsWith("NOTIFY:"))
                            {
                                string msg = command.Substring("NOTIFY:".Length).Trim();

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var popup = new ToastPopup(msg, "🎮"); // Optional icon
                                    popup.Show(); // Show the toast without stealing focus
                                });
                            }



                        });
                    }
                    catch { /* Ignore pipe errors */ }
                }
            });
        }

        private void ShowToastNotification(string message)
        {
            var popup = new ToastPopup(message);
            popup.Show();
        }


        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST = 0x00000008;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static void MakeWindowNoActivate(IntPtr hwnd)
        {
            var exStyle = (int)GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            SetWindowLong(hwnd, GWL_EXSTYLE, (IntPtr)exStyle);

            SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
        private int activeToastCount = 0;


        // Toggle regular shortcut overlay on/off
        private void ToggleOverlay()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                isWelcomeMode = false;
                WelcomeText.Visibility = Visibility.Collapsed;
                WelcomeShortcutScroll.Visibility = Visibility.Collapsed;
                ShortcutCarousel.Visibility = Visibility.Visible;
                welcomeProgressBar.Visibility = Visibility.Collapsed;

                this.Show();
                this.Topmost = true;
                this.Activate();
            }
        }

        // Toggle animated welcome mode
        private void ToggleWelcomeMode()
        {
            if (isWelcomeMode && this.Visibility == Visibility.Visible)
            {
                CloseWelcome();
            }
            else
            {
                WelcomeText.Visibility = Visibility.Visible;
                WelcomeShortcutScroll.Visibility = Visibility.Visible;
                ShortcutCarousel.Visibility = Visibility.Collapsed;
                welcomeProgressBar.Visibility = Visibility.Visible;

                this.Show();
                this.Topmost = true;
                this.Activate();

                isWelcomeMode = true;
                StartWelcomeSlide();

                // Calculate total animation duration
                double totalDuration = Shortcuts.Count * 1.5 + 1.5;
                double interval = totalDuration / 100.0;

                welcomeProgressBar.Value = 0;

                welcomeAutoCloseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(interval)
                };

                welcomeAutoCloseTimer.Tick += (s, e) =>
                {
                    welcomeProgressBar.Value += 1;
                    if (welcomeProgressBar.Value >= 100)
                    {
                        welcomeAutoCloseTimer.Stop();
                        CloseWelcome();
                    }
                };

                welcomeAutoCloseTimer.Start();
            }
        }

        // Hides welcome mode but keeps the app running
        private void CloseWelcome()
        {
            WelcomeText.Visibility = Visibility.Collapsed;
            WelcomeShortcutScroll.Visibility = Visibility.Collapsed;
            ShortcutCarousel.Visibility = Visibility.Collapsed;
            welcomeProgressBar.Visibility = Visibility.Collapsed;
            isWelcomeMode = false;
            this.Visibility = Visibility.Hidden;
        }

        // Slide through all shortcuts in welcome scroll viewer
        private void StartWelcomeSlide()
        {
            if (Shortcuts.Count == 0)
                return;

            WelcomeShortcutViewer.Children.Clear();

            foreach (var shortcut in Shortcuts)
            {
                var panel = CreateShortcutPanel(shortcut);
                WelcomeShortcutViewer.Children.Add(panel);
            }

            double targetOffset = Math.Max(0, (Shortcuts.Count - 2) * 120);
            ScrollAnimationHelper.AnimateVerticalScroll(WelcomeShortcutScroll, 0, targetOffset, Shortcuts.Count * 1.5);
            WelcomeShortcutScroll.ScrollToVerticalOffset(0);
        }

        // Create one visual tile for each shortcut
        private UIElement CreateShortcutPanel(Shortcut shortcut)
        {
            var border = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(40, 45, 60),
                    Color.FromRgb(25, 25, 35),
                    new Point(0, 0),
                    new Point(1, 1)),
                Width = 420,
                Height = 110,
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(15),
                Margin = new Thickness(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                BorderThickness = new Thickness(2)
            };

            var stack = new StackPanel();
            var keyText = new TextBlock
            {
                FontSize = 26,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 220, 255)),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Text = $"{shortcut.Key1} + {shortcut.Key2}"
            };

            var functionText = new TextBlock
            {
                FontSize = 20,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                Text = shortcut.Function
            };

            stack.Children.Add(keyText);
            stack.Children.Add(functionText);
            border.Child = stack;
            return border;
        }
    }

    // Helper class to scroll vertically via animation
    public static class ScrollAnimationHelper
    {
        private static readonly DependencyProperty DummyOffsetProperty =
            DependencyProperty.RegisterAttached(
                "DummyOffset", typeof(double), typeof(ScrollAnimationHelper),
                new PropertyMetadata(0.0, OnDummyOffsetChanged));

        private static void OnDummyOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }

        public static void AnimateVerticalScroll(ScrollViewer scrollViewer, double from, double to, double durationSeconds)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                RepeatBehavior = RepeatBehavior.Forever
            };
            scrollViewer.BeginAnimation(DummyOffsetProperty, animation);
        }
    }

    // Data model for shortcuts
    public class Shortcut
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Function { get; set; }
        public bool Enabled { get; set; }
    }

    
    
    }



