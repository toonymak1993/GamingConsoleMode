using GAMINGCONSOLEMODE;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Threading;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.



namespace gcmloader
{
    public partial class App : Application
    {
        public static MainWindow m_window;

        private static Mutex _mutex = null;
        private const string AppName = "GameConsoleModeLoader";

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // 1. Prüfen, ob bereits eine Instanz der App läuft.
            bool isFirstInstance;
            _mutex = new Mutex(true, AppName, out isFirstInstance);
            if (!isFirstInstance)
            {
                Environment.Exit(0); // Beenden, wenn schon eine Instanz da ist.
                return;
            }

            // 2. Prüfen, ob diese erste Instanz Admin-Rechte hat.
            if (!IsAdministrator())
            {
                RestartAsAdmin(); // Wenn nicht, als Admin neu starten...
                Environment.Exit(0); // ...und diese Instanz sofort beenden.
                return;
            }

            CommunityToolkit.WinUI.Notifications.ToastNotificationManagerCompat.OnActivated += (toastArgs) =>
            {
                // Hier könnte man Code ausführen, wenn jemand auf den Toast klickt.
                // Lassen wir erst mal leer.
            };

            // Nur wenn beide Prüfungen bestanden sind, wird das Fenster erstellt.

            AllyHardwareControl.InitializeAllyButtons();
            m_window = new MainWindow();
            m_window.Activate();
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
