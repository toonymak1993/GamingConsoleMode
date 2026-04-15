using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace gcmloader.Services;

/// <summary>
/// Centralized HKLM/HKCU helpers for non-shell, non-UAC settings.
/// </summary>
public static class RegistryOperations
{
    /// <summary>Valve Steam client install folder from registry, or null if not found.</summary>
    public static string? TryGetSteamInstallDirectory()
    {
        try
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
            {
                if (key != null)
                {
                    var path = key.GetValue("InstallPath")?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }

            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                if (key != null)
                {
                    var path = key.GetValue("InstallPath")?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RegistryOperations] Steam InstallPath: {ex.Message}");
        }

        return null;
    }

    /// <summary>Full path to steam.exe when the Steam client is installed in the default location.</summary>
    public static string? TryGetSteamExePath()
    {
        var dir = TryGetSteamInstallDirectory();
        return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "steam.exe");
    }

    public static void SetDwordCurrentUser(string subKeyPath, string valueName, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
        key?.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
