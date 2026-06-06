using Microsoft.UI.Xaml;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace gcmloader
{
    public partial class App : Application
    {
        // WICHTIG: Statische Variable, damit wir das Fenster von überall steuern können
        public static MainWindow m_window;

        private static Mutex _mutex = null;
        private const string AppName = "GameConsoleModeLoader";
        private static readonly string StartupTracePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "startup_trace.log");
        private static readonly string ElevationSignalDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "elevation");
        private const string ElevationSignalArgumentPrefix = "--elevation-signal=";

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        internal static void StartupTrace(string message)
        {
            try
            {
                string? directory = Path.GetDirectoryName(StartupTracePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(
                    StartupTracePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            StartupTrace("OnLaunched begin");
            TryAcknowledgeElevationLaunch();
            bool launchedAsConfiguredShell = IsRunningAsConfiguredWinlogonShell();

            if (!IsAdministrator())
            {
                StartupTrace("Bootstrap privileges missing. Attempting elevated relaunch.");
                if (TryRelaunchAsAdministrator(out string elevationSignalPath))
                {
                    StartupTrace($"Elevated relaunch requested. Waiting for acknowledgement file '{elevationSignalPath}'.");
                    if (WaitForElevationSignal(elevationSignalPath, TimeSpan.FromSeconds(8)))
                    {
                        StartupTrace("Elevated relaunch acknowledged. Exiting bootstrap instance.");
                        Environment.Exit(0);
                        return;
                    }

                    StartupTrace("Elevated relaunch acknowledgement timed out. Continuing without elevation.");
                }
            }

            if (launchedAsConfiguredShell)
            {
                StartupTrace("Detected GCM as configured shell. Deferring Explorer handoff until MainWindow startup.");
            }

            // 1. Single Instance Check (Mutex)
            bool isFirstInstance;
            _mutex = new Mutex(true, AppName, out isFirstInstance);
            StartupTrace($"Mutex acquired. isFirstInstance={isFirstInstance}");
            if (!isFirstInstance)
            {
                StartupTrace("Exiting because another instance is already running.");
                Environment.Exit(0);
                return;
            }

            StartupTrace(IsAdministrator()
                ? "Bootstrap privileges ready. Running elevated."
                : "Bootstrap privileges ready. Continuing in local mode without elevation.");

            // 3. Toast Notification Setup (Optional)
            CommunityToolkit.WinUI.Notifications.ToastNotificationManagerCompat.OnActivated += (toastArgs) =>
            {
                // Hier könnte Logik für Toast-Klicks rein
            };
            StartupTrace("Toast activation handler attached.");

            // 4. Hauptfenster erstellen
            StartupTrace("Creating MainWindow.");
            m_window = new MainWindow();
            StartupTrace("MainWindow created. Activating.");
            m_window.Activate();
            StartupTrace("MainWindow activated.");
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            StartupTrace($"WinUI unhandled exception: {e.Exception}");
        }

        private static void CurrentDomain_UnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
        {
            StartupTrace($"AppDomain unhandled exception. IsTerminating={e.IsTerminating}. Exception={e.ExceptionObject}");
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            StartupTrace($"TaskScheduler unobserved exception: {e.Exception}");
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            try
            {
                StartupTrace($"ProcessExit fired for {Process.GetCurrentProcess().ProcessName}.");
            }
            catch
            {
                StartupTrace("ProcessExit fired.");
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static void TryAcknowledgeElevationLaunch()
        {
            try
            {
                string? signalPath = GetElevationSignalPathFromArguments();
                if (string.IsNullOrWhiteSpace(signalPath))
                {
                    return;
                }

                string? directory = Path.GetDirectoryName(signalPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(signalPath, DateTime.UtcNow.ToString("O"));
                StartupTrace($"Elevated relaunch handshake written to '{signalPath}'.");
            }
            catch (Exception ex)
            {
                StartupTrace($"Elevated relaunch handshake failed: {ex.Message}");
            }
        }

        private static string? GetElevationSignalPathFromArguments()
        {
            try
            {
                return Environment.GetCommandLineArgs()
                    .FirstOrDefault(arg => arg.StartsWith(ElevationSignalArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                    ?.Substring(ElevationSignalArgumentPrefix.Length);
            }
            catch
            {
                return null;
            }
        }

        private static bool WaitForElevationSignal(string signalPath, TimeSpan timeout)
        {
            try
            {
                DateTime timeoutAt = DateTime.UtcNow.Add(timeout);
                while (DateTime.UtcNow < timeoutAt)
                {
                    if (File.Exists(signalPath))
                    {
                        try
                        {
                            File.Delete(signalPath);
                        }
                        catch
                        {
                        }

                        return true;
                    }

                    Thread.Sleep(150);
                }
            }
            catch (Exception ex)
            {
                StartupTrace($"Waiting for elevated relaunch acknowledgement failed: {ex.Message}");
            }

            return false;
        }

        private static bool IsRunningAsConfiguredWinlogonShell()
        {
            try
            {
                string executablePath = GcmWindowsShellService.ResolveStableExecutablePath(
                    Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    return false;
                }

                const string winlogonPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

                using RegistryKey? localMachineKey = Registry.LocalMachine.OpenSubKey(winlogonPath, false);
                string configuredShell = localMachineKey?.GetValue("Shell") as string ?? string.Empty;
                if (string.IsNullOrWhiteSpace(configuredShell))
                {
                    using RegistryKey? currentUserKey = Registry.CurrentUser.OpenSubKey(winlogonPath, false);
                    configuredShell = currentUserKey?.GetValue("Shell") as string ?? string.Empty;
                }

                string configuredExecutable = ExtractExecutablePath(configuredShell);
                if (string.IsNullOrWhiteSpace(configuredExecutable))
                {
                    return false;
                }

                return AreEquivalentPaths(executablePath, configuredExecutable);
            }
            catch (Exception ex)
            {
                StartupTrace($"Could not determine Winlogon shell bootstrap state: {ex.Message}");
                return false;
            }
        }

        private static string ExtractExecutablePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return string.Empty;
            }

            string trimmed = command.Trim();
            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = trimmed.IndexOf('"', 1);
                if (closingQuote > 1)
                {
                    return trimmed.Substring(1, closingQuote - 1);
                }
            }

            int commaIndex = trimmed.IndexOf(',');
            if (commaIndex >= 0)
            {
                trimmed = trimmed.Substring(0, commaIndex);
            }

            int spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex >= 0)
            {
                trimmed = trimmed.Substring(0, spaceIndex);
            }

            return trimmed.Trim().Trim('"');
        }

        private static bool AreEquivalentPaths(string left, string right)
        {
            try
            {
                string leftFullPath = Path.GetFullPath(left).Trim().Trim('"');
                string rightFullPath = Path.GetFullPath(right).Trim().Trim('"');
                return string.Equals(leftFullPath, rightFullPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left?.Trim().Trim('"'), right?.Trim().Trim('"'), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool TryRelaunchAsAdministrator(out string signalPath)
        {
            signalPath = string.Empty;

            try
            {
                string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                {
                    StartupTrace("Elevated relaunch aborted because the current executable path could not be resolved.");
                    return false;
                }

                Directory.CreateDirectory(ElevationSignalDirectory);
                signalPath = Path.Combine(ElevationSignalDirectory, $"{Guid.NewGuid():N}.signal");

                string arguments = string.Join(" ",
                    Environment.GetCommandLineArgs()
                        .Skip(1)
                        .Where(arg => !arg.StartsWith(ElevationSignalArgumentPrefix, StringComparison.OrdinalIgnoreCase))
                        .Append($"{ElevationSignalArgumentPrefix}{signalPath}")
                        .Select(QuoteArgument));

                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    UseShellExecute = true,
                    Verb = "runas"
                });

                return true;
            }
            catch (Exception ex)
            {
                StartupTrace($"Elevated relaunch failed: {ex.Message}");
                return false;
            }
        }

        private static string QuoteArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

            return argument.Contains(' ') ? $"\"{argument}\"" : argument;
        }

        /* * DOCUMENTATION:
         * Dies ist die entscheidende Methode für deinen Skalierungs-Fix.
         * Da WinUI 3 oft das Layout bei DPI-Wechseln (100% -> 150%) intern "vergisst",
         * schließen wir das alte Fenster und bauen ein komplett neues auf.
         * Nur so wird die neue Windows-Skalierung garantiert zu 100% übernommen.
         */
        public static void RebuildMainWindow()
        {
            var oldWindow = m_window;

            // Neue Instanz erstellen - diese liest die DPI beim Start frisch aus
            m_window = new MainWindow();
            m_window.Activate();

            // Die alte Instanz sauber schließen
            oldWindow?.Close();

            Debug.WriteLine("[GCM] MainWindow wurde aufgrund von Skalierungsänderung neu aufgebaut.");
        }

    }
}
