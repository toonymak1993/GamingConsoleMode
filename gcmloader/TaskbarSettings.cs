using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace gcmloader
{
    /// <summary>
    /// A utility class to control the Windows taskbar's auto-hide setting via the registry.
    /// </summary>
    internal class TaskbarSettings
    {
        #region Registry Constants

        // These constants define the specific registry location for the taskbar settings.
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
        private const string RegistryValueName = "Settings";

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the system setting for automatically hiding the taskbar.
        /// </summary>
        /// <param name="enableAutoHide">Set to 'true' to enable auto-hide, 'false' to disable it.</param>
        public static void SetAutoHide(bool enableAutoHide)
        {
            try
            {
                // Open the registry key with write access.
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key == null) return;

                    // Get the raw byte array value that stores various taskbar settings.
                    if (key.GetValue(RegistryValueName) is not byte[] settings) return;

                    // The 9th byte (index 8) contains the flag for auto-hide.
                    // We use bitwise operations to toggle only the specific flag without affecting other settings.
                    if (enableAutoHide)
                    {
                        // Enable auto-hide by setting the second bit to 1.
                        settings[8] = (byte)(settings[8] | 0x02);
                    }
                    else
                    {
                        // Disable auto-hide by setting the second bit to 0.
                        settings[8] = (byte)(settings[8] & ~0x02);
                    }

                    // Write the modified byte array back to the registry.
                    key.SetValue(RegistryValueName, settings);
                }

                // Restart the Explorer process to apply the changes immediately.
                RestartExplorer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error changing the taskbar setting: " + ex.Message);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Restarts the Windows Explorer process to apply UI changes like taskbar settings.
        /// </summary>
        private static void RestartExplorer()
        {
            // Find all running 'explorer.exe' processes and terminate them.
            // Windows will automatically relaunch the process.
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                process.Kill();
            }
        }

        #endregion
    }
}
