using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace GAMINGCONSOLEMODE
{
    internal class DeckyInstallerLogic
    {
        private const string DOWNLOAD_URL = "https://nightly.link/SteamDeckHomebrew/decky-loader/workflows/build-win/main/PluginLoader%20Win.zip";
        private readonly HttpClient _httpClient;

        // Events, um die Benutzeroberfläche über Änderungen zu informieren
        public event Action<string>? StatusUpdated;
        public event Action<int>? ProgressChanged; // Gibt den Fortschritt in Prozent (0-100) zurück

        public DeckyInstallerLogic()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Führt den gesamten Installationsprozess für Decky Loader aus (ohne Autostart).
        /// </summary>
        public async Task ExecuteInstallationAsync()
        {
            const int totalSteps = 6;
            int currentStep = 0;

            try
            {
                ReportProgress(++currentStep, totalSteps, "Checking Python dependency...");
                await EnsurePythonInstalled();

                ReportProgress(++currentStep, totalSteps, "Setting up Steam CEF debugging...");
                if (!await SetupSteamDebug()) throw new Exception("Failed to setup Steam CEF debugging");

                ReportProgress(++currentStep, totalSteps, "Creating homebrew directories...");
                if (!await CreateHomebrewDirectories()) throw new Exception("Failed to create homebrew directories");

                ReportProgress(++currentStep, totalSteps, "Downloading latest build...");
                string zipPath = Path.Combine(Path.GetTempPath(), "PluginLoader.zip");
                var zipBytes = await _httpClient.GetByteArrayAsync(DOWNLOAD_URL);
                await File.WriteAllBytesAsync(zipPath, zipBytes);

                ReportProgress(++currentStep, totalSteps, "Extracting files...");
                string servicesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "homebrew", "services");
                ZipFile.ExtractToDirectory(zipPath, servicesDir, true);

                ReportProgress(++currentStep, totalSteps, "Creating Steam shortcut...");
                if (!CreateSteamShortcut()) throw new Exception("Failed to create Steam shortcut");

                try { File.Delete(zipPath); } catch { }

                ReportProgress(totalSteps, totalSteps, "Installation complete!");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Macht alle Änderungen der Installation rückgängig.
        /// </summary>
        public async Task ExecuteUninstallationAsync()
        {
            const int totalSteps = 3;
            int currentStep = 0;
            StatusUpdated?.Invoke("Starting uninstallation...");

            try
            {
                // Schritt 1: Desktop-Verknüpfung entfernen
                ReportProgress(++currentStep, totalSteps, "Removing desktop shortcut...");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string desktopShortcut = Path.Combine(desktopPath, "Steam (Decky).lnk");
                if (File.Exists(desktopShortcut))
                {
                    File.Delete(desktopShortcut);
                    StatusUpdated?.Invoke("Desktop shortcut removed.");
                }
                else
                {
                    StatusUpdated?.Invoke("Desktop shortcut not found, skipping.");
                }

                // Schritt 2: Steam-Debugging-Datei entfernen
                ReportProgress(++currentStep, totalSteps, "Removing Steam CEF debugging file...");
                string steamPath = GetSteamPath();
                if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                {
                    string debugFile = Path.Combine(steamPath, ".cef-enable-remote-debugging");
                    if (File.Exists(debugFile))
                    {
                        File.Delete(debugFile);
                        StatusUpdated?.Invoke("Steam CEF debugging file removed.");
                    }
                }

                // Schritt 3: Homebrew-Verzeichnis entfernen
                ReportProgress(++currentStep, totalSteps, "Removing homebrew directories...");
                string homebrewDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "homebrew");
                if (Directory.Exists(homebrewDir))
                {
                    Directory.Delete(homebrewDir, true); // true = rekursiv, löscht alles darin
                    StatusUpdated?.Invoke("Homebrew directory and its contents removed.");
                }

                ReportProgress(totalSteps, totalSteps, "Uninstallation complete!");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error during uninstallation: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prüft, ob Python installiert ist, und installiert es bei Bedarf.
        /// </summary>
        public async Task EnsurePythonInstalled()
        {
            StatusUpdated?.Invoke("Checking for Python installation...");

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(startInfo);
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    StatusUpdated?.Invoke("Python is already installed.");
                    return;
                }
            }
            catch
            {
                // Process.Start schlägt fehl, wenn "python" nicht im PATH gefunden wird.
            }

            StatusUpdated?.Invoke("Python not found. Downloading Python installer...");
            const string pythonUrl = "https://www.python.org/ftp/python/3.11.8/python-3.11.8-amd64.exe";
            string installerPath = Path.Combine(Path.GetTempPath(), "python-installer.exe");

            var pythonBytes = await _httpClient.GetByteArrayAsync(pythonUrl);
            await File.WriteAllBytesAsync(installerPath, pythonBytes);

            StatusUpdated?.Invoke("Installing Python silently... Please approve the UAC prompt.");
            var installProcessInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/quiet InstallAllUsers=1 PrependPath=1",
                UseShellExecute = true,
                Verb = "runas"
            };

            using (var process = Process.Start(installProcessInfo))
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception("Python installation failed.");
                }
            }

            StatusUpdated?.Invoke("Python installation successful.");
            File.Delete(installerPath);
        }

        private void ReportProgress(int current, int total, string message)
        {
            StatusUpdated?.Invoke(message);
            int percentage = (int)((double)current / total * 100);
            ProgressChanged?.Invoke(Math.Min(100, percentage));
        }

        private async Task<bool> SetupSteamDebug()
        {
            try
            {
                string? steamPath = GetSteamPath();
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                {
                    throw new Exception("Steam installation directory not found");
                }

                string debugFile = Path.Combine(steamPath, ".cef-enable-remote-debugging");
                await File.WriteAllTextAsync(debugFile, "");
                StatusUpdated?.Invoke("Created .cef-enable-remote-debugging file.");
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error setting up Steam debug: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CreateHomebrewDirectories()
        {
            try
            {
                string homebrewDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "homebrew");
                string servicesDir = Path.Combine(homebrewDir, "services");

                Directory.CreateDirectory(homebrewDir);
                Directory.CreateDirectory(servicesDir);

                StatusUpdated?.Invoke($"Created directory: {servicesDir}");
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error creating directories: {ex.Message}");
                return false;
            }
        }

        private bool CreateSteamShortcut()
        {
            try
            {
                string? steamPath = GetSteamPath();
                string steamExe = Path.Combine(steamPath, "steam.exe");

                if (!File.Exists(steamExe))
                {
                    throw new Exception("Steam executable not found");
                }

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "Steam (Decky).lnk");

                string psCommand = $@"$WshShell = New-Object -ComObject WScript.Shell; " +
                                     $@"$Shortcut = $WshShell.CreateShortcut('{shortcutPath}'); " +
                                     $@"$Shortcut.TargetPath = '{steamExe}'; " +
                                     $@"$Shortcut.Arguments = '-dev'; " +
                                     $@"$Shortcut.WorkingDirectory = '{steamPath}'; " +
                                     $@"$Shortcut.Description = 'Launch Steam with Decky Loader'; " +
                                     "$Shortcut.Save()";

                if (ExecutePowerShellCommand(psCommand))
                {
                    StatusUpdated?.Invoke("Created Steam shortcut with -dev parameter.");
                    return true;
                }
                throw new Exception("PowerShell command failed to create shortcut.");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error creating Steam shortcut: {ex.Message}");
                return false;
            }
        }

        private string GetSteamPath()
        {
            string? steamPath = null;
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                steamPath = key?.GetValue("InstallPath") as string;
            }
            catch { }

            if (string.IsNullOrEmpty(steamPath))
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                    steamPath = key?.GetValue("InstallPath") as string;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(steamPath))
            {
                steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            }

            return steamPath;
        }

        private bool ExecutePowerShellCommand(string command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
    }
}