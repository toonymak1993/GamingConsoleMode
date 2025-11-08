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

        // Events to let the UI know what's happening.
        public event Action<string>? StatusUpdated;
        public event Action<int>? ProgressChanged; // Reports progress as a percentage (0-100).

        public DeckyInstallerLogic()
        {
            _httpClient = new HttpClient();
            // Some websites are picky, so let's pretend we're a regular browser.
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Kicks off the whole installation process for Decky Loader (autostart is not handled here).
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

                // Clean up the downloaded zip file. No need to leave it lying around.
                try { File.Delete(zipPath); } catch { }

                ReportProgress(totalSteps, totalSteps, "Installation complete!");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error: {ex.Message}");
                throw; // Rethrow the exception so the caller knows something went wrong.
            }
        }

        /// <summary>
        /// This method undoes all the changes made by the installer.
        /// </summary>
        public async Task ExecuteUninstallationAsync()
        {
            const int totalSteps = 3;
            int currentStep = 0;
            StatusUpdated?.Invoke("Starting uninstallation...");

            try
            {
                // Step 1: Get rid of the desktop shortcut.
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

                // Step 2: Remove the Steam debugging file.
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

                // Step 3: Nuke the homebrew directory.
                ReportProgress(++currentStep, totalSteps, "Removing homebrew directories...");
                string homebrewDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "homebrew");
                if (Directory.Exists(homebrewDir))
                {
                    Directory.Delete(homebrewDir, true); // true = recursive, so it deletes everything inside.
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
        /// Checks if Python is installed. If not, it handles the installation.
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
                    return; // We're good to go.
                }
            }
            catch
            {
                // Process.Start will throw an exception if it can't find "python" in the PATH.
                // That's our cue to install it.
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
                Verb = "runas" // This will trigger the UAC prompt for admin rights.
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
            File.Delete(installerPath); // Clean up after ourselves.
        }

        private void ReportProgress(int current, int total, string message)
        {
            StatusUpdated?.Invoke(message);
            int percentage = (int)((double)current / total * 100);
            ProgressChanged?.Invoke(Math.Min(100, percentage)); // Don't let it go over 100%.
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

                // This empty file is all Steam needs to see to enable CEF debugging.
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

                // These methods won't complain if the directories already exist, which is perfect.
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

                // Using PowerShell to create a shortcut is a bit of a hack, but it's reliable.
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
            // First, let's try the registry key for 64-bit systems.
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                steamPath = key?.GetValue("InstallPath") as string;
            }
            catch { /* Best not to crash if registry access fails. */ }

            // If that didn't work, try the 32-bit key.
            if (string.IsNullOrEmpty(steamPath))
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
                    steamPath = key?.GetValue("InstallPath") as string;
                }
                catch { /* Again, silence is golden. */ }
            }

            // If we still can't find it, fall back to the default location.
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
