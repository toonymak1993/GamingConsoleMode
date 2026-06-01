using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
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

        public App()
        {
            this.InitializeComponent();
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

             //2. Admin-Rechte Check
            if (!IsAdministrator())
            {
                StartupTrace("Current process is not elevated. Restarting as admin.");
                RestartAsAdmin();
                Environment.Exit(0);
                return;
            }
          StartupTrace("Admin check passed.");

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

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static void RestartAsAdmin()
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Verb = "runas"
            };
            try
            {
                StartupTrace("Attempting elevated relaunch.");
                Process.Start(startInfo);
                StartupTrace("Elevated relaunch started.");
            }
            catch (Exception ex)
            {
                StartupTrace($"Failed to restart as admin: {ex.Message}");
                Debug.WriteLine($"Failed to restart as admin: {ex.Message}");
            }
        }
    }
}
