using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OverlayWindow
{
    public partial class ToastPopup : Window
    {
        public ToastPopup(string message, string icon = "🔔")
        {
            InitializeComponent();
            ToastText.Text = message;
            ToastIcon.Text = icon;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Position the toast window at the bottom right of the screen
            var screen = SystemParameters.WorkArea;
            this.Left = screen.Right - this.Width - 20;
            this.Top = screen.Bottom - this.Height - 20;

            // Automatically close the toast after 2.5 seconds
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2.5)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                this.Close();
            };
            timer.Start();
        }

        // Make sure the toast doesn't steal focus and remains topmost
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = (int)GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            SetWindowLong(hwnd, GWL_EXSTYLE, (IntPtr)exStyle);

            SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        // Win32 interop declarations for style manipulation
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
    }
}
