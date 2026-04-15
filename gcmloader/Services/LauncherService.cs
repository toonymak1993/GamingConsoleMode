using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace gcmloader.Services;

public sealed class LauncherService : ILauncherService
{
    private readonly LauncherShell _shell;

    private static readonly IntPtr HWND_BOTTOM = new(1);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint TOKEN_DUPLICATE_ACCESS = 0x0002;
    private const uint MAXIMUM_ALLOWED_ACCESS = 0x02000000;
    private const uint CREATE_UNICODE_ENVIRONMENT_FLAG = 0x00000400;

    private enum SECURITY_IMPERSONATION_LEVEL_ENUM
    {
        SecurityAnonymous = 0,
        SecurityIdentification = 1,
        SecurityImpersonation = 2,
        SecurityDelegation = 3
    }

    private enum TOKEN_TYPE_ENUM
    {
        TokenPrimary = 1,
        TokenImpersonation = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES_TOKEN
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOW
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize;
        public uint dwXCountChars, dwYCountChars;
        public uint dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION_W
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>Win32 <c>ShowWindow</c> command: maximize (same as SDK SW_SHOWMAXIMIZED).</summary>
    private const int SwShowMaximized = 3;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, ref SECURITY_ATTRIBUTES_TOKEN lpTokenAttributes, SECURITY_IMPERSONATION_LEVEL_ENUM ImpersonationLevel, TOKEN_TYPE_ENUM TokenType, out IntPtr phNewToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithTokenW(IntPtr hToken, uint dwLogonFlags, string? lpApplicationName, string lpCommandLine, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFOW lpStartupInfo, out PROCESS_INFORMATION_W lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public LauncherService(LauncherShell shell)
    {
        _shell = shell;
    }

    public static string? AutoDetectLauncherPath(string launcher)
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string? path = null;

        switch (launcher)
        {
            case "steam":
                path = RegistryOperations.TryGetSteamExePath();
                break;

            case "playnite":
                string defaultPath = Path.Combine(localAppData, "Playnite", "Playnite.FullscreenApp.exe");
                if (File.Exists(defaultPath))
                {
                    path = defaultPath;
                    break;
                }

                string[] playniteRegPaths =
                {
                    @"SOFTWARE\Playnite",
                    @"SOFTWARE\WOW6432Node\Playnite",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Playnite",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Playnite",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{14AB2B56-32A1-4F29-BEE2-0BA851CC07C7}_is1"
                };

                RegistryKey[] roots = { Registry.CurrentUser, Registry.LocalMachine };
                foreach (RegistryKey root in roots)
                {
                    foreach (string regPath in playniteRegPaths)
                    {
                        using RegistryKey? key = root.OpenSubKey(regPath);
                        if (key != null)
                        {
                            string? installDir = (key.GetValue("InstallPath") as string) ?? (key.GetValue("InstallLocation") as string);
                            if (!string.IsNullOrEmpty(installDir))
                            {
                                string fullPath = Path.Combine(installDir, "Playnite.FullscreenApp.exe");
                                if (File.Exists(fullPath))
                                {
                                    path = fullPath;
                                    break;
                                }
                            }
                        }
                    }

                    if (path != null) break;
                }

                break;

            case "gfn":
                path = Path.Combine(localAppData, "NVIDIA Corporation", "GeForceNOW", "CEF", "GeForceNOW.exe");
                if (!File.Exists(path))
                {
                    string lnkPath = Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\NVIDIA GeForce NOW.lnk");
                    if (File.Exists(lnkPath)) path = lnkPath;
                }

                break;
        }

        if (path != null && File.Exists(path)) return path;

        return null;
    }

