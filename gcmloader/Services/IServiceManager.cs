using System;

namespace gcmloader.Services;

/// <summary>
/// Windows service control via <see cref="System.ServiceProcess.ServiceController"/> and sc.exe (not WMI).
/// </summary>
public interface IServiceManager
{
    void DisableServiceStartup(string serviceName);

    void DisableServiceAndKillProcess(string serviceName, string processName);

    void SetServiceStartupToAuto(string serviceName);

    /// <summary>Debloat list chosen by handheld vs desktop profile.</summary>
    void TaskManagerDebloatServices(bool handheld);

    void TaskManagerReEnableServices(bool handheld);

    void EnsureServiceRunning(string serviceName, TimeSpan waitForRunning);
}
