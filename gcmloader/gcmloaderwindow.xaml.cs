using Discord;
using Discord.WebSocket;
using GAMINGCONSOLEMODE;
using Microsoft.UI;
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
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using NAudio.CoreAudioApi;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Media;
// SteamGridDBHelper.cs
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics; 
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Tomlyn;
using Tomlyn.Model;
using Vanara.PInvoke;
using Vanara.PInvoke;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI;
using WinRT.Interop;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.Shell32;
using static Vanara.PInvoke.User32;
using Application = Microsoft.UI.Xaml.Application;
using Button = Microsoft.UI.Xaml.Controls.Button;
using Color = Windows.UI.Color;
using Image = Microsoft.UI.Xaml.Controls.Image;
using Point = System.Drawing.Point;
using Task = System.Threading.Tasks.Task;

namespace gcmloader
{

    public sealed partial class MainWindow : Window
    {
        #region needed
      

        #region wingamepad
        private DispatcherTimer _wingamepadMonitor;
        private bool _isExiting = false; 
        #endregion wingamepad
        #region Startup Video
        private MediaPlayer _startupMediaPlayer;
        private bool startupVideoFinished = false;
        private bool _isVideoPlaybackInitiated = false;
        #endregion
        #region steamgriddb
        public record SearchResult(int id, string name);
        public record SearchResponse(bool success, SearchResult[] data);
        public record ImageResult(string url);
        public record ImageResponse(bool success, ImageResult[] data);

        private readonly Dictionary<string, SearchResult> _gameIdCache = new(); // Geändert von int? zu SearchResult
        public SteamGridDBHelper _steamGridHelper;
        private readonly string _imageCachePath = Path.Combine(SettingsFolder, "image_cache");

        public class SteamGridDBHelper
        {
            // Die record-Definitionen wurden nach oben in die MainWindow-Klasse verschoben.
            // Die Klasse ist jetzt sauberer.

            private readonly HttpClient _httpClient = new();
            private readonly string _apiKey;

            public SteamGridDBHelper(string apiKey)
            {
                _apiKey = apiKey;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            /// <summary>
            /// Searches for a game on SteamGridDB and returns the entire search result object.
            /// </summary>
            public async Task<SearchResult> SearchForGameIdAsync(string gameName)
            {
                if (string.IsNullOrEmpty(_apiKey)) return null;

                try
                {
                    var response = await _httpClient.GetAsync($"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(gameName)}");
                    if (!response.IsSuccessStatusCode) return null;

                    var json = await response.Content.ReadAsStringAsync();
                    var searchData = JsonSerializer.Deserialize<SearchResponse>(json);

                    if (searchData != null && searchData.success && searchData.data.Length > 0)
                    {
                        return searchData.data[0];
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SteamGridDB] Error searching for '{gameName}': {ex.Message}");
                }
                return null;
            }

            /// <summary>
            /// Gets the URL for the most popular grid/cover image for a given game ID.
            /// </summary>
            public async Task<string> GetGridImageUrlAsync(int gameId)
            {
                if (string.IsNullOrEmpty(_apiKey)) return null;

                try
                {
                    var response = await _httpClient.GetAsync($"https://www.steamgriddb.com/api/v2/grids/game/{gameId}");
                    if (!response.IsSuccessStatusCode) return null;

                    var json = await response.Content.ReadAsStringAsync();
                    var imageData = JsonSerializer.Deserialize<ImageResponse>(json);

                    if (imageData != null && imageData.success && imageData.data.Length > 0)
                    {
                        return imageData.data[0].url;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SteamGridDB] Error getting grid for game ID '{gameId}': {ex.Message}");
                }
                return null;
            }
        }
        #endregion dsteamgriddb

        private List<Border> _launcherAreaButtons;
        private int _selectedLauncherAreaIndex = 0;
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
                    overlappedPresenter.Minimize();
                }
            }
            else
            {
                Debug.WriteLine("Grace period ended, but window regained focus. No action taken.");
            }
        }

        private void ResizeWindowToFillScreen(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                // Get the size of the entire window, including any invisible borders
                GetWindowRect(hwnd, out RECT windowRect);
                // Get the size of the client area (the visible content inside the borders)
                GetClientRect(hwnd, out RECT clientRect);

                // Calculate the total size of the non-client area (the borders)
                int nonClientWidth = (windowRect.Right - windowRect.Left) - clientRect.Right;
                int nonClientHeight = (windowRect.Bottom - windowRect.Top) - clientRect.Bottom;

                // Get the target screen dimensions
                int screenWidth = GetScreenWidth();
                int screenHeight = GetScreenHeight();

                // Calculate the new size and position. We make the window larger than the screen
                // and move it slightly off-screen to hide the invisible borders.
                int newWidth = screenWidth + nonClientWidth;
                int newHeight = screenHeight + nonClientHeight;
                int newX = -(nonClientWidth / 2);
                int newY = -(nonClientHeight / 2);

                Debug.WriteLine($"Resizing window to {newWidth}x{newHeight} at ({newX},{newY}) to perfectly fill the screen.");

                // Apply the new position and size
                SetWindowPos(hwnd, IntPtr.Zero, newX, newY, newWidth, newHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to force-resize window: {ex.Message}");
            }
        }

        /// Wird jede Sekunde vom Timer aufgerufen.
        /// </summary>
        /// <summary>
        private void _focusCheckTimer_Tick(object sender, object e)
        {
            // Breche ab, wenn die anfängliche Gnadenfrist nach dem App-Start noch aktiv ist.
            if (_isStartupGracePeriod) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.IsVisible && !IsWindowInForeground())
            {
                // --- HIER IST DIE NEUE LOGIK ---
                // Anstatt sofort zu minimieren, starten wir den 3-Sekunden-Timer,
                // aber nur, wenn er nicht schon läuft.
                if (!_minimizeGracePeriodTimer.IsEnabled)
                {
                    Debug.WriteLine("Window lost focus. Starting 3-second grace period before minimizing.");
                    _minimizeGracePeriodTimer.Start();
                }
            }
            else
            {
                // Wenn das Fenster den Fokus wiedererlangt, stoppen wir den Timer,
                // damit es nicht unnötig minimiert wird.
                if (_minimizeGracePeriodTimer.IsEnabled)
                {
                    Debug.WriteLine("Window regained focus. Canceling minimize timer.");
                    _minimizeGracePeriodTimer.Stop();
                }
            }
        }
        private DispatcherTimer _focusCheckTimer;
        private DispatcherTimer _minimizeGracePeriodTimer;
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
            public bool Enabled { get; set; }
        }

        // Die Dictionaries zum Speichern und Ausführen der Shortcuts
        private Dictionary<(string, string), string> _activeShortcuts = new();
        private Dictionary<string, System.Action> _shortcutActions = new();

        // Diese Variable speichert den Xbox-Prozess, damit wir ihn später überwachen können.
        private static Process monitoredXboxProcess = null;
        private int _selectedCardIndex = 0;
        private int _selectedButtonIndex = 0;
        private DispatcherTimer _taskRefreshTimer;
        private HashSet<GamepadButtonFlags> _pressedButtons = new();
        private static string startart = null;
        private const byte VK_F11 = 0x7A;
        //gamepad
        // Füge diese Deklarationen für die Gamepad-Steuerung hinzu, falls sie fehlen:

        // Die drei Fokus-Bereiche unserer App
        private enum FocusArea { Launcher, Cards, TopButtons }
private FocusArea _currentFocusArea = FocusArea.Cards;

        // Index und Liste für die oberen Buttons
        private int _selectedTopButtonIndex = 0;