    public static void RenameSteamStartupVideo_Start()
    {
        try
        {
            if (!IsGcmVideoEnabled()) return;
            if (!IsSteamInjectionEnabled()) return;

            string? steamPath = AutoDetectLauncherPath("steam");
            if (string.IsNullOrEmpty(steamPath)) return;

            string? steamDir = Path.GetDirectoryName(steamPath);
            if (string.IsNullOrEmpty(steamDir)) return;

            string moviesPath = Path.Combine(steamDir, "steamui", "movies");
            if (!Directory.Exists(moviesPath)) return;

            string myVideo = Path.Combine(moviesPath, "GCM_vid.webm");
            string steamOriginal = Path.Combine(moviesPath, "bigpicture_startup.webm");
            string steamBackup = Path.Combine(moviesPath, "bigpicture_startup.old.webm");

            if (File.Exists(steamBackup))
            {
                if (File.Exists(steamOriginal)) File.Delete(steamOriginal);
                File.Move(steamBackup, steamOriginal);
            }

            if (!File.Exists(myVideo)) return;

            if (File.Exists(steamOriginal))
            {
                File.Move(steamOriginal, steamBackup);
            }

            File.Copy(myVideo, steamOriginal, true);
            Debug.WriteLine("[SteamInjection] Swap erfolgreich.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SteamInjection] Start-Fehler: {ex.Message}");
        }
    }

    private static bool IsGcmVideoEnabled()
    {
        try { return AppSettings.Load<bool>("usestartupvideo"); }
        catch { return false; }
    }

    private static bool IsSteamInjectionEnabled()
    {
        try { return AppSettings.Load<bool>("usesteamstartupvideo"); }
        catch { return false; }
    }

    public async Task SwitchToSpecificLauncherAsync(string launcherId, CancellationToken cancellationToken = default)
    {
        _shell.MakeSelfNonTopmost();
        Debug.WriteLine($"[GCM] Quick-Launch ausgelöst für: '{launcherId}'...");

        try
        {
            switch (launcherId)
            {
                case "steam":
                    await StartSteamAsync(false, cancellationToken).ConfigureAwait(false);
                    break;
                case "gfn":
                    await StartGfnAsync(cancellationToken).ConfigureAwait(false);
                    break;
                case "xbox":
                    await _shell.StartXboxAsync(false).ConfigureAwait(false);
                    break;
                case "playnite":
                    await StartPlayniteAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] Fehler beim Quick-Launch: {ex.Message}");
        }
    }

