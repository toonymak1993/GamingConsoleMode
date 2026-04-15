// PRIMARY ENTRY POINT — single-instance mutex, admin gate, launches MainWindow.
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
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

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // 1. Single Instance Check (Mutex)
           bool isFirstInstance;
           _mutex = new Mutex(true, AppName, out isFirstInstance);
            if (!isFirstInstance)
            {
                Environment.Exit(0);
            }

             //2. Admin-Rechte Check
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                Environment.Exit(0);
            }

            // 3. Toast Notification Setup (Optional)
            CommunityToolkit.WinUI.Notifications.ToastNotificationManagerCompat.OnActivated += (toastArgs) =>
            {
                // Hier könnte Logik für Toast-Klicks rein
            };

            // 4. Hauptfenster erstellen
            m_window = new MainWindow();
            m_window.Activate();
        }

        /// <summary>Recreate <see cref="m_window"/> so DPI / display scale changes apply cleanly (WinUI quirk).</summary>
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
            try { Process.Start(startInfo); }
            catch (Exception ex) { Debug.WriteLine($"Failed to restart as admin: {ex.Message}"); }
        }
    }
}