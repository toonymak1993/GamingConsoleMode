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
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NAudio.CoreAudioApi;
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
// SteamGridDBHelper.cs
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

        #region mousecontrol 

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
        #region window engine
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
        private DispatcherTimer _wingamepadMonitor;
        private bool _isExiting = false; 
        #endregion wingamepad
        #region Startup Video
        private MediaPlayer _startupMediaPlayer;
        private bool startupVideoFinished = false;
        private bool _isVideoPlaybackInitiated = false;
        #endregion
        #region steamgriddb - picture for taskmanager

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
            // Die record-Definitionen wurden nach oben in die MainWindow-Klasse verschoben.
            // Die Klasse ist jetzt sauberer.

            private readonly HttpClient _httpClient = new();
            private readonly string _apiKey;
            public bool IsApiKeySet => !string.IsNullOrEmpty(_apiKey);
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
            Logger.Log($"[DEBUG] FindLocalSteamImageAsync started. Searching for: {exePath}");
            try
            {
                // --- Priority 1 & 2: Check game's .exe folder and parent folder ---
                string exeDirectory = Path.GetDirectoryName(exePath);
                if (Directory.Exists(exeDirectory))
                {
                    string[] foundFiles = await Task.Run(() =>
                        Directory.GetFiles(exeDirectory, "library_*.jpg"));
                    if (foundFiles.Length > 0)
                    {
                        Logger.Log($"[DEBUG] Prio 1 (EXE-Folder): Image found: {foundFiles[0]}");
                        return foundFiles[0];
                    }
                    string headerPath = Path.Combine(exeDirectory, "header.jpg");
                    if (File.Exists(headerPath))
                    {
                        Logger.Log($"[DEBUG] Prio 1 (EXE-Folder): 'header.jpg' found: {headerPath}");
                        return headerPath;
                    }
                }
                DirectoryInfo parentDir = Directory.GetParent(exeDirectory);
                if (parentDir != null)
                {
                    string parentDirectoryPath = parentDir.FullName;
                    string[] foundParentFiles = await Task.Run(() =>
                        Directory.GetFiles(parentDirectoryPath, "library_*.jpg"));
                    if (foundParentFiles.Length > 0)
                    {
                        Logger.Log($"[DEBUG] Prio 2 (Parent-Folder): Image found: {foundParentFiles[0]}");
                        return foundParentFiles[0];
                    }
                    string headerParentPath = Path.Combine(parentDirectoryPath, "header.jpg");
                    if (File.Exists(headerParentPath))
                    {
                        Logger.Log($"[DEBUG] Prio 2 (Parent-Folder): 'header.jpg' found: {headerParentPath}");
                        return headerParentPath;
                    }
                }
                Logger.Log($"[DEBUG] Prio 1 & 2: No image found in game folders.");


                // --- Priority 3: Check the central Steam Cache (Primary method) ---
                string steamAppId = await Task.Run(() => GetSteamAppIdFromExePath(exePath));
                string steamInstallPath = GetSteamInstallPath();

                Logger.Log($"[DEBUG] Prio 3 (Steam-Cache):\n    Image Cache Path (C:): {steamInstallPath}\n    Found AppID (from D:): {(string.IsNullOrEmpty(steamAppId) ? "NONE" : steamAppId)}");

                if (string.IsNullOrEmpty(steamInstallPath) || string.IsNullOrEmpty(steamAppId))
                {
                    Logger.Log("[DEBUG] Prio 3: Steam path or AppID is invalid. Skipping Steam-Cache search.");
                    return null;
                }

            
                string appCacheDirectory = Path.Combine(steamInstallPath, "appcache", "librarycache", steamAppId);
                string imagePath = await SearchCacheFolderForImageAsync(appCacheDirectory);

                if (!string.IsNullOrEmpty(imagePath))
                {
                    return imagePath; // SUCCESS!
                }
                // ### END OF FIX V5 ###
                // ==================================================================
            }
            catch (Exception ex)
            {
                Logger.Log($"[DEBUG] FindLocalSteamImageAsync: FATAL ERROR: {ex.Message}");
            }

            Logger.Log("[DEBUG] FindLocalSteamImageAsync: Search finished. NO local image found.");
            return null;
        }


        #endregion dsteamgriddb - picture for taskmanager

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

            // Handle des eigenen Fensters holen
            IntPtr selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Handle des Fensters, das gerade im Vordergrund ist
            IntPtr foregroundHwnd = GetForegroundWindow();

            // 1. Prüfen, ob unser eigenes Fenster bereits im Fokus ist
            if (foregroundHwnd == selfHwnd)
            {
                // Wenn unser Fenster den Fokus hat, aber das Overlay noch aktiv ist -> ausblenden
                if (_isOverlayActive)
                {
                    _isOverlayActive = false;
                    AnimateOverlayOpacity(FocusLossOverlay, 0.0, true);
                }
                return; // Nichts weiter zu tun
            }

            // Wenn wir hier ankommen, hat ein ANDERES Fenster den Fokus.
            // Wir finden jetzt heraus, welches es ist.
            StringBuilder classNameBuilder = new StringBuilder(256);
            GetClassName(foregroundHwnd, classNameBuilder, classNameBuilder.Capacity);
            string className = classNameBuilder.ToString();

            // 2. Prüfen, ob der Desktop im Fokus ist
            if (className == "Progman" || className == "WorkerW")
            {
                // Der Desktop ist aktiv! Unser GCM holt sich den Fokus zurück.
                Debug.WriteLine("Desktop is active, reclaiming focus for GCM.");
                BringToFrontAndFocus(selfHwnd);
                // Die Logik zum Ausblenden des Overlays wird im nächsten Tick automatisch greifen,
                // wenn unser Fenster wieder den Fokus hat (siehe Schritt 1).
            }
            // 3. Eine andere Anwendung (weder wir noch der Desktop) ist im Fokus
            else
            {
                // Wenn eine andere App den Fokus hat und unser Overlay noch nicht aktiv ist -> einblenden
                if (!_isOverlayActive)
                {
                    _isOverlayActive = true;
                    FocusLossOverlay.Opacity = 0;
                    FocusLossOverlay.Visibility = Visibility.Visible;
                    AnimateOverlayOpacity(FocusLossOverlay, 1.0);
                }
            }
        }
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
        private enum FocusArea { Launcher, Cards, TopButtons, PowerMenu, AppLauncher }
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
            Logger.Initialize();
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
            LoadDynamicLauncherCards();
            _topButtons = new List<Button> { ExitGcmButton, SettingsButton, AppLauncherButton, ShutdownButton };
            _powerMenuItems = new List<Button> { ShutdownMenuItem, RestartMenuItem, SleepMenuItem , LogOffMenuItem };
            // Catch unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.UnhandledException += CurrentApp_UnhandledException;
            this.Activated += MainWindow_Activated;
            this.Activated += (s, e) => this.Content.Focus(FocusState.Programmatic);

            string startart = AppSettings.Load<string>("launcher");
           
            LoadShortcutsFromSettings();
            SetupGamepad();
            SetupStatusTimer();
            Start();
            //ASYNC PROZES
            ShowTaskManager();
            SetupFocusWatcher();
            SetupMouseIdleBehavior();
            // NEU: Starte einen einmaligen Timer, der die Gnadenfrist beendet.
            var gracePeriodTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10) 
            };
            gracePeriodTimer.Tick += (s, e) =>
            {
                _isStartupGracePeriod = false; // Gnadenfrist beenden
                gracePeriodTimer.Stop();
            };
            gracePeriodTimer.Start();

            //after 10 seconds AND Start Windows Partmode
            StartAsynctasks();

            _appStartTime = DateTime.UtcNow;
            SetupWindowEngine();

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
                    AppGridView.SelectedIndex = 0;
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
                            // Check and hide 4 times per second.x
                            await Task.Delay(50, _taskbarHiderCts.Token);
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
                            Console.WriteLine($"Error attempting to kill : {ex.Message}");
                        }
                    }
                }
            }
        }
        private void BackToWindows()
        {
            TaskbarManager.RestoreOriginalState();

            MakeSelfNonTopmost();
            TaskManagerReEnableServices();
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
                    System.Threading.Thread.Sleep(500);
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
            //Stop Deckyloader
            KillProcess("PluginLoader_noconsole.exe");
            Console.WriteLine("PluginLoader_noconsole killed");
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
                // ### WICHTIG: Prüfe wieder, ob das Fenster überhaupt sichtbar ist. ###
                if (!IsWindowVisible(hWnd))
                    return true; // Weitersuchen

                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == 0) return true;

                try
                {
                    Process p = Process.GetProcessById((int)pid);
                    if (!p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { return true; }

                StringBuilder classNameBuilder = new StringBuilder(256);
                GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                string className = classNameBuilder.ToString();

                if (className.Equals("CUIEngineWin32", StringComparison.OrdinalIgnoreCase))
                {
                    steamHwnd = hWnd;
                    Debug.WriteLine($"[GCM] Zuverlässiges, sichtbares Steam BP Fenster gefunden (Handle: {hWnd})");
                    return false; // Suche beenden
                }

                return true;
            }, IntPtr.Zero);

            if (steamHwnd == IntPtr.Zero)
            {
                Debug.WriteLine("[GCM] Konnte kein laufendes und sichtbares Steam Big Picture Fenster finden.");
            }

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
        /// </summary>
        /// 
        private async Task ForcefullyBringToForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            // --- Step 1: Reliably bring the window to the front ---
            // (This remains the most stable way to gain focus)
            IntPtr foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd != hWnd)
            {
                uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
                uint ourThreadId = GetCurrentThreadId();
                AttachThreadInput(ourThreadId, foregroundThreadId, true);
                SetForegroundWindow(hWnd);
                AttachThreadInput(ourThreadId, foregroundThreadId, false);
            }

            // Ensure the window is visible before we manipulate it
            if (IsIconic(hWnd)) { ShowWindow(hWnd, SW_RESTORE); }
            else { ShowWindow(hWnd, 5); } // SW_SHOW

            string launcher = AppSettings.Load<string>("launcher");
            if (launcher == "playnite")
            {
                // Give the window a moment to become active
                await Task.Delay(250);

                // --- Step 2: Aggressively force true borderless fullscreen ---
                try
                {
                    Debug.WriteLine($"[GCM] Forcing true borderless fullscreen for Playnite (Handle: {hWnd}).");

                    // Get the current window style
                    long style = (long)GetWindowLongPtr(hWnd, GWL_STYLE);

                    // Remove all border, caption, and menu styles
                    style &= ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);

                    // Apply the new, "naked" style
                    SetWindowLongPtr(hWnd, GWL_STYLE, (IntPtr)style);

                    // Get the absolute screen resolution
                    int screenWidth = GetScreenWidth();
                    int screenHeight = GetScreenHeight();

                    // Resize and position the now-borderless window to cover the entire screen
                    SetWindowPos(hWnd, HWND_TOP, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GCM] Failed to apply borderless style: {ex.Message}");
                }

                // Give the UI a moment to settle after resizing
                await Task.Delay(300);

                // --- Step 3: The physical focus correction (Click THEN Escape) ---
                try
                {
                    // Make the cursor invisible
                    while (ShowCursor(false) >= 0) ;
                    await Task.Delay(32);

                    // 3.1: CLICK FIRST (Top Center)
                    int screenWidth = GetScreenWidth();
                    int screenHeight = GetScreenHeight();
                    int dpiY = GetDpiY();
                    int offsetY = (int)((0.5 / 2.54) * dpiY);
                    int clickX = screenWidth / 2;
                    int clickY = offsetY;

                    SetCursorPos(clickX, clickY);
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    await Task.Delay(50);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                    // 3.2: ESCAPE SECOND
                    await Task.Delay(50);
                    keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                    keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                    // 3.3: Move cursor away
                    SetCursorPos(screenWidth - 1, screenHeight - 1);
                    Debug.WriteLine($"[GCM] Playnite focus correction: Clicked at ({clickX},{clickY}), sent Escape, and hid cursor.");
                }
                finally
                {
                    // No matter what happens, make the cursor visible again
                    while (ShowCursor(true) < 0) ;
                }
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
                        //Steam in foreground
                        Debug.WriteLine("[GCM] Nutze Steam-Protokoll für den Wechsel: steam://open/gamepadui");
                        Process.Start(new ProcessStartInfo("steam://open/gamepadui")
                        {
                            UseShellExecute = true
                        });
                        return; // Fertig.

                    case "playnite":
                        // Starte Playnite oder bringe es in den Vordergrund
                        // via Protokoll-Befehl (URI).
                        Debug.WriteLine("[GCM] Starte/Wiederherstelle Playnite via Protokoll: playnite://playnite/restore");
                        try
                        {
                            Process.Start(new ProcessStartInfo("playnite://playnite/restore")
                            {
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            string processNameToFind = "Playnite.FullscreenApp";
                            Process proc = Process.GetProcessesByName(processNameToFind).FirstOrDefault();
                            if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
                            {
                                await ForcefullyBringToForeground(proc.MainWindowHandle);
                            }

                            else
                            {
                                await StartPlaynite();
                            }
                            return;
                        }
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
                        // over xbox protokoll
                        Process.Start(new ProcessStartInfo("xbox:") { UseShellExecute = true });
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

                            System.Threading.Thread.Sleep(5000);
                        
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
                // Schritt 1: Pfad prüfen, um sicherzustellen, dass Steam installiert ist.
                string steamPath = AppSettings.Load<string>("steamlauncherpath");
                if (string.IsNullOrWhiteSpace(steamPath) || !File.Exists(steamPath))
                {
                    throw new FileNotFoundException("The Steam path is invalid or was not found.");
                }

                // Schritt 2: Alle laufenden Steam-Prozesse beenden für einen sauberen Start.
                // Das ist der von dir gewünschte "exit vorher".
                Debug.WriteLine("[GCM] Beende alle laufenden Steam-Prozesse für einen sauberen Neustart...");
                KillProcess("steam.exe");

                // Eine sehr kurze Pause, damit die Prozesse sich vollständig beenden können.
                await Task.Delay(500);

                // Schritt 3: Decky Loader Logik (bleibt unverändert).
                bool useDeckyLoader = false;
                try { useDeckyLoader = AppSettings.Load<bool>("usedeckyloader"); } catch { /* ignore */ }

                if (useDeckyLoader)
                {
                    Console.WriteLine("Decky Loader is enabled. Attempting to start...");
                    KillProcess("PluginLoader_noconsole.exe");

                    string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string pluginLoaderPath = Path.Combine(userHome, "homebrew", "services", "PluginLoader_noconsole.exe");

                    if (File.Exists(pluginLoaderPath))
                    {
                        Process.Start(new ProcessStartInfo(pluginLoaderPath) { UseShellExecute = true });
                        Console.WriteLine("PluginLoader_noconsole.exe started.");
                        await Task.Delay(2000);
                    }
                    else
                    {
                        Console.WriteLine("Decky Loader executable not found, starting Steam normally.");
                    }
                }
              
                string steamExePath = AppSettings.Load<string>("steamlauncherpath");
                
                // Wir verwenden jetzt auch hier den zuverlässigen Protokoll-Befehl.    
                Debug.WriteLine("[GCM] Starte Steam via Protokoll-Befehl: steam://open/gamepadui");
                try
                {
                    Process.Start(new ProcessStartInfo(steamExePath)
                    {
                        Arguments = "-gamepadui",
                        UseShellExecute = true
                    });
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // Fängt Fehler ab, z.B. wenn die steam.exe unter dem Pfad nicht gefunden wurde
                    Debug.WriteLine($"[GCM] Fehler beim Starten der Steam.exe: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StartSteam: {ex.Message}");
                await messagebox("Could not start Steam. Please check the path in the settings.");
                BackToWindows();
            }
        }

        private async Task StartPlaynite()
        {
            try
            {
                // Load the configured path for the Playnite executable from settings.
                string playnitePath = AppSettings.Load<string>("playnitelauncherpath");

                // Validate the path.
                if (string.IsNullOrWhiteSpace(playnitePath) || !File.Exists(playnitePath))
                {
                    // Throw an exception if the path is invalid or the file doesn't exist.
                    throw new FileNotFoundException("The Playnite path is invalid or was not found.");
                }

                // Start Playnite in fullscreen mode and hide the splash screen.
                Process.Start(new ProcessStartInfo(playnitePath)
                {
                    Arguments = "--startfullscreen --hidesplashscreen", // Arguments to launch directly into fullscreen
                    UseShellExecute = true // UseShellExecute allows Windows to handle the process start (recommended for .exe)
                });
                Debug.WriteLine("[GCM] Playnite started with --startfullscreen --hidesplashscreen.");
            }
            catch (Exception ex)
            {
                // Log any error that occurs during the process.
                Debug.WriteLine($"Error in StartPlaynite: {ex.Message}");
                // Show an error message to the user.
                await messagebox("Could not start Playnite. Please check the path in the settings.");
                // Attempt to return the user to a usable desktop state if Playnite fails to start.
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
            KeyboardRedirector.EnableRedirect();
            await StartOverlayAsync();
            await Task.Run(() => RunBoilrNoUI());
            displayfusion("start");
            IsJoyxoffInstalledAndStart();
            EnsureTouchKeyboardServiceIsRunning();
            SetupWingamepadTask();
            await Showwinpartandlauncher();
            cssloader();
            ConsoleModeToShell();
            preaudio(true, false);
            prestartlist();
            await StartLosslessScaling();
            SwitchToConfiguredLauncher();
            await Task.Delay(500); // Gibt dem Launcher kurz Zeit zu starten
            AnimateOverlayOpacity(FocusLossOverlay, 0.0, true);
            _isOverlayActive = false;
        }
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

        private async Task RefreshCardsUIAsync()
        {
            // Fetch the latest process list in a background thread.
            var processDataList = await Task.Run(() =>
            {
                var dataList = new List<ProcessData>();
                var seenHwnds = new HashSet<IntPtr>();

                EnumWindows((hWnd, lParam) =>
                {
                    // This is the same reliable process-finding logic as before.
                    if (!IsWindowVisible(hWnd) || GetWindow(hWnd, (uint)GetWindowCmd.GW_OWNER) != IntPtr.Zero)
                        return true;
                    int textLen = GetWindowTextLength(hWnd);
                    if (textLen == 0) return true;
                    var style = (WindowStylesEx)GetWindowLong(hWnd, WindowLongFlags.GWL_EXSTYLE);
                    if (style.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW)) return true;
                    if (IsCloaked(hWnd)) return true;

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
                    catch { /* Ignore */ }

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

            // Now, update the UI with this fresh data.
            UpdateUiFromData(processDataList);
        }

        private void UpdateLayoutForFocus()
        {
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
                _ = RefreshCardsUIAsync();

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
            // First, initialize the list for gamepad navigation.
            _launcherAreaButtons = new List<Border>();

            // 1. Get the configured launcher from settings to determine the main icon.
            string launcher = AppSettings.Load<string>("launcher");
            string mainLauncherIconPath = null;

            // 2. Select the correct icon path based on the setting.
            switch (launcher)
            {
                case "steam":
                    mainLauncherIconPath = "ms-appx:///Assets/steam_logo.png";
                    break;
                case "playnite":
                    mainLauncherIconPath = "ms-appx:///Assets/playnite_logo.png";
                    break;
                case "xbox":
                    mainLauncherIconPath = "ms-appx:///Assets/xbox_logo.png";
                    break;

                default:
                    mainLauncherIconPath = "ms-appx:///Assets/ownlauncher.png";
                    break;
            }

            // 3. Create the main launcher card item WITH the icon path.
            var mainLauncherItem = new LauncherCardItem
            {
                Name = "Main Launcher",
                ImagePath = mainLauncherIconPath, // This will be null for the default icon.
                TapAction = (s, e) => SwitchToConfiguredLauncher()
            };
            var mainLauncherCard = CreateLauncherCard(mainLauncherItem);
            LauncherAreaPanel.Children.Add(mainLauncherCard);
            _launcherAreaButtons.Add(mainLauncherCard);

            // 4. Create and add the Discord card.
            var discordItem = new LauncherCardItem
            {
                Name = "Discord",
                ImagePath = "ms-appx:///Assets/discord.png",
                TapAction = (s, e) =>
                {
                    MakeSelfNonTopmost();
                    StartDiscord();
                    PlayActivationSound();
                }
            };
            var discordCard = CreateLauncherCard(discordItem);
            LauncherAreaPanel.Children.Add(discordCard);
            _launcherAreaButtons.Add(discordCard);

            // 5. Load and add the 5 custom app slots from settings.toml.
            for (int i = 1; i <= 5; i++)
            {
                try
                {
                    string exePath = AppSettings.Load<string>($"button{i}link");
                    string imagePath = AppSettings.Load<string>($"button{i}image");
                    string args = AppSettings.Load<string>($"button{i}args") ?? "";

                    // Only add if EXE and image are valid and exist.
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath) &&
                        !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        var customItem = new LauncherCardItem
                        {
                            Name = $"Custom App {i}",
                            ImagePath = imagePath,
                            ExePath = exePath,
                            Arguments = args,
                            TapAction = (s, e) =>
                            {
                                MakeSelfNonTopmost();
                                try
                                {
                                    Process.Start(new ProcessStartInfo(exePath)
                                    {
                                        Arguments = args,
                                        UseShellExecute = true
                                    });
                                    PlayActivationSound();
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[ERROR] Failed to start custom app {i}: {ex.Message}");
                                }
                            }
                        };
                        var customCard = CreateLauncherCard(customItem);
                        LauncherAreaPanel.Children.Add(customCard);
                        _launcherAreaButtons.Add(customCard);
                    }
                }
                catch (Exception)
                {
                    // Setting not found, just ignore it and continue with the next slot.
                }
            }
        }



        /// <summary>
        /// Creates a single, clickable launcher card based on the provided data.
        /// Now handles a custom image for the Main Launcher card with a "LAUNCHER" label.
        /// </summary>
        private Border CreateLauncherCard(LauncherCardItem item)
        {
            double cardWidth = (item.Name == "Main Launcher") ? 300 : 150;
            double cardHeight = (item.Name == "Main Launcher") ? 250 : 180;
            UIElement cardContent;

            if (item.Name == "Main Launcher")
            {
                // If an ImagePath is provided, create a StackPanel with the Image and a TextBlock.
                if (!string.IsNullOrEmpty(item.ImagePath))
                {
                    cardContent = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 15,
                        Children = {
                    new Image
                    {
                        Source = new BitmapImage(new Uri(item.ImagePath)),
                        Width = 128,
                        Height = 128,
                        Stretch = Stretch.Uniform
                    },
                    new TextBlock
                    {
                        Text = "LAUNCHER",
                        FontSize = 22,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
                    };
                }
                else // Otherwise, fall back to the default home icon and text.
                {
                    cardContent = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 15,
                        Children = {
                    new FontIcon { Glyph = "\uE80F", FontSize = 48, Foreground = new SolidColorBrush(Colors.White) },
                    new TextBlock { Text = "Launcher", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) }
                }
                    };
                }
            }
            else // For all other cards (Discord, custom apps)
            {
                cardContent = new Image
                {
                    Source = new BitmapImage(new Uri(item.ImagePath)),
                    Width = 80,
                    Height = 80,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            // This part of the method remains exactly the same
            var cardBorder = new Border
            {
                Width = cardWidth,
                Height = cardHeight,
                Background = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)), // #33FFFFFF
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                Child = cardContent,
                Tag = item
            };

            if (item.TapAction != null)
            {
                cardBorder.Tapped += (s, e) => item.TapAction(s, e);
            }

            return cardBorder;
        }

        private void StartAutoTaskRefresh()
        {
            if (_taskRefreshTimer != null) return;
            _taskRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _taskRefreshTimer.Tick += async (s, e) =>
            {
                // This timer now ONLY gathers data in the background. It does not touch the UI.
                _latestProcessData = await Task.Run(() =>
                {
                    var dataList = new List<ProcessData>();
                    var seenHwnds = new HashSet<IntPtr>();

                    EnumWindows((hWnd, lParam) =>
                    {
                        // This entire EnumWindows logic remains unchanged.
                        if (!IsWindowVisible(hWnd) || GetWindow(hWnd, (uint)GetWindowCmd.GW_OWNER) != IntPtr.Zero)
                            return true;
                        int textLen = GetWindowTextLength(hWnd);
                        if (textLen == 0) return true;
                        var style = (WindowStylesEx)GetWindowLong(hWnd, WindowLongFlags.GWL_EXSTYLE);
                        if (style.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW)) return true;
                        if (IsCloaked(hWnd)) return true;

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
                        catch { /* Ignore processes we can't access */ }

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

                // The call to UpdateUiFromData(processDataList) has been REMOVED from the timer.
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
        private async void LoadCardImageAsync(
            Border card,                // The main card
            Image loadedImageControl,   // The (still invisible) image to fade in
            Image defaultIconControl,   // The (visible) default icon
            TextBlock titleControl,     // The title field
            string gameName,
            string exePath)
        {
            Logger.Log($"[DEBUG] LoadCardImageAsync started.\n    Game: {gameName}\n    Path: {(string.IsNullOrEmpty(exePath) ? "NONE (e.g., Anti-Cheat)" : exePath)}");

            // 1. Safety Check (Non-game keywords)
            if (_nonGameKeywords.Any(keyword => gameName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Log($"[DEBUG] LoadCardImageAsync: Game '{gameName}' was classified as 'Non-Game' and skipped.");
                return;
            }

            string localImagePath = null;
            string appId = null;

            // 2. Check if we have a valid path
            if (!string.IsNullOrEmpty(exePath))
            {
                Logger.Log("[DEBUG] exePath is valid. Attempting Prio 1-3 (Local Folder + AppID from Path)...");
                localImagePath = await FindLocalSteamImageAsync(exePath);
            }
            else
            {
                // This is the Anti-Cheat case (exePath is null)
                Logger.Log("[DEBUG] exePath is null. Skipping Prio 1-3. Attempting Prio 4 (Find AppID by Name)...");
                appId = await FindAppIdFromGameNameLocally(gameName);

                if (!string.IsNullOrEmpty(appId))
                {
                    string steamInstallPath = GetSteamInstallPath();
                    if (!string.IsNullOrEmpty(steamInstallPath))
                    {
                        string appCacheDirectory = Path.Combine(steamInstallPath, "appcache", "librarycache", appId);
                        Logger.Log($"[DEBUG] Prio 4: Found AppID '{appId}'. Checking cache path: {appCacheDirectory}");

                        localImagePath = await SearchCacheFolderForImageAsync(appCacheDirectory);
                    }
                }
            }

            Logger.Log($"[DEBUG] LoadCardImageAsync: Local search finished. Found path: {(string.IsNullOrEmpty(localImagePath) ? "NONE" : localImagePath)}");

            // 5. Load local image IF ONE WAS FOUND (from either Prio 1-3 or Prio 4)
            if (!string.IsNullOrEmpty(localImagePath))
            {
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

                        // ### START OF FLICKER FIX ###
                        // Set the source for the (still invisible) image
                        loadedImageControl.Source = bitmap;

                        // Start the cross-fade animation
                        AnimateCrossFade(loadedImageControl, defaultIconControl);
                        // ### END OF FLICKER FIX ###

                        Logger.Log($"[DEBUG] LoadCardImageAsync: Local image for '{gameName}' loaded successfully and fade-in started.");
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[DEBUG] LoadCardImageAsync: ERROR loading local image '{localImagePath}': {ex.Message}\n    Attempting fallback to SteamGridDB...");
                        if (_steamGridHelper.IsApiKeySet)
                        {
                            // Pass the controls to the fallback
                            await LoadFromSteamGridDbAsync(card, loadedImageControl, defaultIconControl, titleControl, gameName, exePath);
                        }
                    }
                });
                return; // IMPORTANT: Exit here
            }

            // 6. Fallback: SteamGridDB
            if (_steamGridHelper.IsApiKeySet)
            {
                Logger.Log("[DEBUG] LoadCardImageAsync: No local image found. Starting SteamGridDB search (using game name)...");
                await LoadFromSteamGridDbAsync(card, loadedImageControl, defaultIconControl, titleControl, gameName, exePath);
            }
            else
            {
                Logger.Log("[DEBUG] LoadCardImageAsync: No local image found. SteamGridDB is disabled. Using default icon.");
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
    "Big Picture Mode",
    "Playnite",// NEW
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
            // This part remains unchanged
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
                catch { /* Ignore */ }
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

            // ### START OF FLICKER FIX ###
            var contentGrid = new Grid();

            // 1. The Image control that will hold the loaded (poster) image.
            // It starts invisible (Opacity=0) and stretches to fill the card.
            var loadedImage = new Image
            {
                Name = "LoadedImage",
                Stretch = Stretch.UniformToFill, // Fills the card
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = 0.0 // Starts invisible
            };
            contentGrid.Children.Add(loadedImage); // Add first (bottom layer)

            // 2. The default icon (game.png or exe icon)
            // This is visible by default (Opacity=1) and sits on top.
            var iconImage = new Image
            {
                Name = "IconImage",
                Source = GetAppIconAsBitmapImage(exePath) ?? new BitmapImage(new Uri("ms-appx:///Assets/game.png")),
                Width = 64,
                Height = 64,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            contentGrid.Children.Add(iconImage); // Add second (top layer)

            // 3. Text elements (unchanged)
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
                        new GradientStop { Color = Color.FromArgb(0, 0, 0, 0), Offset = 0.0 },
                        new GradientStop { Color = Color.FromArgb(180, 0, 0, 0), Offset = 1.0 }
                    }
                }
            };
            var titleText = new TextBlock
            {
                Name = "TitleText",
                Text = name,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 16,
                Margin = new Thickness(10, 0, 10, 10),
                VerticalAlignment = VerticalAlignment.Bottom,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxLines = 2
            };
            contentGrid.Children.Add(textBackground);
            contentGrid.Children.Add(titleText);

            var cardBorder = new Border
            {
                Width = 220,
                Height = 260,
                Background = gradient, // The gradient is the permanent background
                CornerRadius = new CornerRadius(15),
                Margin = new Thickness(10),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Tag = new CardTag { Process = proc, Hwnd = hwnd },
                Child = contentGrid
            };

            // 4. Call the load method, passing BOTH Image controls
            LoadCardImageAsync(cardBorder, loadedImage, iconImage, titleText, name, exePath);
            // ### END OF FLICKER FIX ###

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

        public void BringTaskManagerToFrontAndFocus()
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                string launcher = AppSettings.Load<string>("launcher");
                IntPtr windowHandle = IntPtr.Zero;

                try
                {
                    switch (launcher)
                    {
                        // STEAM LOGIC: Force self (GCM) on top
                        case "steam":
                            windowHandle = FindSteamBigPictureWindow();
                            if (windowHandle != IntPtr.Zero)
                            {
                                await Task.Delay(100);

                                // Bring self (GCM) to front
                                IntPtr _selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                                BringToFrontAndFocus(_selfHwnd);
                            }
                            break;

                        // PLAYNITE / CUSTOM LOGIC: Minimize the launcher
                        case "playnite":
                        case "custom":
                            string processNameToFind = launcher == "playnite"
                                ? "Playnite.FullscreenApp"
                                : Path.GetFileNameWithoutExtension(AppSettings.Load<string>("customlauncherpath"));

                            if (!string.IsNullOrEmpty(processNameToFind))
                            {
                                Process proc = Process.GetProcessesByName(processNameToFind).FirstOrDefault();
                                if (proc != null)
                                {
                                    windowHandle = proc.MainWindowHandle;
                                    if (windowHandle != IntPtr.Zero)
                                    {
                                        await Task.Delay(100);
                                        ShowWindow(windowHandle, SW_MINIMIZE);
                                        Debug.WriteLine($"[GCM] {launcher} window ({windowHandle}) minimized.");
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GCM] Error while pushing launcher to background: {ex.Message}");
                }

                // FINAL FOCUS CALL:
                // This runs *after* the switch-case to ensure GCM is on top,
                // using the new aggressive method.
                await Task.Delay(150);
                IntPtr selfHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                BringToFrontAndFocus(selfHwnd);
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
        #region gamebarkeyboard

        #endregion gamebarkeyboard
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

        /// <summary>
        /// Handles all controller-as-a-mouse logic, including held clicks and right-stick scrolling.
        /// </summary>
       
        private void HandleMouseControl(State controllerState)
        {
            var gamepad = controllerState.Gamepad;
            var currentButtons = gamepad.Buttons;

            // --- Smooth Cursor Movement (Left Stick) ---
            const int deadzone = 4000;
            float thumbX = gamepad.LeftThumbX;
            float thumbY = gamepad.LeftThumbY;

            if (Math.Abs(thumbX) > deadzone || Math.Abs(thumbY) > deadzone)
            {
                GetCursorPos(out POINT currentPos);
                float speedMultiplier = 45.0f; // This is the high speed you wanted.

                float moveX = (thumbX / 32767.0f) * speedMultiplier;
                float moveY = (thumbY / 32767.0f) * speedMultiplier;

                moveX += _cursorXRemainder;
                moveY += _cursorYRemainder;

                int pixelsToMoveX = (int)moveX;
                int pixelsToMoveY = (int)moveY;

                int newX = currentPos.X + pixelsToMoveX;
                int newY = currentPos.Y - pixelsToMoveY; // Y is inverted for screen coordinates.
                SetCursorPos(newX, newY);

                _cursorXRemainder = moveX - pixelsToMoveX;
                _cursorYRemainder = moveY - pixelsToMoveY;
            }
            else
            {
                _cursorXRemainder = 0f;
                _cursorYRemainder = 0f;
            }

            // ### SCROLLING LOGIC RE-ADDED HERE (Right Stick) ###
            float thumbRy = gamepad.RightThumbY;
            // Check if enough time has passed since the last scroll to ensure a smooth speed.
            if ((DateTime.UtcNow - _lastScrollTime).TotalMilliseconds > 50)
            {
                // Scroll Down
                if (thumbRy < -deadzone)
                {
                    // The 'dwData' parameter controls the wheel. -120 is one "tick" down.
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)-120), UIntPtr.Zero);
                    _lastScrollTime = DateTime.UtcNow; // Reset the timer
                }
                // Scroll Up
                else if (thumbRy > deadzone)
                {
                    // 120 is one "tick" up.
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120, UIntPtr.Zero);
                    _lastScrollTime = DateTime.UtcNow; // Reset the timer
                }
            }

            // --- Button Actions ---
            var newPresses = currentButtons & ~_lastButtonState;

            // Left Click (A button) - Supports holding
            if ((newPresses & GamepadButtonFlags.A) != 0) { mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero); }
            else if (((_lastButtonState & GamepadButtonFlags.A) != 0) && ((currentButtons & GamepadButtonFlags.A) == 0)) { mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero); }

            // Right Click (X button)
            if ((newPresses & GamepadButtonFlags.X) != 0)
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            }

            // On-Screen Keyboard Toggle (Y button)
            if ((newPresses & GamepadButtonFlags.Y) != 0)
            {
                SendOverlayNotification("Opening Keyboard");
                ToggleTouchKeyboard();
            }
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
            // Start the dedicated input loop on a background thread.
            Task.Run(() => GamepadInputLoop());
        }
      
      
        
        private async Task GamepadInputLoop()
        {
            // Define the deadzone for the analog stick
            const int deadzone = 12000; // Adjust as needed

            while (true) // This loop runs continuously in the background.
            {
                if (_xinputController == null || !_xinputController.IsConnected)
                {
                    _xinputController = GetConnectedController();
                    _controllerConnected = _xinputController != null;
                }

                if (_controllerConnected)
                {
                    var state = _xinputController.GetState();
                    var gamepadState = state.Gamepad; // More convenient alias
                    var currentButtons = gamepadState.Buttons;

                    // Get Left Thumbstick values
                    var thumbX = gamepadState.LeftThumbX;
                    var thumbY = gamepadState.LeftThumbY; // *** Get Y-axis value ***

                    // ### Mouse Mode Toggle Logic (remains the same) ###
                    bool backPressed = (currentButtons & GamepadButtonFlags.Back) != 0;
                    bool rsPressed = (currentButtons & GamepadButtonFlags.RightThumb) != 0;
                    bool comboIsPressed = backPressed && rsPressed;
                    bool lastBackPressed = (_lastButtonState & GamepadButtonFlags.Back) != 0;
                    bool lastRsPressed = (_lastButtonState & GamepadButtonFlags.RightThumb) != 0;
                    bool comboWasPressed = lastBackPressed && lastRsPressed;
                    // ... (rest of the mouse mode toggle logic is unchanged) ...
                    if (comboIsPressed && !comboWasPressed) { _comboPressTime = DateTime.UtcNow; _mouseModeToggledThisPress = false; }
                    if (comboIsPressed && _comboPressTime.HasValue && !_mouseModeToggledThisPress) { if ((DateTime.UtcNow - _comboPressTime.Value).TotalSeconds >= 2.0) { _isMouseModeActive = !_isMouseModeActive; DispatcherQueue.TryEnqueue(() => SendOverlayNotification(_isMouseModeActive ? "Mouse Mode Activated" : "Mouse Mode Deactivated")); _mouseModeToggledThisPress = true; /* Optional keyboard hide */ } }
                    if (!comboIsPressed && comboWasPressed) { _comboPressTime = null; }


                    // --- Main Input Logic Branch ---
                    if (_isMouseModeActive)
                    {
                        HandleMouseControl(state);
                        _lastButtonState = currentButtons;
                        _lastStickXDirection = 0; // Reset stick directions when entering mouse mode
                        _lastStickYDirection = 0;
                    }
                    else // --- UI Navigation and Shortcut Mode ---
                    {
                        HandleShortcuts(currentButtons); // Handle global shortcuts first

                        // --- NEW: Left Stick Navigation Logic (X and Y axis) ---
                        int currentStickXDirection = 0;
                        int currentStickYDirection = 0; // *** Add Y direction ***
                        bool stickMovedLeft = false;
                        bool stickMovedRight = false;
                        bool stickMovedUp = false;    // *** Add Up flag ***
                        bool stickMovedDown = false;  // *** Add Down flag ***

                        // Determine current stick X direction
                        if (thumbX < -deadzone) { currentStickXDirection = -1; } // Left
                        else if (thumbX > deadzone) { currentStickXDirection = 1; } // Right
                        else { currentStickXDirection = 0; } // Center

                        // Determine current stick Y direction (Inverted: Positive Y is UP on stick)
                        if (thumbY > deadzone) { currentStickYDirection = 1; } // Up
                        else if (thumbY < -deadzone) { currentStickYDirection = -1; } // Down
                        else { currentStickYDirection = 0; } // Center

                        // Check for horizontal movement changes
                        if (currentStickXDirection == -1 && _lastStickXDirection != -1) { stickMovedLeft = true; }
                        else if (currentStickXDirection == 1 && _lastStickXDirection != 1) { stickMovedRight = true; }

                        // Check for vertical movement changes
                        if (currentStickYDirection == 1 && _lastStickYDirection != 1) { stickMovedUp = true; }
                        else if (currentStickYDirection == -1 && _lastStickYDirection != -1) { stickMovedDown = true; }

                        // Update the last stick direction states *after* checking
                        _lastStickXDirection = currentStickXDirection;
                        _lastStickYDirection = currentStickYDirection; // *** Update Y state ***
                                                                       // --- End of Left Stick Navigation Logic ---


                        // Only handle UI navigation if the window is in the foreground
                        if (IsWindowInForeground())
                        {
                            // Dispatch UI handling, passing ALL stick states
                            DispatcherQueue.TryEnqueue(() => HandleGamepadInput(currentButtons, stickMovedLeft, stickMovedRight, stickMovedUp, stickMovedDown));
                            // _lastButtonState is updated on UI thread
                        }
                        else
                        {
                            _lastButtonState = currentButtons; // Update button state if not focused
                            _lastStickXDirection = 0; // Reset stick if focus lost
                            _lastStickYDirection = 0;
                        }
                    }
                }
                else // Controller disconnected
                {
                    if (_isMouseModeActive) { _isMouseModeActive = false; DispatcherQueue.TryEnqueue(() => SendOverlayNotification("Mouse Mode Deactivated (Controller Disconnected)")); }
                    // if (_isKeyboardVisible) { /* DispatcherQueue.TryEnqueue(() => HideOnScreenKeyboard()); */ } // Optional
                    _lastButtonState = GamepadButtonFlags.None;
                    _lastStickXDirection = 0; // Reset stick directions
                    _lastStickYDirection = 0;
                }

                await Task.Delay(16); // ~60 Hz
            }
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
                _shortcutActions["xbox keyboard"] = ToggleTouchKeyboard;
            }
            catch (Exception ex)
            {
                LogGamepadInit($"[ERROR] Failed to load shortcuts from TOML: {ex.Message}");
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

        /// <summary>
        /// Die zentrale Methode, die alle Gamepad-Eingaben für Shortcuts und UI-Navigation verarbeitet.
        /// </summary>
        private void HandleGamepadInput(GamepadButtonFlags currentButtons, bool stickMovedLeft, bool stickMovedRight, bool stickMovedUp, bool stickMovedDown)
        {
            // Safety check: Do nothing if the window is not in focus.
            if (!IsWindowInForeground())
            {
                _lastButtonState = currentButtons; // Still update the state to prevent false presses on refocus.
                return;
            }

            // Calculate which buttons are newly pressed in this frame.
            var newPresses = currentButtons & ~_lastButtonState;

            bool navigated = false; // Wird auf true gesetzt, wenn ein Area-Wechsel stattfindet

            // --- Contextual Navigation (D-Pad, Left Stick, A, B) ---
            switch (_currentFocusArea)
            {
                // --- 1. TopButtons Area ---
                case FocusArea.TopButtons:
                    if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown)
                    {
                        // NACH UNTEN: Gehe zurück zur vorherigen Area (Cards oder Launcher)
                        _currentFocusArea = _previousFocusArea;
                        _previousTopButtonIndex = _selectedTopButtonIndex; // Merke dir die Position

                        // Stelle den Index der Ziel-Area wieder her
                        if (_currentFocusArea == FocusArea.Cards)
                            _selectedCardIndex = _previousCardIndex != -1 ? _previousCardIndex : 0;
                        else if (_currentFocusArea == FocusArea.Launcher)
                            _selectedLauncherAreaIndex = _previousLauncherAreaIndex != -1 ? _previousLauncherAreaIndex : 0;

                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) && _topButtons.Any())
                    {
                        // INNERHALB der Area nach rechts navigieren
                        _selectedTopButtonIndex = (_selectedTopButtonIndex + 1) % _topButtons.Count;
                        UpdateVisualFocus();
                        PlayNavigationSound();
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft) && _topButtons.Any())
                    {
                        // INNERHALB der Area nach links navigieren
                        _selectedTopButtonIndex = (_selectedTopButtonIndex - 1 + _topButtons.Count) % _topButtons.Count;
                        UpdateVisualFocus();
                        PlayNavigationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        ClickSelectedTopButton();
                        PlayActivationSound();
                    }
                    break;

                // --- 2. Cards Area (Hauptbereich) ---
                case FocusArea.Cards:
                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        // NACH OBEN: Gehe zu den TopButtons
                        _previousFocusArea = FocusArea.Cards; // Merke dir, woher wir kommen
                        _previousCardIndex = _selectedCardIndex; // Merke dir die Position
                        _currentFocusArea = FocusArea.TopButtons;
                        _selectedTopButtonIndex = _previousTopButtonIndex != -1 ? _previousTopButtonIndex : 0;
                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) && ProgramCardPanel.Children.Any())
                    {
                        // INNERHALB der Area nach rechts navigieren
                        _selectedCardIndex = (_selectedCardIndex + 1) % ProgramCardPanel.Children.Count;
                        UpdateVisualFocus();
                        PlayNavigationSound();
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft) && ProgramCardPanel.Children.Any())
                    {
                        if (_selectedCardIndex == 0)
                        {
                            // JA: Springe zum Launcher
                            _previousFocusArea = FocusArea.Cards; // Merken für Hoch/Runter
                            _previousCardIndex = 0; // Merken für Rücksprung
                            _currentFocusArea = FocusArea.Launcher;
                            // Setze Fokus auf das LETZTE Launcher-Item
                            _selectedLauncherAreaIndex = _launcherAreaButtons.Any() ? _launcherAreaButtons.Count - 1 : 0;
                            navigated = true; // Signalisiert einen Area-Wechsel
                        }
                        else
                        {
                            // NEIN: Normale Navigation nach links
                            _selectedCardIndex = (_selectedCardIndex - 1 + ProgramCardPanel.Children.Count) % ProgramCardPanel.Children.Count;
                            UpdateVisualFocus();
                            PlayNavigationSound();
                        }
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        TriggerCardAction(_selectedCardIndex, true); // True for launch/focus
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        TriggerCardAction(_selectedCardIndex, false); // False for close/kill
                        PlaydeactivationSound();
                    }
                    break;

                // --- 3. Launcher Area (Linke Spalte, wenn aktiv) ---
                case FocusArea.Launcher:
                    if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp)
                    {
                        // NACH OBEN: Gehe zu den TopButtons
                        _previousFocusArea = FocusArea.Launcher; // Merke dir, woher wir kommen
                        _previousLauncherAreaIndex = _selectedLauncherAreaIndex; // Merke dir die Position
                        _currentFocusArea = FocusArea.TopButtons;
                        _selectedTopButtonIndex = _previousTopButtonIndex != -1 ? _previousTopButtonIndex : 0;
                        navigated = true;
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) && _launcherAreaButtons.Any())
                    {
                        // NEU: Wrap-Around Prüfung
                        if (_selectedLauncherAreaIndex == _launcherAreaButtons.Count - 1)
                        {
                            // JA: Springe zu den Cards
                            _previousFocusArea = FocusArea.Launcher; // Merken für Hoch/Runter
                            _previousLauncherAreaIndex = _selectedLauncherAreaIndex; // Merken für Rücksprung
                            _currentFocusArea = FocusArea.Cards;
                            // Setze Fokus auf das ERSTE Card-Item (oder das gemerkte)
                            _selectedCardIndex = _previousCardIndex != -1 ? _previousCardIndex : 0;
                            navigated = true; // Signalisiert einen Area-Wechsel
                        }
                        else
                        {
                            // NEIN: Normale Navigation nach rechts
                            _selectedLauncherAreaIndex = (_selectedLauncherAreaIndex + 1) % _launcherAreaButtons.Count;
                            UpdateVisualFocus();
                            PlayNavigationSound();
                        }
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft) && _launcherAreaButtons.Any())
                    {
                        // INNERHALB der Area nach links navigieren
                        _selectedLauncherAreaIndex = (_selectedLauncherAreaIndex - 1 + _launcherAreaButtons.Count) % _launcherAreaButtons.Count;
                        UpdateVisualFocus();
                        PlayNavigationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        if (_selectedLauncherAreaIndex >= 0 && _selectedLauncherAreaIndex < _launcherAreaButtons.Count && _launcherAreaButtons[_selectedLauncherAreaIndex].Tag is LauncherCardItem item)
                        {
                            item.TapAction?.Invoke(null, null); // Execute action
                        }
                        PlayActivationSound();
                    }
                    break;

                case FocusArea.PowerMenu:
                    if (((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown) && _powerMenuItems.Any())
                    {
                        _selectedPowerMenuItemIndex = (_selectedPowerMenuItemIndex + 1) % _powerMenuItems.Count;
                        UpdateVisualFocus(); PlayNavigationSound();
                    }
                    else if (((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp) && _powerMenuItems.Any())
                    {
                        _selectedPowerMenuItemIndex = (_selectedPowerMenuItemIndex - 1 + _powerMenuItems.Count) % _powerMenuItems.Count;
                        UpdateVisualFocus(); PlayNavigationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.A) != 0 && _powerMenuItems.Count > _selectedPowerMenuItemIndex)
                    {
                        var selectedButton = _powerMenuItems[_selectedPowerMenuItemIndex];
                        if (selectedButton == ShutdownMenuItem) ShutdownMenuItem_Click(selectedButton, new RoutedEventArgs());
                        else if (selectedButton == RestartMenuItem) RestartMenuItem_Click(selectedButton, new RoutedEventArgs());
                        else if (selectedButton == LogOffMenuItem) LogOffMenuItem_Click(selectedButton, new RoutedEventArgs());
                        else if (selectedButton == SleepMenuItem) SleepMenuItem_Click(selectedButton, new RoutedEventArgs());
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        PowerMenu.Visibility = Visibility.Collapsed;
                        _currentFocusArea = FocusArea.TopButtons; // Return focus
                        UpdateVisualFocus(); PlaydeactivationSound();
                    }
                    break;

                case FocusArea.AppLauncher:
                    if (AppGridView.Items.Count > 0)
                    {
                        int columns = 4; // Default guess
                        try
                        {
                            if (AppGridView.ActualWidth > 0 && AppGridView.ContainerFromIndex(0) is GridViewItem container && container.ActualWidth > 0)
                            {
                                columns = Math.Max(1, (int)Math.Floor(AppGridView.ActualWidth / container.ActualWidth));
                            }
                        }
                        catch { /* Ignore potential layout errors */ }

                        int currentIndex = AppGridView.SelectedIndex;
                        int newIndex = currentIndex;
                        bool appNavigated = false;

                        if ((newPresses & GamepadButtonFlags.DPadDown) != 0 || stickMovedDown) { newIndex = Math.Min(AppGridView.Items.Count - 1, currentIndex + columns); appNavigated = true; }
                        else if ((newPresses & GamepadButtonFlags.DPadUp) != 0 || stickMovedUp) { newIndex = Math.Max(0, currentIndex - columns); appNavigated = true; }
                        else if ((newPresses & GamepadButtonFlags.DPadRight) != 0 || stickMovedRight) { newIndex = (currentIndex + 1) % AppGridView.Items.Count; appNavigated = true; }
                        else if ((newPresses & GamepadButtonFlags.DPadLeft) != 0 || stickMovedLeft) { newIndex = (currentIndex - 1 + AppGridView.Items.Count) % AppGridView.Items.Count; appNavigated = true; }

                        if (appNavigated && newIndex != currentIndex)
                        {
                            AppGridView.SelectedIndex = newIndex;
                            AppGridView.ScrollIntoView(AppGridView.SelectedItem); // Ensure visible
                            PlayNavigationSound();
                        }
                    }

                    if ((newPresses & GamepadButtonFlags.A) != 0)
                    {
                        int selectedIndex = AppGridView.SelectedIndex;
                        if (selectedIndex >= 0 && selectedIndex < AppGridView.Items.Count)
                        {
                            if (AppGridView.Items[selectedIndex] is AppInfo app) { LaunchApp(app); }
                        }
                        PlayActivationSound();
                    }
                    else if ((newPresses & GamepadButtonFlags.B) != 0)
                    {
                        ToggleAppLauncher_Click(null, null); // Close
                    }
                    break;
            }

            // Wenn ein Bereichswechsel stattgefunden hat, UI aktualisieren
            if (navigated)
            {
                UpdateVisualFocus();
                PlayNavigationSound();
            }

            // This must be the VERY LAST line to correctly track the state for the next frame.
            _lastButtonState = currentButtons;
        }
        // Used to track the last direction the left / Right stick was pushed to prevent rapid navigation events
        private int _lastStickXDirection = 0; // -1 for left, 0 for center, 1 for right
        private int _lastStickYDirection = 0; // -1 for down, 0 for center, 1 for up // <-- Add this line
        private void PowerButton_Click(object sender, RoutedEventArgs e)
        {
            if (PowerMenu.Visibility == Visibility.Visible)
            {
                PowerMenu.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Positioniere das Menü direkt unter dem Power-Button
                var transform = ShutdownButton.TransformToVisual(RootGrid);
                var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

                // Passt die Position an, damit es rechtsbündig mit dem infopanel ist
                PowerMenu.Margin = new Thickness(
                    0,
                    position.Y + ShutdownButton.ActualHeight + 5, // Top
                    20, // Right (gleicher Abstand wie das infopanel)
                    0
                );

                PowerMenu.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Updates the UI performantly by only changing the old and new focused elements.
        /// </summary>
        

        private void UpdateVisualFocus(bool isInitial = false)
        {
            UpdateLayoutForFocus();

            // Reset all highlights (dieser Teil bleibt gleich)
            _launcherAreaButtons.ForEach(b => { AnimateScale(b, false); AnimateBorderColor(b, false); });
            _topButtons.ForEach(b => { b.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent); b.BorderThickness = new Thickness(0); });
            _powerMenuItems.ForEach(b => { b.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); });
            for (int i = 0; i < ProgramCardPanel.Children.Count; i++)
            {
                if (ProgramCardPanel.Children[i] is Border card) { AnimateScale(card, false); AnimateBorderColor(card, false); }
            }

            // Highlight the currently focused element
            if (_currentFocusArea == FocusArea.Launcher)
            {
                if (_launcherAreaButtons.Count > _selectedLauncherAreaIndex)
                {
                    var selectedButton = _launcherAreaButtons[_selectedLauncherAreaIndex];
                    AnimateScale(selectedButton, true);
                    AnimateBorderColor(selectedButton, true);
                }
            }
            else if (_currentFocusArea == FocusArea.TopButtons)
            {
                if (_topButtons.Count > _selectedTopButtonIndex)
                {
                    var selectedButton = _topButtons[_selectedTopButtonIndex];
                    selectedButton.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke);
                    selectedButton.BorderThickness = new Thickness(2);
                }
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
            else if (_currentFocusArea == FocusArea.PowerMenu)
            {
                if (_powerMenuItems.Count > _selectedPowerMenuItemIndex)
                {
                    var selectedButton = _powerMenuItems[_selectedPowerMenuItemIndex];
                    selectedButton.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                }
            }
            // ### HIER IST DIE VEREINFACHUNG ###
            // Der alte, komplizierte Code für AppLauncher wird komplett entfernt.
            // Wir müssen hier nichts mehr tun, da das XAML die Hervorhebung übernimmt.
            else if (_currentFocusArea == FocusArea.AppLauncher)
            {
                // Leer lassen! Die GridView kümmert sich dank des neuen Styles selbst darum.
            }
        }

        #endregion

        // ########## ENDE DES KOMPLETTEN CODE-BLOCKS ##########


        /// <summary>
        /// Animiert die Skalierung eines UI-Elements performant.
        /// </summary>
        private void AnimateScale(UIElement element, bool isSelected)
        {
            // Ensure a CompositeTransform exists, creating one if it doesn't.
            // This prevents conflicts with other animations.
            if (element.RenderTransform is not CompositeTransform)
            {
                element.RenderTransform = new CompositeTransform();
            }

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;
            var targetScale = isSelected ? new Vector3(1.05f, 1.05f, 1.0f) : new Vector3(1.0f, 1.0f, 1.0f);

            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(250);
            scaleAnimation.InsertKeyFrame(1.0f, targetScale, compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.0f), new Vector2(0.2f, 1.0f)));

            // Correctly set the center point for the scaling animation
            if (element is FrameworkElement fe)
            {
                visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2, (float)fe.ActualHeight / 2, 0f);
            }

            visual.StartAnimation("Scale", scaleAnimation);
        }


        /// <summary>
        /// Führt die Klick-Aktion für den aktuell ausgewählten oberen Button aus.
        /// </summary>
        private void ClickSelectedTopButton()
        {
            if (_topButtons.Count > _selectedTopButtonIndex)
            {
                var buttonToClick = _topButtons[_selectedTopButtonIndex];

                // Execute the correct click method
                if (buttonToClick == ExitGcmButton)
                {
                    ExitGcmButton_Click_1(null, null);
                }
                // =================================================================
                // ADDED: Logic for the new Settings Button
                // =================================================================
                else if (buttonToClick == SettingsButton)
                {
                    SettingsButton_Click(null, null);
                }
                // =================================================================
                else if (buttonToClick == AppLauncherButton)
                {
                    ToggleAppLauncher_Click(null, null);
                }
                else if (buttonToClick == ShutdownButton)
                {
                    // This opens the Power-Menu, the logic is already in GamepadButtonCheck
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
            // Lade das Hintergrundbild JETZT, direkt bevor die UI erscheint.
            SetBackgroundImage(GetScreenWidth(), GetScreenHeight());

            // Video-Player aufräumen
            if (_startupMediaPlayer != null)
            {
                _startupMediaPlayer.MediaEnded -= OnStartupVideoEnded;
                _startupMediaPlayer.Dispose();
                _startupMediaPlayer = null;
            }
            StartupVideoPlayer.SetMediaPlayer(null);
            StartupVideoPlayer.Visibility = Visibility.Collapsed;

            // --- ÄNDERUNG HIER ---
            // Statt die Haupt-UI einzublenden, zeigen wir sofort das Overlay an.
            // Die Haupt-UI wird im Hintergrund sichtbar, aber vom Overlay verdeckt.
            MainContent.Opacity = 1.0;
            MainContent.Visibility = Visibility.Visible;

            FocusLossOverlay.Opacity = 1.0;
            FocusLossOverlay.Visibility = Visibility.Visible;
            _isOverlayActive = true; // Wichtig: Den Status sofort setzen!
                                     // --- ENDE DER ÄNDERUNG ---

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

        #region shurdownmenuitem
        private void ShutdownMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Shutdown-Button geklickt. Räume auf und fahre den Computer herunter...");

            // ZUERST die Aufräum-Methode aufrufen
            RenameSteamStartupVideo_End();

            // DANN den Computer herunterfahren
            Process.Start("shutdown", "/s /t 0");
        }

        private void SleepMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PowerMenu.Visibility = Visibility.Collapsed;
            _currentFocusArea = FocusArea.TopButtons;
            UpdateVisualFocus();
            // Die beiden anderen Parameter sind Standard und sollten auf false bleiben.
            SetSuspendState(true, false, false);
            PlaydeactivationSound();
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
                UpdateVisualFocus();
                PlaydeactivationSound();
                return;
            }

            AppLauncher.Visibility = Visibility.Visible;
            _currentFocusArea = FocusArea.AppLauncher;

            // NEW: Clear the search bar and reset the list
            AppSearchBox.Text = "";
            AppGridView.ItemsSource = AllInstalledApps; // Show the full list again

            if (!isAppListLoaded)
            {
                await LoadInstalledAppsAsync();
            }

            // Set the focus directly into the search bar for intuitive use
            AppSearchBox.Focus(FocusState.Programmatic);

            UpdateVisualFocus();
            PlayActivationSound();
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
        public static void HideTaskbar()
        {
            // 1. Haupt-Taskleiste (Shell_TrayWnd)
            HideWindowByClass("Shell_TrayWnd");

            // 2. Taskleiste auf Zweitmonitoren
            HideWindowByClass("Shell_SecondaryTrayWnd");

            // 3. Das Startmenü (Klasse in Win 11)
            HideWindowByClass("StartMenu.Internal.Flyout");

            // 4. Das Startmenü (Fallback über Fenstertitel, z.B. Win 10)
            HideWindowByTitle("Start");

            // 5. Das Suchfenster (Win+S)
            HideWindowByTitle("Search"); // Englische Version
            HideWindowByTitle("Suche"); // Deutsche Version

            // 6. Das Info-Center / Schnelleinstellungen (Win+A)
            HideWindowByClass("Windows.UI.Core.CoreWindow"); // Dies ist riskant, aber oft nötig
            // Besserer Weg für Win 11 Schnelleinstellungen:
            HideWindowByClass("ControlCenter.Internal.Flyout");

            // 7. Kalender/Benachrichtigungen (Win+N)
            HideWindowByClass("NativeHWNDHost");
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
