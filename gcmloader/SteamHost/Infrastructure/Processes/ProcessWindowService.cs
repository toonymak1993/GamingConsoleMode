using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.Processes;

public sealed class ProcessWindowService
{
    private static readonly HashSet<string> IgnoredClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
        "DV2ControlHost",
        "MsgrIMEWindowClass",
        "SysShadow",
        "NotifyIconOverflowWindow",
    };

    private static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost",
        "TextInputHost",
        "StartMenuExperienceHost",
        "ShellExperienceHost",
        "Widgets",
        "SearchHost",
        "LockApp",
    };

    public ProcessesSnapshot GetSnapshot()
    {
        var windows = EnumerateWindows()
            .OrderByDescending(window => window.IsForeground)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statusText = windows.Count switch
        {
            0 => "No open app windows detected.",
            1 => "1 open app window ready.",
            _ => $"{windows.Count} open app windows ready.",
        };

        return new ProcessesSnapshot(windows, statusText);
    }

    public ProcessesSnapshot ActivateWindow(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new InvalidOperationException("A target window handle is required.");
        }

        if (!TryParseHandle(handle, out var targetWindow))
        {
            throw new InvalidOperationException("The selected window handle is invalid.");
        }

        if (!IsWindow(targetWindow))
        {
            throw new InvalidOperationException("The selected window is no longer available.");
        }

        FocusWindow(targetWindow);
        return GetSnapshot();
    }

    private static IReadOnlyList<ProcessWindowInfo> EnumerateWindows()
    {
        var windows = new List<ProcessWindowInfo>();
        var shellWindow = GetShellWindow();
        var foregroundWindow = GetForegroundWindow();
        var handle = GCHandle.Alloc(new EnumerationContext(windows, shellWindow, foregroundWindow));
        try
        {
            EnumWindows(
                static (windowHandle, parameter) =>
                {
                    var context = (EnumerationContext)GCHandle.FromIntPtr(parameter).Target!;
                    context.CollectWindow(windowHandle);
                    return true;
                },
                GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return windows;
    }

    private static void FocusWindow(nint windowHandle)
    {
        if (IsIconic(windowHandle))
        {
            ShowWindowAsync(windowHandle, ShowWindowRestore);
        }
        else
        {
            ShowWindowAsync(windowHandle, ShowWindowShow);
        }

        var foregroundWindow = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var foregroundThreadId = foregroundWindow != 0
            ? GetWindowThreadProcessId(foregroundWindow, out _)
            : 0;
        var targetThreadId = GetWindowThreadProcessId(windowHandle, out _);

        try
        {
            if (foregroundThreadId != 0)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            BringWindowToTop(windowHandle);
            SetForegroundWindow(windowHandle);
            SetFocus(windowHandle);
        }
        finally
        {
            if (foregroundThreadId != 0)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    private static bool TryParseHandle(string rawValue, out nint handle)
    {
        if (rawValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(rawValue[2..], System.Globalization.NumberStyles.HexNumber, null, out var hexValue))
        {
            handle = new nint(hexValue);
            return true;
        }

        if (long.TryParse(rawValue, out var decimalValue))
        {
            handle = new nint(decimalValue);
            return true;
        }

        handle = 0;
        return false;
    }

    private sealed class EnumerationContext
    {
        private readonly List<ProcessWindowInfo> _windows;
        private readonly nint _shellWindow;
        private readonly nint _foregroundWindow;

        public EnumerationContext(List<ProcessWindowInfo> windows, nint shellWindow, nint foregroundWindow)
        {
            _windows = windows;
            _shellWindow = shellWindow;
            _foregroundWindow = foregroundWindow;
        }

        public void CollectWindow(nint windowHandle)
        {
            if (windowHandle == 0 || windowHandle == _shellWindow)
            {
                return;
            }

            if (!IsWindowVisible(windowHandle))
            {
                return;
            }

            if (IsWindowCloaked(windowHandle))
            {
                return;
            }

            var className = GetWindowClassName(windowHandle);
            if (IgnoredClassNames.Contains(className))
            {
                return;
            }

            var title = GetWindowTitle(windowHandle);
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var extendedStyle = GetWindowLongPtr(windowHandle, GetWindowLongExStyle).ToInt64();
            var owner = GetWindow(windowHandle, GetWindowOwner);
            var isToolWindow = (extendedStyle & WindowExStyleToolWindow) != 0;
            var hasAppWindowFlag = (extendedStyle & WindowExStyleAppWindow) != 0;

            if (isToolWindow)
            {
                return;
            }

            if (owner != 0 && !hasAppWindowFlag)
            {
                return;
            }

            if (!TryGetWindowRect(windowHandle, out var rectangle))
            {
                return;
            }

            var width = rectangle.Right - rectangle.Left;
            var height = rectangle.Bottom - rectangle.Top;
            if (width < 140 || height < 90)
            {
                return;
            }

            GetWindowThreadProcessId(windowHandle, out var nativeProcessId);
            var processId = unchecked((int)nativeProcessId);
            if (processId <= 0)
            {
                return;
            }

            string processName;
            try
            {
                using var process = Process.GetProcessById(processId);
                processName = process.ProcessName;
            }
            catch
            {
                return;
            }

            if (IgnoredProcessNames.Contains(processName))
            {
                return;
            }

            _windows.Add(new ProcessWindowInfo(
                Handle: $"0x{windowHandle.ToInt64():X}",
                Title: title.Trim(),
                ProcessName: processName,
                ProcessId: processId,
                IsMinimized: IsIconic(windowHandle),
                IsForeground: windowHandle == _foregroundWindow));
        }

        private static bool TryGetWindowRect(nint windowHandle, out Rect rectangle)
        {
            if (GetWindowRect(windowHandle, out rectangle))
            {
                return true;
            }

            rectangle = default;
            return false;
        }

        private static string GetWindowTitle(nint windowHandle)
        {
            var length = GetWindowTextLengthW(windowHandle);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            GetWindowTextW(windowHandle, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string GetWindowClassName(nint windowHandle)
        {
            var builder = new StringBuilder(256);
            return GetClassNameW(windowHandle, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }

        private static bool IsWindowCloaked(nint windowHandle)
        {
            if (DwmGetWindowAttribute(windowHandle, DwmwaCloaked, out var cloaked, sizeof(int)) != 0)
            {
                return false;
            }

            return cloaked != 0;
        }
    }

    private const int DwmwaCloaked = 14;
    private const int GetWindowLongExStyle = -20;
    private const int GetWindowOwner = 4;
    private const int ShowWindowRestore = 9;
    private const int ShowWindowShow = 5;
    private const long WindowExStyleToolWindow = 0x00000080L;
    private const long WindowExStyleAppWindow = 0x00040000L;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(nint windowHandle, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLengthW(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint windowHandle, StringBuilder builder, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(nint windowHandle, StringBuilder builder, int count);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint windowHandle, int attribute, out int value, int size);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint windowHandle, out Rect rectangle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachingThreadId, uint attachedThreadId, bool attach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(nint windowHandle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint windowHandle);
}
