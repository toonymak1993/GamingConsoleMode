using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace gcmloader.Services
{
    /// <summary>
    /// Explorer shell process lifecycle (no Winlogon shell replacement).
    /// </summary>
    public static class ShellManagementService
    {
        /// <summary>Kill all explorer.exe instances; optionally wait afterward (legacy ShowDesktopIcons behavior).</summary>
        public static void KillExplorerProcesses(int sleepAfterKillMs = 3000)
        {
            foreach (var proc in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    proc.Kill(true);
                    Debug.WriteLine("✓ explorer.exe killed.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[!] Error killing explorer.exe: {ex.Message}");
                }
            }

            if (sleepAfterKillMs > 0)
            {
                Thread.Sleep(sleepAfterKillMs);
            }
        }

        public static void StartExplorer()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[!] ShellManagementService.StartExplorer: {ex.Message}");
            }
        }

        /// <summary>Kill explorer processes, optionally pause, then start a fresh shell.</summary>
        public static void RestartExplorer(int sleepBetweenKillAndStartMs = 500)
        {
            KillExplorerProcesses(0);
            if (sleepBetweenKillAndStartMs > 0)
            {
                Thread.Sleep(sleepBetweenKillAndStartMs);
            }

            StartExplorer();
        }

        /// <summary>
        /// Legacy behavior from MainWindow: strip last 4 characters from an executable file name (e.g. ".exe") then kill matching processes until none remain.
        /// </summary>
        public static void KillAllProcessesForExecutableFileName(string exeFileName)
        {
            if (string.IsNullOrEmpty(exeFileName))
            {
                return;
            }

            string processName = exeFileName.Length > 4
                ? exeFileName.Substring(0, exeFileName.Length - 4)
                : exeFileName;

            bool explorersStillRunning = true;
            while (explorersStillRunning)
            {
                var processes = Process.GetProcessesByName(processName);
                if (!processes.Any())
                {
                    explorersStillRunning = false;
                }
                else
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit();
                            Console.WriteLine(process + "process killed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error attempting to kill : {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}
