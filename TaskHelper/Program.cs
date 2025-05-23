using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

namespace TaskHelper
{
    internal class Program
    {
        static void Main(string[] args)
        {



            Logger.Init(); // start new log file
            Logger.Write("Program started.");

            if (!IsRunAsAdmin())
            {
                Logger.Write("ERROR: Not running as administrator.");
                Console.WriteLine("This tool must be run as Administrator.");
                return;
            }

            if (args.Length == 0)
            {
                Logger.Write("No arguments provided.");
                Console.WriteLine("Usage:");
                Console.WriteLine("  TaskHelper.exe --enable");
                Console.WriteLine("  TaskHelper.exe --disable");
                Console.WriteLine("  TaskHelper.exe --uac=enable");
                Console.WriteLine("  TaskHelper.exe --uac=disable");
                return;
            }

            string arg = args[0].ToLower();
            Logger.Write("Received argument: " + arg);

            try
            {
                if (arg == "--enable")
                {
                    EnableAutoLaunchTask();
                    Logger.Write("Task created and started.");
                    Console.WriteLine("Task created and started.");
                }
                else if (arg == "--disable")
                {
                    DisableAutoLaunchTask();
                    Logger.Write("Task removed and process terminated.");
                    Console.WriteLine("Task removed and process terminated.");
                }
                else if (arg == "--uac=enable")
                {
                    SetUAC(true);
                }
                else if (arg == "--uac=disable")
                {
                    SetUAC(false);
                }
                else
                {
                    Logger.Write("Invalid argument.");
                    Console.WriteLine("Invalid argument.");
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Unhandled exception: " + ex.ToString());
                Console.WriteLine("An error occurred. See log for details.");
            }

            Logger.Write("Program finished.");
        }

        private static bool IsRunAsAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void EnableAutoLaunchTask()
        {
            Logger.Write("Attempting to create scheduled task...");

            using (TaskService ts = new TaskService())
            {
                try
                {
                    string taskName = "GCM_wingamepad";

                    // Clean up previous task
                    ts.RootFolder.DeleteTask(taskName, false);
                    Logger.Write("Old task deleted (if it existed).");

                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "Start GCM wingamepad mode";
                    td.Principal.UserId = WindowsIdentity.GetCurrent().Name;
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;
                    td.Principal.RunLevel = TaskRunLevel.Highest;

                    td.Triggers.Add(new LogonTrigger
                    {
                        Delay = TimeSpan.FromSeconds(3),
                        Enabled = true
                    });

                    string exePath = @"C:\Program Files (x86)\GCMcrew\GCM\GCM\wingamepad\wingamepad.exe";
                    td.Actions.Add(new ExecAction(exePath, null, null));

                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.RunOnlyIfIdle = false;
                    td.Settings.RunOnlyIfNetworkAvailable = false;
                    td.Settings.AllowHardTerminate = false;
                    td.Settings.StartWhenAvailable = true;
                    td.Settings.AllowDemandStart = true;

                    ts.RootFolder.RegisterTaskDefinition(taskName, td,
                        TaskCreation.CreateOrUpdate, null, null,
                        TaskLogonType.InteractiveToken);

                    Logger.Write("Task registered successfully.");

                    Task task = ts.FindTask(taskName);
                    if (task != null)
                    {
                        task.Run();
                        Logger.Write("Task started.");
                    }
                    else
                    {
                        Logger.Write("ERROR: Task was not found after registration.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Write("Error while creating task: " + ex.ToString());
                    throw;
                }
            }
        }

        private static void DisableAutoLaunchTask()
        {
            Logger.Write("Attempting to delete task and terminate wingamepad.exe...");

            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask("GCM_wingamepad", false);
                Logger.Write("Task deleted.");
            }

            try
            {
                Process[] processes = Process.GetProcessesByName("wingamepad");
                foreach (Process proc in processes)
                {
                    proc.Kill();
                    proc.WaitForExit();
                    Logger.Write("Terminated process ID: " + proc.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Write("Error terminating wingamepad.exe: " + ex.Message);
            }
        }

        private static void SetUAC(bool enable)
        {
            try
            {
                string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";

                if (enable)
                {
                    Registry.SetValue(key, "ConsentPromptBehaviorAdmin", 5);
                    Registry.SetValue(key, "PromptOnSecureDesktop", 1);
                    Logger.Write("UAC prompt behavior ENABLED.");
                    Console.WriteLine("UAC prompt behavior ENABLED. No reboot required.");
                }
                else
                {
                    Registry.SetValue(key, "ConsentPromptBehaviorAdmin", 0);
                    Registry.SetValue(key, "PromptOnSecureDesktop", 0);
                    Logger.Write("UAC prompt behavior DISABLED.");
                    Console.WriteLine("UAC prompt behavior DISABLED. No reboot required.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Write("Access denied while modifying UAC behavior.");
                Console.WriteLine("Access denied. Please run this tool as Administrator.");
            }
            catch (Exception ex)
            {
                Logger.Write("Error modifying UAC behavior: " + ex.Message);
                Console.WriteLine("Error modifying UAC behavior: " + ex.Message);
            }
        }
    }

    internal static class Logger
    {
        private static string logFile = "";

        public static void Init()
        {
            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (exePath == null)
            {
                exePath = AppDomain.CurrentDomain.BaseDirectory;
            }

            logFile = Path.Combine(exePath, "taskhelper.log");

            try
            {
                File.WriteAllText(logFile, "TaskHelper Log started at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            }
            catch
            {
                // fail silently
            }
        }

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(logFile, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\n");
            }
            catch
            {
                // fail silently
            }
        }
    }
}
