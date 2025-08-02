using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcmloader
{
    internal class TaskbarSettings
    {
        // Diese Konstanten sind "private", also nur für diese Klasse sichtbar.
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
        private const string RegistryValueName = "Settings";

        /// <summary>
        /// Setzt die Systemeinstellung für das automatische Ausblenden der Taskleiste.
        /// </summary>
        public static void SetAutoHide(bool enableAutoHide)
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key == null) return;

                // Die Methode hier drinnen kann auf "RegistryValueName" zugreifen.
                byte[] settings = key.GetValue(RegistryValueName) as byte[];
                if (settings == null) return;

                if (enableAutoHide)
                {
                    settings[8] = (byte)(settings[8] | 0x02);
                }
                else
                {
                    settings[8] = (byte)(settings[8] & ~0x02);
                }

                key.SetValue(RegistryValueName, settings);
                RestartExplorer();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler beim Ändern der Taskleisten-Einstellung: " + ex.Message);
            }
        }

        private static void RestartExplorer()
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                process.Kill();
            }
        }
    }
}
