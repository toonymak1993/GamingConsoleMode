using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace gcmloader
{
  
    public sealed partial class overlaycontrolls : Window
    {
        // --- Win32 API Imports for advanced window manipulation ---
        // These are used to control transparency, positioning, and layering.

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        // Constants for window styles and attributes
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int LWA_ALPHA = 0x2;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        public overlaycontrolls()
        {
            this.InitializeComponent();

            // First, we need to get the native window handle (HWND) to manipulate it.
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Make the window borderless and always on top.
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsAlwaysOnTop = true;
            }

            // Apply styles to make the window see-through and allow clicks to pass through.
            SetWindowLong(hwnd, GWL_EXSTYLE,
                (IntPtr)((int)GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_LAYERED | WS_EX_TRANSPARENT));
            SetLayeredWindowAttributes(hwnd, 0, 250, LWA_ALPHA); // 250 for slight transparency, 255 is fully opaque

            // Resize the window to fill the primary screen, creating the overlay effect.
            var screen = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            MoveWindow(hwnd, 0, 0, screen.Width, screen.Height, true);

            // Set the window to be 'topmost' so it's always visible over other applications.
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

            // Set up the list of shortcuts that will be displayed in the overlay.
            ShortcutList.ItemsSource = new List<ShortcutModel>
            {
                new ShortcutModel { Combo = "Back + Start", Action = "Bring to Foreground" },
                new ShortcutModel { Combo = "Back + Y", Action = "ALT+TAB" },
                new ShortcutModel { Combo = "Back + X", Action = "Toggle Overlay" },
                new ShortcutModel { Combo = "Back + RThumb", Action = "Switch Audio Device" },
            };
        }
    }

    // A simple class to hold the data for our shortcut list.
    public class ShortcutModel
    {
        public string Combo { get; set; }
        public string Action { get; set; }
    }
}
