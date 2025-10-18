using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;
using WinRT.Interop;
using Windows.Graphics;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        string owner = "toonymak1993";      // The owner of the GitHub repository.
        string repo = "GameConsoleMode";    // The name of the repository.
        string currentVersion = "2.3.8";    // The current version of this application. Remember to change this for new releases.

        public MainWindow()
        {
            this.InitializeComponent();

            // Set a good default size for the window on startup.
            SetWindowSize(1500, 1100);

            // On the very first launch, we need to create the settings folder and a default config file.
            AppSettings.FirstStart(this);

            versioninfopanel(currentVersion);

            #region Onboarding
            // Check if the user has completed the onboarding process before.
            try
            {
                if (AppSettings.Load<bool>("onboarding") == true)
                {
                    // They have, so let's go straight to the main content.
                    contentFrame.Navigate(typeof(onboarding), null, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                }
                else
                {
                    // This seems to be their first time, so let's show them the onboarding page.
                    contentFrame.Navigate(typeof(onboarding), null, new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight
                    });
                    // And we'll save the fact that they've now seen it.
                    AppSettings.Save("onboarding", true);
                }
            }
            catch
            {
                // If loading the setting fails (e.g., the file doesn't exist yet),
                // we'll assume it's the first launch and show the onboarding.
                contentFrame.Navigate(typeof(onboarding), null, new SlideNavigationTransitionInfo()
                {
                    Effect = SlideNavigationTransitionEffect.FromRight
                });
                AppSettings.Save("onboarding", true);
            }
            #endregion

            // Check for new updates in the background.
            _ = UpdateCheck(this);

            Updateui();
        }

        #region Program Startup & Navigation
        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer != null)
            {
                if (args.IsSettingsSelected)
                {
                    // Logic for the settings page.
                    contentFrame.Navigate(typeof(settings));
                }
                else
                {
                    string selectedTag = args.SelectedItemContainer.Tag.ToString();

                    // Figure out which page to navigate to based on the item's tag.
                    Type pageType = selectedTag switch
                    {
                        "OnboardingPage" => typeof(onboarding),
                        "LauncherPage" => typeof(launcher),
                        "shortcuts" => typeof(shortcuts),
                        "StartupPage" => typeof(startup),
                        "LinksPage" => typeof(Links),
                        "RogAllyPage" => typeof(rogally),
                        "TaskManagerPage" => typeof(taskmanager),
                        _ => null
                    };

                    if (pageType != null && contentFrame.CurrentSourcePageType != pageType)
                    {
                        // Use a nice sliding animation for the page transition.
                        var transitionInfo = new SlideNavigationTransitionInfo
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight
                        };

                        // Navigate to the new page.
                        contentFrame.Navigate(pageType, null, transitionInfo);
                    }
                }
            }
        }

        private void SetWindowSize(int width, int height)
        {
            // We need to get the window's native handle (HWND) to resize it.
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                // Now we can set the size.
                appWindow.Resize(new SizeInt32(width, height));
            }
        }

        public static implicit operator string(MainWindow v)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Update Logic
        public async Task UpdateCheck(MainWindow mainWindow)
        {
            string latestVersion = await GetLatestReleaseVersion(owner, repo);

            if (!string.IsNullOrEmpty(latestVersion))
            {
                Console.WriteLine($"Latest available version: {latestVersion}");

                if (IsNewerVersion(currentVersion, latestVersion))
                {
                    Console.WriteLine("An update is available!");
                    // Make the update notification bar visible.
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
            // The GitHub API requires a User-Agent header, so we'll add one.
            client.DefaultRequestHeaders.Add("User-Agent", "C# App");

            string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            try
            {
                string json = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("tag_name").GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching latest version: {ex.Message}");
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
            UpdateButton.IsEnabled = false;
            _ = DownloadLatestRelease(owner, repo, UpdateProgressBar);
        }

        private void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Start the Update.exe from our settings folder in AppData.
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "Update.exe"),
                    UseShellExecute = true
                };
                Process.Start(startInfo);

                // Close the current application so the updater can do its job.
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Problem during update process: {ex.Message}");
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
                string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
                if (!Directory.Exists(updateDir))
                {
                    Directory.CreateDirectory(updateDir);
                }

                string filePath = Path.Combine(updateDir, "Update.exe");

                var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                httpResponse.EnsureSuccessStatusCode();

                long totalBytes = httpResponse.Content.Headers.ContentLength ?? -1;
                double totalMB = totalBytes / 1024d / 1024d;

                using var httpStream = await httpResponse.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

                byte[] buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;
                var stopwatch = Stopwatch.StartNew();

                progressBar.Visibility = Visibility.Visible;
                DownloadProgressText.Visibility = Visibility.Visible;

                while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    double percent = (double)totalBytesRead / totalBytes * 100;
                    progressBar.Value = percent;

                    double downloadedMB = totalBytesRead / 1024d / 1024d;
                    double speed = totalBytesRead / stopwatch.Elapsed.TotalSeconds; // bytes/sec
                    double secondsLeft = (totalBytes - totalBytesRead) / speed;

                    DownloadProgressText.Text = $"{downloadedMB:F2} MB / {totalMB:F2} MB - ~{secondsLeft:F0}s left";
                }

                stopwatch.Stop();
                progressBar.Visibility = Visibility.Collapsed;
                DownloadProgressText.Visibility = Visibility.Collapsed;
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
        #endregion

        #region Version Info Panel
        private void versioninfopanel(string newversion)
        {
            try
            {
                var savedVersion = AppSettings.Load<string>("version")?.Trim();
                var current = currentVersion?.Trim();

                if (string.Equals(savedVersion, current, StringComparison.OrdinalIgnoreCase))
                {
                    // The version is the same as last time, so no need to show the "what's new" panel.
                }
                else
                {
                    // It's a new version! Let's save it and show the panel.
                    AppSettings.Save("version", newversion);
                    var versionpanel = new version_news();
                    versionpanel.ShowCenteredTo(this, 420, 600);
                }
            }
            catch (Exception)
            {
                // This probably means it's the first time running, so the "version" setting doesn't exist yet.
                AppSettings.Save("version", newversion);
                var versionpanel = new version_news();
                versionpanel.ShowCenteredTo(this, 420, 600);
            }
        }
        #endregion

        #region Top Bar Button
        private static string exeFolder()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string folderPath = Path.GetDirectoryName(exePath);
            return folderPath;
        }

        private void TopbarButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Navigate to the "gcmloader" subfolder and start the executable inside it.
                string fullExePath = Path.Combine(exeFolder(), "gcmloader", "gcmloader.exe");
                Process.Start(new ProcessStartInfo
                {
                    FileName = fullExePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ContentDialog dialog = new ContentDialog();
                dialog.XamlRoot = (this.Content as FrameworkElement)?.XamlRoot;
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                dialog.Title = "Error starting Game Console Mode";
                dialog.Content = ex.Message;
                dialog.CloseButtonText = "OK";
                _ = dialog.ShowAsync();
            }
        }
        #endregion

        #region UI Update Placeholder
        private void Updateui()
        {
            // This method is a placeholder for any future UI updates that might be needed.
        }
        #endregion

        #region Social Links
        private void DiscordImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Creates a new process to open the link in the user's default web browser.
                Process.Start(new ProcessStartInfo("https://discord.gg/FbjYDeEJce") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // If opening the link fails, write an error message to the debug console.
                Debug.WriteLine($"Error opening Discord link: {ex.Message}");
            }
        }

        private void patreonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Opens the Patreon link in the default browser.
                Process.Start(new ProcessStartInfo("https://patreon.com/GAMINGCONSOLEMODE") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening Patreon link: {ex.Message}");
            }
        }
        #endregion
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

            // A short delay helps ensure the window handle is valid and visible before we try to show it.
            await Task.Delay(100);
            ShowWindow(hwndChild, SW_SHOWNORMAL);
            SetForegroundWindow(hwndChild);
        }
    }
}
