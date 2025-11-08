using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class settings : Page
    {
        public settings()
        {
            this.InitializeComponent();

            try
            {
                // Try to fetch the latest version number and display it.
                string latestVersion = GetLatestVersion();
                versiontext.Text = ("Latest version: " + latestVersion);
            }
            catch (Exception ex)
            {
                // If it fails, just log it. Not a critical error.
                Debug.WriteLine("Error fetching latest version: " + ex.Message);
            }
        }

        public static string GetLatestVersion()
        {
            // This is the GitHub API endpoint to get info on the latest release.
            string apiUrl = "https://api.github.com/repos/Kosnix/GameConsoleMode/releases/latest";

            using (WebClient client = new WebClient())
            {
                // The GitHub API is a bit picky and requires a User-Agent header.
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // Grab the JSON response from the API as a string.
                string json = client.DownloadString(apiUrl);

                // Now, let's parse that JSON to find the version number.
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    string tagName = root.GetProperty("tag_name").GetString();
                    return tagName;
                }
            }
        }

        private void changelogbutton_Click(object sender, RoutedEventArgs e)
        {
            // This just opens the GitHub releases page in the user's default browser.
            string url = "https://github.com/Kosnix/GameConsoleMode/releases";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true // UseShellExecute = true is important for opening URLs.
                });
                Debug.WriteLine("The URL has been opened in your default browser.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening the URL: " + ex.Message);
            }
        }

        // A helper to quickly show a simple info dialog.
        private void ShowSimpleDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot // Make sure it's linked to the current window.
            };

            _ = dialog.ShowAsync();
        }

        private void windowsloginwithoutpassword_Click(object sender, RoutedEventArgs e)
        {
            // This button provides a direct download link to the Sysinternals AutoLogon tool.
            try
            {
                string url = "https://download.sysinternals.com/files/AutoLogon.zip";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open AutoLogon download link: " + ex.Message);
            }
        }

        private void resetconfig_Click(object sender, RoutedEventArgs e)
        {
            // This is a destructive action that deletes the app's settings folder.
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string gcmSettings = Path.Combine(appData, "gcmsettings");

                if (Directory.Exists(gcmSettings))
                {
                    Directory.Delete(gcmSettings, true); // true means recursive delete.
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting settings folder: {ex.Message}");
            }

            // The settings are gone, so the app needs to close to avoid errors.
            Environment.Exit(0);
        }

        private void uactoggle_Click(object sender, RoutedEventArgs e)
        {
            // Turn on the UAC feature.
            AppSettings.Save("uac", true);
            ShowSimpleDialog("GCM UAC ON", "GCM will re-enable UAC upon exit. The changes will take effect on the next GCM launch");
        }

        private void uactoggleoff_Click(object sender, RoutedEventArgs e)
        {
            // Turn off the UAC feature.
            AppSettings.Save("uac", false);
            ShowSimpleDialog("GCM UAC OFF", "GCM will disable UAC upon exit. The changes will take effect on the next GCM launch.");
        }
    }
}
