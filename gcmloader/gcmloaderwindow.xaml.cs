using GAMINGCONSOLEMODE;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml.Media.Animation;
using System.Text;
using Discord.WebSocket;
using System.ServiceProcess;
using System.Linq;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using NAudio.CoreAudioApi;
using Microsoft.UI;
using System.Data;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.UI.Windowing;
using Windows.System;
using SharpDX.XInput;
using Button = Microsoft.UI.Xaml.Controls.Button;
using System.Drawing;
using System.Windows.Forms;
using System.Media;
using Point = System.Drawing.Point;
using System.IO.Pipes;
using Application = Microsoft.UI.Xaml.Application;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using Microsoft.UI.Xaml.Input;
using Color = Windows.UI.Color;
using Discord;
using Vanara.PInvoke;
using static Vanara.PInvoke.Shell32;
using System.Xml;
using Microsoft.Win32.TaskScheduler;



namespace gcmloader
{

    public sealed partial class MainWindow : Window
    {
        #region needed

        private int _selectedCardIndex = 0;
        private int _selectedButtonIndex = 0;
        private DispatcherTimer _taskRefreshTimer;
        private HashSet<GamepadButtonFlags> _pressedButtons = new();





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
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

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
        private static readonly string SettingsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public MainWindow()
        {
            this.InitializeComponent();
            perfectsettings();
            // Zugriff auf das Grid-Root-Element
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.KeyDown += MainWindow_KeyDown;
                rootElement.Loaded += (_, __) =>
                {
                    rootElement.Focus(FocusState.Programmatic);
                };
            }
            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.UnhandledException += CurrentApp_UnhandledException;
            this.Activated += MainWindow_Activated;
            this.Activated += (s, e) => this.Content.Focus(FocusState.Programmatic);
            LoadShortcutsFromSettings();
            SetupGamepad();
            Start();

            //ASYNC PROZES
            ShowTaskManager();
            //after 10 seconds AND Start Windows Partmode
            StartAsynctasks();

        }
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
            // 🔍 Ziel-Element finden
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

                // 🎯 1. Versuche benutzerdefiniertes Icon zu laden
                if (!string.IsNullOrEmpty(optionalIconPath) && File.Exists(optionalIconPath))
                {
                    bitmap = new BitmapImage(new Uri(optionalIconPath, UriKind.Absolute));
                }
                else
                {
                    // 🧩 2. Fallback: Icon aus .exe extrahieren
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
                    // 🖼️ Image erzeugen
                    var image = new Image
                    {
                        Source = bitmap,
                        Width = 64,
                        Height = 64,
                        HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // 🧼 Kachelinhalt setzen
                    tile.Child = image;

                    // 🖱️ Klick zum Starten
                    tile.PointerPressed += (s, e) =>
                    {
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
            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[DOMAIN EXCEPTION] {DateTime.Now}: {e.ExceptionObject}\n");

            // Öffne crash.log automatisch
            Process.Start("notepad.exe", path);
        }

        private void CurrentApp_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[UI EXCEPTION] {DateTime.Now}: {e.Message}\n");

            // Öffne crash.log automatisch
            Process.Start("notepad.exe", path);

            e.Handled = true; // verhindert App-Absturz (optional)
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
        #region overlay window
        private static Process _overlayProcess;

        private static async System.Threading.Tasks.Task StartOverlayAsync()
        {
            try
            {
                string overlayPath = @"C:\Program Files (x86)\GCM\GCM\overlaywindow\OverlayWindow.exe";

                if (!File.Exists(overlayPath))
                {
                    Console.WriteLine($"[ERROR] OverlayWindow.exe not found at: {overlayPath}");
                    return;
                }

                // Start overlay process
                _overlayProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = overlayPath,
                        WorkingDirectory = Path.GetDirectoryName(overlayPath),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                _overlayProcess.Start();
                Console.WriteLine("[INFO] OverlayWindow started.");


                // Load and apply setting for showshortcutsatstartup
                try
                {
                    bool showshortcutsatstartup = AppSettings.Load<bool>("showshortcutsatstartup");
                    if (showshortcutsatstartup)
                    {
                        // Wait a moment to allow it to initialize
                        await System.Threading.Tasks.Task.Delay(500); // give it time to start the pipe server

                        // Send WELCOME mode to overlay
                        using var pipeClient = new NamedPipeClientStream(".", "GCMOverlayPipe", PipeDirection.Out);
                        pipeClient.Connect(2000); // wait up to 2s
                        using var writer = new StreamWriter(pipeClient) { AutoFlush = true };
                        writer.WriteLine("WELCOME");
                        Console.WriteLine("[INFO] Sent WELCOME command to overlay.");

                        // Wait for welcome animation to complete (e.g. 6 seconds)
                        await System.Threading.Tasks.Task.Delay(6000);
                    }
                }    
            
            catch
            {
                // If not found or invalid, default to false
                AppSettings.Save("showshortcutsatstartup", false);
            }
        }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Problem starting OverlayWindow: " + ex.Message);
            }
        }




        private static void StopOverlay()
        {
            try
            {
                if (_overlayProcess != null && !_overlayProcess.HasExited)
                {
                    _overlayProcess.Kill();
                    _overlayProcess.WaitForExit();
                    _overlayProcess.Dispose();
                    _overlayProcess = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Overlay error: {ex.Message}");
            }
        }

        #endregion overlay window

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Get the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Remove window borders and set to popup style
            IntPtr style = GetWindowLongPtr(hwnd, GWL_STYLE);
            style = (IntPtr)((long)style & ~WS_OVERLAPPEDWINDOW | WS_POPUP);
            SetWindowLongPtr(hwnd, GWL_STYLE, style);

            // Get screen dimensions
            int screenWidth = GetScreenWidth();
            int screenHeight = GetScreenHeight();

            // Set the window size to fullscreen
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, SWP_NOZORDER | SWP_SHOWWINDOW);

            // Set the wallpaper as the background
            SetBackgroundImage(screenWidth, screenHeight);
        }


        private bool IsWindowInForeground()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            return GetForegroundWindow() == hWnd;
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
        #region methodes for code

        public static void SendOverlayNotification(string message)
        {
            
            try
            {
                bool shortcutpopup = AppSettings.Load<bool>("shortcutpopup");

                if (shortcutpopup)
                {
                    //play sound

                    try
                    {
                        SoundPlayer player = new SoundPlayer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\shortcut.wav"));
                        player.Play();
                    }
                    catch { }


                    try
                    {
                       


                        using var client = new NamedPipeClientStream(".", "GCMOverlayPipe", PipeDirection.Out);
                        client.Connect(1000);
                        using var writer = new StreamWriter(client) { AutoFlush = true };
                        writer.WriteLine("NOTIFY:" + message);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Could not send toast to overlay: " + ex.Message);
                    }

                }
            }
            catch
            {

                AppSettings.Save("shortcutpopup", true);
            }
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
        static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        #endregion methodes for code
        #region functions
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
                            Thread.Sleep(3000);

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
        public void preaudio(bool start,bool end)
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
        public void WaitForLauncherToClose()
        {

            string Launcher = AppSettings.Load<string>("launcher");
            Process[] processes;
            
            if (Launcher == "custom")
            {
                Launcher = Path.GetFileName(AppSettings.Load<string>("customlauncherpath"));
                Launcher = Launcher.Substring(0, Launcher.Length - 4);

                string ProcessName = Launcher;
                do
                {
                    Thread.Sleep(3000);
                    processes = Process.GetProcessesByName(ProcessName);
                } while (processes.Length > 0);

                // back to windows 
                displayfusion("end");
                BackToWindows();
                CleanupLogging();
                //close


            }
            else if (Launcher == "playnite")
            {
                string ProcessName = "Playnite.FullscreenApp";
                do
                {
                    Thread.Sleep(3000);
                    processes = Process.GetProcessesByName(ProcessName);
                } while (processes.Length > 0);

                // back to windows 
                displayfusion("end");
                BackToWindows();
                CleanupLogging();
                //close
            }
            else if (Launcher == "steam")
            {

                string ProcessName = Launcher;
                do
                {
                    Thread.Sleep(3000);
                    processes = Process.GetProcessesByName(ProcessName);
                } while (processes.Length > 0);

                // back to windows 
                Console.WriteLine("--Launcher Close--");
                displayfusion("end");
                BackToWindows();
                CleanupLogging();
                //close
            }
            else
            {
                // back to windows 
                displayfusion("end");
                BackToWindows();
                CleanupLogging();
                //close
            }

        }
        private static bool IsAlreadyRunning()
        {
            string currentProcessName = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(currentProcessName);
            return processes.Length > 1;
        }
        static void AdminVerify()
        {
            if (!IsAdministrator())
            {
                Console.WriteLine("Restarting as admin");
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Verb = "runas"
                };

                try
                {
                    Process.Start(startInfo);
                    Environment.Exit(0); // Ensure the program exits immediately
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error restarting application as administrator: " + ex.Message);
                    Environment.FailFast("Failed to restart as administrator.", ex);
                }
            }
            else
            {
                uac("off");
            }
        }
        private void SetBackgroundImage(int width, int height)
        {
            try
            {
                try
                {
                    bool gcmwallpapercheck = AppSettings.Load<bool>("gcmwallpaper");
                }
                catch
                {    // If the setting is not found, default to false
                    AppSettings.Save("gcmwallpaper", false);
                }

                string imagePath;
                bool gcmwallpaper = AppSettings.Load<bool>("gcmwallpaper");

                if (gcmwallpaper)
                {
                    // Custom GCM wallpaper logic
                    imagePath = Settwallpaper(); // <- deine eigene Methode
                }
                else
                {
                    // Fallback: get the current desktop wallpaper from registry
                    imagePath = Registry.GetValue(
                        @"HKEY_CURRENT_USER\Control Panel\Desktop",
                        "WallPaper",
                        "") as string;
                }

                if (!File.Exists(imagePath))
                {
                    Debug.WriteLine("Wallpaper path not found: " + imagePath);
                    return;
                }

                // Create an Image control
                var backgroundImage = new Microsoft.UI.Xaml.Controls.Image
                {
                    Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute)),
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                backgroundImage.SetValue(Canvas.ZIndexProperty, -1);

                if (this.Content is Grid mainGrid)
                {
                    var existingBackground = mainGrid.Children.OfType<Microsoft.UI.Xaml.Controls.Image>().FirstOrDefault();
                    if (existingBackground != null)
                    {
                        existingBackground.Source = backgroundImage.Source;
                    }
                    else
                    {
                        mainGrid.Children.Insert(0, backgroundImage);
                    }
                }
                else
                {
                    Grid grid = new Grid();
                    grid.Children.Add(backgroundImage);

                    if (this.Content != null)
                        grid.Children.Add((UIElement)this.Content);

                    this.Content = grid;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error setting wallpaper: " + ex.Message);
            }
        }

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
        public void displayfusion(string art)
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
                // Get the path of the AppData folder
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                // Create the full path to the "gcmsettings" folder in AppData
                string settingsFolderPath = Path.Combine(appDataPath, "gcmsettings");

