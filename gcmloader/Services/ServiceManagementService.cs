using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;

namespace gcmloader.Services;

/// <summary>
/// Stop/start services and configure startup via sc.exe (elevated where the original code used Verb = runas).
/// </summary>
public sealed class ServiceManagementService : IServiceManager
{
    public static readonly ServiceManagementService Instance = new();
    private const int ScWaitTimeoutMs = 15000;

    private static readonly List<(string ServiceName, string? ProcessName)> DebloatServicesDesktop = new()
    {
        ("SysMain", null),
        ("DiagTrack", null),
        ("MapsBroker", null),
        ("RetailDemo", null),
        ("Fax", null),
        ("WSearch", "SearchIndexer"),
        ("OneSyncSvc", "OneDrive"),
        ("PhoneSvc", null),
        ("WerSvc", null),
        ("Spooler", null),
        ("dmwappushservice", null),
        ("ConnectedUserExperiencesAndTelemetry", null),
        ("MessagingService", null),
        ("ContactDataSvc", null),
        ("IpOverUsbSvc", null)
    };

    private static readonly List<(string ServiceName, string? ProcessName)> DebloatServicesHandheld = new()
    {
        ("SysMain", null),
        ("DiagTrack", null),
        ("MapsBroker", null),
        ("RetailDemo", null),
        ("Fax", null),
        ("WSearch", "SearchIndexer"),
        ("OneSyncSvc", "OneDrive"),
        ("WerSvc", null),
        ("dmwappushservice", null),
        ("PhoneSvc", null),
        ("MessagingService", null),
        ("ContactDataSvc", null),
    };

    public void DisableServiceStartup(string serviceName)
    {
        using var process = Process.Start(CreateScConfigStartInfo(serviceName, "disabled"));
        WaitForExitWithTimeout(process, $"sc config {serviceName} disabled");
    }

    public void SetServiceStartupToAuto(string serviceName)
    {
        try
        {
            using var process = Process.Start(CreateScConfigStartInfo(serviceName, "auto"));
            WaitForExitWithTimeout(process, $"sc config {serviceName} auto");

            Debug.WriteLine($"[✓] Service {serviceName} set to automatic startup.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[!] Failed to set {serviceName} to auto: {ex.Message}");
        }
    }

    public void DisableServiceAndKillProcess(string serviceName, string processName)
    {
        try
        {
            using (var service = new ServiceController(serviceName))
            {
                if (service.Status != ServiceControllerStatus.Stopped &&
                    service.Status != ServiceControllerStatus.StopPending)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                }

                using var process = Process.Start(CreateScConfigStartInfo(serviceName, "disabled"));
                WaitForExitWithTimeout(process, $"sc config {serviceName} disabled");
            }

            foreach (var proc in Process.GetProcessesByName(processName))
            {
                try
                {
                    proc.Kill(true);
                    Debug.WriteLine($"Killed process {proc.ProcessName} (PID {proc.Id})");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error killing process {proc.ProcessName}: {ex.Message}");
                }
            }

            Debug.WriteLine($"Service {serviceName} disabled and background process {processName} killed.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disabling service or killing process: {ex.Message}");
        }
    }

    public void TaskManagerDebloatServices(bool handheld)
    {
        var debloatList = handheld ? DebloatServicesHandheld : DebloatServicesDesktop;

        foreach (var (serviceName, processName) in debloatList)
        {
            if (!string.IsNullOrWhiteSpace(processName))
            {
                DisableServiceAndKillProcess(serviceName, processName);
            }
            else
            {
                try
                {
                    using var service = new ServiceController(serviceName);
                    if (service.Status != ServiceControllerStatus.Stopped &&
                        service.Status != ServiceControllerStatus.StopPending)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        Console.WriteLine($"[✓] Stopped: {serviceName}");
                    }

                    DisableServiceStartup(serviceName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Failed: {serviceName} → {ex.Message}");
                }
            }
        }
    }

    public void TaskManagerReEnableServices(bool handheld)
    {
        var servicesToEnable = handheld ? DebloatServicesHandheld : DebloatServicesDesktop;
        foreach (var (serviceName, _) in servicesToEnable)
        {
            SetServiceStartupToAuto(serviceName);
        }
    }

    public void EnsureServiceRunning(string serviceName, TimeSpan waitForRunning)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                Debug.WriteLine($"[GCM] Service ('{serviceName}') is stopped. Starting it now...");
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, waitForRunning);
                Debug.WriteLine($"[GCM] Service '{serviceName}' started successfully.");
            }
            else
            {
                Debug.WriteLine($"[GCM] Service ('{serviceName}') is already running.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] ERROR: Could not start service ('{serviceName}'). Details: {ex.Message}");
        }
    }

    private static void WaitForExitWithTimeout(Process? process, string operationName)
    {
        if (process == null)
            return;

        if (!process.WaitForExit(ScWaitTimeoutMs))
        {
            Debug.WriteLine($"[!] Timeout waiting for operation: {operationName}");
        }
    }

    private static ProcessStartInfo CreateScConfigStartInfo(string serviceName, string startMode) => new()
    {
        FileName = "sc.exe",
        Arguments = $"config \"{serviceName}\" start= {startMode}",
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        Verb = "runas"
    };
}
