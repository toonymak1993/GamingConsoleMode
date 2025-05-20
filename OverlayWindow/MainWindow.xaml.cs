using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace OverlayWindow
{
    public partial class MainWindow : Window
    {
        private readonly string shortcutPath;
        public ObservableCollection<Shortcut> Shortcuts { get; set; }
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "shortcuts");
            Shortcuts = new ObservableCollection<Shortcut>();
            ShortcutCarousel.Items.Clear();
            ShortcutCarousel.ItemsSource = Shortcuts;
            StartShortcutWatcher();
            StartPipeServer();
            this.Hide(); // at start hide
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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
                            {
                                if (this.Visibility == Visibility.Visible)
                                {
                                    this.Hide();
                                }
                                else
                                {
                                    this.Show();
                                    this.Topmost = true;
                                    this.Activate();
                                }
                            }
                            else if (command == "SHOW")
                            {
                                this.Show();
                                this.Topmost = true;
                                this.Activate();
                            }
                            else if (command == "HIDE")
                            {
                                this.Hide();
                            }
                        });
                    }
                    catch
                    {
                        // Ignorieren oder loggen
                    }
                }
            });
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
