using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Linq;
using Windows.Graphics;
using System.Management;
using WinRT.Interop;
using GAMINGCONSOLEMODE;
using Windows.UI.ApplicationSettings;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using gcmloader;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Reflection;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GAMINGCONSOLEMODE
{
    
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        string owner = "Kosnix";  // Repository owner
    string repo = "GameConsoleMode";  // Repository name
    string currentVersion = "2.1.1";  // Your current version // <change when new Verison
        public MainWindow()
        {
            this.InitializeComponent();
            // Set the window size
            SetWindowSize(1500, 1100);
            // 1. First Start: Create folder and default config file if needed
            AppSettings.FirstStart();
            versioninfopanel(currentVersion);
            #region onboarding
            try
                {

                    if (AppSettings.Load<bool>("onboarding") == true)
                    {
                        // Navigate to the 'startup' page on app launch
                        contentFrame.Navigate(typeof(onboarding), null, new SlideNavigationTransitionInfo()
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight
                        });
                    }
                    else
                    {
                        //navigate to the onboarding page
                        // Navigate to the 'startup' page on app launch
                        contentFrame.Navigate(typeof(onboarding), null, new SlideNavigationTransitionInfo()
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight
                        });
                        AppSettings.Save("onboarding", true);

                    }
                }
                catch
                {
                    //navigate to the onboarding page
                    // Navigate to the 'startup' page on app launch
                    contentFrame.Navigate(typeof(onboarding), null, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                    AppSettings.Save("onboarding", true);
                }
                #endregion onboarding
            _ = UpdateCheck(this);
        }

        #region programm start
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {

                if (args.IsSettingsSelected)
                {
                    // Logik for settings
                    contentFrame.Navigate(typeof(settings));

                }
                else
                {

                    string selectedTag = args.SelectedItemContainer.Tag.ToString();

                    // Determine the target page based on the tag
                    Type pageType = selectedTag switch
                    {
                        "OnboardingPage" => typeof(onboarding),
                        "LauncherPage" => typeof(launcher),
                        "shortcuts" => typeof(shortcuts),
                        "StartupPage" => typeof(startup),
                        "LinksPage" => typeof(Links),
                        "RogAllyPage" => typeof(rogally),
                        _ => null
                    };

                    if (pageType != null && contentFrame.CurrentSourcePageType != pageType)
                    {
                        // Use SlideNavigationTransitionInfo to specify the transition direction
                        var transitionInfo = new SlideNavigationTransitionInfo
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight
                        };

                        // Navigate with transition info
                        contentFrame.Navigate(pageType, null, transitionInfo);
                    }
                }
            }
        }

        private void SetWindowSize(int width, int height)
        {
            // Get the native HWND for the current window
            var hWnd = WindowNative.GetWindowHandle(this);

            // Get the WindowId for the HWND
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            // Get the AppWindow using the WindowId
            var appWindow = AppWindow.GetFromWindowId(windowId); // Korrekt aufgerufen mit dem Typnamen AppWindow

            if (appWindow != null)
            {
                // Set the window size
                appWindow.Resize(new SizeInt32(width, height));
            }
        }

        public static implicit operator string(MainWindow v)
        {
            throw new NotImplementedException();
        }
        #endregion programm start

        #region Update
        public async Task UpdateCheck(MainWindow mainWindow)
        {
            string latestVersion = await GetLatestReleaseVersion(owner, repo); // Await the async call

            if (!string.IsNullOrEmpty(latestVersion))
            {
                Console.WriteLine($"Latest available version: {latestVersion}");

                if (IsNewerVersion(currentVersion, latestVersion))
                {
                    Console.WriteLine("An update is available!");
                    mainWindow.UpdateBar.Visibility = Visibility.Visible;
                }
                else
                {
                    Console.WriteLine("You are up to date.");
                }
            }
            else
            {
                Console.WriteLine("Could not retrieve the version.");
            }
        }

        static async Task<string> GetLatestReleaseVersion(string owner, string repo)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "C# App"); // Required for GitHub API

            string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            try
            {
                string json = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("tag_name").GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        private bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            Version.TryParse(currentVersion, out Version current);
            Version.TryParse(latestVersion, out Version latest);
            return latest > current;
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            _ = DownloadLatestRelease(owner, repo, UpdateProgressBar);
        }

        private void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Starte die Update.exe aus dem AppData\gcmsettings Ordner
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "Update.exe"),
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                // Schließe die aktuelle Anwendung
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'exécution de la mise à jour : {ex.Message}");
            }
        }

        private async Task DownloadLatestRelease(string owner, string repo, ProgressBar progressBar)
        {
            using HttpClient client = new HttpClient();
            string latestReleaseUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var response = await client.GetStringAsync(latestReleaseUrl);
            string downloadUrl = ExtractDownloadUrl(response);

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                string fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                // Download in den AppData\gcmsettings Ordner
                string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
                if (!Directory.Exists(updateDir))
                {
                    Directory.CreateDirectory(updateDir);
                }

                string filePath = Path.Combine(updateDir, "Update.exe");

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var httpStream = await client.GetStreamAsync(downloadUrl);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;

                progressBar.Visibility = Visibility.Visible;
                while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    progressBar.Value = (double)totalBytesRead / fileStream.Length * 100;
                }

                progressBar.Visibility = Visibility.Collapsed;
                InstallUpdateButton.Visibility = Visibility.Visible;
                UpdateButton.Visibility = Visibility.Collapsed;
                UpdateBarText.Text = "Install";
                Console.WriteLine($"Downloaded latest version to {filePath}");
            }
            else
            {
                Console.WriteLine("Could not find a valid download URL.");
            }
        }

        private string ExtractDownloadUrl(string jsonResponse)
        {
            using JsonDocument doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;
            if (root.TryGetProperty("assets", out JsonElement assetsArray) && assetsArray.GetArrayLength() > 0)
            {
                foreach (var asset in assetsArray.EnumerateArray())
                {
                    if (asset.TryGetProperty("browser_download_url", out JsonElement downloadUrl))
                    {
                        return downloadUrl.GetString();
                    }
                }
            }
            return string.Empty;
        }
        #endregion Update

        #region filter
        #endregion filter

        #region Versioninfos
        private void versioninfopanel(string newversion)
        {
            
            try
            {
                var savedVersion = AppSettings.Load<string>("version")?.Trim();
                var current = currentVersion?.Trim();
                if (string.Equals(savedVersion, current, StringComparison.OrdinalIgnoreCase))
                {
                    //Verison is identical, not show
                }
                else
                {
                  
                    AppSettings.Save("version", newversion);
                    //show Verison Panel
                    var versionpanel = new version_news();
                    versionpanel.ShowCenteredTo(this, 420, 600);
                }

            }
            catch (Exception ex)
            {
                //first Time not Version Implement
                AppSettings.Save("version", newversion);
                //show Verison Panel
                var versionpanel = new version_news();
                versionpanel.ShowCenteredTo(this, 420, 600);
            }
        }
        #endregion Versioninfos

        #region topbarbutton

        static string exeFolder()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string folderPath = Path.GetDirectoryName(exePath);
            return folderPath;
        }

        private void TopbarButton_Click(object sender, RoutedEventArgs e)
        {
           

            Process.Start(new ProcessStartInfo(Path.Combine(exeFolder(), "gcmloader.exe")));
        
    }

        #endregion topbarbutton
    }

    public static class WindowExtensions
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_SHOWNORMAL = 1;

        public static async void ShowCenteredTo(this Window child, Window parent, int width = 400, int height = 600)
        {
            IntPtr hwndParent = WinRT.Interop.WindowNative.GetWindowHandle(parent);
            WindowId parentId = Win32Interop.GetWindowIdFromWindow(hwndParent);
            AppWindow parentApp = AppWindow.GetFromWindowId(parentId);

            IntPtr hwndChild = WinRT.Interop.WindowNative.GetWindowHandle(child);
            WindowId childId = Win32Interop.GetWindowIdFromWindow(hwndChild);
            AppWindow childApp = AppWindow.GetFromWindowId(childId);

            int centerX = parentApp.Position.X + (parentApp.Size.Width - width) / 2;
            int centerY = parentApp.Position.Y + (parentApp.Size.Height - height) / 2;

            childApp.MoveAndResize(new RectInt32
            {
                X = centerX,
                Y = centerY,
                Width = width,
                Height = height
            });

            child.Activate();

            // Kurze Verzögerung, damit Fensterhandle gültig und sichtbar ist
            await Task.Delay(100);
            ShowWindow(hwndChild, SW_SHOWNORMAL);
            SetForegroundWindow(hwndChild);
        }
    }
    }