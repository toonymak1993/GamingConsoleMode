using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace gcmloader
{
    public class XboxLauncher
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOWMAXIMIZED = 3;

        /// <summary>
        /// Findet den vollen Pfad zur XboxPcApp.exe über die offizielle PackageManager-API.
        /// </summary>
        private string FindXboxExePath()
        {
            var packageManager = new PackageManager();
            // Der "Package Family Name" der Xbox-App
            const string packageFamilyName = "Microsoft.GamingApp_8wekyb3d8bbwe";

            var packages = packageManager.FindPackagesForUser(string.Empty, packageFamilyName);
            var package = packages.FirstOrDefault();

            if (package == null)
            {
                throw new InvalidOperationException("Xbox-App ist nicht für den aktuellen Benutzer installiert.");
            }

            // Der Pfad zur App-Exe innerhalb des Installationsordners
            // AppListEntries[0] ist normalerweise der Haupteintrag.
            return System.IO.Path.Combine(package.InstalledLocation.Path, package.GetAppListEntries()[0].AppUserModelId.Split('!')[1] + ".exe");
        }

        /// <summary>
        /// Startet die Xbox-App und gibt das Fenster-Handle zurück, sobald es verfügbar ist.
        /// </summary>
        public async Task<IntPtr> LaunchAndGetWindowHandleAsync()
        {
            try
            {
                // Schritt 1: Pfad sauber finden
                string exePath = FindXboxExePath();
                if (string.IsNullOrEmpty(exePath)) return IntPtr.Zero;

                // Schritt 2: Prozess starten
                Process xboxProcess = Process.Start(new ProcessStartInfo(exePath));
                if (xboxProcess == null) return IntPtr.Zero;

                Console.WriteLine($"Xbox-Prozess gestartet mit ID: {xboxProcess.Id}");

                // Schritt 3: Zuverlässig auf das Fenster warten (max. 15 Sekunden)
                for (int i = 0; i < 30; i++)
                {
                    xboxProcess.Refresh();
                    if (xboxProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        Console.WriteLine("Fenster-Handle gefunden!");
                        return xboxProcess.MainWindowHandle;
                    }
                    await Task.Delay(500); // Kurz warten
                }

                Console.WriteLine("Zeitüberschreitung: Fenster-Handle wurde nicht gefunden.");
                return IntPtr.Zero; // Handle nicht gefunden
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ein Fehler ist aufgetreten: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Hilfsmethode, um ein Fenster zu maximieren.
        /// </summary>
        public void MaximizeWindow(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_SHOWMAXIMIZED);
            }
        }
    }
}
