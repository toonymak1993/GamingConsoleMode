using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace gcminstaller
{
    public partial class MainWindow : Window
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/toonymak1993/GameConsoleMode/releases/latest";
        private readonly HttpClient httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartInstallButton_Click(object sender, RoutedEventArgs e)
        {
            StartInstallButton.IsEnabled = false;
            StatusText.Text = "Checking for latest release...";

            try
            {
                // GitHub API erfordert User-Agent Header
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GCMWebInstaller/1.0");

                // Hole die neueste Release-Info
                var response = await httpClient.GetStringAsync(GitHubApiUrl);
                using var json = JsonDocument.Parse(response);

                string downloadUrl = null;

                foreach (var asset in json.RootElement.GetProperty("assets").EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString();
                    if (name != null && (name.EndsWith(".exe") || name.EndsWith(".msi")))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }

                if (downloadUrl == null)
                {
                    StatusText.Text = "No valid installer found in latest release.";
                    return;
                }

                string tempPath = Path.Combine(Path.GetTempPath(), "GCMSetup.exe");
                StatusText.Text = "Downloading GCMSetup.exe...";
                await DownloadFileAsync(downloadUrl, tempPath);

                StatusText.Text = "Launching installer...";
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true,
                    Verb = "runas" // verlangt Admin-Rechte
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                StartInstallButton.IsEnabled = true;
            }
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GCMInstaller");

                var json = await client.GetStringAsync("https://api.github.com/repos/toonymak1993/GameConsoleMode/releases/latest");
                using var doc = JsonDocument.Parse(json);

                string body = doc.RootElement.GetProperty("body").GetString();
                ChangelogTextBlock.Text = body?.Trim() ?? "No changelog available.";
            }
            catch (Exception ex)
            {
                ChangelogTextBlock.Text = $"Could not load changelog.\n{ex.Message}";
            }
        }


        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            double lastReported = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) != 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (canReportProgress)
                {
                    double progress = (double)totalRead / totalBytes * 100;
                    double mbDownloaded = totalRead / 1024d / 1024d;
                    double mbTotal = totalBytes / 1024d / 1024d;

                    Dispatcher.Invoke(() =>
                    {
                        InstallerProgressBar.Value = progress;
                        StatusText.Text = $"Downloading... {mbDownloaded:F1} MB / {mbTotal:F1} MB";
                    }, DispatcherPriority.Background);
                }
            }
        }
    }
}