private List<Button> _topButtons;
     


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
        private void DiscordCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            MakeSelfNonTopmost();
            StartDiscord();
            PlayActivationSound();
        }

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
                    Process.Start(new ProcessStartInfo(discordPath, "--processStart Discord.exe") { UseShellExecute = true });
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
        private static readonly string SettingsFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "gcmsettings"
);
private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.toml");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public MainWindow()
        {
            this.InitializeComponent();
            perfectsettings();
            StartTaskbarHidingLoop();
            // Zugriff auf das Grid-Root-Element

            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.KeyDown += MainWindow_KeyDown;
                rootElement.Loaded += (s, e) =>
                {
                    FocusSink.Focus(FocusState.Programmatic);

                    // --- HIER IST DIE KORREKTUR ---
                    // Prüfe, ob das Video nicht bereits gestartet wurde.
                    if (!_isVideoPlaybackInitiated)
                    {
                        // Setze den "Türsteher", damit dieser Code nicht nochmal ausgeführt wird.
                        _isVideoPlaybackInitiated = true;

                        // Starte das Video
                        PlayStartupVideo();
                    }
                    // --- ENDE DER KORREKTUR ---
                };
            }
           


            // Initialisiere den SteamGridDB Helper
            try
            {
                // Lade den API-Schlüssel aus der Einstellungsdatei.
                string apiKey = AppSettings.Load<string>("steamgriddb_api_key");

                // Prüfe explizit, ob der geladene Schlüssel leer oder ungültig ist.
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    // Wenn kein Schlüssel vorhanden ist, wird der Helper mit 'null' initialisiert.
                    // Die SteamGridDB-Funktionalität ist damit sicher deaktiviert, ohne einen Fehler zu werfen.
                    _steamGridHelper = new SteamGridDBHelper(null);
                    Debug.WriteLine("[INFO] SteamGridDB API key is empty or not found in settings. Feature is disabled.");
                }
                else
                {
                    // Nur wenn ein gültiger Schlüssel vorhanden ist, wird der Helper damit initialisiert.
                    _steamGridHelper = new SteamGridDBHelper(apiKey);
                    Directory.CreateDirectory(_imageCachePath); // Erstelle den Cache-Ordner.
                }
            }
            catch (Exception)
            {
                // Dieser Block fängt den Fehler ab, falls der Eintrag "steamgriddb_api_key"
                // gar nicht in der Einstellungsdatei existiert. Auch in diesem Fall wird die Funktion sicher deaktiviert.
                _steamGridHelper = new SteamGridDBHelper(null);
                Debug.WriteLine("[WARN] SteamGridDB API key setting does not exist. Feature is disabled.");
            }

            MinimizeAllWindows();

            // Füllt die Liste mit den UI-Elementen aus dem XAML
            _launcherAreaButtons = new List<Border> { LauncherCard, DiscordCard };
            _topButtons = new List<Button> { ExitGcmButton,ShutdownButton };

            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.UnhandledException += CurrentApp_UnhandledException;
            this.Activated += MainWindow_Activated;
            this.Activated += (s, e) => this.Content.Focus(FocusState.Programmatic);

            string startart = AppSettings.Load<string>("launcher");
           
            LoadShortcutsFromSettings();
            SetupGamepad();
            
            Start();
            //ASYNC PROZES
            ShowTaskManager();
            SetupFocusWatcher();
            SetupMouseIdleBehavior();
            // NEU: Starte einen einmaligen Timer, der die Gnadenfrist beendet.
            var gracePeriodTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15) 
            };
            gracePeriodTimer.Tick += (s, e) =>
            {
                _isStartupGracePeriod = false; // Gnadenfrist beenden
                gracePeriodTimer.Stop();       // Timer stoppen, da er nur einmal laufen soll
            };
            gracePeriodTimer.Start();

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
            BackToWindows();
        }

        private void CurrentApp_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            TaskbarManager.RestoreOriginalState();
            // Verhindert den Standard-Crash-Dialog von Windows, da wir uns selbst um das Beenden kümmern.
            e.Handled = true;

            string path = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.AppendAllText(path, $"[FATAL UI EXCEPTION] {DateTime.Now}: {e.Message}\n");

            // NEU: Rufe die Aufräum-Methode auf, um Windows wiederherzustellen und die App sauber zu beenden.
            BackToWindows();
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
                string overlayPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GCM", "overlaywindow", "OverlayWindow.exe");

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


        /// <summary>
        /// Starts a background loop that persistently hides the taskbar.
        /// This is an aggressive method to fight against the OS trying to reshow it.
        /// </summary>
        private void StartTaskbarHidingLoop()
        {
            try
            {
                _taskbarHiderCts = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    Debug.WriteLine("Starting persistent taskbar hiding loop...");
                    while (!_taskbarHiderCts.Token.IsCancellationRequested)
                    {
                        // Use your robust hide method
                        TaskbarVisibility.HideTaskbar();
                        try
                        {
                            // Check and hide 4 times per second.
                            await Task.Delay(250, _taskbarHiderCts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            // This is expected when we stop the loop.
                            Debug.WriteLine("Taskbar hiding loop stopped.");
                        }
                    }
                    Debug.WriteLine("Taskbar hiding loop stopped.");
                }, _taskbarHiderCts.Token);
            }
            catch
            {
                //error or not ready at this time, repeat
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

            // --- HIER IST DIE ENTSCHEIDENDE ÄNDERUNG ---
            // Set the window size to fullscreen AND set it to be "HWND_TOPMOST".
            // IntPtr.Zero (HWND_TOP) -> new IntPtr(-1) (HWND_TOPMOST)
            SetWindowPos(hwnd, new IntPtr(-1), 0, 0, screenWidth, screenHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
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


        #endregion methodes for code
        #region functions
        #region lossless
        public static void StartLosslessScaling()
        {
            try
            {
                // 1. Check if the feature is enabled in the settings.
                if (!AppSettings.Load<bool>("lossless"))
                {
                    Console.WriteLine("Lossless Scaling auto-start is disabled. Skipping.");
                    return; // Exit the method if the setting is false.
                }

                // 2. Load the application path from the settings.
                string losslessPath = AppSettings.Load<string>("losslesspath");

                // 3. Validate the path and check if the file exists.
                if (string.IsNullOrEmpty(losslessPath) || !File.Exists(losslessPath))
                {
                    Console.WriteLine($"Error: The path to Lossless Scaling is invalid or the file was not found: {losslessPath}");
                    return;
                }

                // 4. Extract the process name from the file path (e.g., "Lossless Scaling" from "Lossless Scaling.exe").
                string processName = Path.GetFileNameWithoutExtension(losslessPath);

                // 5. Check if the process is already running to avoid launching a duplicate instance.
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    Console.WriteLine("Lossless Scaling is already running.");
                    return;
                }

                // 6. Start the application with the specified settings.
                Console.WriteLine("Starting Lossless Scaling minimized...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = losslessPath,
                    // This line ensures that the application starts minimized.
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Process.Start(startInfo);
                Console.WriteLine("Lossless Scaling started successfully.");
            }
            catch (Exception ex)
            {
                // Catch any exceptions that might occur during startup (e.g., permission issues).
                Console.WriteLine($"An unexpected error occurred while starting Lossless Scaling or deaktivated {ex.Message}");
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
                // Get the path of the AppData folder
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                // Create the full path to the "gcmsettings" folder in AppData
                string settingsFolderPath = Path.Combine(appDataPath, "gcmsettings");

                // Create the full path to the "settings.json" file within the folder
                string settingsFilePath = Path.Combine(settingsFolderPath, "settings.toml");

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
        private void BackToWindows()
        {
            TaskbarManager.RestoreOriginalState();

            MakeSelfNonTopmost();
         
            Console.WriteLine("Exit-Button geklickt. Stelle den Desktop wieder her und beende die App...");

            // Schritt 1: Taskleiste und Icons für die aktuelle Sitzung wieder sichtbar machen
            TaskbarVisibility.ShowTaskbar();
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero) ShowWindow(progman, 5); // SW_SHOW
            IntPtr workerw = FindWindow("WorkerW", null);
            if (workerw != IntPtr.Zero) ShowWindow(workerw, 5); // SW_SHOW

            // Schritt 2: Windows-Standard wiederherstellen
            try
            {
                // Registry auf "explorer.exe" zurücksetzen
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true))
                {
                    if (key != null)
                    {
                        key.SetValue("Shell", "explorer.exe", RegistryValueKind.String);
                    }
                }

                // Alle Autostart-Apps wiederherstellen
                if (AppSettings.Load<bool>("usewinpartstartapps"))
                {
                    StartupControl.RestoreStartupApps();
                }

                // Explorer.exe neu starten, falls er nicht läuft
                if (!Process.GetProcessesByName("explorer").Any())
                {
                    
                   
                }
                else
                {
                    KillProcess("explorer.exe");
                    Thread.Sleep(500);
                    Process.Start("explorer.exe");
                }

                   
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Wiederherstellen von Windows: {ex.Message}");
            }

            // --- HIER IST DIE WICHTIGE ERGÄNZUNG ---
            // Stellt sicher, dass das originale Steam-Startvideo zuverlässig wiederhergestellt wird.
            try
            {
                // Da die Methode jetzt Teil der MainWindow-Klasse ist, rufen wir sie direkt so auf:
                RenameSteamStartupVideo_End();
                Debug.WriteLine("[Cleanup] Steam startup video restored.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cleanup] Error restoring Steam startup video: {ex.Message}");
            }
            // --- ENDE DER ERGÄNZUNG ---

            // Schritt 3: Restliche Aufräum-Aktionen
            displayfusion("end");
            CleanupLogging();
            preaudio(false, true);
            StartWingamepad();
           

            // Overlay-Prozess beenden
            foreach (var proc in Process.GetProcessesByName("OverlayWindow"))
            {
                try { proc.Kill(); } catch { }
            }

            // UAC-Einstellungen wiederherstellen
            try
            {
                if (AppSettings.Load<bool>("uac"))
                {
                    uac("on");
                }
            }
            catch { uac("on"); } // Im Zweifel UAC wieder aktivieren

            // Schritt 4: Anwendung sauber beenden
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
        private void ForceWindowFullscreen(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            // Get the actual screen dimensions
            int screenWidth = GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
            int screenHeight = GetSystemMetrics(79); // SM_CYVIRTUALSCREEN

            // Remove window styles that create borders and a title bar
            IntPtr style = GetWindowLongPtr(hwnd, GWL_STYLE);
            style = (IntPtr)((long)style & ~WS_CAPTION & ~0x00040000L); // WS_THICKFRAME
            SetWindowLongPtr(hwnd, GWL_STYLE, style);

            // Resize and move the window to cover the entire screen
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, screenWidth, screenHeight, 0x0020); // SWP_FRAMECHANGED
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
        private async void SwitchToConfiguredLauncher()
        {
            MakeSelfNonTopmost();

            string launcher = AppSettings.Load<string>("launcher");
            Debug.WriteLine($"Switching to launcher '{launcher}'...");

            switch (launcher)
            {
                // ... (steam, playnite, custom bleiben unverändert) ...
                case "steam":
                    Process.Start(new ProcessStartInfo("steam://open/bigpicture") { UseShellExecute = true });
                    break;
                case "playnite":
                    string playnitePath = AppSettings.Load<string>("playnitelauncherpath");
                    string playniteProcessName = "Playnite.FullscreenApp";
                    Process playniteProcess = Process.GetProcessesByName(playniteProcessName).FirstOrDefault();
                    if (playniteProcess != null && playniteProcess.MainWindowHandle != IntPtr.Zero)
                    { TaskManagerBringWindowToForeground(playniteProcess.MainWindowHandle); }
                    else
                    { Process.Start(new ProcessStartInfo(playnitePath, "--startfullscreen") { UseShellExecute = true }); }
                    break;
                case "custom":
                    string customPath = AppSettings.Load<string>("customlauncherpath");
                    string customProcessName = Path.GetFileNameWithoutExtension(customPath);
                    Process customProcess = Process.GetProcessesByName(customProcessName).FirstOrDefault();
                    if (customProcess != null && customProcess.MainWindowHandle != IntPtr.Zero)
                    { TaskManagerBringWindowToForeground(customProcess.MainWindowHandle); }
                    else
                    { Process.Start(new ProcessStartInfo(customPath) { UseShellExecute = true }); }
                    break;

                case "xbox":
                    IntPtr hwnd = await FindXboxWindowHandleAsync(0);

                    if (hwnd != IntPtr.Zero)
                    {
                        // --- DAS IST DIE FINALE LOGIK ---
                        Debug.WriteLine("Xbox window found. Switching focus carefully...");

                        // Schritt 1: Prüfe, ob das Fenster MINIMIERT ist.
                        if (IsIconic(hwnd))
                        {
                            // Nur wenn es minimiert ist, stellen wir es wieder her.
                            Debug.WriteLine("Window is minimized, restoring it...");
                            ShowWindow(hwnd, SW_RESTORE);
                        }

                        // Schritt 2: Bringe das Fenster in den Vordergrund, OHNE seine Größe zu ändern.
                        // Wir benutzen hierfür den zuverlässigen "AttachThreadInput"-Trick.
                        IntPtr foregroundHwnd = GetForegroundWindow();
                        if (foregroundHwnd != hwnd) // Nur ausführen, wenn es nicht schon vorne ist
                        {
                            uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
                            uint ourThreadId = GetCurrentThreadId();

                            AttachThreadInput(ourThreadId, foregroundThreadId, true);
                            SetForegroundWindow(hwnd);
                            AttachThreadInput(ourThreadId, foregroundThreadId, false);
                        }
                    }
                    else
                    {
                        // Wenn das Fenster nicht existiert, starten wir es und maximieren es danach.
                        Debug.WriteLine("Xbox window not found. Launching and then maximizing...");
                        Process.Start(new ProcessStartInfo("xbox:") { UseShellExecute = true });
                        hwnd = await FindXboxWindowHandleAsync(15);

                        if (hwnd != IntPtr.Zero)
                        {
                            TaskManagerBringWindowToForeground(hwnd);
                            await Task.Delay(10);

                            if (!IsWindowMaximized(hwnd))
                            {
                                MaximizeXboxWindow(hwnd);
                            }
                        }
                        else
                        {
                            await messagebox("Xbox App konnte nicht gefunden werden.");
                        }
                    }
                    break;
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
            

            const string keyName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
            const string valueName = "Shell";

            try
            {
                // 1. Pfad dynamisch ermitteln
                string targetExecutable = Process.GetCurrentProcess().MainModule.FileName;
                if (!targetExecutable.StartsWith("\""))
                {
                    targetExecutable = $"\"{targetExecutable}\"";
                }

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, true))
                {
                    if (key == null)
                    {
                        Console.WriteLine($"[ERROR] Registry key '{keyName}' not found or no access.");
                        return;
                    }

                    // 3. NEU: Neuen Shell-Wert mit bis zu 3 Versuchen setzen
                    const int maxRetries = 3;
                    bool success = false;
                    for (int i = 0; i < maxRetries; i++)
                    {
                        key.SetValue(valueName, targetExecutable, RegistryValueKind.String);

                        // Kurze Pause, damit Windows die Änderung verarbeiten kann
                        await Task.Delay(100);

                        string currentValue = key.GetValue(valueName)?.ToString();
                        if (currentValue != null && currentValue.Equals(targetExecutable, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[OK] Shell successfully set on attempt {i + 1}.");
                            success = true;
                            break; // Erfolg, die Schleife wird beendet
                        }

                        Console.WriteLine($"[WARN] Attempt {i + 1} of {maxRetries} failed. Retrying...");
                        await Task.Delay(500); // Längere Pause vor dem nächsten Versuch
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("[ERROR] Access Denied. Run the application as an administrator.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An unexpected error occurred in ConsoleModeToShell: {ex.Message}");
            }

            
        }
        private void SettingsVerify()
        {
            try
            {
                if (!VerifySettings())
                {
                    FirstStart();
                    return;
                }

                string launcher = AppSettings.Load<string>("launcher");

                switch (launcher)
                {
                    case "steam":
                        string steamPath = AppSettings.Load<string>("steamlauncherpath");
                        if (string.IsNullOrEmpty(steamPath) || !File.Exists(steamPath))
                            throw new FileNotFoundException("The Steam path is invalid.");
                        break;
                    case "playnite":
                        string playnitePath = AppSettings.Load<string>("playnitelauncherpath");
                        if (string.IsNullOrEmpty(playnitePath) || !File.Exists(playnitePath))
                            throw new FileNotFoundException("The Playnite path is invalid.");
                        break;
                    case "custom":
                        string customPath = AppSettings.Load<string>("customlauncherpath");
                        if (string.IsNullOrEmpty(customPath) || !File.Exists(customPath))
                            throw new FileNotFoundException("The Custom Launcher path is invalid.");
                        break;
                    case "xbox":
                        break;
                    default:
                        throw new InvalidOperationException($"The launcher '{launcher}' is invalid.");
                }

                Console.WriteLine("Settings verified successfully.");
            }
            catch (Exception ex)
            {
                // =================================================================
                // NEW: Write error to a text file and open it for the user
                // =================================================================
                string logPath = Path.Combine(AppContext.BaseDirectory, "crash.log");

                // 1. Create a user-friendly error message.
                string errorMessage =
                    "================================================================\r\n" +
                    "                GCM CRASH REPORT\r\n" +
                    "================================================================\r\n\r\n" +
                    $"TIMESTAMP: {DateTime.Now}\r\n\r\n" +
                    "REASON:\r\n" +
                    "A critical error was found in the settings (settings.toml).\r\n" +
                    "This is often caused by a missing entry (like 'launcher') or an invalid file path.\r\n\r\n" +
                    "Please open the settings app and verify your configuration.\r\n\r\n" +
                    "----------------------------------------------------------------\r\n" +
                    "TECHNICAL DETAILS:\r\n" +
                    $"{ex.Message}\r\n" +
                    "----------------------------------------------------------------\r\n";

                // 2. Write the message to the crash.log file.
                File.WriteAllText(logPath, errorMessage);

                // 3. Open the log file in the default text editor (Notepad).
                try
                {
                    Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
                }
                catch
                {
                    // Fallback in case even Notepad can't be started.
                }

                // 4. Exit the application cleanly.
                BackToWindows();
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
                        
                        //HideShellWindow("Windows.UI.StartMenu");
                        KillProcess("WidgetBoard");
                KillProcess("WidgetService");
                DesktopIconController.HideDesktopIcons();
                        // Make taskbar invisible
                        //TaskbarSettings.SetAutoHide(true);
                        TaskbarVisibility.HideTaskbar();

         

                Console.WriteLine("Shell windows hidden.");

                        TaskbarManager.EnableAutoHide();
                       
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
                        // Holt den Pfad zum "Programme (x86)"-Ordner, egal wo er auf dem System ist.
                        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                        string targetExecutable = Path.Combine(programFilesX86, "GCM", "gcmloader", "gcmloader.exe");

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

        private async Task Showwinpartandlauncher()
        {
            // Dieser Teil bleibt gleich: Deaktivierung der Autostart-Apps
            try
            {
                bool usewinpartstartapps = AppSettings.Load<bool>("usewinpartstartapps");
                if (usewinpartstartapps == true)
                {
                    StartupControl.DisableAllStartupApps();
                }
            }
            catch
            {
                // Fehler ignorieren, falls die Einstellung nicht existiert
            }

            // NEU: Lade die Launcher-Einstellung, um zu entscheiden, ob gewartet werden soll.
            string launcher = AppSettings.Load<string>("launcher");

            // Führe die 10-sekündige Verzögerung nur aus, wenn es NICHT der Xbox-Launcher ist.
            if (launcher != "xbox")
            {
                Debug.WriteLine("Launcher ist nicht Xbox, warte 10 Sekunden für den WinPart-Modus...");

                launcher = AppSettings.Load<string>("launcher");
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
                    default:
                        AppSettings.Save("launcher", "steam");
                        await StartSteam();
                        break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2));
                winpart();
            }
            else
            {
                Debug.WriteLine("Launcher ist Xbox, WinPart-Modus wird sofort gestartet.");
                winpart(); 
                
                await StartXbox();
            }

           
        }
        #endregion winparts

        #endregion functions
        #region launcher
        private async Task StartSteam()
        {
            try
            {
                // Step 1: Verify the Steam path first.
                string steamPath = AppSettings.Load<string>("steamlauncherpath");
                if (string.IsNullOrWhiteSpace(steamPath) || !File.Exists(steamPath))
                {
                    throw new FileNotFoundException("The Steam path is invalid or was not found.");
                }

                // Always kill the old Steam process to ensure a clean start.
                KillProcess("steam.exe");

                // Step 2: Check if Decky Loader should be used.
                bool useDeckyLoader = false;
                try
                {
                    useDeckyLoader = AppSettings.Load<bool>("usedeckyloader");
                }
                catch
                {
                    // Setting doesn't exist, assume false.
                }

                // Step 3: Start Decky Loader if enabled.
                if (useDeckyLoader)
                {
                    Console.WriteLine("Decky Loader is enabled. Attempting to start...");
                    KillProcess("PluginLoader_noconsole.exe"); // Ensure no old instance is running

                    string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string pluginLoaderPath = Path.Combine(userHome, "homebrew", "services", "PluginLoader_noconsole.exe");

                    if (File.Exists(pluginLoaderPath))
                    {
                        Process.Start(new ProcessStartInfo(pluginLoaderPath) { UseShellExecute = true });
                        Console.WriteLine("PluginLoader_noconsole.exe started.");
                        await Task.Delay(2000); // Give Decky Loader a moment to initialize.
                    }
                    else
                    {
                        Console.WriteLine("Decky Loader executable not found, starting Steam normally.");
                    }
                }

                // Step 4: Start Steam.
                // These arguments are used whether Decky is on or off.
                string arguments = "-gamepadui -noverifyfiles -nobootstrapupdate";
                Process.Start(new ProcessStartInfo(steamPath, arguments));
                Console.WriteLine("Steam launched.");
            }
            catch (Exception ex)
            {
                // Step 5: Catch ANY error and inform the user.
                Debug.WriteLine($"Error in StartSteam: {ex.Message}");
                await messagebox("Could not start Steam. Please check the path in the settings.");
                BackToWindows();
            }
        }

        private async Task StartPlaynite()
        {
            try
            {
                string playnitePath = AppSettings.Load<string>("playnitelauncherpath");

                if (string.IsNullOrWhiteSpace(playnitePath) || !File.Exists(playnitePath))
                {
                    throw new FileNotFoundException("Der Playnite-Pfad ist ungültig oder wurde nicht gefunden.");
                }

                KillProcess("Playnite.FullscreenApp.exe");
                Process.Start(new ProcessStartInfo(playnitePath, "--startfullscreen"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler in StartPlaynite: {ex.Message}");
                await messagebox("Playnite konnte nicht gestartet werden. Bitte den Pfad in den Einstellungen prüfen.");
                BackToWindows();
            }
        }

        private void SetupWingamepadTask()
        {
            const string taskName = "GCM_wingamepad";
            const string processName = "wingamepad";

            // Schritt 1: Beende sofort alle laufenden Instanzen des Prozesses.
            try
            {
                Process[] runningProcesses = Process.GetProcessesByName(processName);
                foreach (var process in runningProcesses)
                {
                    process.Kill();
                    Console.WriteLine($"Laufender Prozess '{processName}' wurde beendet.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Beenden von '{processName}': {ex.Message}");
            }

            // Schritt 2: Prüfe die Einstellung.
            bool seamlessSwitchEnabled = false;
            try
            {
                seamlessSwitchEnabled = AppSettings.Load<bool>("useseamlessswitchtogcm");
            }
            catch
            {
                AppSettings.Save("useseamlessswitchtogcm", false); // Standardwert setzen, falls nicht vorhanden
            }

            // Schritt 3: Task erstellen oder löschen.
            using (TaskService ts = new TaskService())
            {
                var existingTask = ts.FindTask(taskName);

                if (seamlessSwitchEnabled)
                {
                    // Einstellung ist AN: Task erstellen, falls er noch nicht existiert.
                    if (existingTask == null)
                    {
                        Console.WriteLine($"Task '{taskName}' existiert nicht und wird erstellt...");
                        TaskDefinition td = ts.NewTask();
                        td.RegistrationInfo.Description = "Startet den GCM Gamepad Listener für den Seamless Switch.";
                        td.Principal.LogonType = TaskLogonType.InteractiveToken;
                        td.Principal.RunLevel = TaskRunLevel.Highest;

                        // Trigger: Bei jeder Benutzeranmeldung
                        td.Triggers.Add(new LogonTrigger());

                        // Holt den Pfad zum "Programme (x86)"-Ordner, egal wo er auf dem System ist.
                        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                        // Baut den vollständigen Pfad zur .exe-Datei zusammen.
                        string exePath = Path.Combine(programFilesX86, "GCM", "wingamepad", "wingamepad.exe");
                       
                        td.Actions.Add(new ExecAction(exePath));

                        // ### ANPASSUNG FÜR HANDHELDS ###
                        // Erlaube die Ausführung im Akkubetrieb.
                        td.Settings.StopIfGoingOnBatteries = false;
                        td.Settings.DisallowStartIfOnBatteries = false;
                        // ### ENDE DER ANPASSUNG ###

                        ts.RootFolder.RegisterTaskDefinition(taskName, td);
                        Console.WriteLine($"Task '{taskName}' wurde erfolgreich erstellt.");
                    }
                    else
                    {
                        Console.WriteLine($"Task '{taskName}' existiert bereits.");
                    }
                }
                else
                {
                    // Einstellung ist AUS: Task löschen, falls er existiert.
                    if (existingTask != null)
                    {
                        ts.RootFolder.DeleteTask(taskName);
                        Console.WriteLine($"Task '{taskName}' wurde entfernt.");
                    }
                }
            }
        }
        private void StartWingamepad()
        {
            try
            {
                // 1. Prüfen, ob die Funktion in den Einstellungen aktiviert ist.
                if (AppSettings.Load<bool>("useseamlessswitchtogcm"))
                {
                
                    string exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GCM", "wingamepad", "wingamepad.exe");


                    string processName = "wingamepad";

                    // 2. Sicherstellen, dass die Datei existiert.
                    if (File.Exists(exePath))
                    {
                        // NEU: Zuerst alle alten Instanzen von wingamepad.exe beenden.
                        try
                        {
                            Process[] existingProcesses = Process.GetProcessesByName(processName);
                            foreach (var proc in existingProcesses)
                            {
                                proc.Kill();
                                Console.WriteLine("Alter wingamepad-Prozess wurde beendet.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Fehler beim Beenden alter wingamepad-Prozesse: {ex.Message}");
                        }

                        // 3. Den neuen Prozess starten.
                        Process.Start(exePath);
                        Console.WriteLine("wingamepad.exe wurde für den Seamless Switch gestartet.");
                    }
                    else
                    {
                        Console.WriteLine($"Fehler: wingamepad.exe wurde nicht unter '{exePath}' gefunden.");
                    }
                }
                else
                {
                    Console.WriteLine("Seamless Switch ist deaktiviert, wingamepad.exe wird nicht gestartet.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Versuch, wingamepad.exe zu starten: {ex.Message}");
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
                Process.Start(new ProcessStartInfo(launcherPath));
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

        private async Task Start()
        {
            
            // Warten, bis das Video fertig ist
            while (startupVideoFinished == false)
            {
                await Task.Delay(50);
            }
            SetupLogging();
            uac("off");
            // ===================================
            SettingsVerify();
            
            await StartOverlayAsync();
            await Task.Run(() => RunBoilrNoUI());
            displayfusion("start");
            IsJoyxoffInstalledAndStart();
            SetupWingamepadTask();
            await Showwinpartandlauncher();
            cssloader();
            ConsoleModeToShell();
            preaudio(true, false);
            prestartlist();
            StartLosslessScaling();
            SwitchToConfiguredLauncher();
        }
        #endregion start
        #region TaskManager


        private void StartAutoTaskRefresh()
        {
            if (_taskRefreshTimer != null) return;
            _taskRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _taskRefreshTimer.Tick += async (s, e) =>
            {
                if (!IsWindowInForeground()) return;

                var processDataList = await Task.Run(() =>
                {
                    var dataList = new List<ProcessData>();
                    var seenHwnds = new HashSet<IntPtr>();

                    EnumWindows((hWnd, lParam) =>
                    {
                        if (!IsWindowVisible(hWnd) || GetWindow(hWnd, (uint)GetWindowCmd.GW_OWNER) != IntPtr.Zero)
                            return true;

                        int textLen = GetWindowTextLength(hWnd);
                        if (textLen == 0)
                            return true;

                        var style = (WindowStylesEx)GetWindowLong(hWnd, WindowLongFlags.GWL_EXSTYLE);
                        if (style.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW))
                            return true;

                        if (IsCloaked(hWnd))
                            return true;

                        var titleBuilder = new StringBuilder(textLen + 1);
                        GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                        string windowTitle = titleBuilder.ToString();

                        if (string.IsNullOrWhiteSpace(windowTitle) ||
                            _excludedTitles.Any(t => windowTitle.Contains(t, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }

                        Process proc = null;
                        string exePath = null;
                        try
                        {
                            GetWindowThreadProcessId(hWnd, out uint pid);
                            if (pid != 0)
                            {
                                proc = Process.GetProcessById((int)pid);
                                if (proc.Id == Process.GetCurrentProcess().Id) return true;
                                exePath = proc.MainModule?.FileName;
                            }
                        }
                        catch
                        {
                            proc = null;
                            exePath = null;
                        }

                        if (!seenHwnds.Add(hWnd)) return true;

                        dataList.Add(new ProcessData
                        {
                            ProductName = windowTitle,
                            Hwnd = hWnd,
                            Proc = proc,
                            ExePath = exePath
                        });

                        return true;
                    }, IntPtr.Zero);

                    return dataList;
                });

                UpdateUiFromData(processDataList);
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
        private async void LoadCardImageAsync(Border card, string gameName, string exePath)
        {
            // Stufe 1: Keyword-Blacklist-Filter
            if (_nonGameKeywords.Any(keyword => gameName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // Suche im Cache oder über die API
            SearchResult searchResult = null;
            if (_gameIdCache.ContainsKey(gameName))
            {
                searchResult = _gameIdCache[gameName];
            }
            else
            {
                searchResult = await _steamGridHelper.SearchForGameIdAsync(gameName);
                _gameIdCache[gameName] = searchResult;
            }

            if (searchResult == null)
            {
                return;
            }

            // Stufe 2: Ähnlichkeits-Filter
            double similarity = CalculateSimilarity(gameName, searchResult.name);
            if (similarity < 0.5)
            {
                return;
            }

            // Alle Filter bestanden. Lade das Bild.
            var imageUrl = await _steamGridHelper.GetGridImageUrlAsync(searchResult.id);

            if (!string.IsNullOrEmpty(imageUrl))
            {
                try
                {
                    string localImagePath = Path.Combine(_imageCachePath, $"{searchResult.id}.jpg");
                    if (!File.Exists(localImagePath))
                    {
                        using (var client = new HttpClient())
                        {
                            var imageData = await client.GetByteArrayAsync(imageUrl);
                            await File.WriteAllBytesAsync(localImagePath, imageData);
                        }
                    }

                    // --- UI-Update auf dem UI-Thread durchführen ---
                    card.DispatcherQueue.TryEnqueue(() =>
                    {
                        // 1. Hintergrund der Karte mit dem SteamGridDB-Bild ersetzen
                        card.Background = new ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri(localImagePath)),
                            Stretch = Stretch.UniformToFill
                        };

                        // 2. Zugriff auf das Grid und seine Elemente
                        if (card.Child is Grid contentGrid)
                        {
                            // 3. Das ursprüngliche App-Icon ausblenden
                            var iconImage = contentGrid.Children.OfType<Image>().FirstOrDefault(img => img.Name == "IconImage");
                            if (iconImage != null)
                            {
                                iconImage.Visibility = Visibility.Collapsed;
                            }

                            // 4. Den Text mit dem präziseren Spieletitel von SteamGridDB aktualisieren
                            var titleText = contentGrid.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "TitleText");
                            if (titleText != null)
                            {
                                titleText.Text = searchResult.name;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageCache] FEHLER beim Herunterladen für '{gameName}': {ex.Message}");
                }
            }
        }

        private void UpdateUiFromData(List<ProcessData> processDataList)
        {
            if (processDataList == null) return;

            var currentProductNames = new HashSet<string>(_cardCache.Select(c => c.ProductName));
            var newDataProductNames = new HashSet<string>(processDataList.Select(pd => pd.ProductName));

            // Remove old cards that are no longer in the new list
            var cardsToRemove = _cardCache.Where(c => !newDataProductNames.Contains(c.ProductName)).ToList();
            foreach (var entry in cardsToRemove)
            {
                ProgramCardPanel.Children.Remove(entry.Card);
                _cardCache.Remove(entry);
            }

            // Add new cards that are not yet in the cache
            foreach (var data in processDataList)
            {
                if (!currentProductNames.Contains(data.ProductName))
                {
                    // The card is created instantly
                    var border = CreateProgramCard(data.ProductName, data.ExePath, data.Proc, data.Hwnd);

                    // CORRECTED: Create the cache entry with all required properties
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
                }
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
    
    // Specific Apps to always ignore
    "Steam", // The main Steam window, not games
    "Big-Picture-Modus", // NEW
    "Big Picture Mode",  // NEW
    "Realtek Audio Console",
    "NVIDIA App",
    "Xbox.Apps.TCUI",
    "Xbox"
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


        private static readonly string[] _excludedProcessNames = new[]
{
    "steam", "steamwebhelper", "EADesktop", "epicgameslauncher",
    "GalaxyClient", "battle.net", "UbisoftConnect", "start_protected_game"
};
        

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

        private Border CreateProgramCard(string name, string exePath, Process proc, IntPtr hwnd)
        {
            // Basis-Farbverlauf als Fallback erstellen
            Color avgColor = Color.FromArgb(255, 60, 60, 60);
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    using (var iconBmp = GetBitmapFromExeIcon(exePath))
                    {
                        if (iconBmp != null) avgColor = GetAverageColor(iconBmp);
                    }
                }
                catch { /* Ignorieren */ }
            }

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0.5, 0),
                EndPoint = new Windows.Foundation.Point(0.5, 1),
                GradientStops = new GradientStopCollection
        {
            new GradientStop { Color = Color.FromArgb(220, avgColor.R, avgColor.G, avgColor.B), Offset = 0.0 },
            new GradientStop { Color = Color.FromArgb(220, 20, 20, 20), Offset = 1.0 }
        }
            };

            // --- NEUE STRUKTUR MIT GRID FÜR TEXT-OVERLAY ---

            // 1. Das Grid, das alles enthalten wird
            var contentGrid = new Grid();

            // 2. Das App-Icon (wird später bei Bedarf ausgeblendet)
            var iconImage = new Image
            {
                Name = "IconImage", // Wichtig für späteren Zugriff
                Source = GetAppIconAsBitmapImage(exePath) ?? new BitmapImage(new Uri("ms-appx:///Assets/game.png")),
                Width = 64,
                Height = 64,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 3. Der dunkle Farbverlauf am unteren Rand für die Lesbarkeit
            var textBackground = new Border
            {
                Height = 80,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0.5, 0),
                    EndPoint = new Windows.Foundation.Point(0.5, 1),
                    GradientStops = new GradientStopCollection
            {
                new GradientStop { Color = Colors.Transparent, Offset = 0.0 },
                new GradientStop { Color = Color.FromArgb(200, 0, 0, 0), Offset = 1.0 }
            }
                }
            };

            // 4. Der TextBlock für den Titel
            var titleText = new TextBlock
            {
                Name = "TitleText", // Wichtig für späteren Zugriff
                Text = name, // Zuerst den Fenstertitel als Fallback verwenden
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 16,
                Margin = new Thickness(10, 0, 10, 10),
                VerticalAlignment = VerticalAlignment.Bottom,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxLines = 2
            };

            // Elemente zum Grid hinzufügen
            contentGrid.Children.Add(iconImage);
            contentGrid.Children.Add(textBackground);
            contentGrid.Children.Add(titleText);

            // Die finale Karte erstellen
            var cardBorder = new Border
            {
                Width = 220,
                Height = 260,
                Background = gradient, // Der farbige Verlauf als Hintergrund
                CornerRadius = new CornerRadius(15),
                Margin = new Thickness(10),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Tag = new CardTag { Process = proc, Hwnd = hwnd },
                Child = contentGrid // Das Grid mit dem Inhalt ist jetzt das Child
            };

            // Asynchron das SteamGridDB-Bild laden
            LoadCardImageAsync(cardBorder, name, exePath);

            return cardBorder;
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
        private void HighlightSelectedCard(bool skipScroll = false, bool forceAnimation = true)
        {
            for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
            {
                if (ProgramCardPanel.Children[i] is Border border)
                {
                    bool isSelected = (i == _selectedCardIndex);

                    // Rufe jetzt BEIDE Animationen auf. Sie laufen synchron.
                    AnimateBorderColor(border, isSelected);
                    AnimateScale(border, isSelected);
                }
            }

            // Scrollt zur Karte, aber nur, wenn der Karten-Bereich aktiv ist.
            if (_currentFocusArea == FocusArea.Cards && !skipScroll && ProgramCardPanel.Children.Count > _selectedCardIndex)
            {
                ScrollToCardAnimated(ProgramCardPanel.Children[_selectedCardIndex]);
            }
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
        public void BringTaskManagerToFrontAndFocus()
        {
            // Die BringToFrontAndFocus-Methode ist stark genug, um den Fokus zu übernehmen.

            // Wir müssen nur sicherstellen, dass dieser Aufruf auf dem UI-Thread ausgeführt wird.
            DispatcherQueue.TryEnqueue(() =>
            {
                IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
                BringToFrontAndFocus(hWnd);
            });
        }
        private async Task StartXbox()
        {
            try
            {
                // --- NEUE, OPTIMIERTE REIHENFOLGE ---

                // Schritt 1: Taskleiste auf "Automatisch ausblenden" stellen.
                // Das bereitet das System darauf vor, dass eine App den ganzen Bildschirm nutzen will.
                TaskbarManager.EnableAutoHide();

                // Schritt 2: Xbox App starten.
                Process.Start(new ProcessStartInfo("xbox:") { UseShellExecute = true });
                Debug.WriteLine("Xbox App launched via protocol.");

                // Schritt 3: Zuverlässig auf das Fenster warten.
                IntPtr xboxHwnd = await FindXboxWindowHandleAsync(15);
                if (xboxHwnd == IntPtr.Zero)
                {
                    throw new Exception("The main window of the Xbox App could not be found.");
                }
                Debug.WriteLine($"Xbox window with handle {xboxHwnd} found.");

                // Schritt 4: Fenster in den Vordergrund holen.
                TaskManagerBringWindowToForeground(xboxHwnd);
                await Task.Delay(750); // Stabile Wartezeit.

                // Schritt 5: Fenster maximieren.
                // Da die Taskleiste jetzt auf "ausblenden" steht, sollte sich das Fenster
                // ohne Spalt über den gesamten Bildschirm maximieren.
                if (!IsWindowMaximized(xboxHwnd))
                {
                    Debug.WriteLine("Xbox window is not maximized. Maximizing now...");
                    MaximizeXboxWindow(xboxHwnd);
                }
                else
                {
                    Debug.WriteLine("Xbox window is already maximized.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StartXbox: {ex.Message}");
            }
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
            _activeShortcuts.Clear();
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

                // 1. Lade die benutzerdefinierten Shortcuts
                if (settingsModel.TryGetValue("shortcuts", out var shortcutsObj) && shortcutsObj is TomlTableArray shortcutsArray)
                {
                    foreach (TomlTable shortcutTable in shortcutsArray)
                    {
                        var data = new ShortcutData
                        {
                            Key1 = shortcutTable["key1"]?.ToString(),
                            Key2 = shortcutTable["key2"]?.ToString(),
                            Function = shortcutTable["function"]?.ToString(),
                            Enabled = Convert.ToBoolean(shortcutTable["enabled"])
                        };

                        // WICHTIG: Lade nur aktivierte Shortcuts
                        if (data.Enabled && !string.IsNullOrEmpty(data.Key1) && !string.IsNullOrEmpty(data.Key2))
                        {
                            _activeShortcuts[(data.Key1, data.Key2)] = data.Function;
                            LogGamepadInit($"[OK] Loaded Custom Shortcut: {data.Key1} + {data.Key2} -> {data.Function}");
                        }
                    }
                }

                // 2. Lade den "Seamless Switch" (winmode_shortcut)
                if (settingsModel.TryGetValue("winmode_shortcut", out var winShortcutObj) && winShortcutObj is TomlTable winShortcutTable)
                {
                    var data = new ShortcutData
                    {
                        Key1 = winShortcutTable["key1"]?.ToString(),
                        Key2 = winShortcutTable["key2"]?.ToString(),
                        Enabled = Convert.ToBoolean(winShortcutTable["enabled"])
                    };

                    if (data.Enabled && !string.IsNullOrEmpty(data.Key1) && !string.IsNullOrEmpty(data.Key2))
                    {
                        _activeShortcuts[(data.Key1, data.Key2)] = "winmodechange"; // Funktion ist hier fest codiert
                        LogGamepadInit($"[OK] Loaded Seamless Switch: {data.Key1} + {data.Key2} -> winmodechange");
                    }
                }

                // 3. Weise den geladenen Funktionen die entsprechenden Aktionen zu
                _shortcutActions["taskmanager"] = BringTaskManagerToFrontAndFocus;
                _shortcutActions["switch tab"] = SendWinTab;
                _shortcutActions["audio switch"] = SwitchToNextAudioDevice;
                _shortcutActions["performance overlay"] = TriggerPerformanceOverlay;
                _shortcutActions["show overlay"] = showoverlay;
                _shortcutActions["xbox bar"] = xboxbar;
                _shortcutActions["lossless scaling"] = LosslessScaling;
                _shortcutActions["winmodechange"] = Triggerbacktowin; 
            }
            catch (Exception ex)
            {
                LogGamepadInit($"[ERROR] Failed to load shortcuts from TOML: {ex.Message}");
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

        // ########## ANFANG DES KOMPLETTEN CODE-BLOCKS ##########

        #region Gamepad Navigation

        // --- Zustandsvariablen, um den vorherigen Fokus zu speichern (für Performance) ---
        private FocusArea _previousFocusArea = FocusArea.Cards;
        private int _previousCardIndex = -1;
        private int _previousTopButtonIndex = -1;


        /// <summary>
        /// Verarbeitet alle globalen Gamepad-Shortcuts.
        /// </summary>
        private void HandleShortcuts(GamepadButtonFlags currentButtons)
        {
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
        }

        /// <summary>
        /// Die zentrale Methode, die alle Gamepad-Eingaben für Shortcuts und UI-Navigation verarbeitet.
        /// </summary>
        /// 

        /// <summary>
        /// Startet den Computer neu.
        /// </summary>
        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Neustart-Button geklickt...");
            // Der Parameter /r steht für "restart"
            Process.Start("shutdown", "/r /t 0");
        }
        private void GamepadButtonCheck()
        {
            if (!_controllerConnected || !_xinputController.IsConnected) return;

            var state = _xinputController.GetState();
            var currentButtons = state.Gamepad.Buttons;

            HandleShortcuts(currentButtons);

            if (!IsWindowInForeground())
            {
                _lastButtonState = currentButtons;
                return;
            }

            if (IsNewButtonPress(GamepadButtonFlags.RightShoulder, currentButtons))
            {
                switch (_currentFocusArea)
                {
                    case FocusArea.Launcher: _currentFocusArea = FocusArea.Cards; break;
                    case FocusArea.Cards: _currentFocusArea = FocusArea.TopButtons; break;
                    case FocusArea.TopButtons: _currentFocusArea = FocusArea.Launcher; break;
                }
                _selectedCardIndex = 0;
                _selectedTopButtonIndex = 0;
                _selectedLauncherAreaIndex = 0;
                UpdateVisualFocus();
                PlayNavigationSound();
            }

            if (_currentFocusArea == FocusArea.Launcher)
            {
                if (IsNewButtonPress(GamepadButtonFlags.DPadRight, currentButtons))
                {
                    _selectedLauncherAreaIndex = (_selectedLauncherAreaIndex + 1) % _launcherAreaButtons.Count;
                    UpdateVisualFocus();
                    PlayNavigationSound();
                }
                else if (IsNewButtonPress(GamepadButtonFlags.DPadLeft, currentButtons))
                {
                    _selectedLauncherAreaIndex = (_selectedLauncherAreaIndex - 1 + _launcherAreaButtons.Count) % _launcherAreaButtons.Count;
                    UpdateVisualFocus();
                    PlayNavigationSound();
                }
            }
            else if (_currentFocusArea == FocusArea.TopButtons)
            {
                if (IsNewButtonPress(GamepadButtonFlags.DPadRight, currentButtons))
                {
                    _selectedTopButtonIndex = (_selectedTopButtonIndex + 1) % _topButtons.Count;
                    UpdateVisualFocus();
                    PlayNavigationSound();
                }
                else if (IsNewButtonPress(GamepadButtonFlags.DPadLeft, currentButtons))
                {
                    _selectedTopButtonIndex = (_selectedTopButtonIndex - 1 + _topButtons.Count) % _topButtons.Count;
                    UpdateVisualFocus();
                    PlayNavigationSound();
                }
            }
            else if (_currentFocusArea == FocusArea.Cards)
            {
                if (IsNewButtonPress(GamepadButtonFlags.DPadRight, currentButtons))
                {
                    if (ProgramCardPanel.Children.Any())
                        _selectedCardIndex = (_selectedCardIndex + 1) % ProgramCardPanel.Children.Count;
                    UpdateVisualFocus();
                    PlayNavigationSound();
                }
                else if (IsNewButtonPress(GamepadButtonFlags.DPadLeft, currentButtons))
                {
                    if (ProgramCardPanel.Children.Any())
                        _selectedCardIndex = (_selectedCardIndex - 1 + ProgramCardPanel.Children.Count) % ProgramCardPanel.Children.Count;
                    UpdateVisualFocus();
                    PlayNavigationSound();
                }
            }

            if (IsNewButtonPress(GamepadButtonFlags.A, currentButtons))
            {
                if (_currentFocusArea == FocusArea.Launcher)
                {
                    if (_selectedLauncherAreaIndex == 0) LauncherCard_Tapped(null, null);
                    else if (_selectedLauncherAreaIndex == 1) DiscordCard_Tapped(null, null);
                }
                else if (_currentFocusArea == FocusArea.Cards) TriggerCardAction(_selectedCardIndex, true);
                else if (_currentFocusArea == FocusArea.TopButtons) ClickSelectedTopButton();
                PlayActivationSound();
            }

            if (IsNewButtonPress(GamepadButtonFlags.B, currentButtons))
            {
                if (_currentFocusArea == FocusArea.Cards) TriggerCardAction(_selectedCardIndex, false);
                PlaydeactivationSound();
            }

            _lastButtonState = currentButtons;
        }

        /// <summary>
        /// Aktualisiert die UI performant, indem nur das alte und neue Element geändert wird.
        /// </summary>
        private void UpdateVisualFocus(bool isInitial = false)
        {
            // Alle Elemente zurücksetzen
            _launcherAreaButtons.ForEach(b => { AnimateScale(b, false); AnimateBorderColor(b, false); });
            _topButtons.ForEach(b => { b.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent); b.BorderThickness = new Thickness(0); });
            for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
            {
                if (ProgramCardPanel.Children[i] is Border card) { AnimateScale(card, false); AnimateBorderColor(card, false); }
            }

            // Aktuelles Element hervorheben
            if (_currentFocusArea == FocusArea.Launcher)
            {
                var selectedButton = _launcherAreaButtons[_selectedLauncherAreaIndex];
                AnimateScale(selectedButton, true);
                AnimateBorderColor(selectedButton, true);
            }
            else if (_currentFocusArea == FocusArea.TopButtons)
            {
                var selectedButton = _topButtons[_selectedTopButtonIndex];
                selectedButton.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke);
                selectedButton.BorderThickness = new Thickness(2);
            }
            else if (_currentFocusArea == FocusArea.Cards && ProgramCardPanel.Children.Count > _selectedCardIndex)
            {
                if (ProgramCardPanel.Children[_selectedCardIndex] is Border card)
                {
                    AnimateScale(card, true);
                    AnimateBorderColor(card, true);
                    ScrollToCardAnimated(card);
                }
            }
        }

        #endregion

        // ########## ENDE DES KOMPLETTEN CODE-BLOCKS ##########


        /// <summary>
        /// Animiert die Skalierung eines UI-Elements performant.
        /// </summary>
        private void AnimateScale(UIElement element, bool isSelected)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var targetScale = isSelected ? new Vector3(1.04f, 1.04f, 1.0f) : new Vector3(1.0f, 1.0f, 1.0f);

            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(250);
            scaleAnimation.InsertKeyFrame(1.0f, targetScale, compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.0f), new Vector2(0.2f, 1.0f)));

            // ENTFERNT: Die langsame CenterPoint-Berechnung für maximale Performance.
            // Die Animation startet jetzt von der oberen linken Ecke.

            visual.StartAnimation("Scale", scaleAnimation);
        }

        /// <summary>
        /// Verarbeitet alle globalen Gamepad-Shortcuts.
        /// </summary>
     
      

        /// <summary>
        /// Führt die Klick-Aktion für den aktuell ausgewählten oberen Button aus.
        /// </summary>
        private void ClickSelectedTopButton()
        {
            if (_topButtons.Count > _selectedTopButtonIndex)
            {
                var buttonToClick = _topButtons[_selectedTopButtonIndex];

                // Führe die passende Klick-Methode aus
                if (buttonToClick == ExitGcmButton) ExitGcmButton_Click_1(null, null);
                else if (buttonToClick == ShutdownButton) ShutdownButton_Click(null, null);
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

        /// <summary>
        /// Sucht nach allen laufenden Anwendungen und aktualisiert die UI-Karten.
        /// </summary>
        private async Task RefreshAppListAsync()
        {
            // Die Suche nur durchführen, wenn das Fenster im Vordergrund ist
            if (!IsWindowInForeground()) return;

            var processDataList = await Task.Run(() =>
            {
                var dataList = new List<ProcessData>();
                var seenHwnds = new HashSet<IntPtr>();

                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd) || GetWindow(hWnd, (uint)GetWindowCmd.GW_OWNER) != IntPtr.Zero)
                        return true;

                    int textLen = GetWindowTextLength(hWnd);
                    if (textLen == 0)
                        return true;

                    var style = (WindowStylesEx)GetWindowLong(hWnd, WindowLongFlags.GWL_EXSTYLE);
                    if (style.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW))
                        return true;

                    if (IsCloaked(hWnd))
                        return true;

                    var titleBuilder = new StringBuilder(textLen + 1);
                    GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                    string windowTitle = titleBuilder.ToString();

                    if (string.IsNullOrWhiteSpace(windowTitle) ||
                        _excludedTitles.Any(t => windowTitle.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }

                    Process proc = null;
                    string exePath = null;
                    try
                    {
                        GetWindowThreadProcessId(hWnd, out uint pid);
                        if (pid != 0)
                        {
                            proc = Process.GetProcessById((int)pid);
                            if (proc.Id == Process.GetCurrentProcess().Id) return true;
                            exePath = proc.MainModule?.FileName;
                        }
                    }
                    catch
                    {
                        proc = null;
                        exePath = null;
                    }

                    if (!seenHwnds.Add(hWnd)) return true;

                    dataList.Add(new ProcessData
                    {
                        ProductName = windowTitle,
                        Hwnd = hWnd,
                        Proc = proc,
                        ExePath = exePath
                    });

                    return true;
                }, IntPtr.Zero);

                return dataList;
            });

            UpdateUiFromData(processDataList);
        }


        private async void TriggerCardAction(int index, bool launch)
        {
            if (index < 0 || index >= _cardCache.Count) return;

            var entry = _cardCache[index];
            if (!(entry.Card.Tag is CardTag tag)) return;

            if (launch)
            {
                MakeSelfNonTopmost();
                TaskManagerBringWindowToForeground(tag.Hwnd);
            }
            else // B-Button zum Schließen
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

                // Warte kurz und aktualisiere dann die Liste
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
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;


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
                    ShowWindow(hWnd, SW_RESTORE);
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
      

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        #endregion
        #region Startupvideo

        #region Startup Video Logic

        private void PlayStartupVideo()
        {
            try
            {
                bool useSteamHack;
                bool useVideo = AppSettings.Load<bool>("usestartupvideo");
                try
                {
                    useSteamHack = AppSettings.Load<bool>("usesteamstartupvideo");
                }
                catch
                {
                    AppSettings.Save("usesteamstartupvideo", false);
                }
               useSteamHack = AppSettings.Load<bool>("usesteamstartupvideo");

                if (!useVideo || useSteamHack)
                {
                    TransitionToMainUI();
                    if (useSteamHack)
                    {
                        RenameSteamStartupVideo_Start();
                    }
                    return;
                }
            }
            catch
            {
                TransitionToMainUI();
                return;
            }

            try
            {
                string videoPath = AppSettings.Load<string>("startupvideo_path");
                if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                {
                    TransitionToMainUI();
                    return;
                }

                _startupMediaPlayer = new MediaPlayer { AutoPlay = true };
                _startupMediaPlayer.Source = MediaSource.CreateFromUri(new Uri(videoPath, UriKind.Absolute));
                _startupMediaPlayer.MediaEnded += OnStartupVideoEnded;
                StartupVideoPlayer.SetMediaPlayer(_startupMediaPlayer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StartupVideo] Error playing video: {ex.Message}");
                TransitionToMainUI();
            }
        }

        private void OnStartupVideoEnded(MediaPlayer sender, object args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                TransitionToMainUI();
            });
        }

        private void TransitionToMainUI()
        {
            // === HIER IST DIE ÄNDERUNG ===
            // Lade das Hintergrundbild JETZT, direkt bevor die UI erscheint.
            SetBackgroundImage(GetScreenWidth(), GetScreenHeight());
            // =============================

            // Video-Player aufräumen
            if (_startupMediaPlayer != null)
            {
                _startupMediaPlayer.MediaEnded -= OnStartupVideoEnded;
                _startupMediaPlayer.Dispose();
                _startupMediaPlayer = null;
            }
            StartupVideoPlayer.SetMediaPlayer(null);
            StartupVideoPlayer.Visibility = Visibility.Collapsed;

            // Haupt-UI mit einer sanften Einblend-Animation sichtbar machen
            MainContent.Visibility = Visibility.Visible;
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(500))
            };
            var storyboard = new Storyboard();
            storyboard.Children.Add(fadeInAnimation);
            Storyboard.SetTarget(fadeInAnimation, MainContent);
            Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");
            storyboard.Begin();

            // Signal an die Start()-Methode, dass sie weitermachen kann
            startupVideoFinished = true;
        }

        // Logik für den Steam-Videotausch
        public static void RenameFile(string oldFilePath, string newFilePath)
        {
            try { if (File.Exists(oldFilePath)) { File.Move(oldFilePath, newFilePath, true); } }
            catch (Exception ex) { Debug.WriteLine($"[StartupVideo] Error renaming file '{oldFilePath}': {ex.Message}"); }
        }

        public static void RenameSteamStartupVideo_Start()
        {
            try
            {
                string steamPath = AppSettings.Load<string>("steamlauncherpath");
                if (string.IsNullOrEmpty(steamPath)) return;
                string moviesPath = Path.Combine(Path.GetDirectoryName(steamPath), "steamui", "movies");
                string steamVideoPath = Path.Combine(moviesPath, "bigpicture_startup.webm");
                string steamVideoBackupPath = Path.Combine(moviesPath, "bigpicture_startup.old.webm");
                string gcmVideoPath = Path.Combine(moviesPath, "GCM_vid.webm");

                if (File.Exists(steamVideoBackupPath))
                {
                    File.Move(steamVideoBackupPath, steamVideoPath, true);
                }

                if (File.Exists(steamVideoPath))
                {
                    File.Move(steamVideoPath, steamVideoBackupPath, true);
                }
                if (File.Exists(gcmVideoPath))
                {
                    File.Copy(gcmVideoPath, steamVideoPath, true);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[StartupVideo] Error in RenameSteamStartupVideo_Start: {ex.Message}"); }
        }

        public static void RenameSteamStartupVideo_End()
        {
            try
            {
                string steamPath = AppSettings.Load<string>("steamlauncherpath");
                if (string.IsNullOrEmpty(steamPath)) return;
                string moviesPath = Path.Combine(Path.GetDirectoryName(steamPath), "steamui", "movies");
                string steamVideoPath = Path.Combine(moviesPath, "bigpicture_startup.webm");
                string steamVideoBackupPath = Path.Combine(moviesPath, "bigpicture_startup.old.webm");

                if (File.Exists(steamVideoBackupPath))
                {
                    if (File.Exists(steamVideoPath))
                    {
                        File.Delete(steamVideoPath);
                    }
                    File.Move(steamVideoBackupPath, steamVideoPath, true);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[StartupVideo] Error in RenameSteamStartupVideo_End: {ex.Message}"); }
        }

        #endregion

        #endregion Startupvideo

        #endregion methodes

        private void LauncherTileRow_Loaded(object sender, RoutedEventArgs e)
        {
           
        }

        /// <summary>
        /// Fährt den Computer herunter.
        /// </summary>
        private async void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Shutdown-Button geklickt. Räume auf und fahre den Computer herunter...");

            // ZUERST die Aufräum-Methode aufrufen
            RenameSteamStartupVideo_End();

            // Gib dem System einen winzigen Moment Zeit, die Dateioperation abzuschließen
            await Task.Delay(200);

            // DANN den Computer herunterfahren
            Process.Start("shutdown", "/s /t 0");
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
}