    public async Task SwitchToConfiguredLauncherAsync(CancellationToken cancellationToken = default)
    {
        _shell.MakeSelfNonTopmost();
        await StartConfiguredLauncherAsync(forceSteamRestart: false, forceXboxRestart: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartConfiguredLauncherAsync(bool forceSteamRestart = false, bool forceXboxRestart = false, CancellationToken cancellationToken = default)
    {
        string launcher = "steam";
        try { launcher = AppSettings.Load<string>("launcher"); } catch { }
        Debug.WriteLine($"[GCM] Wechsle zu konfiguriertem Launcher: '{launcher}'...");

        try
        {
            switch (launcher)
            {
                case "steam":
                    await StartSteamAsync(forceSteamRestart, cancellationToken).ConfigureAwait(false);
                    return;

                case "gfn":
                    await StartGfnAsync(cancellationToken).ConfigureAwait(false);
                    return;

                case "playnite":
                    await StartPlayniteAsync(cancellationToken).ConfigureAwait(false);
                    return;

                case "custom":
                    string customPath = AppSettings.Load<string>("customlauncherpath");
                    string customProcessName = Path.GetFileNameWithoutExtension(customPath);
                    Process? customProc = Process.GetProcessesByName(customProcessName).FirstOrDefault();
                    if (customProc != null && customProc.MainWindowHandle != IntPtr.Zero)
                    {
                        await _shell.ForcefullyBringToForeground(customProc.MainWindowHandle).ConfigureAwait(false);
                    }
                    else
                    {
                        await StartOtherLauncherAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return;

                case "xbox":
                    await _shell.StartXboxAsync(forceXboxRestart).ConfigureAwait(false);
                    return;

                default:
                    AppSettings.Save("launcher", "steam");
                    await StartSteamAsync(forceSteamRestart, cancellationToken).ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] Fehler während des Launcher-Wechsels: {ex.Message}");
        }
    }

    public async Task StartSteamAsync(bool forceRestart = false, CancellationToken cancellationToken = default)
    {
        try
        {
            string? steamExePath = AutoDetectLauncherPath("steam");
            if (string.IsNullOrWhiteSpace(steamExePath))
                throw new FileNotFoundException("Steam could not be found automatically on this system.");

            IntPtr myHwnd = _shell.GetHostWindowHandle();
            SetWindowPos(myHwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            bool steamLiefSchon = false;

            Process? steamProc = Process.GetProcessesByName("steam").FirstOrDefault();
            bool isSteamRunning = steamProc != null;

            bool isColdStart = forceRestart || !isSteamRunning;

            bool useDeckyLoader = false;
            try
            {
                useDeckyLoader = AppSettings.Load<bool>("usedeckyloader");
            }
            catch
            {
                Debug.WriteLine("[GCM] 'usedeckyloader' setting not found, defaulting to false.");
            }

            if (useDeckyLoader && isColdStart)
            {
                Debug.WriteLine("[GCM] Decky Loader enabled & Cold Boot required. Preparing environment...");

                var allSteamProcs = Process.GetProcessesByName("steam")
                    .Concat(Process.GetProcessesByName("steamwebhelper"))
                    .ToList();

                if (allSteamProcs.Any())
                {
                    foreach (Process proc in allSteamProcs)
                    {
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                    }
                }

                Process[] deckyProcs = Process.GetProcessesByName("PluginLoader_noconsole");
                if (deckyProcs.Any())
                {
                    foreach (Process proc in deckyProcs)
                    {
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                    }
                }

                await Task.Delay(1500, cancellationToken).ConfigureAwait(false);

                string userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string deckyPath = Path.Combine(userProfileFolder, "homebrew", "services", "PluginLoader_noconsole.exe");

                if (File.Exists(deckyPath))
                {
                    Debug.WriteLine("[GCM] Launching PluginLoader...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = deckyPath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });

                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Debug.WriteLine($"[GCM] WARNING: Decky Loader executable not found at {deckyPath}");
                }

                forceRestart = true;
                isSteamRunning = false;
            }

            if (forceRestart || !isSteamRunning)
            {
                Debug.WriteLine("[GCM] Steam Cold Boot into Big Picture Mode...");

                if (!useDeckyLoader && forceRestart)
                {
                    var steamProcs = Process.GetProcessesByName("steam")
                        .Concat(Process.GetProcessesByName("steamwebhelper"))
                        .ToList();
                    if (steamProcs.Any())
                    {
                        foreach (Process proc in steamProcs) { try { if (!proc.HasExited) proc.Kill(); } catch { } }
                        await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                    }
                }

                RenameSteamStartupVideo_Start();
                StartProcessAsNonAdmin(steamExePath, "-gamepadui");
            }
            else
            {
                Debug.WriteLine("[GCM] Steam is already running. Triggering Big Picture switch (Warmstart)...");
                Process.Start(new ProcessStartInfo("steam://open/gamepadui") { UseShellExecute = true });
                steamLiefSchon = true;
            }

            IntPtr steamHwnd = IntPtr.Zero;
            int attempts = 0;
            int maxAttempts = steamLiefSchon ? 20 : 60;

            while (attempts < maxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                steamHwnd = _shell.FindSteamBigPictureWindow();
                if (steamHwnd != IntPtr.Zero)
                {
                    await Task.Delay(800, cancellationToken).ConfigureAwait(false);
                    break;
                }

                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                attempts++;
            }

            if (steamHwnd != IntPtr.Zero)
            {
                Debug.WriteLine($"[GCM] Steam BP window ready. Applying Nuclear-Focus...");
                await _shell.ForcefullyBringToForeground(steamHwnd).ConfigureAwait(false);
                ShowWindow(steamHwnd, SwShowMaximized);
            }
            else
            {
                Debug.WriteLine("[GCM] Timeout! Steam BP window was not found.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] Error in StartSteam: {ex.Message}");
        }
    }

    public async Task StartPlayniteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Process? proc = Process.GetProcessesByName("Playnite.FullscreenApp").FirstOrDefault();
            if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
            {
                Debug.WriteLine("[GCM] Playnite Fullscreen läuft bereits. Bringe in den Vordergrund...");
                _shell.MakeSelfNonTopmost();
                await _shell.ForcefullyBringToForeground(proc.MainWindowHandle).ConfigureAwait(false);
                return;
            }

            Process[] desktopProcs = Process.GetProcessesByName("Playnite.DesktopApp");
            foreach (Process dp in desktopProcs)
            {
                try
                {
                    dp.Kill();
                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                }
                catch { }
            }

            string? playnitePath = AutoDetectLauncherPath("playnite");

            if (string.IsNullOrWhiteSpace(playnitePath) || !File.Exists(playnitePath))
            {
                throw new FileNotFoundException("Playnite Fullscreen.exe konnte nicht gefunden werden.");
            }

            Debug.WriteLine($"[GCM] Starte Playnite Fullscreen von: {playnitePath}");
            _shell.MakeSelfNonTopmost();

            StartProcessAsNonAdmin(playnitePath, "--startfullscreen --hidesplashscreen", Path.GetDirectoryName(playnitePath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] Error in StartPlaynite: {ex.Message}");
            await _shell.ShowMessageAsync("Playnite Fullscreen could not be started. Please check the installation.").ConfigureAwait(false);
        }
    }

    public async Task StartGfnAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Debug.WriteLine("[GCM] Prüfe GeForce Now...");

            Process[] runningProcs = Process.GetProcessesByName("GeForceNOW");
            foreach (Process p in runningProcs)
            {
                if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                {
                    _shell.MakeSelfNonTopmost();
                    await _shell.ForcefullyBringToForeground(p.MainWindowHandle).ConfigureAwait(false);
                    ShowWindow(p.MainWindowHandle, SwShowMaximized);
                    return;
                }
            }

            string? gfnPath = AutoDetectLauncherPath("gfn");

            if (string.IsNullOrWhiteSpace(gfnPath))
            {
                throw new FileNotFoundException("GeForce Now could not be detected automatically on this system.");
            }

            Process.Start(new ProcessStartInfo(gfnPath) { UseShellExecute = true });

            int attempts = 0;
            IntPtr gfnHwnd = IntPtr.Zero;

            while (attempts < 40)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                Process[] procs = Process.GetProcessesByName("GeForceNOW");
                foreach (Process p in procs)
                {
                    if (p.MainWindowHandle != IntPtr.Zero && IsWindowVisible(p.MainWindowHandle))
                    {
                        gfnHwnd = p.MainWindowHandle;
                        break;
                    }
                }

                if (gfnHwnd != IntPtr.Zero) break;
                attempts++;
            }

            if (gfnHwnd != IntPtr.Zero)
            {
                _shell.MakeSelfNonTopmost();
                await _shell.ForcefullyBringToForeground(gfnHwnd).ConfigureAwait(false);
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                ShowWindow(gfnHwnd, SwShowMaximized);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] Fehler in StartGfn: {ex.Message}");
            await _shell.ShowMessageAsync("GeForce Now konnte nicht gefunden oder gestartet werden.").ConfigureAwait(false);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public async Task StartOtherLauncherAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string launcherPath = AppSettings.Load<string>("customlauncherpath");

            if (string.IsNullOrWhiteSpace(launcherPath) || !File.Exists(launcherPath))
            {
                throw new FileNotFoundException("Der Pfad für den Custom Launcher ist ungültig oder wurde nicht gefunden.");
            }

            ShellManagementService.KillAllProcessesForExecutableFileName(Path.GetFileName(launcherPath));
            StartProcessAsNonAdmin(launcherPath, null, Path.GetDirectoryName(launcherPath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler in StartOtherLauncher: {ex.Message}");
            await _shell.ShowMessageAsync("Der Custom Launcher konnte nicht gestartet werden. Bitte den Pfad in den Einstellungen prüfen.").ConfigureAwait(false);
            _shell.BackToWindows();
        }
    }

    public static void StartProcessAsNonAdmin(string filePath, string? arguments = null, string? workingDirectory = null)
    {
        IntPtr userToken = GetNonElevatedUserToken();

        if (userToken == IntPtr.Zero)
        {
            Debug.WriteLine("[GCM] StartProcessAsNonAdmin: could not obtain non-elevated token, falling back to elevated launch.");
            var psi = new ProcessStartInfo(filePath) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(arguments)) psi.Arguments = arguments;
            if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
            Process.Start(psi);
            return;
        }

        IntPtr duplicateToken = IntPtr.Zero;
        try
        {
            var sa = new SECURITY_ATTRIBUTES_TOKEN { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES_TOKEN>() };

            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED_ACCESS, ref sa,
                    SECURITY_IMPERSONATION_LEVEL_ENUM.SecurityImpersonation, TOKEN_TYPE_ENUM.TokenPrimary, out duplicateToken))
                throw new InvalidOperationException($"DuplicateTokenEx failed: {Marshal.GetLastWin32Error()}");

            var si = new STARTUPINFOW
            {
                cb = Marshal.SizeOf<STARTUPINFOW>(),
                lpDesktop = "winsta0\\default"
            };

            string cmdLine = string.IsNullOrEmpty(arguments)
                ? $"\"{filePath}\""
                : $"\"{filePath}\" {arguments}";

            if (!CreateProcessWithTokenW(duplicateToken, 0, null, cmdLine,
                    CREATE_UNICODE_ENVIRONMENT_FLAG, IntPtr.Zero, workingDirectory, ref si, out var pi))
                throw new InvalidOperationException($"CreateProcessWithTokenW failed: {Marshal.GetLastWin32Error()}");

            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
        }
        finally
        {
            CloseHandle(userToken);
            if (duplicateToken != IntPtr.Zero) CloseHandle(duplicateToken);
        }
    }

    private static IntPtr GetNonElevatedUserToken()
    {
        IntPtr token = TryGetTokenFromProcessName("explorer");
        if (token != IntPtr.Zero) return token;

        string[] candidates = { "RuntimeBroker", "sihost", "ctfmon", "taskhostw", "ShellExperienceHost" };
        foreach (string name in candidates)
        {
            token = TryGetTokenFromProcessName(name);
            if (token != IntPtr.Zero)
            {
                Debug.WriteLine($"[GCM] GetNonElevatedUserToken: obtained token from '{name}'.");
                return token;
            }
        }

        try
        {
            uint sessionId = WTSGetActiveConsoleSessionId();
            if (sessionId != 0xFFFFFFFF && WTSQueryUserToken(sessionId, out token))
            {
                Debug.WriteLine($"[GCM] GetNonElevatedUserToken: obtained token via WTSQueryUserToken (session {sessionId}).");
                return token;
            }

            Debug.WriteLine($"[GCM] WTSQueryUserToken failed: {Marshal.GetLastWin32Error()}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GCM] WTSQueryUserToken exception: {ex.Message}");
        }

        return IntPtr.Zero;
    }

    private static IntPtr TryGetTokenFromProcessName(string processName)
    {
        try
        {
            Process? proc = Process.GetProcessesByName(processName).FirstOrDefault();
            if (proc == null) return IntPtr.Zero;

            if (OpenProcessToken(proc.Handle, TOKEN_DUPLICATE_ACCESS, out IntPtr token))
                return token;
        }
        catch { }

        return IntPtr.Zero;
    }
}
