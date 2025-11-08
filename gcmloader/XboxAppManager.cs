using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Management.Deployment;

namespace gcmloader
{
    /// <summary>
    /// A helper class designed to find, launch, and manage the Windows Xbox application.
    /// </summary>
    public class XboxLauncher
    {
        #region Win32 Imports & Constants

        // Imports the user32.dll function needed to control window states.
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // Constant for the ShowWindow function to maximize the window.
        private const int SW_SHOWMAXIMIZED = 3;

        #endregion

        #region Core Logic

        /// <summary>
        /// Finds the full executable path for the Xbox App using the official PackageManager API.
        /// This is the reliable way to locate UWP/packaged apps.
        /// </summary>
        /// <returns>The full path to the Xbox app executable.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the Xbox app is not installed for the current user.</exception>
        private string FindXboxExePath()
        {
            var packageManager = new PackageManager();
            // This is the static "Package Family Name" for the Microsoft Xbox App.
            const string packageFamilyName = "Microsoft.GamingApp_8wekyb3d8bbwe";

            var packages = packageManager.FindPackagesForUser(string.Empty, packageFamilyName);
            var package = packages.FirstOrDefault();

            if (package == null)
            {
                throw new InvalidOperationException("The Xbox App is not installed for the current user.");
            }

            // The path to the app executable is constructed from its installation folder and its AppUserModelId.
            // AppListEntries[0] is typically the main entry point for the application.
            string appName = package.GetAppListEntries()[0].AppUserModelId.Split('!')[1];
            return System.IO.Path.Combine(package.InstalledLocation.Path, $"{appName}.exe");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Launches the Xbox app and waits asynchronously for its main window handle to become available.
        /// </summary>
        /// <returns>The window handle (IntPtr) of the main Xbox app window, or IntPtr.Zero if it fails.</returns>
        public async Task<IntPtr> LaunchAndGetWindowHandleAsync()
        {
            try
            {
                // Step 1: Reliably find the executable path.
                string exePath = FindXboxExePath();
                if (string.IsNullOrEmpty(exePath)) return IntPtr.Zero;

                // Step 2: Start the process.
                Process xboxProcess = Process.Start(new ProcessStartInfo(exePath));
                if (xboxProcess == null) return IntPtr.Zero;

                Console.WriteLine($"Xbox process started with ID: {xboxProcess.Id}");

                // Step 3: Wait reliably for the window handle to be created.
                // UWP apps can take a moment to initialize, so we poll for the handle.
                // We'll try for a maximum of 15 seconds (30 attempts * 500ms delay).
                for (int i = 0; i < 30; i++)
                {
                    xboxProcess.Refresh(); // Refresh process info
                    if (xboxProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        Console.WriteLine("Window handle found!");
                        return xboxProcess.MainWindowHandle;
                    }
                    await Task.Delay(500); // Wait a moment before checking again.
                }

                Console.WriteLine("Timeout: Window handle was not found in time.");
                return IntPtr.Zero; // Return zero if the handle wasn't found.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// A helper method to maximize a window using its handle.
        /// </summary>
        /// <param name="hwnd">The window handle (IntPtr) of the window to maximize.</param>
        public void MaximizeWindow(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_SHOWMAXIMIZED);
            }
        }

        #endregion
    }
}