                // Create the full path to the "settings.json" file within the folder
                string settingsFilePath = Path.Combine(settingsFolderPath, "settings.json");

                // Check if the "gcmsettings" folder exists
                if (Directory.Exists(settingsFolderPath))
                {
                    // Check if the "settings.json" file exists in the folder
                    if (File.Exists(settingsFilePath))
                    {
                        Console.WriteLine($"The file 'settings.json' exists in the folder '{settingsFolderPath}'.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"The file 'settings.json' is missing in the folder '{settingsFolderPath}'.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"The folder 'gcmsettings' does not exist in AppData.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while verifying the settings: {ex.Message}");
                return false;
            }

        }
        private void FirstStart()
        {
            // Warning message about modifying the Windows registry
            string message = "This application modifies the Windows registry and may temporarily block your PC if used improperly. " +
                             "I disclaim any responsibility for improper use. If you encounter any issues, please visit the project on GitHub: " +
                             "https://github.com/Kosnix/GameConsoleMode";
            string caption = "First Start";

            Console.WriteLine(message);

            // Thank you message and initial configuration instructions
            message = "Thank you for downloading my app. This is the first start of the application, please configure it. The settings window will appear.";
            Console.WriteLine(message);
            // Notification for the next startup
            message = "Next time, the application will start directly.";
            Console.WriteLine(message);

            // Launch the settings file and terminate the program
            Process.Start(new ProcessStartInfo(Path.Combine(exeFolder(), "GAMINGCONSOLEMODE.exe")));
            Console.WriteLine("Settings launched");
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
                            Console.WriteLine($"Error attempting to kill explorer.exe: {ex.Message}");
                        }
                    }
                }
            }
        }
        static void BackToWindows()
        {
            try
            {
                //check  startup apps winpart
                try
                {
                    bool usewinpartstartapps = AppSettings.Load<bool>("usewinpartstartapps");
                    if (usewinpartstartapps == true)
                    {
                        //First restore all Autostartapps Not POPUPS
                        StartupControl.RestoreStartupApps();
                    }
                    else
                    {

                    }
                }
                catch
                {

                }


                const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                const string valueName = "Shell";
                const string newValue = @"explorer.exe";

                // Open registry key for writing
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, true))
                {
                    if (key != null)
                    {
                        Console.WriteLine("Registry key opened successfully.");

                        // Modify value in registry key
                        key.SetValue(valueName, newValue, RegistryValueKind.String);
                        Console.WriteLine($"Value '{valueName}' has been changed to '{newValue}'.");
                        //End Decky Loader process if running
                        Process[] deckyLoaderProcesses = Process.GetProcessesByName("PluginLoader_noconsole");

                        if (deckyLoaderProcesses.Length > 0)
                        {
                            foreach (var process in deckyLoaderProcesses)
                            {
                                process.Kill();
                                process.WaitForExit();
                                Console.WriteLine("Decky Loader process killed successfully.");
                            }
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Unable to open registry key '{keyName}'.");
                    }
                }

            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access Denied. Run the application as an administrator.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            
        }
        static void StartLauncher()
        {
            string launcher = AppSettings.Load<string>("launcher");
            switch (launcher)
            {
                case "steam":
                    StartSteam();
                    break;

                case "playnite":

                    StartPlaynite();
                    break;

                case "custom":
                    StartOtherLauncher();
                    break;

                default:
                    Console.WriteLine("Invalid launcher. Defaulting to Custom.");
                    launcher = "steam";
                    AppSettings.Save("launcher", launcher);
                    BackToWindows();

                    break;
            }

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
        static void ConsoleModeToShell()
        {
                try
                {
                    const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                    const string valueName = "Shell";

                string targetExecutable = @"C:\Program Files (x86)\GCM\GCM\gcmloader\gcmloader.exe";



                if (!File.Exists(targetExecutable))
                    {
                        //Logger.Logger.Log($"Error: The file '{targetExecutable}' does not exist.");
                        return;
                    }

                    // Open registry key for writing
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, writable: true))
                    {
                        if (key != null)
                        {
                            // Modify value in registry key
                            key.SetValue(valueName, targetExecutable, RegistryValueKind.String);

                            // Verify the change
                            string currentValue = key.GetValue(valueName)?.ToString();
                            if (currentValue == targetExecutable)
                            {

                                //KillProcess("explorer.exe");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to set '{valueName}'. Current value: {currentValue}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Unable to open registry key '{keyName}'.");
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Error: Access Denied. Run the application as an administrator.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
        }
        private void SettingsVerify()
        {

            if (!VerifySettings())
            {
                Console.WriteLine("The settings folder or file is missing. Initializing first start process...");
                FirstStart();
            }

            //verif launcher//
            string launcher = AppSettings.Load<string>("launcher");
            if (launcher == "steam" || launcher == "playnite" || launcher == "custom")
            {
                Console.WriteLine("The selected launcher is valid");



                if (launcher == "steam")
                {
                    string steamPath = AppSettings.Load<string>("steamlauncherpath");
                    if (!string.IsNullOrEmpty(steamPath) && File.Exists(steamPath))
                    {
                        Console.WriteLine("The Steam path is valid.");

                        // Try to load deckyloader setting
                        bool usedeckyloader = false;

                        try
                        {
                            // Try to load the value from settings
                            usedeckyloader = AppSettings.Load<bool>("usedeckyloader");
                        }
                        catch
                        {
                            // Key doesn't exist or loading failed → set to false
                            AppSettings.Save("usedeckyloader", false);
                            usedeckyloader = false;
                        }

                        // Now handle logic cleanly
                        if (usedeckyloader)
                        {
                            // deckyloader is enabled
                            Console.WriteLine("DeckyLoader is enabled");
                        }
                        else
                        {
                            // deckyloader is disabled or was not set and now defaulted
                            Console.WriteLine("DeckyLoader is disabled or not set");
                            //set deckyloader disabled
                            AppSettings.Save("usedeckyloader", false);
                        }


                    }
                    else
                    {
                        //MessageBox.Show("The Steam path is invalid or non-existent. Use the Settings.exe file to correct this.");
                        CleanupLogging();
                        Environment.Exit(0);
                    }
                }

                if (launcher == "playnite")
                {
                    string PlaynitePath = AppSettings.Load<string>("playnitelauncherpath");
                    if (!string.IsNullOrEmpty(PlaynitePath) && File.Exists(PlaynitePath))
                    {
                        Console.WriteLine("The playnite path is valid.");
                    }
                    else
                    {
                        Console.WriteLine("The playnite path is invalid or non-existent. Use the Settings.exe file to correct this.");
                        CleanupLogging();
                        Environment.Exit(0);
                    }
                }

                if (launcher == "custom")
                {
                    string OtherLauncherPath = AppSettings.Load<string>("customlauncherpath");
                    if (!string.IsNullOrEmpty(OtherLauncherPath) && File.Exists(OtherLauncherPath))
                    {
                        Console.WriteLine("The launcher path is valid.");
                    }
                    else
                    {
                        // MessageBox.Show("The launcher path is invalid or non-existent. Use the Settings.exe file to correct this.");
                        Environment.Exit(0);
                    }
                }
            }
            else
            {
                //MessageBox.Show("The selected launcher is invalid or non-existent. Use the Settings.exe file to fix this");
                CleanupLogging();
                Environment.Exit(0);
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


        public static void MinimizeAllWindows()
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                    {
                        // 🛑 Skip UWP shell host to preserve child window
                        return true;
                    }

                    ShowWindow(hWnd, SW_MINIMIZE);
                }
                catch { }

                return true;
            }, IntPtr.Zero);
        }


        //needed
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        private const int SW_HIDE = 0;

        public static void winpart()
        {
            
            try
            {
                bool usewinpart = true;
                
                if (usewinpart == true)
                {

            try
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
                        
                Console.WriteLine("Starting explorer.exe...");

                        // Check if explorer.exe is already running
                        var explorerRunning = Process.GetProcessesByName("explorer").Any();
                        TaskManagerDebloatServices();
                        if (!explorerRunning)
                        {
                            // Start explorer.exe if not running
                            Process.Start("explorer.exe");
                        }
                        else
                        {
                            
                        }

                            Thread.Sleep(5000);
                //setTaskbar();
                //HideShellWindow("Windows.UI.StartMenu");
                KillProcess("WidgetBoard");
                KillProcess("WidgetService");
                DesktopIconController.HideDesktopIcons();
                 // Make taskbar invisible
                TaskbarVisibility.HideTaskbar();

         

                Console.WriteLine("Shell windows hidden.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            //set gcmloader again
            try
            {
                const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                const string valueName = "Shell";

                        // Get the path of the current directory and append the target executable name
                        // Get the directory of the current executable
                        string targetExecutable = @"C:\Program Files (x86)\GCM\GCM\gcmloader\gcmloader.exe";


                        if (!File.Exists(targetExecutable))
                {
                    //Logger.Logger.Log($"Error: The file '{targetExecutable}' does not exist.");
                    return;
                }

                // Open registry key for writing
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, writable: true))
                {
                    if (key != null)
                    {
                        // Modify value in registry key
                        key.SetValue(valueName, targetExecutable, RegistryValueKind.String);

                        // Verify the change
                        string currentValue = key.GetValue(valueName)?.ToString();
                        if (currentValue == targetExecutable)
                        {
                            Console.WriteLine($" set Current value: {currentValue} without kill for later");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to set '{valueName}'. Current value: {currentValue}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unable to open registry key '{keyName}'.");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access Denied. Run the application as an administrator.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }




                }
                else
                {

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
    ("BthAvctpSvc", null),                   // Bluetooth AV
    ("PhoneSvc", null),                      // Telefon-Anbindung
    ("WerSvc", null),                        // Fehlerberichte
    ("WbioSrvc", null),                      // Biometrie (wenn kein Fingerprint)
    ("Spooler", null),                       // Druckerwarteschlange
    ("dmwappushservice", null),              // Push Notifications
    ("ConnectedUserExperiencesAndTelemetry", null), // Feedback & Telemetrie :contentReference[oaicite:2]{index=2}
    ("AppXSvc", null),                       // AppX Deployment :contentReference[oaicite:3]{index=3}
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
    ("BthAvctpSvc", null),
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
                Thread.Sleep(3000);
            }
        }

        private void Showwinpart()
        {
            //check  startup apps winpart
            try {
                bool usewinpartstartapps = AppSettings.Load<bool>("usewinpartstartapps");
                if (usewinpartstartapps == true)
                {
                    //First Disable all Autostartapps for Not Popups and Windows Partstart
                    StartupControl.DisableAllStartupApps();
                }
                else
                {

                }
            }
            catch
            {
                
            }
            // Creates a timer for 10 seconds
            var hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            hideTimer.Tick += (s, e) =>
            {
                // Stops the timer and shows the StackPanel
                hideTimer.Stop();
                winpart();
            };
            hideTimer.Start();
        }

        #endregion winparts

        #endregion functions
        #region launcher
        static void StartSteam()
        {

            //for deckyloader
            bool deckyloadertrigger = false;

            if (string.IsNullOrWhiteSpace(AppSettings.Load<string>("steamlauncherpath")) || !File.Exists(AppSettings.Load<string>("steamlauncherpath")))
            {
                Console.WriteLine("Error: SteamPath is empty, invalid, or does not exist.");
                BackToWindows();
                return;
            }

            KillProcess("steam.exe");
            Console.WriteLine("try start Steam");

            // Check if decky Loader is activated
            if (AppSettings.Load<bool>("usedeckyloader") == true)
            {

                deckyloadertrigger = true;
                //first clear other process with deckyloader
                //End Decky Loader process if running
                Process[] deckyLoaderProcesses = Process.GetProcessesByName("PluginLoader_noconsole");
                if (deckyLoaderProcesses.Length > 0)
                {
                    foreach (var process in deckyLoaderProcesses)
                    {
                        process.Kill();
                        process.WaitForExit();
                        Console.WriteLine("Decky Loader process killed successfully.");
                    }
                }
                //set the trigger for Deckyloader
               

                //Start Decky Loader Steam
                //search and start decky loader no console for steam.
                // Get the user's home directory dynamically
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Construct the full path to PluginLoader_noconsole.exe
                string pluginLoaderPath = Path.Combine(userHome, "homebrew", "services", "PluginLoader_noconsole.exe");

                // Check if the executable file exists
                if (File.Exists(pluginLoaderPath))
                {
                    Console.WriteLine("PluginLoader_noconsole.exe found. Starting...");

                    // Start the process
                    Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pluginLoaderPath,
                            UseShellExecute = true // Ensures the executable runs properly
                        }
                    };
                    process.Start();

                    // Wait until the process is running and not exited
                    while (true)
                    {
                        process.Refresh(); // Update the process info

                        if (process.HasExited)
                        {
                            Console.WriteLine("PluginLoader exited unexpectedly.");
                            break;
                        }

                        try
                        {
                            // Check if the process has a valid start time (indicates it has initialized)
                            var _ = process.StartTime;
                            break;
                        }
                        catch
                        {
                            // StartTime not yet available, wait and try again
                        }

                        Thread.Sleep(1000); // Wait a bit before checking again
                    }

                    Console.WriteLine("PluginLoader is running. Continuing...");

                    try
                    {
                        string Path = AppSettings.Load<string>("steamlauncherpath");
                        string arguments;
                        //  if (AppSettings.Load<bool>("usestartupvideo")){
                        // arguments = "-gamepadui -noverifyfiles -nobootstrapupdate -skipinitialbootstrap -overridepackageurl";
                        //  }
                        //  else
                        //  {
                        arguments = "-dev -gamepadui -noverifyfiles -nobootstrapupdate -skipinitialbootstrap -overridepackageurl -noinstro ";
                        //  }

                        Process.Start(new ProcessStartInfo(Path, arguments));
                        Console.WriteLine("Steam launched");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error launching Steam: " + ex.Message);
                        BackToWindows();
                        Console.WriteLine("explorer restored");
                    }
                }
                else
                {
                    Console.WriteLine("Error: PluginLoader_noconsole.exe not found start normal!");
                    // Start Steam normal
                    try
                    {
                        string Path = AppSettings.Load<string>("steamlauncherpath");
                        string arguments;
                        //  if (AppSettings.Load<bool>("usestartupvideo")){
                        // arguments = "-gamepadui -noverifyfiles -nobootstrapupdate -skipinitialbootstrap -overridepackageurl";
                        //  }
                        //  else
                        //  {
                        arguments = "-gamepadui -noverifyfiles -nobootstrapupdate -skipinitialbootstrap -overridepackageurl -nointro";
                        //  }

                        Process.Start(new ProcessStartInfo(Path, arguments));
                        Console.WriteLine("Steam launched");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error launching Steam: " + ex.Message);
                        BackToWindows();
                        Console.WriteLine("explorer restored");
                    }


                }

              
            }
            else
            {
          
            }

            if (deckyloadertrigger == true) // deckyloader is not triggered
            {
            }
            else if (deckyloadertrigger == false)
            {
                // Start Steam normal
                try
                {
                    string Path = AppSettings.Load<string>("steamlauncherpath");
                    string arguments;
                    //  if (AppSettings.Load<bool>("usestartupvideo")){
                    // arguments = "-gamepadui -noverifyfiles -nobootstrapupdate -skipinitialbootstrap -overridepackageurl";
                    //  }
                    //  else
                    //  {
                    arguments = "-gamepadui -noverifyfiles -nobootstrapupdate -skipinitialbootstrap -overridepackageurl -nointro";
                    //  }

                    Process.Start(new ProcessStartInfo(Path, arguments));
                    Console.WriteLine("Steam launched");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error launching Steam: " + ex.Message);
                    BackToWindows();
                    Console.WriteLine("explorer restored");
                }
            }
        }
        static void StartPlaynite()
        {
            if (string.IsNullOrWhiteSpace(AppSettings.Load<string>("playnitelauncherpath")) || !File.Exists(AppSettings.Load<string>("playnitelauncherpath")))
            {

                //Logger.Logger.Log($"Error: PlaynitePath is empty, invalid, or does not exist.");
                BackToWindows();
                return;
            }
            KillProcess("Playnite.FullscreenApp.exe");
            try
            {
                string arguments = " --hidesplashscreen";
                string Path = AppSettings.Load<string>("playnitelauncherpath");
                Process.Start(new ProcessStartInfo(Path, arguments));
                // Logger.Logger.Log("Playnite launched");
            }
            catch (Exception ex)
            {
                //Logger.Logger.Log("Error launching Playnite");
                BackToWindows();
                //Logger.Logger.Log("explorer restored");
            }
        }
        static void StartOtherLauncher()
        {
            string launcherPath = AppSettings.Load<string>("customlauncherpath");

            if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
            {
                Console.WriteLine("Error: OtherLauncherPath is empty, invalid, or does not exist.");
                BackToWindows();
                return;
            }

            KillProcess(Path.GetFileName(launcherPath));

            try
            {
                Process.Start(new ProcessStartInfo(launcherPath));
                Console.WriteLine("OtherLauncher launched");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error launching OtherLauncher: " + ex.Message);
                BackToWindows();
                Console.WriteLine("Explorer restored");
            }
        }
        #endregion launcher
        #region start
        private async System.Threading.Tasks.Task Start()
        {
            if (IsAlreadyRunning())
            {
                Console.WriteLine("Another instance of the application is already running.");
                Environment.Exit(0);
            }
            else // First Instance
            {
                // Logger.Logger.Log($"start SETUPLOGGIN:");
                SetupLogging();
                // Logger.Logger.Log($"start ADMINVERIFY:");
                AdminVerify();
                if (IsAdministrator())
                {
                    
                    SettingsVerify();
                    MinimizeAllWindows();
                    Console.WriteLine("--overlay--");
                    await StartOverlayAsync();
                    Console.WriteLine("--Start showwinpart--");
                    Showwinpart();
                    Console.WriteLine("--preinstall check--");
                    #region pre install/start check if needed
                    if (IsHandheld() == true)
                    {

                        //infopanel
                        wifibutton.Visibility = Visibility.Visible;
                        blebutton.Visibility = Visibility.Visible;

                        // Handheld Launcher
                        try
                            {
                            bool handheldlauncher = AppSettings.Load<bool>("handheldtouchlauncher");
                            if(handheldlauncher)
                            {
                                LauncherTileRow.Visibility = Visibility.Visible;
                               
                            }
                            else
                            {
                                LauncherTileRow.Visibility = Visibility.Collapsed;
                              
                            }

                            }
                            catch
                            {
                            AppSettings.Save("handheldtouchlauncher", false);
                            }
                    }
                    #endregion pre install/start check if needed
                    Console.WriteLine("--startupvideo--");
                    StartupVideo.Play();
                    Console.WriteLine("--displayfusion--");
                    displayfusion("start");
                    Console.WriteLine("--joyxoff--");
                    IsJoyxoffInstalledAndStart(); //only check if is installed, than start
                    Console.WriteLine("--startlauncher--");
                    StartLauncher();
                    Console.WriteLine("--kill--");
                    #region kill distubing process
                    //KillTargetProcess("");
                    #endregion kill distubing process
                    Console.WriteLine("--cssloader--");
                    cssloader(); //only check if is installed, than start
                    
                    ///////////////
                    Console.WriteLine("--modetoshell--");
                    ConsoleModeToShell();
                    Console.WriteLine("--preaudio--");
                    preaudio(true,false);
                    Console.WriteLine("--prestartlist--");
                    prestartlist();
                    Console.WriteLine("--waitclose--");
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        WaitForLauncherToClose();

                    });
                    Console.WriteLine("--startupvideoend--");
                    try
                    {
                        StartupVideo.RenameSteamStartupVideo_End();
                    }
                    catch { }

                    Console.WriteLine("--end--");
                    preaudio(false, true);

                    #region Handheld
                    #region Ally
                    if(IsHandheld() == true)
                    {
                       
                    }
                    #endregion Ally
                    #endregion Handheld
                    #region uac
                    try
                    {
                        
                        bool useuac = AppSettings.Load<bool>("uac");
                        if (useuac == true)
                        {
                            Console.WriteLine("UAC is enabled, setting to on");
                            //useuac is aktive 
                            uac("on");
                        }
                        else if (useuac == false)
                        {
                            Console.WriteLine("UAC is disabled, setting to off");
                            //User set it to off dont do nothing
                            uac("off");
                        }
                    }
                    catch
                    {
                        Console.WriteLine("UAC is enabled, setting to on");
                        //error in read
                        AppSettings.Save("uac", true);
                        uac("on");
                    }
                    #endregion uac
                    StopOverlay();
                    // Show taskbar again
                    TaskManagerReEnableServices();
                    DesktopIconController.ShowDesktopIcons();
                    TaskbarVisibility.ShowTaskbar();
                    #region wingamepad
                    // check if wingamepas is aktiv and set tasksheduller
                   // test it needed
                    try
                    {
                        AppSettings.Load<bool>("useseamlessswitchtogcm");
                    }
                    catch
                    {
                        // if not set, set it to false
                        AppSettings.Save("useseamlessswitchtogcm", false);
                      
                    }
                    // test if wingamepad is aktiv in task sheduller
                    bool taskExistsAndEnabled = IsTaskActive("GCM_wingamepad");

                    //task is OFF
                    if (!taskExistsAndEnabled) //if job is not aktivated and useseamlessswitchtogcm is true
                    {
                    if (AppSettings.Load<bool>("useseamlessswitchtogcm") == true)
                    {
                        //aktivate task job
                        using (TaskService ts = new TaskService())
                        {
                            try
                            {
                                string taskName = "GCM_wingamepad";

                                // Clean up previous task
                                ts.RootFolder.DeleteTask(taskName, false);
                                

                                TaskDefinition td = ts.NewTask();
                                td.RegistrationInfo.Description = "Start GCM wingamepad mode";
                                td.Principal.UserId = WindowsIdentity.GetCurrent().Name;
                                td.Principal.LogonType = TaskLogonType.InteractiveToken;
                                td.Principal.RunLevel = TaskRunLevel.Highest;

                                td.Triggers.Add(new LogonTrigger
                                {
                                    Delay = TimeSpan.FromSeconds(3),
                                    Enabled = true
                                });

                                string exePath = @"C:\Program Files (x86)\GCM\GCM\wingamepad\wingamepad.exe";
                                td.Actions.Add(new ExecAction(exePath, null, null));

                                td.Settings.StopIfGoingOnBatteries = false;
                                td.Settings.DisallowStartIfOnBatteries = false;
                                td.Settings.RunOnlyIfIdle = false;
                                td.Settings.RunOnlyIfNetworkAvailable = false;
                                td.Settings.AllowHardTerminate = false;
                                td.Settings.StartWhenAvailable = true;
                                td.Settings.AllowDemandStart = true;

                                ts.RootFolder.RegisterTaskDefinition(taskName, td,
                                    TaskCreation.CreateOrUpdate, null, null,
                                    TaskLogonType.InteractiveToken);

                               

                                Microsoft.Win32.TaskScheduler.Task task = ts.FindTask(taskName);
                                if (task != null)
                                {
                                    task.Run();
                                    //Logger.Write("Task started.");
                                }
                                else
                                {
                                    //Logger.Write("ERROR: Task was not found after registration.");
                                }
                            }
                            catch (Exception ex)
                            {
                                //Logger.Write("Error while creating task: " + ex.ToString());
                                throw;
                            }
                        }
                    }
                    }
                    //task is ON
                    if (taskExistsAndEnabled)
                    {
                        if (AppSettings.Load<bool>("useseamlessswitchtogcm") == false) //if job is aktivated and useseamlessswitchtogcm is false
                        {
                            //deaktivate task job
                            using (TaskService ts = new TaskService())
                            {
                                try
                                {
                                    ts.RootFolder.DeleteTask("GCM_wingamepad", false);
                                }
                                catch
                                {
                                    Console.WriteLine("Error deleting GCM_wingamepad task, it may not exist.");
                                }
                            }
                        }
                        else
                        {
                            //job aktiviert / gcmswitch is true
                            try
                            {
                                string exePath = @"C:\Program Files (x86)\GCM\GCM\wingamepad\wingamepad.exe";

                                if (!File.Exists(exePath))
                                {
                                    throw new FileNotFoundException("wingamepad.exe not found.", exePath);
                                }

                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = exePath,
                                    UseShellExecute = true,
                                    Verb = "runas"
                                };

                                Process.Start(startInfo);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ERROR] Failed to launch wingamepad.exe: {ex.Message}");
                            }
                        }
                    }
                    #endregion wingamepad
                    this.Close();
                }
            }
        }
        #endregion start
        #region TaskManager

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
                Interval = TimeSpan.FromSeconds(10)
            };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                ProgramCardPanel.Visibility = Visibility.Visible;
                LoadTaskManagerList();

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

        }

        private void StartAutoTaskRefresh()
        {
            if (_taskRefreshTimer != null)
                return;

            _taskRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            _taskRefreshTimer.Tick += (s, e) =>
            {
                if (IsWindowInForeground())
                {
                    LoadTaskManagerList();
                }
            };

            _taskRefreshTimer.Start();
        }


        // ====================================================================
        // Loads the list of applications
        // ====================================================================

        private static readonly string[] _excludedTitles = new[]
        {
    "Windows® Operating System",
    "System Microsoft® Windows",
    "Windows®-Betriebssystem",
    "Windows Operating System",
    "Windows-Betriebssystem",
    "ApplicationFrameHost",
    "ShellExperienceHost",
    "Realtek Audio Console",
    "StartMenuExperienceHost",
    "NVIDIA App",
    "Xbox.Apps.TCUI"
};

        private static readonly Dictionary<string, string> _nameOverrides = new()
{
    { "Steam Client WebHelper", "Steam" },
    { "ApplicationFrameHost", "UWP Host" }
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
        private const int SW_SHOWMAXIMIZED = 3;

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



        private void LoadTaskManagerList()
        {
            double scrollOffset = ProgramScrollViewer.HorizontalOffset;
            int lastIndex = _selectedCardIndex;
            var seenNames = new HashSet<string>();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) == 0)
                    return true;

                // ✅ Safely check for overlay/toolwindow flags to skip overlays
                try
                {
                    const int GWL_EXSTYLE = -20;
                    const int WS_EX_TOOLWINDOW = 0x00000080;
                    const int WS_EX_NOACTIVATE = 0x08000000;
                    const int WS_EX_LAYERED = 0x00080000;

                    int style = (int)User32.GetWindowLong(hWnd, User32.WindowLongFlags.GWL_EXSTYLE);

                    if ((style & WS_EX_TOOLWINDOW) != 0 || (style & WS_EX_NOACTIVATE) != 0 || (style & WS_EX_LAYERED) != 0)
                        return true; // skip known overlay/tool/ghost windows
                }
                catch
                {
                    // If anything fails (e.g. invalid handle), just skip this window
                    return true;
                }

                GetWindowThreadProcessId(hWnd, out uint pid);

                Process p;
                try { p = Process.GetProcessById((int)pid); } catch { return true; }

                if (pid == (uint)Process.GetCurrentProcess().Id)
                    return true;

                // 💡 Special case: UWP window via ApplicationFrameHost
                if (p.ProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                {
                    IntPtr realWindow = IntPtr.Zero;
                    uint realPid = 0;

                    EnumChildWindows(hWnd, (childHwnd, _) =>
                    {
                        GetWindowThreadProcessId(childHwnd, out realPid);
                        if (realPid != 0 && realPid != pid)
                        {
                            realWindow = childHwnd;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (realWindow != IntPtr.Zero)
                    {
                        try
                        {
                            p = Process.GetProcessById((int)realPid);
                            hWnd = realWindow;
                        }
                        catch
                        {
                            return true;
                        }
                    }
                }

                string productName;
                try { productName = p.MainModule?.FileVersionInfo?.ProductName; } catch { productName = null; }
                if (string.IsNullOrWhiteSpace(productName)) productName = p.ProcessName;

                if (_excludedTitles.Any(t => productName?.Contains(t, StringComparison.OrdinalIgnoreCase) == true))
                    return true;

                if (_nameOverrides.TryGetValue(productName, out var overrideName))
                    productName = overrideName;

                if (productName?.Contains("GAMECONSOLEMODE", StringComparison.OrdinalIgnoreCase) == true)
                    return true;

                if (!seenNames.Add(productName)) return true;

                var existing = _cardCache.FirstOrDefault(c => c.ProductName == productName);
                if (existing == null)
                {
                    var border = CreateProgramCard(productName, p.MainModule?.FileName ?? "", p, hWnd);

                    var entry = new ProgramCardEntry
                    {
                        ProductName = productName,
                        ExePath = p.MainModule?.FileName ?? "",
                        Hwnd = hWnd,
                        Proc = p,
                        Card = border
                    };

                    _cardCache.Add(entry);
                    ProgramCardPanel.Children.Add(border);
                }

                return true;
            }, IntPtr.Zero);

            // 🗑 Remove outdated entries
            for (int i = _cardCache.Count - 1; i >= 0; i--)
            {
                if (!seenNames.Contains(_cardCache[i].ProductName))
                {
                    ProgramCardPanel.Children.Remove(_cardCache[i].Card);
                    _cardCache.RemoveAt(i);
                }
            }

            // 🔁 Restore scroll and selection
            if (ProgramCardPanel.Children.Count == 0)
                _selectedCardIndex = 0;
            else if (lastIndex >= ProgramCardPanel.Children.Count)
                _selectedCardIndex = ProgramCardPanel.Children.Count - 1;
            else
                _selectedCardIndex = lastIndex;

            HighlightSelectedCard(skipScroll: true, forceAnimation: false);
            ProgramScrollViewer.ChangeView(scrollOffset, null, null, true);
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



        private Border CreateProgramCard(string name, string exePath, Process proc, IntPtr hwnd)
        {
            BitmapImage icon = null;
            Bitmap iconBitmap = null;
            Color avgColor = Colors.DimGray; // Fallback

            string appUserModelId = null;

            // Check if it's a UWP App based on path
            if (!string.IsNullOrEmpty(exePath) && exePath.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string manifestPath = Path.Combine(Path.GetDirectoryName(exePath), "AppxManifest.xml");
                    if (File.Exists(manifestPath))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(manifestPath);

                        var nsMgr = new XmlNamespaceManager(doc.NameTable);
                        nsMgr.AddNamespace("x", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                        // Try to get AppUserModelId from manifest
                        var identityNode = doc.SelectSingleNode("/x:Package/x:Identity", nsMgr);
                        var appNode = doc.SelectSingleNode("/x:Package/x:Applications/x:Application", nsMgr);

                        if (identityNode != null && appNode != null)
                        {
                            string packageName = identityNode.Attributes["Name"]?.Value;
                            string publisher = identityNode.Attributes["Publisher"]?.Value;
                            string appId = appNode.Attributes["Id"]?.Value;

                            if (!string.IsNullOrEmpty(packageName) && !string.IsNullOrEmpty(publisher) && !string.IsNullOrEmpty(appId))
                            {
                                appUserModelId = $"{packageName}_{publisher}!{appId}";
                            }
                        }

                        // Try to get DisplayName
                        var nameNode = doc.SelectSingleNode("/x:Package/x:Properties/x:DisplayName", nsMgr);
                        if (nameNode != null && !string.IsNullOrEmpty(nameNode.InnerText))
                        {
                            name = nameNode.InnerText;
                        }

                        // Try to get Logo as icon
                        var logoNode = doc.SelectSingleNode("/x:Package/x:Properties/x:Logo", nsMgr);
                        if (logoNode != null && !string.IsNullOrEmpty(logoNode.InnerText))
                        {
                            string logoPath = Path.Combine(Path.GetDirectoryName(manifestPath), logoNode.InnerText.Replace('/', '\\'));
                            if (File.Exists(logoPath))
                            {
                                icon = new BitmapImage(new Uri(logoPath));
                            }
                        }
                    }
                }
                catch
                {
                    // If parsing fails, fallback logic below
                }
            }

            // Fallback icon loading
            if (icon == null)
            {
                icon = GetAppIconAsBitmapImage(exePath);
            }

            // Fallback average color
            iconBitmap = GetBitmapFromExeIcon(exePath);
            if (iconBitmap != null)
            {
                avgColor = GetAverageColor(iconBitmap);
            }

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                Opacity = 0.6,
                GradientStops = new GradientStopCollection
        {
            new GradientStop { Color = avgColor, Offset = 0 },
            new GradientStop
            {
                Color = Color.FromArgb(180,
                    (byte)Math.Min(255, avgColor.R + 40),
                    (byte)Math.Min(255, avgColor.G + 40),
                    (byte)Math.Min(255, avgColor.B + 40)),
                Offset = 1
            }
        }
            };

            string description = !string.IsNullOrEmpty(proc.MainWindowTitle) ? proc.MainWindowTitle : "Running process";

            var contentStack = new StackPanel
            {
                Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var image = new Microsoft.UI.Xaml.Controls.Image
            {
                Source = icon,
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 10, 0, 10),
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
            };

            var title = new TextBlock
            {
                Text = name.Length > 12 ? name.Substring(0, 12) + "..." : name,
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
            };

            var desc = new TextBlock
            {
                Text = description,
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.LightGray),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                Margin = new Thickness(5, 5, 5, 10),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 180
            };

            contentStack.Children.Add(image);
            contentStack.Children.Add(title);
            contentStack.Children.Add(desc);

            var border = new Border
            {
                Width = 250,
                Height = 280,
                Background = gradient,
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(15),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Child = contentStack,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new ScaleTransform { ScaleX = 1.0, ScaleY = 1.0 },
                Tag = new CardTag { Process = proc, Hwnd = hwnd, BaseColor = avgColor, AppUserModelId = appUserModelId }
            };

            return border;
        }












        private Bitmap GetBitmapFromExeIcon(string exePath)
        {
            Icon icon = Icon.ExtractAssociatedIcon(exePath);
            return icon?.ToBitmap();
        }
        private Color GetAverageColor(Bitmap bmp)
        {
            long r = 0, g = 0, b = 0;
            int total = 0;

            for (int y = 0; y < bmp.Height; y += 2)
            {
                for (int x = 0; x < bmp.Width; x += 2)
                {
                    var pixel = bmp.GetPixel(x, y);
                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                    total++;
                }
            }

            byte avgR = (byte)(r / total);
            byte avgG = (byte)(g / total);
            byte avgB = (byte)(b / total);

            return Color.FromArgb(255, avgR, avgG, avgB);
        }





        private int _lastAnimatedCardIndex = -1;

        private void HighlightSelectedCard(bool skipScroll = false, bool forceAnimation = true)
        {
            for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
            {
                if (ProgramCardPanel.Children[i] is Border border)
                {
                    bool isSelected = i == _selectedCardIndex;

                    // Set Border Color basierend auf Basisfarbe aus CardTag
                    if (border.Tag is CardTag tag)
                    {
                        var baseColor = tag.BaseColor;

                        var borderColor = isSelected
                            ? Color.FromArgb(255,
                                (byte)Math.Max(0, baseColor.R * 0.5),
                                (byte)Math.Max(0, baseColor.G * 0.5),
                                (byte)Math.Max(0, baseColor.B * 0.5))
                            : Color.FromArgb(0, 0, 0, 0); // Transparent, wenn nicht selektiert

                        border.BorderBrush = new SolidColorBrush(borderColor);
                    }


                    // Animate only if allowed
                    if (border.RenderTransform is ScaleTransform scale)
                    {
                        if (isSelected)
                        {
                            if (forceAnimation && _lastAnimatedCardIndex != _selectedCardIndex)
                            {
                                AnimateScale(border, true);
                                _lastAnimatedCardIndex = _selectedCardIndex;
                            }
                            else
                            {
                                scale.ScaleX = 1.08;
                                scale.ScaleY = 1.08;
                            }
                        }
                        else
                        {
                            scale.ScaleX = 1.0;
                            scale.ScaleY = 1.0;
                        }
                    }
                }
            }

            if (!skipScroll && ProgramCardPanel.Children.Count > _selectedCardIndex)
            {
                ScrollToCardAnimated(ProgramCardPanel.Children[_selectedCardIndex]);
            }
        }



        private void AnimateScale(Border card, bool isSelected)
        {
            if (card.RenderTransform is not ScaleTransform scaleTransform)
                return;

            double targetScale = isSelected ? 1.08 : 1.0;

            var scaleXAnim = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EnableDependentAnimation = true
            };

            var scaleYAnim = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                EnableDependentAnimation = true
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(scaleXAnim);
            storyboard.Children.Add(scaleYAnim);

            Storyboard.SetTarget(scaleXAnim, scaleTransform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");

            Storyboard.SetTarget(scaleYAnim, scaleTransform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

            storyboard.Begin();
        }




        private void ScrollToCardAnimated(UIElement card)
        {
            if (card == null)
            {
                Debug.WriteLine("ScrollToCardAnimated: card is null – aborting.");
                return;
            }


            card.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var transform = card.TransformToVisual(ProgramCardPanel);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

            double cardX = position.X;
            double cardWidth = card.DesiredSize.Width;

            double targetOffset = cardX - (ProgramScrollViewer.ActualWidth - cardWidth) / 2;

            // Clamp innerhalb des Scrollbereichs
            double maxOffset = ProgramScrollViewer.ScrollableWidth;
            targetOffset = Math.Max(0, Math.Min(targetOffset, maxOffset));

            // Scrollen mit Animation (float!)
            _ = ProgramScrollViewer.ChangeView((float)targetOffset, null, null, false);
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

            LoadTaskManagerList();
        }



        private BitmapImage GetAppIconAsBitmapImage(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return null;

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
                        return bmpImage;
                    }
                }
            }
            catch
            {
                return null;
            }
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


        private void PlayNavigationSound()
        {
            try
            {
                SoundPlayer player = new SoundPlayer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\nav.wav"));
                player.Play();
            }
            catch { }
        }

        private void PlayActivationSound()
        {
            try
            {
                SoundPlayer player = new SoundPlayer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\play.wav"));
                player.Play();
            }
            catch { }
        }

        private void PlaydeactivationSound()
        {
            try
            {
                SoundPlayer player = new SoundPlayer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\pause.wav"));
                player.Play();
            }
            catch { }
        }

        


        #endregion // TaskManager
        #region Gamepad/Keyboard_Navigation
        #region shortcuts
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
            SendOverlayNotification("Shortcut: Task View");

            keybd_event(0x5B, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero); // WIN down
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero); // TAB down
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);   // TAB up
            keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);     // WIN up
        }



        #endregion alt tab
        #region performance overlay shortcut AHK
        private void TriggerPerformanceOverlay()
        {
            //usernotification
            SendOverlayNotification("Shortcut: Performance Overlay");

            // Build full path to the overlay .exe located in the same folder 
            string overlayPath = Path.Combine(AppContext.BaseDirectory, "amdnvidiap.exe");

            if (File.Exists(overlayPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = overlayPath,
                        UseShellExecute = true
                    });

                    Console.WriteLine("Performance overlay trigger executed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error launching overlay trigger: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Overlay trigger executable not found.");
            }
        }

        #endregion performance overlay shortcut AHK
        #region audio management
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
        public static void showoverlay()
        {

            using var client = new NamedPipeClientStream(".", "GCMOverlayPipe", PipeDirection.Out);
                client.Connect(100); // max 100ms warten
                using var writer = new StreamWriter(client);
                writer.WriteLine("TOGGLE");
                writer.Flush();

        }
        #endregion shortcut overlay
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
            //usernotification
            SendOverlayNotification("Shortcut: Back to Windows");

            string useuac = AppSettings.Load<string>("launcher");
            if(useuac == "steam")
            {
                KillProcess("steam.exe");
            }
            else if (useuac == "playnite")
            {
                KillProcess("Playnite.FullscreenApp.exe");
                KillProcess("playnite"); 
            }
            else if (useuac == "otherlauncher")
            {
              string otherlauncher = AppSettings.Load<string>("customlauncherpath");
                KillProcess("otherlauncher");
            }
            else if (useuac == "none")
            {

            }
        }
        #endregion backtowin
        #endregion shortcuts


        private Controller _xinputController;
        private bool _controllerConnected = false;

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


        private void SetupGamepad()
        {
            // Timer für Gamepad-Abfrage
            DispatcherTimer gamepadInputTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            gamepadInputTimer.Tick += (s, e) =>
            {
                if (_xinputController == null || !_xinputController.IsConnected)
                {
                    _xinputController = GetConnectedController();
                    _controllerConnected = _xinputController != null;

                    if (_controllerConnected)
                    {
                        Debug.WriteLine($"Controller connected on index: {_xinputController.UserIndex}");
                    }
                }

                if (_controllerConnected)
                {
                    GamepadButtonCheck();
                }
            };

            gamepadInputTimer.Start();
        }


        //Taskmanager
        private GamepadButtonFlags _lastButtonState = GamepadButtonFlags.None;
        private bool _ignoreNextInputFrame = false;
       

        private Dictionary<(string, string), string> _activeShortcuts = new();



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
            ["RightShoulder"] = GamepadButtonFlags.RightShoulder
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
        private Dictionary<string, System.Action> _shortcutActions = new();
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
            try
            {
                string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "shortcuts");
                if (!Directory.Exists(folderPath)) return;

                _activeShortcuts.Clear();
                bool winmodeLoaded = false;

                foreach (var filePath in Directory.GetFiles(folderPath, "*.json"))
                {
                    LogGamepadInit($"🔍 Checking: {filePath}");

                    try
                    {
                        string content = File.ReadAllText(filePath);
                        var entry = JsonSerializer.Deserialize<Dictionary<string, object>>(content);
                        if (entry == null)
                        {
                            LogGamepadInit("⚠️ Skipped (null entry)");
                            continue;
                        }

                        // WINMODE-Datei einlesen, wenn noch nicht geschehen
                        if (!winmodeLoaded)
                        {
                            string winFile = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "gcmsettings", "shortcutswin", "winmode_change.json");

                            LogGamepadInit($"🧭 Checking WinMode File: {winFile}");

                            if (File.Exists(winFile))
                            {
                                LogGamepadInit("📁 WinMode file exists!");

                                try
                                {
                                    string winContent = File.ReadAllText(winFile);
                                    var winEntry = JsonSerializer.Deserialize<Dictionary<string, object>>(winContent);

                                    if (winEntry != null &&
                                     winEntry.TryGetValue("Key1", out var wk1) &&
                                     winEntry.TryGetValue("Key2", out var wk2) &&
                                     winEntry.TryGetValue("Function", out var wfn))

                                    {
                                        string key1 = wk1?.ToString()?.Trim();
                                        string key2 = wk2?.ToString()?.Trim();
                                        string function = wfn?.ToString()?.Trim();

                                        LogGamepadInit($"➡️ WinMode Parsed: {key1} + {key2} → {function}");

                                        if (!string.IsNullOrEmpty(key1) && !string.IsNullOrEmpty(key2) && !string.IsNullOrEmpty(function))
                                        {
                                            _activeShortcuts[(key1, key2)] = function;
                                            LogGamepadInit($"✅ WinMode Shortcut Loaded: {key1} + {key2} → {function}");
                                        }
                                        else
                                        {
                                            LogGamepadInit("❌ WinMode data incomplete.");
                                        }
                                    }
                                    else
                                    {
                                        LogGamepadInit("❌ WinMode data structure invalid or not enabled.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogGamepadInit($"❌ Error reading winmode_change.json: {ex.Message}");
                                }

                                winmodeLoaded = true;
                            }
                            else
                            {
                                LogGamepadInit("⛔ WinMode file NOT found.");
                            }
                        }

                        // Normale Datei auswerten
                        if (entry.TryGetValue("Key1", out var k1) &&
                            entry.TryGetValue("Key2", out var k2) &&
                            entry.TryGetValue("Function", out var fn))
                        {
                            string key1 = k1?.ToString()?.Trim();
                            string key2 = k2?.ToString()?.Trim();
                            string function = fn?.ToString()?.Trim();

                            if (!string.IsNullOrEmpty(key1) &&
                                !string.IsNullOrEmpty(key2) &&
                                !string.IsNullOrEmpty(function))
                            {
                                _activeShortcuts[(key1, key2)] = function;
                                LogGamepadInit($"📥 Loaded Shortcut: {key1} + {key2} → {function}");
                            }
                            else
                            {
                                LogGamepadInit("⚠️ Skipped file due to empty values.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogGamepadInit($"❌ Error reading shortcut: {filePath} → {ex.Message}");
                    }
                }

                // 🧠 Funktionale Aktionen zuweisen
                _shortcutActions.Clear();
                _shortcutActions["taskmanager"] = BringTaskManagerToFrontAndFocus;
                _shortcutActions["switch tab"] = SendWinTab;
                _shortcutActions["audio switch"] = SwitchToNextAudioDevice;
                _shortcutActions["performance overlay"] = TriggerPerformanceOverlay;
                _shortcutActions["winmodechange"] = Triggerbacktowin;
                _shortcutActions["show overlay"] = showoverlay;

                // 🔎 Übersicht anzeigen
                foreach (var s in _activeShortcuts)
                {
                    LogGamepadInit($"✅ ACTIVE SHORTCUT: {s.Key.Item1} + {s.Key.Item2} → {s.Value}");
                }
            }
            catch (Exception ex)
            {
                LogGamepadInit($"[ShortcutLoader] General Error: {ex.Message}");
            }
        }

        private bool IsNewButtonPress(GamepadButtonFlags button, GamepadButtonFlags currentButtons)
        {
            // If the button is currently pressed AND was not already held → allow
            if ((currentButtons & button) != 0)
            {
                if (!_pressedButtons.Contains(button))
                {
                    _pressedButtons.Add(button);
                    return true;
                }
                return false;
            }
            else
            {
                // Button is released → clear from pressed list
                _pressedButtons.Remove(button);
                return false;
            }
        }


        private void GamepadButtonCheck()
        {
            if (!_controllerConnected || !_xinputController.IsConnected)
                return;

            var state = _xinputController.GetState();
            var gamepad = state.Gamepad;
            var currentButtons = gamepad.Buttons;

            // Skip input for one frame after ALT+TAB to avoid double detection
            if (_ignoreNextInputFrame)
            {
                _ignoreNextInputFrame = false;
                _lastButtonState = currentButtons;
                return;
            }

            // Handle custom shortcuts
            foreach (var pair in _activeShortcuts)
            {
                var key1 = pair.Key.Item1;
                var key2 = pair.Key.Item2;
                var function = pair.Value;

                bool key1Pressed = IsButtonPressed(currentButtons, key1);
                bool key2Pressed = IsButtonPressed(currentButtons, key2);

                if (key1Pressed && !_heldButtonTimestamps.ContainsKey(key1))
                {
                    _heldButtonTimestamps[key1] = DateTime.UtcNow;
                }

                if (_heldButtonTimestamps.TryGetValue(key1, out var heldTime))
                {
                    if (DateTime.UtcNow - heldTime < _comboTimeout && key2Pressed)
                    {
                        if (!_triggeredCombos.Contains(pair.Key))
                        {
                            _triggeredCombos.Add(pair.Key);
                            if (_shortcutActions.TryGetValue(function, out var action))
                            {
                                action?.Invoke();
                            }
                        }
                    }
                    else if (!key1Pressed)
                    {
                        _heldButtonTimestamps.Remove(key1);
                    }
                }
                else
                {
                    _triggeredCombos.Remove(pair.Key);
                }
            }

            // TaskManager Navigation (only if NOT in AltTab)
            if (IsWindowInForeground() && !isAltTabMode)
            {
                if (IsNewButtonPress(GamepadButtonFlags.DPadRight, currentButtons))
                {
                    _selectedCardIndex++;
                    if (_selectedCardIndex >= ProgramCardPanel.Children.Count)
                        _selectedCardIndex = 0;
                    PlayNavigationSound();
                    HighlightSelectedCard();
                }

                if (IsNewButtonPress(GamepadButtonFlags.DPadLeft, currentButtons))
                {
                    _selectedCardIndex--;
                    if (_selectedCardIndex < 0)
                        _selectedCardIndex = ProgramCardPanel.Children.Count - 1;
                    PlayNavigationSound();
                    HighlightSelectedCard();
                }

                if (IsNewButtonPress(GamepadButtonFlags.A, currentButtons))
                {
                    TriggerCardAction(_selectedCardIndex, true);
                    PlayActivationSound();
                }

                if (IsNewButtonPress(GamepadButtonFlags.B, currentButtons))
                {
                    TriggerCardAction(_selectedCardIndex, false);
                    PlaydeactivationSound();
                }

                if (IsNewButtonPress(GamepadButtonFlags.DPadUp, currentButtons))
                {
                    _selectedButtonIndex = 0;
                    HighlightSelectedCard();
                }

                if (IsNewButtonPress(GamepadButtonFlags.DPadDown, currentButtons))
                {
                    _selectedButtonIndex = 1;
                    HighlightSelectedCard();
                }
            }

            // Update button state for next check
            _lastButtonState = currentButtons;
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


        private void TriggerCardAction(int index, bool launch)
        {
            if (index < 0 || index >= ProgramCardPanel.Children.Count)
                return;

            if (ProgramCardPanel.Children[index] is Border border && border.Tag is CardTag tag)
            {
                var proc = tag.Process;
                var hwnd = tag.Hwnd;

                if (launch)
                {
                    if (!string.IsNullOrEmpty(tag.AppUserModelId))
                    {
                        

                        try
                        {
                            ActivateUwpApp(tag.AppUserModelId);
                            RestoreUwpAppWindow(tag.Hwnd);
                            TaskManagerBringWindowToForeground(tag.Hwnd);
                        }
                        catch (Exception ex)
                        {
                           
                        }
                    }
                    else
                    {
                        // Legacy App
                       
                        TaskManagerBringWindowToForeground(tag.Hwnd);
                    }

                }
                else
                {
                    try
                    {
                        if (proc.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                        {
                            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                        else
                        {
                            proc.Kill();
                        }
                    }
                    catch { }

                    LoadTaskManagerList();
                }
            }
        }




        [DllImport("user32.dll")]
        private static extern bool SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        private bool _altPressed = false;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;

        public static void BringTaskManagerToFrontAndFocus()
        {
            IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
            BringToFrontAndFocus(hWnd);
        }

        public static void BringToFrontAndFocus(IntPtr hWnd)
        {
            try
            {
                ShowWindow(hWnd, SW_RESTORE);

                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                IntPtr foreground = GetForegroundWindow();
                uint foreThread = GetWindowThreadProcessId(foreground, out _);
                uint currentThread = GetCurrentThreadId();

                AttachThreadInput(currentThread, foreThread, true);

                SetForegroundWindow(hWnd);
                SetFocus(hWnd);
                SetActiveWindow(hWnd); // <- added

                AttachThreadInput(currentThread, foreThread, false);
                AllowSetForegroundWindow(-1);

                // → Simulate click into the window
                SimulateMouseClickInWindow(hWnd);

                Console.WriteLine("Window brought to front and focused.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BringToFrontAndFocus failed: {ex.Message}");
            }

        }

        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
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
        [DllImport("user32.dll")] private static extern bool ShowCursor(bool bShow);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private static void SimulateInvisibleClick(IntPtr hWnd)
        {
            // Mausposition auf das Fenster setzen (aber wird vorher unsichtbar gemacht!)
            RECT rect;
            GetWindowRect(hWnd, out rect);
            int centerX = rect.Left + (rect.Right - rect.Left) / 2;
            int centerY = rect.Top + (rect.Bottom - rect.Top) / 2;

            // Cursorposition setzen
            SetCursorPos(centerX, centerY);

            // Klick simulieren
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(30);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }
        private static void SimulateMouseClickInWindow(IntPtr hWnd)
        {
            try
            {
                // 1. Fenster sichtbar machen
                ShowWindow(hWnd, SW_RESTORE);

                // 2. Temporär TopMost setzen, um sicher oben zu liegen
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

                // 3. Input-Attach vorbereiten
                IntPtr foreground = GetForegroundWindow();
                uint foreThread = GetWindowThreadProcessId(foreground, out _);
                uint currentThread = GetCurrentThreadId();
                AttachThreadInput(currentThread, foreThread, true);

                // 4. Maus unsichtbar machen
                ShowCursor(false);

                // 5. Fokus setzen
                SetForegroundWindow(hWnd);
                SetFocus(hWnd);
                SetActiveWindow(hWnd);

                // 6. Unsichtbaren Klick ausführen
                SimulateInvisibleClick(hWnd);

                // 7. Input-Threads trennen
                AttachThreadInput(currentThread, foreThread, false);

                // 8. Maus ganz aus dem Sichtfeld verschieben
                SetCursorPos(-10000, -10000); // weit außerhalb des sichtbaren Bereichs

                // 9. Falls nötig, Cursor später wieder sichtbar machen
                // ShowCursor(true);

                AllowSetForegroundWindow(-1);

                Console.WriteLine("Window brought to front and focused (invisible click).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BringToFrontAndFocus failed: {ex.Message}");
            }
        }



        #endregion
        #region Startupvideo

        public static class StartupVideo
        {
            [DllImport("user32.dll")]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
            private const uint SWP_NOSIZE = 0x0001;
            private const uint SWP_NOMOVE = 0x0002;
            private const uint SWP_SHOWWINDOW = 0x0040;
           

            #region SteamStartup

            public static void RenameFile(string oldFilePath, string newFilePath)
            {
                try
                {
                    // Vérifie si le fichier existe
                    if (File.Exists(oldFilePath))
                    {
                        // Renomme le fichier
                        File.Move(oldFilePath, newFilePath);
                        Console.WriteLine($"Le fichier a été renommé avec succès : {newFilePath}");
                    }
                    else
                    {
                        Console.WriteLine("Le fichier spécifié n'existe pas.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors du renommage du fichier : {ex.Message}");
                }
            }

            public static void RenameSteamStartupVideo_Start()
            {
                string SteamVideoPath = Path.Combine(Path.GetDirectoryName(AppSettings.Load<string>("steamlauncherpath")), "steamui", "movies", "bigpicture_startup.webm");
                string SteamVideoPathNew = Path.Combine(Path.GetDirectoryName(AppSettings.Load<string>("steamlauncherpath")), "steamui", "movies", "bigpicture_startup.old.webm");
                string GCMVideoPath = Path.Combine(Path.GetDirectoryName(AppSettings.Load<string>("steamlauncherpath")), "steamui", "movies", "GCM_vid.webm");
                RenameFile(SteamVideoPath, SteamVideoPathNew); //change the name of the real file
                RenameFile(GCMVideoPath, SteamVideoPath); //put the name of the real file to the selected video
            }

            public static void RenameSteamStartupVideo_End()
            {
                string SteamVideoPath = Path.Combine(Path.GetDirectoryName(AppSettings.Load<string>("steamlauncherpath")), "steamui", "movies", "bigpicture_startup.webm");
                string SteamVideoPathNew = Path.Combine(Path.GetDirectoryName(AppSettings.Load<string>("steamlauncherpath")), "steamui", "movies", "bigpicture_startup.old.webm");
                string GCMVideoPath = Path.Combine(Path.GetDirectoryName(AppSettings.Load<string>("steamlauncherpath")), "steamui", "movies", "GCM_vid.webm");
                RenameFile(SteamVideoPath, GCMVideoPath); // give the GCM Video file its real name
                RenameFile(SteamVideoPathNew, SteamVideoPath); // give the steam file its real name
            }
            #endregion SteamStartup


            [DllImport("user32.dll")]
            private static extern bool SetFocus(IntPtr hWnd);

            [DllImport("user32.dll")]
            private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("user32.dll")]
            private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            private static extern int GetSystemMetrics(int nIndex);

            private const int GWL_STYLE = -16;
            private const int GWL_EXSTYLE = -20;
            private const int WS_POPUP = unchecked((int)0x80000000);
            private const int WS_VISIBLE = 0x10000000;
            private const int WS_EX_TOOLWINDOW = 0x00000080;
            private const int WS_EX_NOACTIVATE = 0x08000000;



            private const int SM_CXSCREEN = 0;
            private const int SM_CYSCREEN = 1;

            private const uint SWP_FRAMECHANGED = 0x0020;

            public static void Play()
            {
                //Set steam startup video if not set
                try
                {
                    AppSettings.Load<bool>("usesteamstartupvideo");
                }
                catch
                {
                    AppSettings.Save("usesteamstartupvideo", false);
                }
                //Standard Use Startup Video
                try
                {
                    AppSettings.Load<bool>("usestartupvideo");
                }
                catch
                {
                    AppSettings.Save("usestartupvideo", false); // oder true, wenn du es standardmäßig aktivieren willst
                }


                try
                {
                    Console.WriteLine("Playing startup video...");

                    bool useStartupVideo = AppSettings.Load<bool>("usestartupvideo");
                    if (!useStartupVideo)
                    {
                        Console.WriteLine("Startup video disabled.");
                        return;
                    }

                    if (AppSettings.Load<bool>("usesteamstartupvideo"))
                    {
                        RenameSteamStartupVideo_Start();
                        return;
                    }

                    string videoPath = AppSettings.Load<string>("startupvideo_path");
                    if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                    {
                        ShowErrorMessage("The specified video file was not found.");
                        return;
                    }

                    string extension = Path.GetExtension(videoPath)?.ToLower();
                    string[] validExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv" };
                    if (Array.IndexOf(validExtensions, extension) == -1)
                    {
                        ShowErrorMessage("Unsupported video format.");
                        return;
                    }

                    var videoWindow = new Window();
                    var mediaElement = CreateMediaElement(videoPath, videoWindow);
                    mediaElement.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch;
                    mediaElement.VerticalAlignment = VerticalAlignment.Stretch;
                    videoWindow.Content = mediaElement;
                    videoWindow.Activate();

                    IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(videoWindow);

                    // Make it a true invisible system-level fullscreen window
                    int style = GetWindowLong(hWnd, GWL_STYLE);
                    SetWindowLong(hWnd, GWL_STYLE, style | WS_POPUP | WS_VISIBLE);

                    int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                    SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

                    int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                    int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                    SetForegroundWindow(hWnd);
                    SetFocus(hWnd);

                    System.Diagnostics.Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

                    KeepWindowOnTop(hWnd);
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"Error playing video: {ex.Message}");
                }
            }

            private static MediaPlayerElement CreateMediaElement(string videoPath, Window window)
            {
                var mediaPlayer = new MediaPlayer { AutoPlay = true };
                string uriPath = new Uri(Path.GetFullPath(videoPath)).AbsoluteUri;
                mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(uriPath));

                mediaPlayer.MediaFailed += (s, e) =>
                {
                    Console.WriteLine("Video load failed: " + e.ErrorMessage);
                };

                mediaPlayer.MediaEnded += (s, e) =>
                {
                    window.DispatcherQueue.TryEnqueue(() => window.Close());
                };

                var mediaPlayerElement = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = false,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.Fill
                };

                mediaPlayerElement.SetMediaPlayer(mediaPlayer);
                return mediaPlayerElement;
            }



            private static AppWindow GetAppWindow(Window window)
            {
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                return AppWindow.GetFromWindowId(windowId);
            }

            private static async void KeepWindowOnTop(IntPtr hWnd)
            {
                while (true)
                {
                    await System.Threading.Tasks.Task.Delay(1000); // Vérifie toutes les secondes
                    SetForegroundWindow(hWnd);
                    SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW);
                }
            }

            private static void ShowErrorMessage(string message)
            {
                Console.WriteLine(message);
            }
        }

        #endregion Startupvideo

        #endregion methodes

        private void LauncherTileRow_Loaded(object sender, RoutedEventArgs e)
        {
            FocusBlocker.Focus(FocusState.Programmatic);
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

        const uint SWP_HIDEWINDOW = 0x0080;
        const uint SWP_SHOWWINDOW = 0x0040;

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        public static void HideTaskbar()
        {
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero)
            {
                ShowWindow(taskbarHandle, SW_HIDE);
                SetWindowPos(taskbarHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_HIDEWINDOW);
            }
        }

        public static void ShowTaskbar()
        {
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle != IntPtr.Zero)
            {
                ShowWindow(taskbarHandle, SW_SHOW);
                SetWindowPos(taskbarHandle, HWND_BOTTOM, 0, 0, 0, 0, SWP_SHOWWINDOW);
            }
        }
    }
}
