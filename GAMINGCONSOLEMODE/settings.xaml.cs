using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Windows.Forms;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GAMINGCONSOLEMODE
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class settings : Page
    {
        public settings()
        {
            this.InitializeComponent();

            try
            {
                string latestVersion = GetLatestVersion();
                versiontext.Text = ("Latest version: " + latestVersion);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }


        }

        public static string GetLatestVersion()
        {
            // GitHub API endpoint for the latest release
            string apiUrl = "https://api.github.com/repos/Kosnix/GameConsoleMode/releases/latest";

            using (WebClient client = new WebClient())
            {
                // GitHub API requires a User-Agent header.
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                // Synchronously download the JSON response from the API.
                string json = client.DownloadString(apiUrl);

                // Parse the JSON document to extract the "tag_name" property.
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
            string url = "https://github.com/Kosnix/GameConsoleMode/releases";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Console.WriteLine("The URL has been opened in your default browser.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening the URL: " + ex.Message);
            }
        }

        private void windowsloginwithoutpassword_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://usblogon.quadsoft.org/de/downloads/USBLogonSetup.exe"; // URL of the installer
            string tempPath = Path.Combine(Path.GetTempPath(), "USBLogonSetup.exe"); // Save path in temp folder

            try
            {
                Console.WriteLine("Downloading USBLogonSetup.exe...");

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(url, tempPath);
                }

                Console.WriteLine("Download complete. Launching installer...");

                // Start the installer
                Process process = new Process();
                process.StartInfo.FileName = tempPath;
                process.StartInfo.UseShellExecute = true; // Ensures it runs with UI
                process.Start();

                Console.WriteLine("Installer started successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void resetconfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string gcmSettings = Path.Combine(appData, "gcmsettings");

                if (Directory.Exists(gcmSettings))
                {
                    Directory.Delete(gcmSettings, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler: {ex.Message}");
            }

            // Anwendung beenden
            Environment.Exit(0);
        }

        private void uactoggle_Click(object sender, RoutedEventArgs e)
        {
            //On
           

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = @"C:\Program Files (x86)\GCMcrew\GCM\GCM\TaskHelper.exe",
                    Arguments = "--uac=disable",
                    Verb = "runas",              // triggers UAC prompt
                    UseShellExecute = true
                };

                var process = Process.Start(psi);
                AppSettings.Save("useuac", false);

                if (process == null)
                {
                    // This should rarely happen, but handle it just in case
                    //MessageBox.Show("Failed to start TaskHelper.");
                }
                else
                {
                    // Optionally: Wait or log that it started successfully
                    MessageBox.Show("UAC has been disabled,A system restart is required for the change to take effect");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // This exception is thrown when the user clicks "No" on the UAC prompt
                if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                   // MessageBox.Show("Operation was canceled by the user.");
                    AppSettings.Save("useuac", true);
                }
                else
                {
                    //MessageBox.Show($"Unexpected error: {ex.Message}");
                }
            }

        }

        private void uactoggleoff_Click(object sender, RoutedEventArgs e)
        {
            //off
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = @"C:\Program Files (x86)\GCMcrew\GCM\GCM\TaskHelper.exe", // change this path accordingly
                    Arguments = "--uac=enable",
                    Verb = "runas",               // triggers UAC prompt
                    UseShellExecute = true        // required for runas
                };

                var process = Process.Start(psi);

                AppSettings.Save("useuac", true);
                if (process == null)
                {
                    //MessageBox.Show("Failed to start TaskHelper.");
                }
                else
                {
                    MessageBox.Show("UAC has been enabled,A system restart is required for the change to take effect");
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // ERROR_CANCELLED = 1223 — User clicked "No"
                if (ex.NativeErrorCode == 1223)
                {
                    //MessageBox.Show("UAC change was canceled by the user.");
                    AppSettings.Save("useuac", true);
                }
                else
                {
                    //MessageBox.Show($"Unexpected error: {ex.Message}");
                }
            }

        }
    }
}